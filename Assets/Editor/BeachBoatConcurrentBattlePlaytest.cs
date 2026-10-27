using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
using StakeZone=BeachBoatStory.RiverStakeZone;

public static class BeachBoatConcurrentBattlePlaytest
{
    const string Flag="ConcurrentFleet938QA";
    static double started;
    static int phase;
    static float launched,ebbTime,ambushTime,highWater;
    static float phaseTime,boatDistance;
    static Vector3 chaseStart;
    static Dictionary<string,Vector3> escortStarts;
    static int lastFrame=-1;
    static string runtimeError;
    static Vector3[] berths;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
    public static void RunInEditor(){phase=0;lastFrame=-1;boatDistance=0;started=0;runtimeError=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    public static void RunBatch(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");RunInEditor();}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string m,string stack,LogType t){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(t==LogType.Error||t==LogType.Exception))runtimeError=m+"\n"+stack;}
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    static void RowUntil(float stopZ)
    {
        var boat=QuestRowboat.Instance;
        float remaining=boat.transform.position.z-stopZ;
        if(remaining>0)boat.Step(1,0,Mathf.Min(Time.deltaTime*2,remaining/(boat.maxSpeed+1)));
    }
    static bool HullsSeparate(EnemyFleetDirector fleet)
    {
        for(int i=0;i<fleet.enemyShips.Length;i++)for(int j=i+1;j<fleet.enemyShips.Length;j++)
        {
            var a=BoatHullCollision.Get(fleet.enemyShips[i].transform).SolidHull;
            var b=BoatHullCollision.Get(fleet.enemyShips[j].transform).SolidHull;
            if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>.1f)return false;
        }
        return true;
    }
    static void Capture()
    {
        var bounds=StakeZone.Instance.FieldBounds;var camera=new GameObject("QA_Concurrent_Fleet_Camera").AddComponent<Camera>();
        camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.fieldOfView=70;
        camera.transform.position=bounds.center+new Vector3(-850,850,-1000);camera.transform.LookAt(bounds.center+Vector3.up*30);
        var rt=new RenderTexture(1280,800,24);var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
        File.WriteAllBytes("Logs/Fleet_Concurrent_Exposed_Stakes.png",pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
        Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    static void Finish(string error)
    {
        var fleet=EnemyFleetDirector.Instance;Time.timeScale=1;
        File.WriteAllText("Logs/ConcurrentBattlePlaytest.json",JsonConvert.SerializeObject(new{success=error==null,error,phase,checks,launched,ebbTime,ambushTime,water=TideSystem.CurrentWaterHeight,exposed=StakeZone.Instance?.ExposedStakeCount,
            lure=fleet?.Pursuit.Stage.ToString(),boatDistance,boat=QuestRowboat.Instance?.transform.position.ToString(),field=StakeZone.Instance?.FieldBounds.ToString(),
            ships=fleet?.enemyShips.Select(s=>new{s.name,active=s.gameObject.activeSelf,s.isDestroyed,s.HasSettled,inside=s.IsHullInsideStakeField,p=s.transform.position.ToString(),spawn=s.spawnPoint.ToString(),goal=s.targetPoint.position.ToString(),blocker=s.GetComponent<BoatHullCollision>()?.LastBlocker?.name})},Formatting.Indented));
        SessionState.SetBool(Flag,false);BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
        if(Application.isBatchMode)BeachBoatQAExit.AfterPlayMode(error==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<4)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            if(EditorApplication.timeSinceStartup-started>220)throw new Exception("Concurrent battle timed out at phase "+phase);
            var fleet=EnemyFleetDirector.Instance;var zone=StakeZone.Instance;var story=Story.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(p.EndsWith(".json")||p.EndsWith(".bak"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/ConcurrentQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                zone.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.collectedWood=story.totalWoodRequired;
                story.TransitionTo(Story.StoryState.Act3_BattleInProgress,true);
                BachDangBattleTimeline.Instance.BeginLure(true);
                QuestRowboat.Instance.RestoreSave(true,QuestRowboat.Instance.BattleLurePosition,Quaternion.identity);
                RiverBoardingCombat.Instance.RestoreSave(new BachDangSaveData.Boarding{state=0});
                berths=fleet.enemyShips.Select(s=>s.targetPoint.position).ToArray();
                // Reproduce the former deadlock: a hull can be deep inside the field
                // while more than 55 units away from its exact parking marker.
                var probe=fleet.enemyShips[0];var old=probe.transform.position;var rotation=probe.transform.rotation;
                probe.transform.SetPositionAndRotation(probe.targetPoint.position+Vector3.forward*100,Quaternion.Euler(0,180,0));Physics.SyncTransforms();
                Check(probe.IsHullInsideStakeField,"Full hull inside the stake field is recognized before the final 55-unit waypoint radius");
                probe.transform.SetPositionAndRotation(old,rotation);Physics.SyncTransforms();
                RiverPlayerCombat.Instance.ReturnFireEnabled=false;
                RiverBoardingCombat.Instance.enabled=true;fleet.StartBattle();fleet.GetComponent<RiverInvasionIntro>().Cancel();highWater=TideSystem.CurrentWaterHeight;launched=Time.time;Time.timeScale=4;phase=1;return;
            }
            if(phase==1)
            {
                int active=fleet.enemyShips.Count(s=>s.gameObject.activeInHierarchy);if(active==0)return;
                Check(active==fleet.ShipCount,"All ten enemy vessels activate in the same fleet entry");
                Check(fleet.enemyShips.All(s=>Vector3.Angle(s.transform.forward,s.targetPoint.position-s.transform.position)<1),"Every ship starts facing into its own lane without a blocked 180-degree turn");
                Check(fleet.enemyShips.Select((s,i)=>Vector3.Distance(s.targetPoint.position,berths[i])<.01f).All(v=>v),"Existing dispersed enemy wreck berths stay unchanged");
                Check(!fleet.BattleCaption.Contains("Đợt"),"Fleet entry no longer announces timed waves");
                Check(fleet.enemyShips.Min(s=>s.spawnPoint.z)>zone.FieldBounds.max.z+1400,"Enemy fleet spawns far upstream for the rower to sail out and bait it");
                escortStarts=RiverBoardingCombat.Instance.friendlyBoats.Where(b=>b.lightPatrol).ToDictionary(b=>b.name,b=>b.transform.position);
                phaseTime=Time.time;phase=10;return;
            }
            if(phase==10)
            {
                if(Time.time-phaseTime<20)return;
                Check(!fleet.Pursuit.IsPursuing&&!fleet.EbbStarted&&fleet.ShipsInside==0,"Waiting near home cannot automatically lure the fleet or trigger falling tide");
                var escorts=RiverBoardingCombat.Instance.friendlyBoats.Where(b=>b.lightPatrol).ToArray();
                Check(escorts.Length>0&&escorts.All(b=>b.IsEscortingLure),"All light boats adopt lure escort orders before the player meets the enemy");
                Check(escorts.Any(b=>QuestRowboat.FlatDistance(escortStarts[b.name],b.transform.position)>40),"Lure escorts physically row toward their flank formation");
                Check(RiverBoardingCombat.Instance.CaptureSave().allies.All(a=>a.state==0&&a.arrows==2),"Allied crew remain aboard with their arrows unspent throughout the initial lure");
                var boat=QuestRowboat.Instance;var p=fleet.Pursuit.GuidePosition;
                Check(boat.CanOccupy(p,Quaternion.Euler(0,180,0)),"The approach arrow offers a navigable position outside the nearest enemy hull");
                var voices=fleet.GetComponent<RiverBattleVoices>();voices.ResetForNewBattle();voices.PlayInvasionIntroduction();
                boat.RestoreSave(true,p,Quaternion.Euler(0,180,0));RiverBoardingCombat.Instance.RestoreSave(new BachDangSaveData.Boarding{state=0});Physics.SyncTransforms();phase=11;return;
            }
            if(phase==11)
            {
                if(!fleet.Pursuit.IsPursuing)return;
                Check(!fleet.EbbStarted,"Meeting the bait starts pursuit without exposing the stakes prematurely");
                var voices=fleet.GetComponent<RiverBattleVoices>();
                Check(!voices.PursuitPlayed&&voices.SpeechSource.clip.name=="Enemy_Advance_1","Detection queues the pursuit shout without cutting the current spoken sentence");
                chaseStart=fleet.enemyShips[0].transform.position;phaseTime=Time.time;phase=12;return;
            }
            if(phase==12)
            {
                var boat=QuestRowboat.Instance;var before=boat.transform.position;
                // Exercise the production hull sweep/rowing controls along a straight return lane.
                RowUntil(zone.FieldBounds.max.z+400);
                boatDistance+=QuestRowboat.FlatDistance(before,boat.transform.position);
                if(Time.time-phaseTime<12)return;
                Check(QuestRowboat.FlatDistance(chaseStart,fleet.enemyShips[0].transform.position)>80,"The enemy formation physically follows the retreating rower");
                Check(HullsSeparate(fleet),"The pursuing enemy fleet retains separate solid hulls");
                Check(!fleet.Pursuit.HasEnteredTrap&&!fleet.EbbStarted,"Pursuit outside the stake field does not trigger the ambush");
                Check(fleet.GetComponent<RiverBattleVoices>().PursuitPlayed,"The pursuit cry is issued after the enemy meets the bait");
                var boarding=RiverBoardingCombat.Instance;var crew=boarding.CaptureSave().allies;
                Check(crew.All(a=>a.state==0&&a.arrows==2)&&boarding.AlliedArrowHits==0&&boarding.AlliedSwordHits==0,"Live allied AI cannot shoot, board or attack during pursuit");
                Check(boarding.HostileBoardings==0,"Enemy boarding parties do not leave the pursuing fleet before the ambush");
                var lights=boarding.friendlyBoats.Where(b=>b.lightPatrol).ToArray();
                float left=fleet.enemyShips.Min(s=>s.transform.position.x-s.stakeHitDistance);
                Check(lights.All(b=>b.LureEscortGoal.x<left-200&&b.LureEscortGoal.z>zone.FieldBounds.max.z+250),"Escort goals stay outside the incoming enemy lanes and the stake entrance");
                var save=BachDangSaveSystem.Capture("QA pursuit");var anchor=fleet.Pursuit.FormationAnchor;var positions=fleet.enemyShips.Select(s=>s.transform.position).ToArray();
                // Reproduce an older save with an ally already sent to an enemy deck.
                save.boarding.allies[0].state=3;save.boarding.allies[0].boat=fleet.enemyShips[0].name;
                save.boarding.allies[0].target=BachDangSaveSystem.GuardId(RiverPlayerCombat.Instance.guards.First(g=>g.IsAlive));
                BachDangSaveSystem.Apply(save);RiverBoardingCombat.Instance.enabled=true;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
                boarding.StepAllies(.05f);
                Check(boarding.CaptureSave().allies.All(a=>a.state==0&&a.arrows==2),"Loading an old premature allied assault returns its crew to escort duty without shooting");
                Check(fleet.Pursuit.Stage==RiverFleetPursuit.LureStage.Pursuit&&Vector3.Distance(fleet.Pursuit.FormationAnchor,anchor)<.01f,"A pursuit save restores the stage and formation anchor");
                Check(fleet.enemyShips.Select((s,i)=>Vector3.Distance(positions[i],s.transform.position)<.01f).All(v=>v),"Loading pursuit preserves all physical ship positions without respawning them");
                Check(fleet.GetComponent<RiverBattleVoices>().PursuitPlayed,"Loading pursuit retains the spoken order so it is not repeated");
                fleet.Pursuit.RestoreSave(new BachDangSaveData.Fleet{active=true});
                Check(fleet.Pursuit.HasEnteredTrap,"An older active-battle save retains its ongoing berth approach instead of restarting baiting");
                fleet.Pursuit.RestoreSave(save.fleet);
                phase=13;return;
            }
            if(phase==13)
            {
                var boat=QuestRowboat.Instance;var before=boat.transform.position;
                RowUntil(zone.FieldBounds.min.z-180);
                boatDistance+=QuestRowboat.FlatDistance(before,boat.transform.position);
                if(!fleet.Pursuit.HasEnteredTrap)return;
                Check(boatDistance>500,"The player rows a substantial real distance from the enemy approach into the trap");
                phase=14;
            }
            if(phase==14)
            {
                var boat=QuestRowboat.Instance;
                if(boat.transform.position.z>zone.FieldBounds.min.z-180)
                    RowUntil(zone.FieldBounds.min.z-180);
                else if(phaseTime>=0)
                {
                    Check(!boat.IsBroken,"The rower can pass across the submerged stakes before the falling tide");
                    var save=BachDangSaveSystem.Capture("QA entering trap");BachDangSaveSystem.Apply(save);RiverBoardingCombat.Instance.enabled=true;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
                    Check(fleet.Pursuit.HasEnteredTrap,"A save after leading the fleet inside does not restart baiting");phaseTime=-1;
                }
                if(!fleet.EbbStarted)return;phase=2;
            }
            if(phase==2)
            {
                if(!fleet.EbbStarted)return;ebbTime=Time.time;
                Check(fleet.ShipsInside==fleet.ShipCount,"The complete physical formation enters deeply before the ebb begins");
                Check(HullsSeparate(fleet),"The entire baited formation reaches the trap without overlapping hulls");
                Check(TideSystem.Instance.IsControlledEbb&&TideSystem.Instance.TargetWaterHeight==TideSystem.Instance.lowTideY,"Entire fleet inside actually starts controlled falling water");phase=3;
                Check(RiverBoardingCombat.Instance.CaptureSave().allies.All(a=>a.state==0&&a.arrows==2),"Falling water alone still does not authorize allied attacks before exposed stakes");
            }
            if(phase==3)
            {
                if(!fleet.AmbushStarted)return;ambushTime=Time.time;
                Check(TideSystem.CurrentWaterHeight<highWater-5&&zone.ExposedStakeCount>0,"Falling water visibly exposes the planted stakes");
                Check(fleet.enemyShips.All(s=>s.isDestroyed),"The whole fleet is caught and starts sinking in one ambush event");phase=4;
                var boarding=RiverBoardingCombat.Instance;var ally=boarding.CaptureSave().allies.First(a=>a.health>0&&a.arrows>0);
                var guard=RiverPlayerCombat.Instance.guards.FirstOrDefault(g=>g.IsAlive);
                if(guard!=null)
                {
                    var old=guard.transform.position;guard.transform.position=ally.position+Vector3.forward*100;
                    int arrows=boarding.CaptureSave().allies.Sum(a=>a.arrows);boarding.StepAllies(.05f);
                    Check(boarding.CaptureSave().allies.Sum(a=>a.arrows)<arrows,"The exposed-stake ambush actually releases allied bow fire again");guard.transform.position=old;
                }
                boarding.enabled=false;
            }
            if(phase==4)
            {
                if(!fleet.AllShipsSunk)return;
                Check(fleet.enemyShips.All(s=>s.gameObject.activeInHierarchy&&s.HasSolidWreck),"All ten physical wrecks are retained after sinking");
                Check(zone.ExposedStakeCount>0,"Stakes remain exposed after the fleet settles");Capture();
                foreach(var guard in RiverPlayerCombat.Instance.guards)guard.DefeatFromVolley();phase=5;return;
            }
            if(phase==5)
            {
                if(fleet.BattleStarted)return;
                Check(RiverBoardingCombat.Instance.friendlyBoats.All(b=>b.IsReturningToHarbor||b.HasReturnedToHarbor),"Allied ships receive their withdrawal order after victory");
                foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
