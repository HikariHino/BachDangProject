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

public static class BeachBoatGroundedBoatPlaytest
{
    const string Flag="Grounded938QA";static int phase,lastFrame=-1;static double began;static string runtimeError;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool pass,string label){if(!pass)throw new Exception(label);checks.Add(label);}
    static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    public static void RunInEditor(){phase=0;began=0;lastFrame=-1;runtimeError=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(kind==LogType.Exception||kind==LogType.Error))runtimeError=text+"\n"+stack;}
    static void Finish(string error)
    {
        SessionState.SetBool(Flag,false);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GroundedBoatPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));
        BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    static void Setup()
    {
        foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(p.EndsWith(".json")||p.EndsWith(".bak"))hashes[p]=Hash(p);
        BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/GroundedQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
        var year=BachDangFrontEnd.Instance.transform.Find("Home/Year").GetComponent<TMPro.TMP_Text>();
        Check(year.text=="938"&&year.characterSpacing<=2,"Title year uses three adjacent digits and compact tracking");
        BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        Story.Instance.collectedWood=5;Story.Instance.TransitionTo(Story.StoryState.Act3_BattleInProgress,true);
        RiverBoardingCombat.Instance.enabled=false;RiverPlayerCombat.Instance.enabled=false;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
        foreach(var b in RiverBoardingCombat.Instance.friendlyBoats)b.gameObject.SetActive(false);
        TideSystem.Instance.enabled=false;TideSystem.Instance.HoldTide(BachDangBattleTimeline.Instance,true,true);
    }
    static Vector3 FindParking()
    {
        var boat=QuestRowboat.Instance;var terrain=Terrain.activeTerrain;
        for(float z=1780;z<=2020;z+=20)for(float x=-3750;x<-3600;x+=5)
        {
            var p=new Vector3(x,TideSystem.CurrentWaterHeight,z);float floor=terrain.SampleHeight(p)+terrain.transform.position.y;
            if(floor<75||floor>94||!boat.CanOccupy(p,Quaternion.identity))continue;
            return p;
        }
        throw new Exception("No valid high-water shoreline parking fixture");
    }
    static void AssertKeel(string label)
    {
        var boat=QuestRowboat.Instance;var terrain=Terrain.activeTerrain;var model=boat.transform.Find("Thuyen_Go_Song");int submerged=0;float worst=0;
        foreach(var mesh in model.GetComponentsInChildren<MeshFilter>(true))foreach(var vertex in mesh.sharedMesh.vertices)
        {
            var point=mesh.transform.TransformPoint(vertex);float ground=terrain.SampleHeight(point)+terrain.transform.position.y;
            worst=Mathf.Max(worst,ground-point.y);if(point.y<ground-.3f)submerged++;
        }
        Check(submerged==0,label+": actual hull vertices stay above sand (worst penetration "+worst+")");
    }
    static void Bank()
    {
        var boat=QuestRowboat.Instance;var game=RiverBoardingCombat.Instance;var tide=TideSystem.Instance;var parking=FindParking();
        boat.RestoreSave(true,parking,Quaternion.identity);Set(game,"<State>k__BackingField",RiverBoardingCombat.PlayerState.Land);
        Check(!boat.IsGroundedOnBank&&Mathf.Abs(boat.transform.position.y-tide.highTideY)<.01f,"Valid high-water parking floats normally");
        tide.HoldTide(BachDangBattleTimeline.Instance,false,true);boat.Step(0,0,.02f);
        Check(boat.IsGroundedOnBank&&boat.transform.position.y>tide.lowTideY+2,"Falling tide rests a beached boat on sand instead of burying it");AssertKeel("Low tide");
        Set(game,"<State>k__BackingField",RiverBoardingCombat.PlayerState.Rowboat);boat.ResumeRowing();
        Set(EnemyFleetDirector.Instance,"<BattleStarted>k__BackingField",true);
        var leaving=BeachBoatQuestSession.Instance.player;leaving.transform.position-=boat.transform.right*4;leaving.GetComponent<Rigidbody>().position=leaving.transform.position;
        Check(game.TryTraverse()&&game.State==RiverBoardingCombat.PlayerState.Land,"Leaving a grounded boat lands on exposed sand in the same frame");
        var before=boat.transform.position;boat.Step(1,0,0);Check(boat.transform.position==before,"Pause does not move a grounded boat");
        var blocker=new GameObject("QA_Solid_Push_Blocker");var c=blocker.AddComponent<BoxCollider>();c.size=BoatHullCollision.Get(boat.transform).SolidHull.bounds.size+Vector3.one*10;blocker.transform.position=boat.transform.position;Physics.SyncTransforms();
        Check(!boat.StepPushOff(.05f)&&boat.transform.position==before,"Pushing cannot bypass a solid box while sand contact is allowed");Object.DestroyImmediate(blocker);Physics.SyncTransforms();
        // Stand on the bank side, clear of the hull's escape path. The player
        // remains solid after F exits onto land, just like other obstacles.
        var player=BeachBoatQuestSession.Instance.player;var bounds=BoatHullCollision.Get(boat.transform).SolidHull.bounds;
        var near=bounds.center;near.x=bounds.min.x-player.GetComponent<CapsuleCollider>().bounds.extents.x-2;near.y=player.transform.position.y;
        player.transform.position=near;player.GetComponent<Rigidbody>().position=near;Physics.SyncTransforms();
        Check(boat.TryPushOff(),"A nearby player can start the same push-off action used by the E prompt");
        int moved=0;for(int i=0;i<250&&boat.WaterClearance<boat.minimumDepth;i++)if(boat.StepPushOff(.05f))moved++;
        Check(moved>0&&QuestRowboat.FlatDistance(before,boat.transform.position)>10,"Push-off moves the hull in small swept steps toward the river");
        Check(!boat.IsGroundedOnBank&&boat.WaterClearance>=boat.minimumDepth,"The boat becomes usable again in sufficiently deep water");AssertKeel("Pushed into water");
        Check(boat.CanOccupy(boat.transform.position,boat.transform.rotation),"Recovered boat occupies a physically clear afloat pose");
        tide.HoldTide(BachDangBattleTimeline.Instance,true,true);boat.RestoreSave(true,parking,Quaternion.identity);
        tide.HoldTide(BachDangBattleTimeline.Instance,false,true);boat.Step(0,0,.02f);
        var saved=BachDangSaveSystem.Capture("QA beached boat");saved.boatPosition.y=tide.lowTideY;
        BachDangSaveSystem.Apply(saved);boat.MaintainBuoyancy();Check(boat.IsGroundedOnBank,"Old low-water saves repair an embedded boat onto the bank");AssertKeel("Old-save restore");
        tide.HoldTide(BachDangBattleTimeline.Instance,true,true);boat.Step(0,0,.02f);
        Check(!boat.IsGroundedOnBank&&Mathf.Abs(boat.transform.position.y-tide.highTideY)<.01f,"Rising water refloats the same grounded boat");
    }
    static void Voices()
    {
        var fleet=EnemyFleetDirector.Instance;var voices=fleet.GetComponent<RiverBattleVoices>();voices.enabled=false;
        Set(fleet,"<BattleStarted>k__BackingField",true);Set(fleet,"<AmbushStarted>k__BackingField",false);BachDangBattleTimeline.Instance.BeginLure();voices.ResetForNewBattle();
        Check(voices.AssetsReady&&!voices.SpeechSource.loop,"Short recorded film cues load on a non-looping voice channel");
        Check(voices.SpeechSource.transform.GetComponents<AudioSource>().Length==1,"Battle voice has exactly one AudioSource");
        for(int i=0;i<600;i++)voices.Step(.1f);
        Check(voices.InvasionCues==2,"The complete advance order and distinct crowd response each play once without a repeated reading loop");
        int prior=voices.InvasionCues;for(int i=0;i<600;i++)voices.Step(.1f);Check(voices.InvasionCues==prior,"An exhausted advance playlist does not restart");
        Set(fleet,"<AmbushStarted>k__BackingField",true);BachDangBattleTimeline.Instance.BeginAmbush();voices.Step(.01f);var clip=voices.SpeechSource.clip;
        Check(voices.CommanderCues==1&&clip.length>2.5f&&clip.length<4,"The initial recorded command keeps the complete sentence and its natural tail");
        Set(BeachBoatSoundscape.Instance,"nextWreckSound",0f);BeachBoatSoundscape.Instance.PlayShipwreck(QuestRowboat.Instance.transform.position);
        Check((float)typeof(RiverBattleVoices).GetField("nextEnemy",Private).GetValue(voices)<=.5f&&voices.SpeechSource.clip==clip,"The real shipwreck sound path queues a cry on the fleet voice coordinator without replacing the command");
        for(int i=0;i<8;i++){voices.RequestWreckCry();voices.Step(.02f);Check(voices.SpeechSource.clip==clip,"Wreck cries wait for the commander rather than overlapping");}
        for(int i=0;i<600;i++)voices.Step(.1f);
        Check(voices.PanicCues==3&&voices.CommanderCues==2,"Panic excerpts and both distinct commands each play once");
        var data=fleet.CaptureSave();voices.RestoreSave(data);for(int i=0;i<600;i++)voices.Step(.1f);
        Check(voices.PanicCues==3&&voices.CommanderCues==2,"Saved cue masks prevent repetition after load");
        foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<3)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Grounded boat QA timeout");
            if(phase==0){Setup();phase=1;return;}if(phase==1){Bank();phase=2;return;}Voices();Finish(null);
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
