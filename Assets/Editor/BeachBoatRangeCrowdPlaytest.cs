using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Walker=BeachBoatStory.NPCWander;
using Object=UnityEngine.Object;

public static class BeachBoatRangeCrowdPlaytest
{
    const string Flag="ArcheryCrowd938QA";static int phase,frame;static float at,nextSample;static double began;static string failure;
    static Walker[] group;static ArcheryTraining range;static float worstOverlap;static int laneSamples;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string path)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path)));
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;at=nextSample=0;began=0;failure=null;worstOverlap=0;laneSamples=0;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/RangeCrowdQA/PrePlay");EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&(type==LogType.Error||type==LogType.Exception))failure=text+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/RangeCrowdPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,worstOverlap,laneSamples,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static NPCWalkNavigation Nav(Walker walker)=>(NPCWalkNavigation)typeof(Walker).GetField("navigation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(walker);
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>115)throw new Exception("Range crowd QA timeout");
            if(phase==0)
            {
                foreach(var path in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[path]=Hash(path);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/RangeCrowdQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();range=ArcheryTraining.Instance;
                BeachBoatPhysicalVillagePlaytest.Place(range.Approach);BeachBoatQuestSession.Instance.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().SnapToTarget();AmbientPopulationBudget.Instance.Refresh();
                var actors=Object.FindObjectsByType<Walker>(FindObjectsSortMode.None).Where(w=>w.isActiveAndEnabled&&!w.IsRaisedPatrol&&Nav(w)!=null).ToArray();
                Check(actors.All(w=>!range.ContainsLane(w.transform.position,Nav(w).Radius)),"Camp NPCs start outside the firing lane with capsule clearance");
                var forward=(range.Targets[1].transform.position-range.Approach);forward.y=0;forward.Normalize();var right=Vector3.Cross(Vector3.up,forward);var centre=Vector3.Lerp(range.Approach,range.Targets[1].transform.position,.5f);
                var route=new List<Vector3>();var nav=Nav(actors.OrderBy(w=>QuestRowboat.FlatDistance(w.transform.position,range.Approach)).First());
                Check(!nav.Clear(centre-right*70,centre+right*70,false),"A route cannot cut across the reserved archery lane even if the endpoint is outside");
                Check(!nav.Route(range.Approach,centre,route),"A patrol cannot choose a target inside the shooting lane");
                group=actors.OrderBy(w=>QuestRowboat.FlatDistance(w.transform.position,range.Approach)).Take(8).ToArray();Check(group.Length==8,"Eight real camp walkers reproduce the dense soldier cluster");
                Check(Nav(group[0]).FindSpawn(range.Approach+right*65,out var spot),"The crowded-soldier reproduction starts on supported, clear ground outside the range");
                foreach(var actor in group){actor.transform.position=spot;actor.animator.enabled=true;}Physics.SyncTransforms();at=Time.time;phase=1;return;
            }
            if(Time.time-at<12)return;
            if(Time.time>=nextSample)
            {
                nextSample=Time.time+.3f;
                var nearby=Object.FindObjectsByType<Walker>(FindObjectsSortMode.None).Where(w=>w.isActiveAndEnabled&&!w.IsRaisedPatrol&&Nav(w)!=null&&QuestRowboat.FlatDistance(w.transform.position,range.Approach)<260).ToArray();
                foreach(var actor in nearby)
                {
                    if(range.ContainsLane(actor.transform.position,Nav(actor).Radius-.1f))throw new Exception("NPC entered the firing lane: "+actor.name);laneSamples++;
                }
                for(int i=0;i<group.Length;i++)for(int j=i+1;j<group.Length;j++)
                {
                    var a=group[i].GetComponent<ActorSolidBody>().SolidCollider;var b=group[j].GetComponent<ActorSolidBody>().SolidCollider;
                    if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth))worstOverlap=Mathf.Max(worstOverlap,depth);
                }
            }
            if(Time.time-at<30)return;
            Check(worstOverlap<.2f,"All eight crowded actors separate without leaving intersecting solid bodies: "+worstOverlap);
            Check(group.All(w=>w.DistanceTravelled>8),"Every crowded soldier resumes actual movement");Check(laneSamples>50,"Normal patrols stay outside the range through repeated observations: "+laneSamples);
            foreach(var path in hashes)Check(File.Exists(path.Key)&&Hash(path.Key)==path.Value,"User save unchanged: "+Path.GetFileName(path.Key));Finish(null);
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
