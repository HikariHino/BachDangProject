using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatMotionPlaytest
{
    const string Flag="MotionAndHealthQA";
    static double started,until;
    static int phase,samples,movingSamples,walkClipSamples,excursions,lastFrame=-1;
    static float minimumCadence=10,maximumTravel;
    static EnemyDeckGuard target;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<NPCWander,Vector3> starts=new Dictionary<NPCWander,Vector3>();
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
    static void Finish(string error)
    {
        File.WriteAllText("Logs/MotionPlaytest.json",JsonConvert.SerializeObject(new{success=error==null,error,phase,checks,samples,movingSamples,walkClipSamples,minimumCadence,maximumTravel,excursions,walkers=starts.Keys.Where(w=>w!=null&&w.isActiveAndEnabled).Select(w=>new{w.name,w.CurrentSpeed,speed=w.animator.speed,controller=w.animator.runtimeAnimatorController.name,culling=w.animator.cullingMode.ToString(),walking=w.animator.GetBool("isWalking"),w.walkParam,state=w.animator.GetCurrentAnimatorStateInfo(0).fullPathHash,clips=w.animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name),visible=w.GetComponentsInChildren<Renderer>().Any(r=>r.isVisible)})},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(error==null?0:1);
    }
    static void CaptureHealthBars()
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;
        var canvas=GameObject.Find("Enemy_Health_Bars").GetComponent<Canvas>();
        var mode=canvas.renderMode;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=2;
        var rt=new RenderTexture(Screen.width,Screen.height,24);var pixels=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();File.WriteAllBytes("Logs/Enemy_Health_Boarding.png",pixels.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;canvas.renderMode=mode;Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);
    }
    static float Height(Animator a)=>a.GetBoneTransform(HumanBodyBones.Head).position.y-Mathf.Min(a.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,a.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<4)return;
        try
        {
            var session=BeachBoatQuestSession.Instance;var boat=QuestRowboat.Instance;var game=RiverBoardingCombat.Instance;var combat=RiverPlayerCombat.Instance;var fleet=EnemyFleetDirector.Instance;
            if(phase==0)
            {
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();StoryDialogueUI.Instance?.Close();
                foreach(var walker in Object.FindObjectsByType<NPCWander>().Where(w=>w.isActiveAndEnabled))starts[walker]=walker.transform.position;
                until=EditorApplication.timeSinceStartup+15;phase=1;return;
            }
            if(phase==1)
            {
                if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
                foreach(var entry in starts)
                {
                    var walker=entry.Key;if(!walker.isActiveAndEnabled)continue;
                    samples++;minimumCadence=Mathf.Min(minimumCadence,walker.animator.speed);
                    maximumTravel=Mathf.Max(maximumTravel,QuestRowboat.FlatDistance(walker.transform.position,entry.Value));
                    if(walker.CurrentActivity.Contains("qua làng"))excursions++;
                    if(walker.CurrentSpeed>walker.NominalStrideSpeed*.8f&&!walker.animator.IsInTransition(0)&&(walker.animator.cullingMode!=AnimatorCullingMode.CullCompletely||walker.GetComponentsInChildren<Renderer>().Any(r=>r.isVisible)))
                    {
                        movingSamples++;
                        if(walker.animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name.IndexOf("walk",StringComparison.OrdinalIgnoreCase)>=0))walkClipSamples++;
                    }
                }
                if(EditorApplication.timeSinceStartup<until)return;
                Check(samples>100&&minimumCadence>=.94f,"NPC walk and idle animations never enter slow motion");
                Check(movingSamples>50&&walkClipSamples>movingSamples*.9f,"Animated moving NPCs play walking clips rather than sliding in idle");
                Check(maximumTravel>40&&excursions>0,"NPCs take longer village/camp trips");
                var hull=boat.transform.Find("Thuyen_Go_Song");Check(hull.localScale.x<1&&hull.localScale.z<1&&boat.hullProbeSpacing<26,"Rowboat hull and collision footprint are smaller");
                Check(Mathf.Abs(session.player.transform.lossyScale.y-14)<.01f,"Boat resize preserves player scale");
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,BeachBoatStory.RiverStakeZone.Instance.ClusterCount).ToArray());
                var story=BeachBoatStory.BachDangStoryManager.Instance;story.collectedWood=story.totalWoodRequired;story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act3_WaitForLowTide,true);
                var player=session.player;player.transform.SetPositionAndRotation(boat.boardingPoint.position,boat.boardingPoint.rotation);var body=player.GetComponent<Rigidbody>();body.position=player.transform.position;body.rotation=player.transform.rotation;Physics.SyncTransforms();
                Check(boat.TryBoard(),"Can board resized rowboat");combat.ReturnFireEnabled=false;game.enabled=false;
                until=EditorApplication.timeSinceStartup+2;phase=2;return;
            }
            if(EditorApplication.timeSinceStartup<until)return;
            if(phase==2)
            {
                foreach(var ship in fleet.enemyShips){ship.moveSpeed=0;ship.Activate();ship.transform.position=boat.transform.position+Vector3.forward*100;}
                foreach(var friendly in game.friendlyBoats)friendly.transform.position=boat.transform.position+Vector3.right*1500;
                for(int i=0;i<260;i++)game.StepHostiles(.1f);
                var boarded=game.CaptureSave().hostiles.Where(h=>h.state==3&&h.boat==boat.name).ToArray();
                Check(boarded.Length>0,"Enemy swimmers actually finish climbing onto the player boat");
                target=combat.guards.First(g=>g.IsIndependentBoarder&&g.IsAlive&&boarded.Any(h=>h.id==BachDangSaveSystem.GuardId(g)));
                Check(game.HandleActionKey(KeyCode.Q)&&game.SwordDrawn,"Q still draws the player sword");
                until=EditorApplication.timeSinceStartup+.5;phase=3;return;
            }
            if(phase==3)
            {
                var reference=session.player.GetComponentInChildren<Animator>();var enemy=target.visual.GetComponentInChildren<Animator>();reference.Update(0);enemy.Update(0);
                Check(Mathf.Abs(EnemyDeckGuard.RigHeight(enemy)/EnemyDeckGuard.RigHeight(reference)-1)<.05f,"Active boarded enemy skeleton matches player size");
                float ratio=Height(enemy)/Height(reference);Check(ratio>.88f&&ratio<1.12f,"Boarded enemy standing height matches the player");
                Check(Mathf.Abs(Mathf.Min(enemy.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,enemy.GetBoneTransform(HumanBodyBones.RightFoot).position.y)-target.transform.position.y)<1,"Boarded enemy feet stand above deck rather than sinking into hull");
                var camera=session.storyCamera;camera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;
                camera.transform.position=target.transform.position+new Vector3(-36,35,-58);camera.transform.LookAt(target.AimPoint);
                Check(target.Hit(1)&&target.Health==1,"Damage removes real enemy health");
                until=EditorApplication.timeSinceStartup+.3;phase=4;return;
            }
            if(phase==4)
            {
                var hud=combat.GetComponent<EnemyHealthHUD>();Check(Mathf.Abs(hud.DisplayedHealth(target)-.5f)<.001f,"Enemy health bar tracks half health");
                Check(hud.VisibleCount>0,"Enemy health bars visible in combat camera");
                CaptureHealthBars();
                Check(target.Hit(1)&&!target.IsAlive,"Second hit defeats the enemy");
                until=EditorApplication.timeSinceStartup+.2;phase=5;return;
            }
            Check(combat.GetComponent<EnemyHealthHUD>().DisplayedHealth(target)==0,"Dead enemies have no remaining health fill");Finish(null);
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
