using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatFramePacingReview
{
    const string Flag="FramePacing938";
    static int phase,frames,last=-1;
    static double began;
    static string error;
    static readonly Dictionary<string,ProfilerRecorder> recorders=new Dictionary<string,ProfilerRecorder>();
    static readonly Dictionary<string,List<Dictionary<string,double>>> samples=new Dictionary<string,List<Dictionary<string,double>>>();
    public static void RunInEditor(string label){SessionState.SetString(Flag+"Label",label);SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Move(Vector3 p){var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;player.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms();}
    static void Measure(string label)
    {
        if(!samples.ContainsKey(label))samples[label]=new List<Dictionary<string,double>>();
        var data=new Dictionary<string,double>{{"frameMs",Time.unscaledDeltaTime*1000}};foreach(var pair in recorders)data[pair.Key]=pair.Value.LastValue/(pair.Key=="gcBytes"?1.0:1000000);samples[label].Add(data);
    }
    static void Finish(string failure)
    {
        var report=samples.ToDictionary(p=>p.Key,p=>p.Value[0].Keys.ToDictionary(k=>k,k=>{var values=p.Value.Select(v=>v[k]).OrderBy(v=>v).ToArray();return new{mean=values.Average(),p95=values[(int)((values.Length-1)*.95f)],max=values.Last(),frames=values.Length};}));
        File.WriteAllText("Logs/FramePacing_"+SessionState.GetString(Flag+"Label","Review")+".json",JsonConvert.SerializeObject(new{success=failure==null,error=failure,report},Formatting.Indented));foreach(var r in recorders.Values)r.Dispose();recorders.Clear();SessionState.SetBool(Flag,false);EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4||last==Time.frameCount)return;last=Time.frameCount;
        try
        {
            if(EditorApplication.timeSinceStartup-began>150)throw new Exception("Frame pacing review timed out");
            var boat=QuestRowboat.Instance;var story=Story.Instance;
            if(phase==0)
            {
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                recorders["playerLoopMs"]=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"PlayerLoop",1);
                recorders["physicsSyncMs"]=ProfilerRecorder.StartNew(ProfilerCategory.Physics,"Physics.SyncTransforms",1);
                recorders["gcBytes"]=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
                foreach(var marker in new[]{"Rowboat","HullSweep","RiverPath","NPCMovement"})recorders[marker+"Ms"]=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"BachDang."+marker,1);
                frames=0;phase=1;return;
            }
            frames++;
            if(phase==1)
            {
                Measure("camp");if(frames<150)return;
                story.collectedWood=story.totalWoodRequired;story.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver,true);Move(boat.boardingPoint.position);if(!boat.TryBoard())throw new Exception("Cannot board pacing fixture");boat.transform.position=new Vector3(-2400,TideSystem.CurrentWaterHeight,1400);Physics.SyncTransforms();frames=0;phase=2;return;
            }
            if(phase==2)
            {
                boat.Step(.8f,.15f,Mathf.Min(Time.deltaTime,.05f));Measure("rowing");if(frames<150)return;
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);story.OnReadyForBattle();frames=0;phase=3;return;
            }
            if(phase==3){boat.Step(.5f,.05f,Mathf.Min(Time.deltaTime,.05f));Measure("battleRowing");if(frames>=200)Finish(null);}
        }
        catch(Exception e){error=e.ToString();Finish(error);}
    }
}
