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
using Story=BeachBoatStory.BachDangStoryManager;
using Dialogue=BeachBoatStory.StoryDialogueUI;

public static class BeachBoatBattleFlowPlaytest
{
    const string Flag="BattleFlow938QA";
    static readonly List<string> checks=new List<string>();
    static double started,until;
    static int phase,returnTicks;
    static string runtimeError;
    static EnemyShip wreck;
    static readonly HashSet<EnemyShip> observed=new HashSet<EnemyShip>();
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string m,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Exception||type==LogType.Error||type==LogType.Assert))runtimeError=m+"\n"+stack;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
    static void Move(Vector3 p,Quaternion q){var player=BeachBoatQuestSession.Instance.player;player.transform.SetPositionAndRotation(p,q);var body=player.GetComponent<Rigidbody>();body.position=p;body.rotation=q;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void Finish(string error)
    {
        Time.timeScale=1;Directory.CreateDirectory("Logs");
        var fleet=EnemyFleetDirector.Instance;
        File.WriteAllText("Logs/BattleFlowPlaytest.json",JsonConvert.SerializeObject(new{success=error==null,failure=error,phase,checks,ships=fleet?.enemyShips.Select(s=>new{s.name,target=s.targetPoint.position.ToString(),position=s.transform.position.ToString(),s.isDestroyed,s.HasSettled,blocker=s.GetComponent<BoatHullCollision>()?.LastBlocker?.name,blockerBoat=s.GetComponent<BoatHullCollision>()?.LastBlocker?.GetComponentInParent<BoatHullCollision>()?.name}),allies=RiverBoardingCombat.Instance?.friendlyBoats.Select(b=>new{b.name,position=b.transform.position.ToString(),berth=b.ReturnBerth.ToString(),b.HasNavigableRoute,b.HasReturnedToHarbor,blocker=b.GetComponent<BoatHullCollision>()?.LastBlocker?.name,boat=b.GetComponent<BoatHullCollision>()?.LastBlocker?.GetComponentInParent<BoatHullCollision>()?.name}),remaining=fleet?.RemainingEnemies},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(error==null?0:1);
    }
    static void Capture(string path,Vector3 position,Vector3 focus)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var p=camera.transform.position;var q=camera.transform.rotation;var rt=new RenderTexture(1280,720,24){hideFlags=HideFlags.HideAndDontSave};var image=new Texture2D(1280,720,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};
        camera.transform.position=position;camera.transform.LookAt(focus);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/"+path+".png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;camera.transform.SetPositionAndRotation(p,q);Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<4||EditorApplication.timeSinceStartup<until)return;
        if(EditorApplication.timeSinceStartup-started>300){Finish("Timeout at phase "+phase);return;}
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            var session=BeachBoatQuestSession.Instance;var story=Story.Instance;var fleet=EnemyFleetDirector.Instance;var boarding=RiverBoardingCombat.Instance;var combat=RiverPlayerCombat.Instance;var boat=QuestRowboat.Instance;
            if(phase==0)
            {
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();Dialogue.Instance.Close();
                var drum=Object.FindObjectsByType<BeachBoatMinigameStation>().First(s=>s.kind==BeachBoatMinigameStation.ActivityKind.WarDrum);
                story.TransitionTo(Story.StoryState.Act1_Training,true);Move(drum.transform.position,Quaternion.identity);drum.BeginActivity();
                Check(BeachBoatMinigameStation.Active==drum&&drum.RhythmLength==8&&drum.TimeLimit==55,"Quest 1 has two shorter eight-beat rounds and 55 seconds");
                drum.SubmitRhythm(drum.ExpectedBeat);drum.SubmitRhythm(drum.ExpectedBeat);drum.SubmitRhythm(drum.ExpectedBeat%3+1);
                Check(drum.Progress==2&&drum.Mistakes==1&&drum.Elapsed==1,"A wrong drum beat costs one second without erasing correct beats");
                for(int i=0;i<16&&BeachBoatMinigameStation.Active==drum;i++)drum.SubmitRhythm(drum.ExpectedBeat);
                Dialogue.Instance.Close();Check(drum.HasCompleted&&story.currentState==Story.StoryState.Act1_GoToCampGate,"Completing easier training still advances the quest");Dialogue.Instance.Close();
                var commander=session.GetComponentInChildren<BeachBoatStory.NPCQuestGiver>();story.TransitionTo(Story.StoryState.Act1_ExploreCamp,true);
                Move(commander.transform.position+Vector3.forward*80,Quaternion.Euler(0,180,0));Check(!commander.CanReachPlayer&&!commander.TryTalk(),"Ngo Quyen rejects a distant conversation despite the old serialized radius");
                Move(commander.transform.position+Vector3.forward*9,Quaternion.Euler(0,180,0));
                var capsule=session.player.GetComponent<CapsuleCollider>();float bodyHeight=capsule.height*Mathf.Abs(session.player.transform.lossyScale.y);
                var sight=Physics.RaycastAll(session.player.transform.position+Vector3.up*bodyHeight*.55f,Vector3.back,9,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                Check(commander.CanReachPlayer&&commander.TryTalk(),"Ngo Quyen accepts a nearby face-to-face conversation: height "+bodyHeight+" / reachable "+commander.CanReachPlayer+" / blocked "+BeachBoatQuestSession.InteractionBlocked+" / rays "+string.Join(",",sight.Select(h=>h.collider.name+":"+h.transform.IsChildOf(commander.transform))));Dialogue.Instance.Close();
                Move(commander.transform.position+Vector3.up*50,Quaternion.identity);Check(!commander.CanReachPlayer,"Cannot speak to the commander from a different elevation");
                Move(session.playerSpawn.position,session.playerSpawn.rotation);
                foreach(var vessel in fleet.enemyShips)
                {
                    var route=new[]{vessel.spawnPoint}.Concat(vessel.routeWaypoints.Select(w=>w.position)).Concat(new[]{vessel.targetPoint.position}).ToArray();
                    var terrain=Terrain.activeTerrain;var tide=Object.FindAnyObjectByType<TideSystem>();
                    for(int segment=1;segment<route.Length;segment++)for(int step=0;step<=32;step++)
                    {
                        var p=Vector3.Lerp(route[segment-1],route[segment],step/32f);
                        if(terrain.SampleHeight(p)+terrain.transform.position.y>tide.lowTideY-3)throw new Exception("Fleet route crosses shallow land: "+vessel.name+" / "+p);
                    }
                }
                Check(true,"Every invasion lane stays over water from northern entry to its assigned berth");
                for(int i=0;i<fleet.enemyShips.Length;i++)for(int j=0;j<i;j++)
                {
                    var a=fleet.enemyShips[i];var b=fleet.enemyShips[j];var delta=a.targetPoint.position-b.targetPoint.position;
                    Check(Mathf.Abs(delta.x)>=a.stakeHitDistance+b.stakeHitDistance+29||Mathf.Abs(delta.z)>=a.hullHalfLength+b.hullHalfLength+29,"Separate full hull berths: "+i+" / "+j);
                }
                phase=1;until=EditorApplication.timeSinceStartup+5;return;
            }
            if(phase==1)
            {
                var walkers=Object.FindObjectsByType<NPCWander>().Where(w=>w.isActiveAndEnabled&&w.CurrentSpeed>1).ToArray();
                Check(walkers.Length>0,"Village/camp citizens actually move between activities");
                Check(walkers.All(w=>w.walkSpeed>=BachDangWorldScale.ForScene(w.gameObject.scene)*1.14f),"NPC walking speed raised to a natural world-scaled pace");
                Check(walkers.All(w=>w.animator.speed>=.94f&&w.animator.speed<=1.31f),"Walking and stopping keep a normal animation cadence");
                Check(walkers.Where(w=>w.autoGenerateWaypoints).All(w=>w.wanderRadius>=BachDangWorldScale.ForScene(w.gameObject.scene)*8),"Free-roaming NPCs cover larger areas");
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.collectedWood=story.totalWoodRequired;story.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);
                Move(boat.boardingPoint.position,boat.boardingPoint.rotation);Check(boat.TryBoard(),"Can launch quest 3");combat.ReturnFireEnabled=false;boarding.enabled=false;
                foreach(var ship in fleet.enemyShips)ship.moveSpeed=150;
                Time.timeScale=6;phase=2;return;
            }
            foreach(var ship in fleet.enemyShips)if(ship.isDestroyed&&observed.Add(ship))Check(QuestRowboat.FlatDistance(ship.transform.position,ship.targetPoint.position)<=40,"Hull only sinks after reaching its own inner-field berth: "+ship.name);
            if(phase==2)
            {
                if(!fleet.EbbStarted)return;
                Check(fleet.ShipsInside==fleet.ShipCount,"Ebb waits until the entire fleet is deep inside the stake field");Check(!fleet.TrySignalAttack()&&fleet.CommandScore==0,"Space no longer triggers any flag order");phase=3;
            }
            if(phase==3)
            {
                wreck=fleet.enemyShips.FirstOrDefault(s=>s.HasSettled);if(wreck==null)return;
                Time.timeScale=1;boarding.enabled=false;Vector3 surface=default;bool found=false;
                for(int x=-120;x<=120&&!found;x+=24)for(int z=-160;z<=160&&!found;z+=24)
                {var probe=wreck.transform.position+new Vector3(x,0,z);probe.y=TideSystem.CurrentWaterHeight-5;found=wreck.TryWreckBoardingPoint(probe,out surface);}
                Check(found,"A settled wreck exposes real climbable wooden surfaces");Check(boarding.HandleActionKey(KeyCode.F),"Can enter the water to approach a wreck");
                var entry=surface+Vector3.right*8;entry.y=TideSystem.CurrentWaterHeight-5;Move(entry,Quaternion.identity);
                Check(boarding.HandleActionKey(KeyCode.Space)&&boarding.State==RiverBoardingCombat.PlayerState.Climbing,"Space starts climbing the sunken hull");
                boarding.StepPlayer(1.9f);Check(boarding.State==RiverBoardingCombat.PlayerState.EnemyDeck&&boarding.SwordDrawn,"Climb completes on the wreck with sword drawn");
                var standing=session.player.transform.position;Check(wreck.TryWreckSurfaceAt(standing,out var support)&&Mathf.Abs(standing.y-support.y)<2,"Player stands on the retained mesh, not an invisible raft");
                wreck.transform.position+=Vector3.right*7;Physics.SyncTransforms();boarding.StepPlayer(0);
                Check(Mathf.Abs(session.player.transform.position.x-standing.x-7)<1,"Player stays attached when the solid wreck moves");
                wreck.transform.position-=Vector3.right*7;Physics.SyncTransforms();boarding.StepPlayer(0);
                var victim=combat.guards.First(g=>g.IsAlive);victim.transform.position=standing+session.player.transform.forward*16;
                int health=victim.Health;Check(boarding.TrySlash()&&victim.Health==health-1,"Sword deals damage while standing on a wreck");
                Capture("Wreck_Climb_Fixed",standing+new Vector3(-100,50,-100),standing+Vector3.up*10);
                boarding.enabled=true;boarding.AlliedAIEnabled=true;Time.timeScale=6;phase=4;return;
            }
            if(phase==4)
            {
                if(!fleet.AllShipsSunk)return;
                Check(fleet.RemainingEnemies==0||story.currentState==Story.StoryState.Act3_BattleInProgress,"Clear remaining enemies objective stays active after the last ship sinks");
                Capture("Fleet_Dispersed_Wrecks",new Vector3(-2550,TideSystem.CurrentWaterHeight+1000,1200),new Vector3(-1450,TideSystem.CurrentWaterHeight,2150));
                foreach(var guard in combat.guards)guard.DefeatFromVolley();phase=5;return;
            }
            if(phase==5)
            {
                if(story.currentState!=Story.StoryState.Act3_Victory)return;
                Time.timeScale=1;Check(fleet.CommandScore==0&&fleet.RemainingEnemies==0,"Quest 3 wins automatically without a single flag order");
                var friends=boarding.friendlyBoats;Check(friends.All(b=>b.IsReturningToHarbor||b.HasReturnedToHarbor),"All allied ships receive the return-to-harbor order");
                phase=6;started=EditorApplication.timeSinceStartup;return;
            }
            if(phase==6)
            {
                var friends=boarding.friendlyBoats;
                for(int tick=0;tick<40&&friends.Any(a=>!a.HasReturnedToHarbor)&&returnTicks<8000;tick++,returnTicks++)foreach(var moving in friends)moving.Step(.05f);
                File.WriteAllText("Logs/SolidReturnProgress.json",JsonConvert.SerializeObject(new{returnTicks,allies=friends.Select(b=>new{b.name,pos=b.transform.position.ToString(),berth=b.ReturnBerth.ToString(),b.HasReturnedToHarbor,b.HasNavigableRoute,blocker=b.GetComponent<BoatHullCollision>()?.LastBlocker?.name,owner=b.GetComponent<BoatHullCollision>()?.LastBlocker?.GetComponentInParent<BoatHullCollision>()?.name})},Formatting.Indented));
                if(returnTicks<8000&&friends.Any(a=>!a.HasReturnedToHarbor))return;
                foreach(var ally in friends)
                {
                    Check(ally.HasReturnedToHarbor&&QuestRowboat.FlatDistance(ally.transform.position,ally.ReturnBerth)<13,"Ally reaches and stops at its harbor berth: "+ally.name+" / pos "+ally.transform.position+" / berth "+ally.ReturnBerth+" / dist "+QuestRowboat.FlatDistance(ally.transform.position,ally.ReturnBerth)+" / returning "+ally.IsReturningToHarbor+" / blocked "+BeachBoatQuestSession.InteractionBlocked+" / boat "+boat.transform.position);
                }
                Check(!story.objectiveDescText.text.Contains("phất")&&story.objectiveDescText.text.Contains("rút về"),"Victory HUD explains the result and returning fleet");Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
