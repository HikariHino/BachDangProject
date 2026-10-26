using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatShorelinePlaytest
{
    const string Flag="Shoreline938QA";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int phase,lastFrame=-1;static double began;static string runtimeError;
    static bool previousAudioPause;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static Vector3[] stations;static int landmasses;
    static void Check(bool pass,string label){if(!pass)throw new Exception(label);checks.Add(label);}
    static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
    public static void RunInEditor()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Start in Edit Mode");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Save the scene before QA");
        phase=0;lastFrame=-1;began=0;runtimeError=null;checks.Clear();hashes.Clear();
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Exception||type==LogType.Error))runtimeError=text+"\n"+stack;}
    static void Finish(string error)
    {
        SessionState.SetBool(Flag,false);
        try{Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ShorelinePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks,landmasses,stations=stations?.Select(p=>new{p.x,p.y,p.z}).ToArray()},Newtonsoft.Json.Formatting.Indented));}
        finally{BachDangSaveSystem.TestDirectory=null;AudioListener.pause=previousAudioPause;EditorApplication.isPlaying=false;}
    }
    static Vector3 PointAtDepth(float depth)
    {
        var terrain=Terrain.activeTerrain;var origin=QuestRowboat.Instance.boardingPoint.position;
        for(float radius=120;radius<1500;radius+=10)
        for(int i=0;i<72;i++)
        {
            var p=origin+Quaternion.Euler(0,i*5,0)*Vector3.forward*radius;p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
            var local=p-terrain.transform.position;var size=terrain.terrainData.size;
            if(Mathf.Abs(TideSystem.CurrentWaterHeight-p.y-depth)>.65f||terrain.terrainData.GetInterpolatedNormal(local.x/size.x,local.z/size.z).y<.75f)continue;
            if(RiverHullCollision.ClearAt(p+Vector3.up*10,5,BeachBoatQuestSession.Instance.player.transform))return p;
        }
        throw new Exception("No clear beach point for depth "+depth);
    }
    static float Feet()
    {
        var player=BeachBoatQuestSession.Instance.player;var c=player.GetComponent<CapsuleCollider>();return (c.center.y-c.height*.5f)*player.transform.lossyScale.y;
    }
    static void Place(Vector3 point,bool standing)
    {
        var player=BeachBoatQuestSession.Instance.player;point.y=standing?point.y-Feet()+.5f:TideSystem.CurrentWaterHeight-5;
        player.transform.position=point;var body=player.GetComponent<Rigidbody>();if(!body.isKinematic)body.linearVelocity=Vector3.zero;body.position=point;
        Physics.SyncTransforms();
    }
    static void Setup()
    {
        previousAudioPause=AudioListener.pause;AudioListener.pause=true;
        foreach(var path in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(path.EndsWith(".json")||path.EndsWith(".bak"))hashes[path]=Hash(path);
        BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/ShorelineQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
        BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        Story.Instance.collectedWood=5;BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());Story.Instance.TransitionTo(Story.StoryState.Act3_BattleInProgress,true);
        BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();RiverPlayerCombat.Instance.ReturnFireEnabled=false;RiverPlayerCombat.Instance.enabled=false;RiverBoardingCombat.Instance.enabled=false;
        foreach(var b in RiverBoardingCombat.Instance.friendlyBoats)b.enabled=false;
        var tide=TideSystem.Instance;tide.enabled=false;tide.HoldTide(BachDangBattleTimeline.Instance,true,true);
        QuestRowboat.Instance.RestoreSave(true,new Vector3(-3200,tide.highTideY,1880),Quaternion.identity);
    }
    static void FloatModes()
    {
        var boat=QuestRowboat.Instance;var game=RiverBoardingCombat.Instance;var tide=TideSystem.Instance;
        foreach(var state in new[]{RiverBoardingCombat.PlayerState.Land,RiverBoardingCombat.PlayerState.Swimming,RiverBoardingCombat.PlayerState.Climbing,RiverBoardingCombat.PlayerState.FriendlyDeck,RiverBoardingCombat.PlayerState.EnemyDeck,RiverBoardingCombat.PlayerState.Rowboat})
        {
            Set(game,"<State>k__BackingField",state);Set(game,"<SwordDrawn>k__BackingField",state==RiverBoardingCombat.PlayerState.Rowboat);
            foreach(bool high in new[]{false,true})
            {tide.HoldTide(BachDangBattleTimeline.Instance,high,true);boat.Step(0,0,.02f);Check(Mathf.Abs(boat.transform.position.y-TideSystem.CurrentWaterHeight)<.01f,"Boat follows "+(high?"rising":"falling")+" water during "+state);}
        }
        Set(game,"<State>k__BackingField",RiverBoardingCombat.PlayerState.Rowboat);Set(game,"<SwordDrawn>k__BackingField",false);boat.ResumeRowing();
    }
    static void ShoreTransitions()
    {
        var game=RiverBoardingCombat.Instance;var tide=TideSystem.Instance;var player=BeachBoatQuestSession.Instance.player;
        tide.HoldTide(BachDangBattleTimeline.Instance,true,true);
        var shallow=PointAtDepth(4);Call(game,"BeginSwim",shallow);game.StepPlayer(0,Vector2.zero);
        Check(game.State==RiverBoardingCombat.PlayerState.Swimming,"Zero time does not change traversal while paused");
        game.StepPlayer(.02f,Vector2.zero);
        Check(game.State==RiverBoardingCombat.PlayerState.Land,"Swimming on a shallow beach immediately becomes standing");
        Check(Mathf.Abs(player.transform.position.y+Feet()-shallow.y)<.6f,"Standing feet align with the real sand surface");
        Check(player.GetComponent<BeachBoatStory.Character_Movement>().enabled&&!player.GetComponent<Rigidbody>().isKinematic,"Ashore restores normal walking physics and controller");
        for(int i=0;i<40;i++)game.StepPlayer(.02f,Vector2.zero);
        Check(game.State==RiverBoardingCombat.PlayerState.Land,"Standing in shallow water stays stable instead of repeatedly entering swim");
        player.GetComponent<Rigidbody>().isKinematic=true;
        var band=PointAtDepth((game.WadeExitDepth+game.SwimEntryDepth)*.5f);Place(band,true);Set(game,"<State>k__BackingField",RiverBoardingCombat.PlayerState.Land);
        Check(!game.TryEnterWaterFromShore(),"Wading uses a deeper entry threshold than the swim exit threshold");
        Call(game,"BeginSwim",band);game.StepPlayer(.02f,Vector2.zero);
        Check(game.State==RiverBoardingCombat.PlayerState.Swimming,"Existing swim remains stable in the threshold gap");
        var deep=PointAtDepth(game.SwimEntryDepth+3);Set(game,"<State>k__BackingField",RiverBoardingCombat.PlayerState.Land);Place(deep,true);
        Check(game.TryEnterWaterFromShore()&&game.State==RiverBoardingCombat.PlayerState.Swimming,"Walking into genuinely deep water still enters swim");
        game.StepPlayer(.02f,Vector2.zero);Check(game.State==RiverBoardingCombat.PlayerState.Swimming,"Deep-water swimming is retained");
        tide.HoldTide(BachDangBattleTimeline.Instance,false,true);game.StepPlayer(.02f,Vector2.zero);
        Check(game.State==RiverBoardingCombat.PlayerState.Land,"Receding water restores a stationary swimmer onto exposed sand");
        Check(Mathf.Abs(player.transform.position.y+Feet()-deep.y)<.6f,"Low-tide landing does not leave the player under the beach");
        tide.HoldTide(BachDangBattleTimeline.Instance,true,true);Set(game,"<State>k__BackingField",RiverBoardingCombat.PlayerState.Land);
        var above=deep;above.y=TideSystem.CurrentWaterHeight+20;Place(above,true);
        Check(!game.TryEnterWaterFromShore(),"Being above deep seabed on a pier does not force swimming");
        Set(game,"<State>k__BackingField",RiverBoardingCombat.PlayerState.Rowboat);QuestRowboat.Instance.ResumeRowing();
    }
    static void BoardingAndLoad()
    {
        var boat=QuestRowboat.Instance;var tide=TideSystem.Instance;var game=RiverBoardingCombat.Instance;
        tide.HoldTide(BachDangBattleTimeline.Instance,false,true);boat.Step(0,0,.02f);
        Call(game,"BeginSwim",boat.transform.position+boat.transform.right*30);
        Check(game.NearRowboat&&game.TryReturnToRowboat(),"Player can reboard the now-floating rowboat after ebbing tide");
        game.enabled=false;boat.ResumeRowing();boat.Step(0,0,.02f);
        var stale=BachDangSaveSystem.Capture("QA legacy floating boat");stale.boatPosition.y=tide.highTideY;stale.playerPosition.y+=40;
        BachDangSaveSystem.Apply(stale);boat.Step(0,0,.02f);
        Check(Mathf.Abs(boat.transform.position.y-TideSystem.CurrentWaterHeight)<.01f,"Loading an old floating-boat pose snaps the hull to the restored tide");
        Check(Vector3.Distance(BeachBoatQuestSession.Instance.player.transform.position,boat.seat.position)<.1f,"Loaded rowing player reattaches to the corrected boat seat");
    }
    static void ArcherCoverage()
    {
        var archers=EnemyFleetDirector.Instance.GetComponent<RiverShoreArchers>();stations=archers.PreparedBanks;landmasses=archers.CoveredLandmasses;
        Check(stations.Length>=15,"Prepared ambush has at least fifteen shoreline archers");
        Check(landmasses>=2,"Separate dry landmasses receive archery groups, not only rays toward a distant bank");
        Check(stations.Min(p=>QuestRowboat.FlatDistance(p,QuestRowboat.Instance.boardingPoint.position))<300,"The reporting-dock bank also has nearby ambush soldiers");
        Check(stations.Max(p=>p.x)-stations.Min(p=>p.x)>1800,"Archers cover opposite sides of the river mouth");
        foreach(var p in stations)
        {var floor=Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y;Check(floor>TideSystem.Instance.highTideY+1.9f,"Archer station stays on dry land at high tide: "+p);}
        var voices=EnemyFleetDirector.Instance.GetComponent<RiverBattleVoices>();Check(voices.AssetsReady,"Remastered commands and the three-voice crowd chant are loadable");
        foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<3)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Shoreline QA timeout");
            if(phase==0){Setup();phase=1;return;}
            if(phase==1){FloatModes();ShoreTransitions();phase=2;return;}
            if(phase==2){BoardingAndLoad();phase=3;return;}
            ArcherCoverage();Finish(null);
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
