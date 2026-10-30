using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;
public static class BeachBoatWreckPerformancePlaytest
{
    const string Flag="WreckPerformance30QA";static int phase,frame;static double began;static string failure;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();static double activationMs,maxSimulationMs;static int samples;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;activationMs=maxSimulationMs=0;samples=0;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/WreckPerformancePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,activationMs,maxSimulationMs,samples,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Wreck performance QA timed out");
            var fleet=EnemyFleetDirector.Instance;var boarding=RiverBoardingCombat.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/WreckPerfQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());BeachBoatStory.BachDangStoryManager.Instance.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act3_BattleInProgress,true);
                TideSystem.Instance.SetTide(false,true);fleet.enabled=false;boarding.enabled=false;
                foreach(var ship in fleet.enemyShips){ship.transform.position=ship.targetPoint.position;ship.gameObject.SetActive(true);ship.enabled=false;typeof(EnemyShip).GetField("battleStarted",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ship,true);ship.PrepareWreck();}
                foreach(var pose in Object.FindObjectsByType<RiverFighterPose>())if(pose.GetComponent<EnemyDeckGuard>()!=null)pose.WarmTraversal();
                Physics.SyncTransforms();phase=1;return;
            }
            if(phase==1)
            {
                Check(fleet.enemyShips.All(s=>s.GetComponent<ShipwreckPresentation>().Colliders.Length>0),"Every split-hull collision mesh is prepared before the sinking frame");
                var clock=System.Diagnostics.Stopwatch.StartNew();foreach(var ship in fleet.enemyShips)ship.TakeDamage();clock.Stop();activationMs=clock.Elapsed.TotalMilliseconds;BachDangBattleTimeline.Instance.BeginAmbush();
                Check(fleet.enemyShips.All(s=>s.isDestroyed&&s.HasSolidWreck),"All ten wrecks activate together with solid broken hulls");
                Check(boarding.ShipwreckSurvivors>0&&boarding.ShipwreckDeaths>0,"Sinking retains both swimming survivors and visible casualties");phase=2;return;
            }
            if(phase==2)
            {
                var clock=System.Diagnostics.Stopwatch.StartNew();boarding.StepHostiles(.02f);clock.Stop();maxSimulationMs=Math.Max(maxSimulationMs,clock.Elapsed.TotalMilliseconds);
                if(++samples<120)return;
                Check(Object.FindObjectsByType<Transform>().Count(t=>t.name.Contains("_ManhGo_"))<=120,"Debris uses a bounded prebuilt pool instead of allocating at impact");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
