using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Walker=BeachBoatStory.NPCWander;
using Gate=BeachBoatStory.CampGateTransition;
using Opening=BeachBoatStory.OpeningEvent;
using Dialogue=BeachBoatStory.StoryDialogueUI;
using Story=BeachBoatStory.BachDangStoryManager;
using Orbit=BeachBoatStory.Camera_Script;

public static class BeachBoatDailyLifePlaytest
{
    const string Flag="DailyLife938QA";static int phase,frame;static double start;static float at;static string error;
    static Walker[] pair;static readonly List<string> checks=new List<string>();static readonly HashSet<NPCDailyActivity.Task> observed=new HashSet<NPCDailyActivity.Task>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string path)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path)));
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;start=0;error=null;checks.Clear();observed.Clear();hashes.Clear();SessionState.SetBool(Flag,true);BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/DailyLifeQA/PrePlay");EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&(type==LogType.Error||type==LogType.Exception))error=text+"\n"+stack;}
    static void Finish(string problem){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/DailyLifePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=problem==null,error=problem,phase,tasks=observed.Select(x=>x.ToString()).ToArray(),checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Capture(NPCDailyActivity worker)
    {
        var source=BeachBoatQuestSession.Instance.storyCamera;var camera=new GameObject("DailyLife_QA").AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
        var centre=worker.transform.position+Vector3.up*12;camera.transform.position=centre-worker.transform.forward*30+worker.transform.right*34+Vector3.up*15;camera.transform.LookAt(centre+worker.transform.forward*12);
        if(worker.CurrentTask==NPCDailyActivity.Task.Garden)Check(worker.ArrivedAtWorksite&&worker.WorksiteName.StartsWith("Vuon_Rau_"),"Gardening happens at the actual vegetable plot after arrival, not at a failed route stop");
        var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes("Logs/DailyLife_"+worker.CurrentTask+".png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||frame==Time.frameCount)return;EditorApplication.QueuePlayerLoopUpdate();frame=Time.frameCount;
        if(start==0)start=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-start<4)return;
        try
        {
            if(error!=null)throw new Exception(error);if(EditorApplication.timeSinceStartup-start>150)throw new Exception("Daily life QA timeout");
            if(phase==0)
            {
                foreach(var path in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[path]=Hash(path);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/DailyLifeQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<Opening>().SkipOpening();Dialogue.Instance.DismissForCheckpoint();Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);
                var gate=Object.FindObjectsByType<Gate>(FindObjectsSortMode.None).First(g=>g.gateType==Gate.GateType.VillageGate);
                pair=Object.FindObjectsByType<Walker>(FindObjectsSortMode.None).Where(w=>w.GetComponent<NPCDailyActivity>()!=null).OrderBy(w=>QuestRowboat.FlatDistance(w.transform.position,gate.transform.position)).Take(2).ToArray();
                Check(pair.Length==2,"Village gate has two actual walking actors to reproduce the crowd bug");
                var navigation=(NPCWalkNavigation)typeof(Walker).GetField("navigation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pair[0]);
                Check(navigation.FindSpawn(gate.transform.position+gate.transform.forward*12,out var spot),"A solid, supported test position exists at the village gate");
                foreach(var actor in pair){actor.enabled=true;actor.animator.enabled=true;actor.transform.position=spot;}
                BeachBoatPhysicalVillagePlaytest.Place(spot-gate.transform.forward*25);BeachBoatQuestSession.Instance.storyCamera.GetComponent<Orbit>().SnapToTarget();AmbientPopulationBudget.Instance.Refresh();Physics.SyncTransforms();
                at=Time.time;phase=1;return;
            }
            if(phase==1)
            {
                if(Time.time-at<4)return;
                var a=pair[0].GetComponent<ActorSolidBody>().SolidCollider;var b=pair[1].GetComponent<ActorSolidBody>().SolidCollider;
                bool overlap=Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth);
                Check(!overlap||depth<.15f,"Exactly overlapping gate actors separate using actual movement and solid capsules: remaining depth="+depth);
                Check(pair.All(w=>w.DistanceTravelled>1),"Both gate actors keep moving instead of freezing against each other");
                Check(NPCDailyActivity.WorksiteCount>=10,"Existing gardens, laundry yards, wells and markets provide distinct work sites");at=Time.time;phase=2;
            }
            if(phase==2)
            {
                var workers=Object.FindObjectsByType<NPCDailyActivity>(FindObjectsSortMode.None);
                foreach(var worker in workers)if(worker.Working&&worker.GetComponent<Animator>().enabled&&observed.Add(worker.CurrentTask))Capture(worker);
                if(Time.time-at<45)return;
                Check(observed.Count>=3,"At least three visible daily activities occur during normal NPC schedules: "+string.Join(",",observed));
                Check(workers.Any(w=>w.WorkCycles>1&&w.CurrentTask!=NPCDailyActivity.Task.Maintain),"Civilian work gestures repeat actual work cycles, beyond guard idle poses");
                if(!observed.Contains(NPCDailyActivity.Task.Garden))
                {
                    var gardens=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Vuon_Rau_")).ToArray();
                    var farmers=Object.FindObjectsByType<NPCAmbientDialogue>(FindObjectsSortMode.None).Where(n=>n.role==NPCAmbientDialogue.Role.Farmer).ToArray();
                    var match=(from g in gardens from f in farmers orderby QuestRowboat.FlatDistance(g.position,f.transform.position) select new {garden=g,farmer=f}).First();
                    BeachBoatPhysicalVillagePlaytest.Place(Vector3.Lerp(match.garden.position,match.farmer.transform.position,.5f));BeachBoatQuestSession.Instance.storyCamera.GetComponent<Orbit>().SnapToTarget();AmbientPopulationBudget.Instance.Refresh();at=Time.time;phase=3;return;
                }
                foreach(var path in hashes)Check(File.Exists(path.Key)&&Hash(path.Key)==path.Value,"User save unchanged: "+Path.GetFileName(path.Key));Finish(null);
            }
            if(phase==3)
            {
                foreach(var worker in Object.FindObjectsByType<NPCDailyActivity>(FindObjectsSortMode.None))if(worker.Working&&worker.GetComponent<Animator>().enabled&&observed.Add(worker.CurrentTask))Capture(worker);
                if(Time.time-at<50&&!observed.Contains(NPCDailyActivity.Task.Garden))return;
                Check(observed.Contains(NPCDailyActivity.Task.Garden),"A real farmer reaches an existing vegetable plot and performs garden work");
                foreach(var path in hashes)Check(File.Exists(path.Key)&&Hash(path.Key)==path.Value,"User save unchanged: "+Path.GetFileName(path.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
