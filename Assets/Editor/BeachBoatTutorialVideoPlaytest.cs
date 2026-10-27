using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatTutorialVideoPlaytest
{
    const string Flag="RationsVideo938QA";const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int phase,lastFrame=-1;static double began,waitUntil,pausedAt;static float seconds;static string runtimeError;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static VillageWorkStall stall;static VillageWorkUI ui;static RationsTutorialVideo video;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
    static void Invoke(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static Transform Instructions=>ui.transform.Find("WorkOverlay/Workbench/Instructions");
    static void Click(string path)=>Instructions.Find(path).GetComponent<Button>().onClick.Invoke();
    static void Move(Vector3 p){var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;player.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms();}
    public static void RunInEditor(){phase=0;lastFrame=-1;began=0;runtimeError=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string s,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=s+"\n"+stack;}
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TutorialVideoPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=failure==null,failure,phase,checks},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    static void Setup()
    {
        foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(p.EndsWith(".json")||p.EndsWith(".bak"))hashes[p]=Hash(p);
        BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/TutorialVideoQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
        BachDangFrontEnd.Instance.Play();UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);stall=VillagePreparationQuest.Instance.stalls.First(s=>s.kind==VillageWorkStall.WorkKind.Provisions);
        Move(stall.InteractionPosition);Check(stall.TryOpen(),"Rations opens at its actual stall");ui=UnityEngine.Object.FindAnyObjectByType<VillageWorkUI>();Invoke(ui,"Update");video=UnityEngine.Object.FindAnyObjectByType<RationsTutorialVideo>();
        Check(video!=null&&RationsTutorialVideo.IsWatching,"The instructions screen automatically opens its packaged video");
        Check(!Instructions.Find("Rules").gameObject.activeSelf,"The long rations instruction paragraph is replaced by the video");
        var clip=Resources.Load<VideoClip>("BachDang938/Tutorials/RationsHowTo");Check(clip!=null&&clip.length>=14.9&&clip.audioTrackCount>0&&video.IsReady,"The full 15-second video, predecoded frames and audio track are packaged");
        Check(Instructions.Find("StartWork").gameObject.activeInHierarchy,"Start is available immediately to skip watching");
    }
    static void VideoReady()
    {
        if(video.HasFailed)throw new Exception("The platform video decoder reports an error");if(!video.IsReady||!video.IsPlaying||video.Position<2.5)return;
        Check(!video.HasFailed,"The codec-independent movie player loads all required media");
        Check(stall.Phase==VillageWorkStall.WorkPhase.Instructions&&stall.Elapsed==0&&stall.Progress==0,"The video never submits real ingredients or starts the minigame timer");
        var raw=Instructions.Find("RationsVideo/Screen").GetComponent<RawImage>();
        Check(raw.texture!=null&&video.CurrentFrame>=20&&raw.uvRect.width<.2f,"The UI has advanced through visible video frames rather than staying on a poster");
        typeof(BeachBoatVillageWorkPlaytest).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"RationsVideoInGame"});
        seconds=CampaignProgress.Instance.Record.seconds;Click("VideoControls/Pause");pausedAt=video.Position;waitUntil=EditorApplication.timeSinceStartup+.6;phase=2;
    }
    static void Paused()
    {
        if(EditorApplication.timeSinceStartup<waitUntil)return;
        Check(!video.IsPlaying&&Math.Abs(video.Position-pausedAt)<.12,"Pause freezes the demonstration without leaving the instructions");
        Check(CampaignProgress.Instance.Record.seconds==seconds,"Watching or pausing the tutorial does not charge campaign achievement time");
        Click("VideoControls/Replay");phase=3;
    }
    static void Replayed()
    {
        if(!video.IsReady||!video.IsPlaying||video.Position<.3)return;
        Check(video.Position<2,"Replay restarts the video from the beginning");
        Click("StartWork");Check(stall.Phase==VillageWorkStall.WorkPhase.Playing&&stall.Elapsed==0,"Start skips the movie and begins a fresh timed attempt directly");Invoke(ui,"Update");
        Check(!RationsTutorialVideo.IsWatching&&!video.IsPlaying,"Starting play stops and hides the video and its audio");
        stall.AdvanceTime(.2f);Check(stall.Elapsed>=.2f,"The real round timer works independently of video playback");
        Check(!Instructions.parent.Find("Work/Ingredients/Ingredient0/Recipe").GetComponent<TMPro.TMP_Text>().text.Contains("="),"The timed round still hides calculated answers");
        Check(BachDangFrontEnd.Instance.HandleEscape()&&VillageWorkStall.Active==null,"ESC exits the minigame after the video without opening Pause");
        Invoke(ui,"Update");Check(!RationsTutorialVideo.IsWatching&&!ui.IsVisible,"No video or modal remains visible after closing");
        var save=BachDangSaveSystem.Capture("QA learned video");save.stallLevels[Array.IndexOf(VillagePreparationQuest.Instance.stalls,stall)]=1;BachDangSaveSystem.Apply(save);Move(stall.InteractionPosition);Check(stall.TryOpen(),"An existing learned rations save can reopen the new video screen");Invoke(ui,"Update");
        Check(stall.CompletedLevels==1&&stall.Difficulty==1,"Video changes preserve saved difficulty and completed levels");
        Click("VideoControls/Practice");Check(stall.IsPractice&&stall.Elapsed==0&&stall.CompletedLevels==1,"Optional hands-on practice stays untimed even at a saved Medium level");
        phase=4;
    }
    static void ClosePractice()
    {
        Check(BachDangFrontEnd.Instance.HandleEscape()&&VillageWorkStall.Active==null,"ESC also closes optional practice");
        phase=5;
    }
    static void OpenFullVideo()
    {
        Move(stall.InteractionPosition);Check(stall.TryOpen(),"The tutorial can be reopened after closing practice");Invoke(ui,"Update");phase=6;
    }
    static void FullVideoEnds()
    {
        if(video.Position<RationsTutorialVideo.Duration)return;
        Check(!video.IsPlaying&&video.Position==RationsTutorialVideo.Duration,"The complete movie reaches its final frame and stops without looping");
        Check(!video.GetComponent<AudioSource>().isPlaying,"Tutorial audio stops at the end of the movie");
        Check(Instructions.Find("VideoStatus").GetComponent<TMPro.TMP_Text>().text.Contains("Đến lượt bạn"),"The finished video invites the player to begin");
        Click("VideoControls/Replay");phase=7;
    }
    static void ReplayAfterEnd()
    {
        if(!video.IsPlaying||video.Position<.3)return;
        Check(video.Position<2,"Replay also works after watching the entire movie");
        Check(BachDangFrontEnd.Instance.HandleEscape()&&VillageWorkStall.Active==null,"ESC closes the video screen immediately");Invoke(ui,"Update");
        Check(!video.IsPlaying&&!video.GetComponent<AudioSource>().isPlaying,"Closing the video stops both pictures and audio");
        foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<3)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>90)throw new Exception("Video QA timeout, phase "+phase);
            if(phase==0){Setup();phase=1;return;}if(phase==1){VideoReady();return;}if(phase==2){Paused();return;}if(phase==3){Replayed();return;}if(phase==4){ClosePractice();return;}if(phase==5){OpenFullVideo();return;}if(phase==6){FullVideoEnds();return;}ReplayAfterEnd();
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
