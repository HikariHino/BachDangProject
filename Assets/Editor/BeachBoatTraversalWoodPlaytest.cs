using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
using Dialogue=BeachBoatStory.StoryDialogueUI;
using Intro=BeachBoatStory.OpeningEvent;
using Motor=BeachBoatStory.Character_Movement;
using StakeZone=BeachBoatStory.RiverStakeZone;

public static class BeachBoatTraversalWoodPlaytest
{
    const string Flag="TraversalWood938QA";static int phase,lastFrame=-1;static double began;static string error;
    static readonly List<string> checks=new List<string>();static Vector3 openWater,shore;static EnemyDeckGuard enemy;
    public static void RunInEditor(){phase=0;lastFrame=-1;began=0;error=null;checks.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))error=text+"\n"+stack;}
    static void Check(bool yes,string label){if(!yes)throw new Exception(label);checks.Add(label);}
    static void Move(Vector3 p){var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;var b=player.GetComponent<Rigidbody>();b.position=p;if(!b.isKinematic)b.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void SetState(RiverBoardingCombat.PlayerState state,Transform carrier=null,Vector3? point=null)
    {
        var game=RiverBoardingCombat.Instance;var data=game.CaptureSave();data.state=(int)state;data.sword=false;data.deckShip=carrier?.name;
        if(point.HasValue&&carrier!=null)data.deckLocal=carrier.InverseTransformPoint(point.Value);
        game.RestoreSave(data);if(point.HasValue)Move(point.Value);
    }
    static void Finish(string failure)
    {
        File.WriteAllText("Logs/TraversalWoodPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=failure==null,error=failure,phase,checks},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static void Capture(string name)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var old=camera.targetTexture;var rt=new RenderTexture(1100,700,24);var img=new Texture2D(1100,700,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();var active=RenderTexture.active;RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,1100,700),0,0);img.Apply();File.WriteAllBytes("Logs/"+name+".png",img.EncodeToPNG());RenderTexture.active=active;camera.targetTexture=old;Object.DestroyImmediate(img);Object.DestroyImmediate(rt);
    }
    static void WoodChecks()
    {
        var s=BeachBoatQuestSession.Instance;var story=Story.Instance;var p=CampaignProgress.Instance;
        story.TransitionTo(Story.StoryState.Act1_GoToCampGate,true);story.TransitionTo(Story.StoryState.Act2_CollectWoodInVillage,true);
        Check(s.woodItems.Length==5&&s.woodItems.All(w=>w.isActiveAndEnabled&&!w.IsCollected)&&story.totalWoodRequired==5,"New journeys expose exactly five collectable logs");
        Check(s.woodItems.All(w=>!w.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.gameObject.activeInHierarchy)),"No floating wood labels remain visible");
        Check(s.woodItems.All(w=>w.interactDistance==10)&&s.woodItems.SelectMany((w,i)=>s.woodItems.Skip(i+1).Select(v=>QuestRowboat.FlatDistance(w.transform.position,v.transform.position))).Min()>=60,"Logs require close E interaction and are spread apart");
        var first=s.woodItems[0];Move(first.transform.position+Vector3.right*50);Check(!first.TryCollect(),"Cannot collect wood from far away");
        Move(first.transform.position+Vector3.up*2);Check(first.TryCollect()&&story.collectedWood==1,"Nearby E pickup collects exactly one log");
        var saved=BachDangSaveSystem.Capture("QA random village logs");var pos=s.woodItems.Select(w=>w.transform.position).ToArray();
        foreach(var w in s.woodItems)w.transform.position+=Vector3.forward*200;
        VillageWoodHunt.Instance.Restore(saved);
        Check(s.woodItems.Select((w,i)=>Vector3.Distance(w.transform.position,pos[i])<.01f&&w.IsCollected==saved.woods[i]).All(x=>x),"Save restore preserves all random positions and previously collected wood");
        p.Record.woodSeconds=90;
        foreach(var w in s.woodItems.Where(w=>!w.IsCollected)){Move(w.transform.position+Vector3.up*2);Check(w.TryCollect(),"Collect remaining reachable log "+w.name);}
        Check(story.collectedWood==5&&story.currentState==Story.StoryState.Act2_SupplyPreparation&&p.Record.woodFinished,"The fifth pickup ends the timed search and starts material preparation");
        Check(p.ConditionsFor(3).Length==9&&p.ConditionsFor(3)[8].met&&p.WoodRank==3,"Five-log search is a real ninth rank requirement");
        p.Record.woodSeconds=p.WoodTarget(3);Check(p.ConditionsFor(3)[8].met,"Exact S search deadline is accepted");p.Record.woodSeconds+=.1f;Check(!p.ConditionsFor(3)[8].met&&p.WoodRank==2,"Search beyond S drops to A instead of silently keeping S");
        var record=p.Capture();p.Restore(record,(int)story.currentState);Check(p.Record.woodFinished&&p.Record.woodSeconds==record.woodSeconds,"Search duration and target survive campaign serialization");
        var legacy=new BachDangSaveData.Campaign{completedMask=1<<2};p.Restore(legacy,10);Check(!p.WoodTimeKnown&&p.Record.historyIncomplete,"Old saves cannot invent a search achievement");p.Restore(record,(int)story.currentState);
        var legacyWood=new BachDangSaveData{woods=new[]{false,false,false,false,false,true,true},woodCount=2};VillageWoodHunt.Instance.Restore(legacyWood);Check(s.woodItems.Count(w=>w.IsCollected)==2,"Legacy seven-log saves preserve pickups outside the first five indices");VillageWoodHunt.Instance.Restore(saved);
    }
    static void NavigationChecks()
    {
        var origin=openWater+Vector3.right*900;origin.y=TideSystem.CurrentWaterHeight-4;
        var actor=new GameObject("QA swimmer path");actor.transform.position=origin;
        var pole=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pole.name="QA solid stake";pole.transform.position=origin+Vector3.forward*30;pole.transform.localScale=new Vector3(10,40,10);
        var target=origin+Vector3.forward*90;Physics.SyncTransforms();var nav=new RiverActorNavigation();
        for(int i=0;i<240;i++){actor.transform.position=nav.Move(actor.transform,actor.transform.position,target,1.2f,4);Check(RiverHullCollision.ClearAt(actor.transform.position,3.95f,actor.transform),"Swimmer path remains outside the solid stake, step "+i);if(QuestRowboat.FlatDistance(actor.transform.position,target)<4)break;}
        Check(QuestRowboat.FlatDistance(actor.transform.position,target)<4,"A swimmer detours around the pole and reaches the other side");
        var inside=pole.transform.position;inside.y=origin.y;actor.transform.position=inside;Physics.SyncTransforms();var escaped=RiverHullCollision.Move(inside,inside+Vector3.right,4,true,actor.transform);Check(RiverHullCollision.ClearAt(escaped,3.9f,actor.transform),"A legacy swimmer embedded inside a stake is separated out of that overlap");
        Object.DestroyImmediate(pole);Object.DestroyImmediate(actor);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(began==0)began=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-began<5||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(error!=null)throw new Exception(error);if(EditorApplication.timeSinceStartup-began>150)throw new Exception("Traversal review timed out");
            var s=BeachBoatQuestSession.Instance;var game=RiverBoardingCombat.Instance;var boat=QuestRowboat.Instance;var combat=RiverPlayerCombat.Instance;var fleet=EnemyFleetDirector.Instance;var story=Story.Instance;var player=s.player;var body=player.GetComponent<Rigidbody>();var motor=player.GetComponent<Motor>();var rig=player.GetComponentInChildren<Animator>();
            switch(phase)
            {
                case 0:BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<Intro>().SkipOpening();Dialogue.Instance.DismissForCheckpoint();break;
                case 1:WoodChecks();break;
                case 2:
                    Dialogue.Instance.DismissForCheckpoint();StakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.collectedWood=5;story.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);Move(boat.boardingPoint.position);Check(boat.TryBoard(),"Board the battle rowboat");fleet.StopAllCoroutines();game.enabled=false;combat.enabled=false;combat.ReturnFireEnabled=false;foreach(var ship in fleet.enemyShips)ship.enabled=false;foreach(var friendly in game.friendlyBoats)friendly.enabled=false;
                    TideSystem.Instance.HoldTide(BachDangBattleTimeline.Instance,false,true);openWater=new Vector3(-2400,TideSystem.CurrentWaterHeight,1400);boat.transform.SetPositionAndRotation(openWater,Quaternion.identity);Physics.SyncTransforms();boat.ResumeRowing();break;
                case 3:
                    Check(game.TryTraverse()&&game.State==RiverBoardingCombat.PlayerState.Swimming,"F leaves the rowboat into water");
                    Check(RiverHullCollision.ClearAt(player.transform.position+Vector3.up*3,4,player.transform),"Water exit is outside the solid hull");
                    Check(game.TryReturnToRowboat()&&game.State==RiverBoardingCombat.PlayerState.Rowboat,"Can board own rowboat from the water again");
                    var start=boat.transform.position;for(int i=0;i<30;i++)boat.Step(1,0,.05f);Check(QuestRowboat.FlatDistance(start,boat.transform.position)>10,"Reboarded boat physically rows forward");
                    var terrain=Terrain.activeTerrain;bool found=false;
                    for(float radius=20;radius<850&&!found;radius+=15)for(int angle=0;angle<360&&!found;angle+=15)
                    {var at=boat.dockPoint.position+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;float floor=terrain.SampleHeight(at)+terrain.transform.position.y;if(floor<TideSystem.CurrentWaterHeight+6||floor>TideSystem.CurrentWaterHeight+30)continue;at.y=TideSystem.CurrentWaterHeight-5;SetState(RiverBoardingCombat.PlayerState.Swimming,null,at);if(game.TryWalkAshore(at)){shore=player.transform.position;found=true;}}
                    Check(found&&motor.enabled&&!body.isKinematic,"Swimming onto a real bank restores walking");
                    combat.SetAim(true);combat.SetAim(false);Check(game.ToggleSword(),"Draw sword on land after bow aim");rig.Update(.3f);Check(rig.runtimeAnimatorController==game.swordController,"Sword stance keeps the sword animator after aiming stops");
                    var hip=rig.GetBoneTransform(HumanBodyBones.RightUpperLeg);var knee=rig.GetBoneTransform(HumanBodyBones.RightLowerLeg);var foot=rig.GetBoneTransform(HumanBodyBones.RightFoot);float kneeAngle=Vector3.Angle(hip.position-knee.position,foot.position-knee.position);Check(kneeAngle>125,"Standing sword guard does not retain the seated rowing knee: "+kneeAngle);
                    hip=rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg);knee=rig.GetBoneTransform(HumanBodyBones.LeftLowerLeg);foot=rig.GetBoneTransform(HumanBodyBones.LeftFoot);kneeAngle=Vector3.Angle(hip.position-knee.position,foot.position-knee.position);Check(kneeAngle>125,"Both legs return to a standing pose after swimming: "+kneeAngle);
                    var pose=player.GetComponent<RiverFighterPose>();typeof(RiverFighterPose).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pose,null);
                    Check(Vector3.Distance(pose.Weapon.TransformPoint(pose.Weapon.GetComponent<MeshFilter>().sharedMesh.bounds.center-Vector3.up*pose.Weapon.GetComponent<MeshFilter>().sharedMesh.bounds.size.y*.395f),pose.GripPosition)<.2f,"Sword handle stays anchored inside the gripping palm");
                    var camera=s.storyCamera;camera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;camera.transform.position=player.transform.position+player.transform.right*48+Vector3.up*21-player.transform.forward*24;camera.transform.LookAt(player.transform.position+Vector3.up*12);Capture("Sword_Guard_Revision");
                    SetState(RiverBoardingCombat.PlayerState.Land,null,shore);boat.transform.position=new Vector3(shore.x,TideSystem.CurrentWaterHeight,shore.z)+Vector3.right*20;Physics.SyncTransforms();Check(game.TryReturnToRowboat(),"Return directly from shore using the actual rowboat hull rather than its old dock");Check(!motor.enabled&&body.isKinematic&&!player.GetComponent<CapsuleCollider>().enabled&&!game.SwordDrawn,"Shore reboarding clears sword and restores kinematic rowing controls");
                    boat.transform.position=openWater;boat.ResumeRowing();start=boat.transform.position;for(int i=0;i<30;i++)boat.Step(1,0,.05f);Check(QuestRowboat.FlatDistance(start,boat.transform.position)>10,"Boat rows again after a complete water-land-boat round trip");NavigationChecks();break;
                case 4:
                    FriendlyDeckChecks();break;
                case 5:Finish(null);return;
            }
            phase++;
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void FriendlyDeckChecks()
    {
        var s=BeachBoatQuestSession.Instance;var game=RiverBoardingCombat.Instance;var combat=RiverPlayerCombat.Instance;var boat=game.friendlyBoats[0];var hull=BoatHullCollision.Get(boat.transform);bool clear=false;
        for(int ring=1;ring<=12&&!clear;ring++)for(int angle=0;angle<360&&!clear;angle+=45){var candidate=openWater+Quaternion.Euler(0,angle,0)*Vector3.forward*(ring*300);if(hull.CanOccupy(candidate,Quaternion.identity,false)){boat.transform.SetPositionAndRotation(candidate,Quaternion.identity);clear=true;}}
        Check(clear,"Place the deck fixture clear of other ships and land");Physics.SyncTransforms();var box=hull.SolidHull;var point=boat.transform.TransformPoint(box.center);
        Check(hull.TryDeckSurface(point,out var floor),"Friendly flagship has a real plank surface");var rig=s.player.GetComponentInChildren<Animator>();ActorSolidBody.WalkingDimensions(rig,out _,out float sole,out _);point=floor-Vector3.up*sole;
        SetState(RiverBoardingCombat.PlayerState.FriendlyDeck,boat.transform,point);game.ToggleSword();rig.Update(.2f);typeof(RiverBoardingCombat).GetMethod("FitPlayerToDeck",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
        var camera=s.storyCamera;camera.transform.rotation=Quaternion.identity;var start=s.player.transform.position;for(int i=0;i<30;i++)game.StepPlayer(.05f,Vector2.up);Check(QuestRowboat.FlatDistance(start,s.player.transform.position)>12,"Player can walk along the allied ship deck without its navigation box trapping them: start="+start+", end="+s.player.transform.position+", contacts="+string.Join(",",Physics.OverlapSphere(s.player.transform.position+Vector3.up*6,8).Select(c=>c.transform.parent.name+"/"+c.name)));
        Check(game.HandleActionKey(KeyCode.F)&&game.State==RiverBoardingCombat.PlayerState.Swimming,"F can leave an allied warship");Check(RiverHullCollision.ClearAt(s.player.transform.position+Vector3.up*3,4,s.player.transform),"F on a large ship lands outside its hull, not under its floor");
        Check(game.TryTraverse()&&game.State==RiverBoardingCombat.PlayerState.Climbing,"Space can climb back onto the allied ship immediately after leaving");game.StepPlayer(2,Vector2.zero);Check(game.State==RiverBoardingCombat.PlayerState.FriendlyDeck,"Reclimb completes on the allied deck");
        var data=game.CaptureSave();var hostile=data.hostiles[0];hostile.active=true;hostile.source=EnemyFleetDirector.Instance.enemyShips[0].name;hostile.boat=boat.name;hostile.state=3;hostile.targetPlayer=true;hostile.position=floor+boat.transform.forward*40;hull.TryDeckSurface(hostile.position,out var support);hostile.position.y=support.y+.2f;hostile.local=boat.transform.InverseTransformPoint(hostile.position);game.RestoreSave(data);
        foreach(var crew in boat.GetComponentInChildren<FleetDeckCrew>().soldiers)crew.gameObject.SetActive(false);
        enemy=combat.guards.First(g=>BachDangSaveSystem.GuardId(g)==hostile.id);enemy.ResetGuard();enemy.FitStandingOnDeck();SetState(RiverBoardingCombat.PlayerState.FriendlyDeck,boat.transform,point);var before=enemy.transform.position;game.StepHostiles(.01f);game.StepEnemies(.2f);Check(QuestRowboat.FlatDistance(before,enemy.transform.position)>1,"An enemy boarder can advance on the allied deck: start="+before+", end="+enemy.transform.position+", target="+s.player.transform.position);
        var attack=typeof(RiverBoardingCombat).GetMethod("CanMeleeReach",BindingFlags.Instance|BindingFlags.NonPublic);enemy.transform.position=s.player.transform.position+Vector3.forward*14;hull.TryDeckSurface(enemy.transform.position,out support);enemy.transform.position=new Vector3(enemy.transform.position.x,support.y+.2f,enemy.transform.position.z);enemy.FitStandingOnDeck();Physics.SyncTransforms();Check((bool)attack.Invoke(game,new object[]{enemy.transform,s.player.transform,27f}),"Navigation hull does not block sword reach between fighters on the same allied deck");
        var vitals=s.player.GetComponent<PlayerVitals>();float health=vitals.Health;game.StepEnemies(.01f);Check(vitals.Health<health,"Enemy on the allied ship can actually damage the nearby player");
    }
}
