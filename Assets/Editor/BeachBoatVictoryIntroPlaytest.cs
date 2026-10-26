using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatVictoryIntroPlaytest
{
    const string Flag="VictoryIntro938QA";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> originals=new Dictionary<string,string>();
    static int phase,lastFrame=-1;static double began;static string runtimeError;
    static float initialDistance,initialFov,returnFov;static bool middleChecked;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
    static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
    public static void RunInEditor()
    {
        if(EditorApplication.isPlaying)throw new Exception("Start in Edit Mode");
        phase=0;lastFrame=-1;began=0;runtimeError=null;middleChecked=false;checks.Clear();originals.Clear();
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=text+"\n"+stack;}
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/VictoryIntroPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=failure==null,failure,phase,checks},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    static void Setup()
    {
        foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(p.EndsWith(".json")||p.EndsWith(".bak"))originals[p]=Hash(p);
        BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/VictoryIntroQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
        BachDangFrontEnd.Instance.Play();UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        Story.Instance.collectedWood=5;var zone=BeachBoatStory.RiverStakeZone.Instance;zone.RestoreCheckpoint(Enumerable.Repeat(true,zone.CaptureCheckpoint().Length).ToArray());
        RiverPlayerCombat.Instance.ReturnFireEnabled=false;RiverBoardingCombat.Instance.AlliedAIEnabled=false;
    }
    static void PlacePlayer(Vector3 p)
    {
        var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;player.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms();
    }
    static void VictorySwimming()
    {
        var boat=QuestRowboat.Instance;var game=RiverBoardingCombat.Instance;var fleet=EnemyFleetDirector.Instance;var tide=TideSystem.Instance;
        Story.Instance.TransitionTo(Story.StoryState.Act3_Victory,true);Set(fleet,"<BattleStarted>k__BackingField",false);
        tide.HoldTide(BachDangBattleTimeline.Instance,false,true);boat.RestoreSave(true,new Vector3(-3505,70,1880),Quaternion.identity);Physics.SyncTransforms();
        var bounds=BoatHullCollision.Get(boat.transform).SolidHull.bounds;
        var outside=bounds.center;outside.x=bounds.max.x+12;outside.y=TideSystem.CurrentWaterHeight-5;
        game.RestoreSave(new BachDangSaveData.Boarding{state=(int)RiverBoardingCombat.PlayerState.Swimming});PlacePlayer(outside);
        Check(!RiverPlayerCombat.Instance.BattleActive&&game.TraversalActive,"Winning ends combat while keeping the boat traversal session alive");
        tide.HoldTide(BachDangBattleTimeline.Instance,true,true);boat.Step(0,0,.02f);Invoke(game,"Update");
        Check(game.State==RiverBoardingCombat.PlayerState.Swimming&&Mathf.Abs(BeachBoatQuestSession.Instance.player.transform.position.y-(tide.highTideY-5))<1,"A victory swimmer follows the rising tide through the real Update path");
        var before=BeachBoatQuestSession.Instance.player.transform.position;game.StepPlayer(.3f,Vector2.up);
        Check(QuestRowboat.FlatDistance(before,BeachBoatQuestSession.Instance.player.transform.position)>1,"Swimming remains movable after the final enemy dies");
        outside.y=tide.highTideY-5;PlacePlayer(outside);
        Check(game.NearRowboat,"A swimmer can reach the solid hull after the tide rises");
        Check(game.HandleActionKey(KeyCode.Space)&&game.State==RiverBoardingCombat.PlayerState.Rowboat,"SPACE boards the rowboat after victory even when BattleActive is false");
        Check(game.HandleActionKey(KeyCode.F),"F can leave the rowboat after victory");
        Set(game,"wasBattle",true);Set(game,"<State>k__BackingField",RiverBoardingCombat.PlayerState.Swimming);Invoke(game,"Update");
        Check(game.State==RiverBoardingCombat.PlayerState.Swimming&&game.PlayerAway,"Battle cleanup preserves an existing swimmer instead of forcing a seated state");
        PlacePlayer(outside);var saved=BachDangSaveSystem.Capture("QA victory swimmer");BachDangSaveSystem.Apply(saved);
        Check(game.State==RiverBoardingCombat.PlayerState.Swimming&&!RiverInvasionIntro.IsRunning,"A victory swimming save restores without replaying the invasion cinematic");
        tide.HoldTide(BachDangBattleTimeline.Instance,true,true);boat.Step(0,0,.02f);Invoke(game,"Update");
        Check(Mathf.Abs(BeachBoatQuestSession.Instance.player.transform.position.y-(tide.highTideY-5))<1,"Loaded victory swimmers continue following the water");
        PlacePlayer(outside);Check(game.TryReturnToRowboat(),"A loaded victory swimmer can return to rowing");
        game.ResetBattle();boat.LeaveForCheckpoint();
    }
    static void StartIntro()
    {
        var boat=QuestRowboat.Instance;var fleet=EnemyFleetDirector.Instance;fleet.ResetForCheckpoint();
        Story.Instance.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);
        boat.RestoreSave(true,new Vector3(-3505,TideSystem.CurrentWaterHeight,1880),Quaternion.identity);
        returnFov=BeachBoatQuestSession.Instance.storyCamera.fieldOfView;Story.Instance.OnReadyForBattle();
        Check(fleet.BattleStarted&&RiverInvasionIntro.IsRunning,"A fresh battle queues the fleet cinematic before spawning");
        Check(BeachBoatQuestSession.InputBlocked&&!BachDangSaveSystem.CanSave,"Cinematic controls and saves wait until camera control returns");
        var targets=fleet.enemyShips.Select(s=>s.targetPoint.position).ToArray();RiverFleetLayout.Configure(fleet);
        Check(fleet.enemyShips.Select((s,i)=>(s.targetPoint.position-targets[i]).sqrMagnitude<.001f).All(x=>x),"Widening the approach preserves every dispersed wreck berth");
        float longest=fleet.enemyShips.Max(s=>s.hullHalfLength);
        Check(fleet.enemyShips[4].spawnPoint.z-fleet.enemyShips[0].spawnPoint.z>=longest*2+219,"Enemy ranks have 100 extra world units of longitudinal clearance");
        foreach(var s in fleet.enemyShips)Check(BoatHullCollision.Get(s.transform).CanOccupy(s.spawnPoint,Quaternion.Euler(0,180,0),false),"Full spawn hull has clear water and solids: "+s.name);
    }
    static void ObserveIntro()
    {
        var intro=RiverInvasionIntro.Instance;var fleet=EnemyFleetDirector.Instance;var camera=BeachBoatQuestSession.Instance.storyCamera;
        if(!intro.IsPlaying)return;
        if(initialDistance==0)
        {
            var focus=BoatHullCollision.Get(fleet.enemyShips[0].transform).SolidHull.bounds.center+Vector3.up*30;
            initialDistance=Vector3.Distance(camera.transform.position,focus);initialFov=camera.fieldOfView;
            Check(fleet.enemyShips.All(s=>s.gameObject.activeInHierarchy),"All enemy vessels enter together before the cinematic starts");
            Check(!camera.GetComponent<BeachBoatStory.Camera_Script>().enabled,"The orbit camera yields control to the invasion shot");
            var voice=fleet.GetComponent<RiverBattleVoices>();Check(voice.InvasionCues==1&&voice.SpeechSource.clip.length>3,"The invasion shot starts one complete recorded enemy order with its tail intact");
        }
        if(!middleChecked&&intro.Elapsed>=3)
        {
            var focus=BoatHullCollision.Get(fleet.enemyShips[0].transform).SolidHull.bounds.center+Vector3.up*30;
            Check(Vector3.Distance(camera.transform.position,focus)<initialDistance*.85f&&camera.fieldOfView<initialFov-5,"Camera dollies and zooms closer to the moving enemy vessel");
            Check(Vector3.Dot(camera.transform.forward,(focus-camera.transform.position).normalized)>.99f,"The close shot keeps the moving ship in frame");
            ScreenCapture.CaptureScreenshot("Logs/InvasionIntro938.png");middleChecked=true;
        }
    }
    static void AfterIntro()
    {
        var fleet=EnemyFleetDirector.Instance;var camera=BeachBoatQuestSession.Instance.storyCamera;
        Check(middleChecked&&!RiverInvasionIntro.IsRunning&&camera.GetComponent<BeachBoatStory.Camera_Script>().enabled,"The cinematic completes and restores the player's orbit controls");
        Check(Mathf.Abs(camera.fieldOfView-returnFov)<.01f&&!BeachBoatQuestSession.InputBlocked&&BachDangSaveSystem.CanSave,"Camera field of view, gameplay input and saving are restored");
        var saved=BachDangSaveSystem.Capture("QA after invasion");BachDangSaveSystem.Apply(saved);
        Check(!RiverInvasionIntro.IsRunning&&fleet.BattleStarted,"Loading an ongoing battle resumes gameplay without replaying the cinematic");
        var voices=fleet.GetComponent<RiverBattleVoices>();voices.enabled=false;voices.ResetForNewBattle();
        Set(fleet,"<AmbushStarted>k__BackingField",false);BachDangBattleTimeline.Instance.BeginLure();voices.PlayInvasionIntroduction();var line=voices.SpeechSource.clip;
        Set(fleet,"<AmbushStarted>k__BackingField",true);BachDangBattleTimeline.Instance.BeginAmbush();voices.Step(.1f);
        Check(voices.SpeechSource.clip==line&&!voices.AmbushPlayed,"An ambush cannot cut the active enemy sentence halfway through");
        for(int i=0;i<40;i++)voices.Step(.1f);
        Check(voices.AmbushPlayed&&voices.CommanderCues==1&&voices.SpeechSource.clip.length>2.5f,"The full ambush order follows after the enemy's line finishes");
        for(int i=0;i<220;i++)voices.Step(.1f);
        Check(voices.CommanderCues==2,"The second complete commander line plays once");
        voices.ResetForNewBattle();Set(fleet,"<AmbushStarted>k__BackingField",false);BachDangBattleTimeline.Instance.BeginLure();voices.PlayInvasionIntroduction();voices.Step(.01f);line=voices.SpeechSource.clip;
        Set(fleet,"<BattleStarted>k__BackingField",false);voices.Step(.1f);
        Check(voices.SpeechSource.clip==line&&voices.VoiceBusySeconds>2,"Victory lets the current sentence finish instead of cutting the voice channel");
        for(int i=0;i<40;i++)voices.Step(.1f);
        Check(voices.VoiceBusySeconds==0,"The final sentence ends without starting more calls after victory");
        foreach(var p in originals)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<3)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Victory/cinematic QA timeout");
            if(phase==0){initialDistance=0;Setup();phase=1;return;}
            if(phase==1){VictorySwimming();phase=2;return;}
            if(phase==2){StartIntro();phase=3;return;}
            if(phase==3){ObserveIntro();if(!RiverInvasionIntro.IsRunning&&middleChecked)phase=4;return;}
            AfterIntro();Finish(null);
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
