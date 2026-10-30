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
    static float entryTime,peakPursuitSpeed;
    static bool approachSaveChecked,sawApproachDrop;
    static float phaseTime,boatDistance;
    static Vector3 chaseStart;
    static Vector3 baitStart;
    static Vector3[] previousPositions;
    static Quaternion[] previousRotations;
    static float maxYawRate,maxSlip;
    static int motionSamples;
    static Dictionary<string,Vector3> escortStarts;
    static readonly Dictionary<int,Vector3> supportHands=new Dictionary<int,Vector3>();
    static float supportHandMotion;
    static int lastFrame=-1;
    static string runtimeError;
    static Vector3[] berths;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
    public static void RunInEditor(){supportHands.Clear();supportHandMotion=0;phase=0;lastFrame=-1;boatDistance=0;ebbTime=entryTime=ambushTime=peakPursuitSpeed=0;approachSaveChecked=sawApproachDrop=false;previousPositions=null;previousRotations=null;maxYawRate=maxSlip=0;motionSamples=0;started=0;runtimeError=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    public static void RunWithConstructionField(){SessionState.SetBool("UseConstructionField30QA",true);RunInEditor();}
    public static void RunBatch(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");RunInEditor();}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string m,string stack,LogType t){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(t==LogType.Error||t==LogType.Exception))runtimeError=m+"\n"+stack;}
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    static void MeasureMotion(EnemyFleetDirector fleet)
    {
        if(Time.deltaTime<=0)return;
        for(int i=0;i<fleet.enemyShips.Length;i++)
        {
            var ship=fleet.enemyShips[i];var p=ship.transform.position;var r=ship.transform.rotation;
            peakPursuitSpeed=Mathf.Max(peakPursuitSpeed,ship.ForwardSpeed);
            if(!ship.isDestroyed&&previousPositions!=null)
            {
                var delta=p-previousPositions[i];delta.y=0;
                float yaw=Mathf.Abs(Vector3.SignedAngle(previousRotations[i]*Vector3.forward,r*Vector3.forward,Vector3.up))/Time.deltaTime;
                maxYawRate=Mathf.Max(maxYawRate,yaw);
                if(delta.magnitude>.03f)
                {maxSlip=Mathf.Max(maxSlip,Vector3.Angle(delta,r*Vector3.forward));motionSamples++;}
            }
        }
        previousPositions=fleet.enemyShips.Select(s=>s.transform.position).ToArray();
        previousRotations=fleet.enemyShips.Select(s=>s.transform.rotation).ToArray();
    }
    static void RowUntil(float stopZ)
    {
        var boat=QuestRowboat.Instance;
        float remaining=boat.transform.position.z-stopZ;
        if(remaining>0){BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().RestoreCheckpoint(0);boat.Step(1,0,Mathf.Min(Time.deltaTime,remaining/(boat.maxSpeed*boat.sprintMultiplier+1)),true);}
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
        var rt=new RenderTexture(1280,800,24);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
        File.WriteAllBytes("Logs/Fleet_Concurrent_Exposed_Stakes.png",pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
        Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    static void Finish(string error)
    {
        var fleet=EnemyFleetDirector.Instance;Time.timeScale=1;
        File.WriteAllText("Logs/ConcurrentBattlePlaytest.json",JsonConvert.SerializeObject(new{success=error==null,error,phase,checks,launched,ebbTime,entryTime,ambushTime,peakPursuitSpeed,sawApproachDrop,approachSaveChecked,water=TideSystem.CurrentWaterHeight,exposed=StakeZone.Instance?.ExposedStakeCount,
            lure=fleet?.Pursuit.Stage.ToString(),boatDistance,maxYawRate,maxSlip,motionSamples,boat=QuestRowboat.Instance?.transform.position.ToString(),field=StakeZone.Instance?.FieldBounds.ToString(),
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
            if(phase>=11||phase>=2&&phase<=3)MeasureMotion(fleet);
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(p.EndsWith(".json")||p.EndsWith(".bak"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/ConcurrentQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                zone.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.collectedWood=story.totalWoodRequired;
                if(SessionState.GetBool("UseConstructionField30QA",false)){SessionState.SetBool("UseConstructionField30QA",false);var field=File.Exists(".utmp/LegacyFullConstructionCheckpoint.json")?JsonUtility.FromJson<BachDangSaveData>(File.ReadAllText(".utmp/LegacyFullConstructionCheckpoint.json")):new BachDangSaveData{storyState=6,stakes=Enumerable.Repeat(true,5).ToArray(),stakeSamplesVersion=1,stakeSamplePositions=zone.CaptureSamplePositions(),stakeSampleRotations=zone.CaptureSampleRotations(),constructionSites=new[]{new BachDangSaveData.ConstructionSite{index=0,origin=zone.placements[0].approach.position,elapsed=18,parked=true}}};zone.RestoreSamples(field);RiverConstructionCamp.Ensure(zone).Restore(field);Check(RiverConstructionCamp.Ensure(zone).Workers>0&&RiverBoardingCombat.Instance.friendlyBoats.Any(b=>b.IsConstructionSupport)&&!Object.FindObjectsByType<Transform>().Any(t=>t.name.StartsWith("Coc_mau_vi_tri_")||t.name.StartsWith("Coc_nhon_cong_binh")),"Loading the old finished construction checkpoint retains manned support skiffs without symbolic poles");}
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
                escortStarts=RiverBoardingCombat.Instance.friendlyBoats.Where(b=>b.lightPatrol&&!b.IsConstructionSupport).ToDictionary(b=>b.name,b=>b.transform.position);
                phaseTime=Time.time;phase=10;return;
            }
            if(phase==10)
            {
                if(Time.time-phaseTime<20)return;
                Check(!fleet.Pursuit.IsPursuing&&!fleet.EbbStarted&&fleet.ShipsInside==0,"Waiting near home cannot automatically lure the fleet or trigger falling tide");
                var escorts=RiverBoardingCombat.Instance.friendlyBoats.Where(b=>b.lightPatrol&&!b.IsConstructionSupport).ToArray();
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
                baitStart=QuestRowboat.Instance.transform.position;phaseTime=Time.time;phase=111;return;
            }
            if(phase==111)
            {
                float elapsed=Time.time-phaseTime;
                var boat=QuestRowboat.Instance;
                var p=baitStart+new Vector3(Mathf.Sin(elapsed*2.5f)*80,0,-elapsed*45);
                if(boat.CanOccupy(p,Quaternion.Euler(0,180,0)))
                    boat.RestoreSave(true,p,Quaternion.Euler(0,180,0));
                if(elapsed<8)return;
                Check(fleet.Pursuit.Stage==RiverFleetPursuit.LureStage.Pursuit,"Alternating the bait's heading keeps the fleet in pursuit outside the trap");
                Check(maxYawRate<10.3f&&maxSlip<2,"Repeated side changes do not snap enemy headings or slide their hulls sideways");
                chaseStart=fleet.enemyShips[0].transform.position;phaseTime=Time.time;phase=12;return;
            }
            if(phase==12)
            {
                var boat=QuestRowboat.Instance;var before=boat.transform.position;
                // Exercise the production hull sweep/rowing controls along a straight return lane.
                RowUntil(zone.FieldBounds.max.z+200);
                boatDistance+=QuestRowboat.FlatDistance(before,boat.transform.position);
                if(Time.time-phaseTime<12)return;
                Check(QuestRowboat.FlatDistance(chaseStart,fleet.enemyShips[0].transform.position)>80,"The enemy formation physically follows the retreating rower");
                Check(HullsSeparate(fleet),"The pursuing enemy fleet retains separate solid hulls");
                Check(!fleet.Pursuit.HasEnteredTrap&&!fleet.EbbStarted,"Pursuit outside the stake field does not trigger the ambush");
                Check(fleet.GetComponent<RiverBattleVoices>().PursuitPlayed,"The pursuit cry is issued after the enemy meets the bait");
                var boarding=RiverBoardingCombat.Instance;var crew=boarding.CaptureSave().allies;
                Check(crew.All(a=>a.state==0&&a.arrows==2)&&boarding.AlliedArrowHits==0&&boarding.AlliedSwordHits==0,"Live allied AI cannot shoot, board or attack during pursuit");
                Check(boarding.HostileBoardings==0,"Enemy boarding parties do not leave the pursuing fleet before the ambush");
                var lights=boarding.friendlyBoats.Where(b=>b.lightPatrol&&!b.IsConstructionSupport).ToArray();
                float left=fleet.enemyShips.Min(s=>s.transform.position.x-s.stakeHitDistance);
                var close=lights.Where(b=>b.IsCloseLureEscort).ToArray();
                Check((QuestRowboat.Instance.transform.position.z<=zone.FieldBounds.max.z+350&&close.Length==0)||(close.Length==2&&close.Any(b=>b.LureEscortGoal.x<QuestRowboat.Instance.transform.position.x)&&close.Any(b=>b.LureEscortGoal.x>QuestRowboat.Instance.transform.position.x)),"Two light escorts aim for parallel flanks on opposite sides of the bait rowboat");
                Check(lights.Where(b=>!b.IsCloseLureEscort).All(b=>b.LureEscortGoal.x<left-200&&b.LureEscortGoal.z>zone.FieldBounds.max.z+250),"The remaining escort goals stay outside incoming enemy lanes and the stake entrance");
                var save=BachDangSaveSystem.Capture("QA pursuit");var anchor=fleet.Pursuit.FormationAnchor;var positions=fleet.enemyShips.Select(s=>s.transform.position).ToArray();
                // Reproduce an older save with an ally already sent to an enemy deck.
                save.boarding.allies[0].state=3;save.boarding.allies[0].boat=fleet.enemyShips[0].name;
                save.boarding.allies[0].target=BachDangSaveSystem.GuardId(RiverPlayerCombat.Instance.guards.First(g=>g.IsAlive));
                BachDangSaveSystem.Apply(save);RiverBoardingCombat.Instance.enabled=true;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
                boarding.StepAllies(.05f);
                Check(boarding.CaptureSave().allies.All(a=>a.state==0&&a.arrows==2),"Loading an old premature allied assault returns its crew to escort duty without shooting");
                Check(fleet.Pursuit.Stage==RiverFleetPursuit.LureStage.Pursuit&&Vector3.Distance(fleet.Pursuit.FormationAnchor,anchor)<.01f,"A pursuit save restores the stage and formation anchor");
                Check(fleet.enemyShips.Select((s,i)=>Vector3.Distance(positions[i],s.transform.position)<.01f).All(v=>v),"Loading pursuit preserves all physical ship positions without respawning them");
                Check(fleet.enemyShips.Select((s,i)=>Mathf.Abs(s.ForwardSpeed-save.ships.First(x=>x.name==s.name).speed)<.01f).All(v=>v),"Pursuit save restores each vessel's forward speed without a restart jerk");
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
                if(!fleet.EbbStarted)return;
                if(ebbTime==0)
                {
                    ebbTime=Time.time;
                    Check(fleet.ShipsInside>0&&fleet.ShipsInside<fleet.ShipCount,"The visible ebb starts while the rear rank is still entering, not after the fleet stops");
                }
                if(fleet.ShipsInside<fleet.ShipCount)
                {
                    if(zone.ExposedStakeCount>0||fleet.enemyShips.Any(s=>s.isDestroyed))throw new Exception("The staged ebb exposed stakes or wrecked ships before the rear rank entered");
                    if(TideSystem.CurrentWaterHeight<highWater-.5f)
                    {
                        sawApproachDrop=true;
                        if(!approachSaveChecked)
                        {
                            var save=BachDangSaveSystem.Capture("QA approach ebb");float h=TideSystem.CurrentWaterHeight;
                            BachDangSaveSystem.Apply(save);RiverBoardingCombat.Instance.enabled=true;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
                            Check(fleet.EbbStarted&&TideSystem.Instance.TargetWaterHeight>TideSystem.Instance.lowTideY&&Mathf.Abs(TideSystem.CurrentWaterHeight-h)<.01f,"Loading the approach ebb preserves the safe covered water target and visible height");
                            approachSaveChecked=true;
                        }
                    }
                    return;
                }
                entryTime=Time.time;
                Check(sawApproachDrop&&approachSaveChecked,"Water visibly falls during entry while all stakes and hulls remain safely clear");
                Check(HullsSeparate(fleet),"The entire baited formation reaches the trap without overlapping hulls");
                Check(motionSamples>100&&maxYawRate<10.3f&&maxSlip<2,"All live hulls keep smooth bounded turns and bow-aligned movement through the full pursuit and trap entry");
                Check(peakPursuitSpeed>QuestRowboat.Instance.maxSpeed*QuestRowboat.Instance.sprintMultiplier,"Enemy vessels reach a pursuit speed above the boosted rowboat");
                Check(TideSystem.Instance.IsControlledEbb&&TideSystem.Instance.TargetWaterHeight==TideSystem.Instance.lowTideY,"The last full hull entering releases the short final ebb immediately");phase=3;
                Check(RiverBoardingCombat.Instance.CaptureSave().allies.All(a=>a.state==0&&a.arrows==2),"Falling water alone still does not authorize allied attacks before exposed stakes");
                var oldEbb=BachDangSaveSystem.Capture("QA legacy long ebb");float loadedHeight=TideSystem.CurrentWaterHeight;
                oldEbb.tide.duration=22;oldEbb.tide.elapsed=0;oldEbb.tide.start=loadedHeight;
                BachDangSaveSystem.Apply(oldEbb);RiverBoardingCombat.Instance.enabled=true;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
                Check(TideSystem.Instance.EbbSecondsRemaining<=4&&Mathf.Abs(TideSystem.CurrentWaterHeight-loadedHeight)<.01f,"An older 22-second ebb save adopts the short final drop without jumping the water height");
            }
            if(phase==3)
            {
                if(!fleet.AmbushStarted)return;ambushTime=Time.time;
                var battleVoice=fleet.GetComponent<RiverBattleVoices>();var spoken=battleVoice.SpeechSource.clip;
                Check(battleVoice.QueueWreckCry(Resources.LoadAll<AudioClip>("BachDang938/WreckAudio")[0])&&battleVoice.SpeechSource.clip==spoken,"Wreck cries queue behind the existing battle sentence without interrupting or layering another speech channel");
                Check(!BeachBoatSoundscape.Instance.transform.Find("Shipwreck_Scream_Channel").GetComponent<AudioSource>().isPlaying,"The ambush uses the single battle voice channel instead of overlapping the independent wreck source");
                Check(ambushTime-entryTime<=3.1f,"The entire fleet is caught within three seconds of the rear hull entering the field");
                Check(TideSystem.CurrentWaterHeight<highWater-5&&zone.ExposedStakeCount>0,"Falling water visibly exposes the planted stakes");
                Check(fleet.enemyShips.All(s=>s.isDestroyed),"The whole fleet is caught and starts sinking in one ambush event");phase=301;phaseTime=Time.time;
                var boarding=RiverBoardingCombat.Instance;var ally=boarding.CaptureSave().allies.First(a=>a.health>0&&a.arrows>0);
                var guard=RiverPlayerCombat.Instance.guards.FirstOrDefault(g=>g.IsAlive);
                if(guard!=null)
                {
                    var old=guard.transform.position;guard.transform.position=ally.position+Vector3.forward*100;
                    int arrows=boarding.CaptureSave().allies.Sum(a=>a.arrows);boarding.StepAllies(.05f);
                    Check(boarding.CaptureSave().allies.Sum(a=>a.arrows)<arrows,"The exposed-stake ambush actually releases allied bow fire again");guard.transform.position=old;
                }
            }
            if(phase==301)
            {
                var supportPoses=Object.FindObjectsByType<RiverFighterPose>().Where(p=>p.GetComponentInParent<AmbientRiverBoat>()?.IsConstructionSupport==true).ToArray();
                foreach(var pose in supportPoses.Where(p=>p.BowShots>0))
                {
                    int id=pose.GetEntityId().GetHashCode();var hand=pose.transform.InverseTransformPoint(pose.Animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                    if(supportHands.TryGetValue(id,out var oldHand))supportHandMotion=Mathf.Max(supportHandMotion,Vector3.Distance(hand,oldHand));else supportHands[id]=hand;
                }
                if(Time.time-phaseTime<6)return;
                var boarding=RiverBoardingCombat.Instance;
                Check(boarding.CaptureSave().allies.Any(a=>a.health>0&&a.state!=0)&&boarding.AlliedArrowHits>0,"Allies both launch their boarding/swimming assault and use bows during the live ambush");
                if(supportPoses.Length>0)
                {
                    Check(supportPoses.Any(p=>p.BowShots>0)&&supportHandMotion>.5f,"Retained skiff crew visibly animate drawing and releasing their bows during the actual ambush");
                    Check(boarding.friendlyBoats.Where(b=>b.IsConstructionSupport).All(b=>b.gameObject.activeInHierarchy),"Construction skiffs stay active throughout the ambush instead of disappearing");
                }
                Check(boarding.AlliedArrowsReleased>0&&boarding.CaptureSave().allies.Any(a=>a.health>0&&a.arrows<2&&a.state==0),"Deck archers physically release arrows and remain aboard while melee troops assault");
                boarding.enabled=false;fleet.GetComponent<RiverShoreArchers>().enabled=false;phase=4;phaseTime=Time.unscaledTime;
            }
            if(phase==4)
            {
                if(!fleet.AllShipsSunk||Time.unscaledTime-phaseTime<14)return;
                Check(fleet.GetComponent<RiverBattleVoices>().EntryPlayed&&fleet.GetComponent<RiverBattleVoices>().WreckCalls==1&&fleet.GetComponent<RiverBattleVoices>().CommanderCues==1,"Trap entry order precedes one wreck scream and the single commander sentence");
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
