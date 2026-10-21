using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatRiverCombatPlaytest
{
    const string Flag="RiverCombatPlaytest";
    static double started,waitUntil;
    static int phase;
    static EnemyDeckGuard target;
    static float healthBefore;
    static bool miss,weakHit,strongHit,cooldown,reload,signalGated,dodged,returnedFire,checkpoint;
    static string failure;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void MoveBoat(Vector3 p)
    {p.y=TideSystem.CurrentWaterHeight;QuestRowboat.Instance.transform.position=p;QuestRowboat.Instance.Step(0,0,.001f);Physics.SyncTransforms();}
    static bool Shoot(float draw)
    {
        var combat=RiverPlayerCombat.Instance;combat.SetAim(true);Physics.SyncTransforms();
        var origin=BeachBoatQuestSession.Instance.player.transform.position+Vector3.up*14;
        bool shot=combat.TryFire(new Ray(origin,(target.AimPoint-origin).normalized),draw);combat.StepArrows(1);return shot;
    }
    static void Finish()
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RiverCombatPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,miss,weakHit,strongHit,cooldown,reload,signalGated,dodged,returnedFire,checkpoint,guards=RiverPlayerCombat.Instance?.guards.Length},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<3||EditorApplication.timeSinceStartup<waitUntil)return;
        if(EditorApplication.timeSinceStartup-started>55){failure="Combat timeout at phase "+phase;Finish();return;}
        try
        {
            var combat=RiverPlayerCombat.Instance;var fleet=EnemyFleetDirector.Instance;var boat=QuestRowboat.Instance;var session=BeachBoatQuestSession.Instance;var vitals=session.player.GetComponent<PlayerVitals>();
            if(combat==null||combat.guards.Length!=20)throw new Exception("Expected 20 scene-owned enemy archers");
            if(phase==0)
            {
                foreach(var intro in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();BeachBoatStory.StoryDialogueUI.Instance?.Close();
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,BeachBoatStory.RiverStakeZone.Instance.ClusterCount).ToArray());
                Story.Instance.collectedWood=Story.Instance.totalWoodRequired;Story.Instance.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);
                session.player.transform.position=boat.boardingPoint.position;var body=session.player.GetComponent<Rigidbody>();body.position=boat.boardingPoint.position;Physics.SyncTransforms();
                if(!boat.TryBoard())throw new Exception("Combat fixture cannot board");combat.ReturnFireEnabled=false;
                foreach(var ship in fleet.enemyShips)ship.moveSpeed=0;
                phase=1;waitUntil=EditorApplication.timeSinceStartup+2.5;return;
            }
            if(phase==1)
            {
                target=combat.guards.First(g=>g.IsAlive);var ship=target.ship;
                typeof(EnemyShip).GetField("routeIndex",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ship,ship.routeWaypoints.Length);
                ship.transform.position=ship.targetPoint.position-ship.transform.forward*50;ship.Step(.01f);MoveBoat(target.AimPoint+Vector3.left*190);
                BachDangBattleTimeline.Instance.BeginAmbush();
                signalGated=!fleet.TrySignalAttack()&&fleet.CommandScore==0;
                if(!signalGated)throw new Exception("Flag bypassed the player's combat objective");
                combat.SetAim(true);var p=session.player.transform.position+Vector3.up*14;
                if(!combat.TryFire(new Ray(p,Vector3.up),1))throw new Exception("Could not fire a miss");combat.StepArrows(1);
                miss=combat.Kills==0&&combat.Hits==0&&combat.Arrows==5;
                cooldown=!Shoot(1);if(!miss||!cooldown)throw new Exception("Miss or cooldown rule failed");
                phase=2;waitUntil=EditorApplication.timeSinceStartup+.7;return;
            }
            if(phase==2)
            {
                weakHit=Shoot(.1f)&&target.Health==1&&combat.Kills==0&&combat.Hits==1;
                if(!weakHit)throw new Exception("Weak arrow did not damage actual guard hitbox");
                session.player.GetComponentInChildren<Animator>().Update(.1f);
                typeof(RiverPlayerCombat).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
                Capture("PlayerAim",session.player.transform.position+Vector3.up*12,new Vector3(25,12,-25));
                Capture("EnemyArcher",target.AimPoint,new Vector3(24,12,24));
                phase=3;waitUntil=EditorApplication.timeSinceStartup+.7;return;
            }
            if(phase==3)
            {
                strongHit=Shoot(1)&&!target.IsAlive&&combat.Kills==1&&combat.HasSuppressedWave(target.waveIndex);
                if(!strongHit)throw new Exception("Charged arrow did not kill/suppress the wave");
                BachDangBattleTimeline.Instance.BeginAmbush();if(!fleet.TrySignalAttack()||fleet.CommandScore!=1)throw new Exception("Combat did not unlock flag order");
                if(!combat.Reload()||!combat.IsReloading||Shoot(1))throw new Exception("Reload gate failed");
                phase=4;waitUntil=EditorApplication.timeSinceStartup+2.2;return;
            }
            if(phase==4)
            {
                reload=combat.Arrows==6&&!combat.IsReloading;if(!reload)throw new Exception("Reload did not restore quiver");
                var attacker=combat.guards.FirstOrDefault(g=>g.IsAlive&&!g.ship.IsReleasedForAmbush);
                if(attacker==null)return;
                target=attacker;MoveBoat(target.AimPoint+Vector3.left*240);combat.ReturnFireEnabled=true;
                healthBefore=vitals.Health;phase=5;return;
            }
            if(phase==5)
            {
                if(combat.HostileShots<1)return;
                MoveBoat(boat.transform.position+Vector3.left*100);phase=6;waitUntil=EditorApplication.timeSinceStartup+1;return;
            }
            if(phase==6)
            {
                dodged=combat.DodgedShots>=1&&vitals.Health==healthBefore;if(!dodged)throw new Exception("Moving boat did not evade incoming arrow");
                MoveBoat(target.AimPoint+Vector3.left*200);healthBefore=vitals.Health;phase=7;return;
            }
            if(phase==7)
            {
                if(vitals.Health>=healthBefore)return;
                returnedFire=vitals.LastImpact=="Trúng tên địch";if(!returnedFire)throw new Exception("Return fire did not damage player");
                combat.SetAim(true);vitals.TakeImpact(10000,"Combat checkpoint test");
                if(!QuestCheckpoint.Instance.IsDefeated||!QuestCheckpoint.Instance.Retry())throw new Exception("Death retry failed");
                checkpoint=combat.Kills==0&&combat.Arrows==6&&!combat.IsAiming&&!combat.ThreatPending&&vitals.Health==vitals.maxHealth;
                if(!checkpoint)throw new Exception("Combat progress/camera state leaked across checkpoint");Finish();
            }
        }
        catch(Exception e){failure=e.ToString();Finish();}
    }
    static void Capture(string name,Vector3 focus,Vector3 offset)
    {
        var go=new GameObject("RiverCombatQACamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.fieldOfView=55;camera.farClipPlane=700;
        camera.transform.position=focus+offset;camera.transform.LookAt(focus);
        var rt=new RenderTexture(1000,750,24);var pixels=new Texture2D(1000,750,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1000,750),0,0);pixels.Apply();
        Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/Combat_"+name+".png",pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);
    }
}
