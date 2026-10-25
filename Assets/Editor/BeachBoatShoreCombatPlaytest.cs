using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
using Motor=BeachBoatStory.Character_Movement;

public static class BeachBoatShoreCombatPlaytest
{
    const string Flag="ShoreCombat938QA";
    static double started;
    static int phase;
    static float until;
    static string runtimeError;
    static readonly List<string> checks=new List<string>();
    static Vector3 shore;
    static float bankRise;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
    public static void RunInEditor(){SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string message,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(kind==LogType.Error||kind==LogType.Exception))runtimeError=message+"\n"+stack;}
    static void Move(Vector3 point)
    {
        var player=BeachBoatQuestSession.Instance.player;var body=player.GetComponent<Rigidbody>();body.position=point;player.transform.position=point;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
    }
    static void SwimmingAt(Vector3 point)
    {
        var game=RiverBoardingCombat.Instance;var data=game.CaptureSave();data.state=(int)RiverBoardingCombat.PlayerState.Swimming;data.sword=false;game.RestoreSave(data);point.y=TideSystem.CurrentWaterHeight-5;Move(point);
    }
    static void Finish(string error)
    {
        File.WriteAllText("Logs/ShoreCombatPlaytest.json",JsonConvert.SerializeObject(new{success=error==null,error,phase,shore=shore.ToString(),bankRise,checks},Formatting.Indented));SessionState.SetBool(Flag,false);EditorApplication.isPlaying=false;
    }
    static void Capture()
    {
        var player=BeachBoatQuestSession.Instance.player;var go=new GameObject("ShoreReviewCamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;
        go.transform.position=player.transform.position+new Vector3(-42,31,-55);go.transform.LookAt(player.transform.position+Vector3.up*8);
        var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("Logs/Shore_Standing_After_Low_Tide.png",pixels.EncodeToPNG());RenderTexture.active=previous;Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<4)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-started>80)throw new Exception("Shore combat review timeout");
            var session=BeachBoatQuestSession.Instance;var game=RiverBoardingCombat.Instance;var boat=QuestRowboat.Instance;var fleet=EnemyFleetDirector.Instance;var combat=RiverPlayerCombat.Instance;var player=session.player;var vitals=player.GetComponent<PlayerVitals>();
            if(phase==0)
            {
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());
                var story=Story.Instance;story.collectedWood=story.totalWoodRequired;story.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);Move(boat.boardingPoint.position);Check(boat.TryBoard(),"Can board the quest 3 boat");fleet.StopAllCoroutines();game.enabled=false;combat.enabled=false;combat.ReturnFireEnabled=false;
                foreach(var vessel in fleet.enemyShips)vessel.enabled=false;foreach(var patrol in game.friendlyBoats)patrol.enabled=false;
                TideSystem.Instance.HoldTide(BachDangBattleTimeline.Instance,false,true);Check(game.TryTraverse(),"Can enter swimming during the battle");phase=1;return;
            }
            if(phase==1)
            {
                var terrain=Terrain.activeTerrain;var origin=boat.dockPoint.position;int landings=0;
                for(float r=20;r<900&&landings<5;r+=14)for(int angle=0;angle<360&&landings<5;angle+=12)
                {
                    var at=origin+Quaternion.Euler(0,angle,0)*Vector3.forward*r;var local=at-terrain.transform.position;var size=terrain.terrainData.size;if(local.x<0||local.z<0||local.x>size.x||local.z>size.z)continue;
                    float floor=terrain.SampleHeight(at)+terrain.transform.position.y;var normal=terrain.terrainData.GetInterpolatedNormal(local.x/size.x,local.z/size.z);
                    if(floor<TideSystem.CurrentWaterHeight+15||floor>TideSystem.CurrentWaterHeight+45||Vector3.Angle(normal,Vector3.up)>45)continue;
                    SwimmingAt(at);game.StepPlayer(0);if(game.State!=RiverBoardingCombat.PlayerState.Land)continue;
                    landings++;shore=player.transform.position;bankRise=floor-TideSystem.CurrentWaterHeight;
                }
                Check(landings==5,"Five real low-tide bank positions above the former 12-unit limit restore land movement");
                var capsule=player.GetComponent<CapsuleCollider>();var body=player.GetComponent<Rigidbody>();var motor=player.GetComponent<Motor>();
                Check(capsule.enabled&&!body.isKinematic&&motor.enabled,"Landing enables the solid player capsule, gravity and walking motor");
                Check(game.ToggleSword()&&game.SwordDrawn,"Sword can be drawn on the recovered shore");game.ToggleSword();combat.SetAim(true);Check(combat.IsAiming,"Bow can be aimed on shore");
                float standingHeight=player.transform.position.y;typeof(RiverPlayerCombat).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
                Check(Mathf.Abs(player.transform.position.y-standingHeight)<.01f,"Aiming the bow on shore cannot pull the player down to the waterline");combat.SetAim(false);
                var rig=player.GetComponentInChildren<Animator>();rig.Update(.2f);
                Check(Vector3.Dot((rig.GetBoneTransform(HumanBodyBones.Head).position-rig.GetBoneTransform(HumanBodyBones.Hips).position).normalized,Vector3.up)>.8f,"Standing animation replaces the horizontal swimming pose on shore");
                var input=typeof(Motor).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic);var freeLook=typeof(Motor).GetField("freeLook",BindingFlags.Instance|BindingFlags.NonPublic);var step=typeof(Motor).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
                var simulation=Physics.simulationMode;var walkStart=player.transform.position;var away=walkStart-origin;away.y=0;player.transform.rotation=Quaternion.LookRotation(away);Physics.simulationMode=SimulationMode.Script;
                try{freeLook.SetValue(motor,true);for(int i=0;i<35;i++){input.SetValue(motor,Vector2.up);step.Invoke(motor,null);Physics.Simulate(.02f);}}
                finally{input.SetValue(motor,Vector2.zero);freeLook.SetValue(motor,false);Physics.simulationMode=simulation;}
                Check(QuestRowboat.FlatDistance(walkStart,player.transform.position)>8,"Recovered walking motor physically moves along the beach");Move(shore);rig.Update(.2f);
                // A valid bank must still reject buildings and other solid objects.
                SwimmingAt(shore);var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="ShoreLandingSolidBlocker";obstacle.transform.position=shore+Vector3.up*8;obstacle.transform.localScale=new Vector3(22,45,22);Physics.SyncTransforms();
                Check(!game.TryWalkAshore(shore)&&game.State==RiverBoardingCombat.PlayerState.Swimming,"Shore exit cannot place the player inside a solid object");Object.DestroyImmediate(obstacle);
                // Isolate one surviving enemy. He is beside the abandoned boat,
                // while his real player target swims hundreds of units away.
                var ship=fleet.enemyShips[0];ship.Activate();ship.transform.position=boat.transform.position+Vector3.forward*900;ship.enabled=false;
                var data=game.CaptureSave();var hostile=data.hostiles[0];hostile.active=true;hostile.source=ship.name;hostile.boat=boat.name;hostile.state=1;hostile.targetPlayer=true;
                var box=BoatHullCollision.Get(boat.transform).SolidHull;var edge=box.center+new Vector3(-(box.size.x*.5f+12),0,Mathf.Clamp(-1.5f*7,-box.size.z*.35f,box.size.z*.35f));
                hostile.position=boat.transform.TransformPoint(edge);hostile.position.y=TideSystem.CurrentWaterHeight-4;game.RestoreSave(data);
                var enemy=combat.guards.First(g=>BachDangSaveSystem.GuardId(g)==hostile.id);foreach(var guard in combat.guards)if(guard!=enemy)guard.DefeatFromVolley();enemy.ResetGuard();
                var deep=boat.transform.position+Vector3.right*450;SwimmingAt(deep);var old=enemy.transform.position;float health=vitals.Health;float before=QuestRowboat.FlatDistance(old,player.transform.position);game.StepHostiles(.1f);
                Check(Mathf.Approximately(vitals.Health,health),"Enemy beside the empty boat cannot damage the distant swimming player");
                Check(QuestRowboat.FlatDistance(enemy.transform.position,player.transform.position)<before,"A player-targeted swimmer pursues the player rather than the old boat edge");
                var cooldown=typeof(RiverBoardingCombat).GetField("enemyAttacks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game) as Dictionary<EnemyDeckGuard,float>;
                cooldown[enemy]=Time.time-1;enemy.transform.position=player.transform.position+Vector3.right*10;Physics.SyncTransforms();health=vitals.Health;game.StepHostiles(.01f);
                Check(vitals.Health<health,"A swimmer actually in melee range can still damage the player");
                cooldown[enemy]=Time.time-1;enemy.transform.position=player.transform.position+Vector3.right*14;var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="UnderwaterMeleeSolidBlocker";wall.transform.position=player.transform.position+new Vector3(7,8,0);wall.transform.localScale=new Vector3(2,30,30);Physics.SyncTransforms();health=vitals.Health;game.StepHostiles(.01f);
                Check(Mathf.Approximately(vitals.Health,health),"Underwater melee cannot hit through a solid hull or wall");Object.DestroyImmediate(wall);
                SwimmingAt(shore);Check(game.TryWalkAshore(shore),"Player can return ashore after a swim fight");
                enemy.transform.position=shore-Vector3.right*80;game.StepHostiles(.01f);
                Check(game.CaptureSave().hostiles.First(h=>h.id==hostile.id).state==4,"Pursuing enemy transitions from swimming onto the real bank");
                var distanceOnLand=QuestRowboat.FlatDistance(enemy.transform.position,player.transform.position);game.StepHostiles(.2f);
                Check(QuestRowboat.FlatDistance(enemy.transform.position,player.transform.position)<distanceOnLand,"Enemy follows the player across land instead of attacking the empty boat");
                var oldSave=BachDangSaveSystem.Capture("QA stuck low-tide shoreline");oldSave.playerPosition=new Vector3(shore.x,TideSystem.CurrentWaterHeight-5,shore.z);oldSave.boarding.state=(int)RiverBoardingCombat.PlayerState.Swimming;oldSave.boarding.sword=false;
                BachDangSaveSystem.Apply(oldSave);game.enabled=false;combat.enabled=false;fleet.StopAllCoroutines();game.StepPlayer(0);
                Check(game.State==RiverBoardingCombat.PlayerState.Land&&motor.enabled,"Loading an old save stuck swimming in a walkable bank automatically recovers land movement");
                phase=2;until=Time.time+1;return;
            }
            if(phase==2&&Time.time>=until)
            {
                var rig=player.GetComponentInChildren<Animator>();var floor=Terrain.activeTerrain.SampleHeight(player.transform.position)+Terrain.activeTerrain.transform.position.y;
                float feet=Mathf.Min(rig.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,rig.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                Check(Mathf.Abs(feet-floor)<6,"After several live frames the recovered standing pose stays supported on the bank");Capture();Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
