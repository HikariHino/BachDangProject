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
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();static double activationMs,maxSimulationMs,coldWeaponMs,warmWeaponMs,firstSwordHitMs,firstArrowHitMs;static long weaponBytes;static int samples;static readonly List<double> timings=new List<double>();static readonly List<double> battleTimings=new List<double>();static readonly List<double> frameTimings=new List<double>();static Unity.Profiling.ProfilerRecorder mainThread;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;activationMs=maxSimulationMs=coldWeaponMs=warmWeaponMs=firstSwordHitMs=firstArrowHitMs=0;weaponBytes=0;samples=0;timings.Clear();battleTimings.Clear();frameTimings.Clear();checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/WreckPerformancePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,activationMs,maxSimulationMs,coldWeaponMs,warmWeaponMs,firstSwordHitMs,firstArrowHitMs,weaponBytes,samples,p95SimulationMs=timings.Count==0?0:timings.OrderBy(x=>x).ElementAt((int)((timings.Count-1)*.95f)),meanSimulationMs=timings.Count==0?0:timings.Average(),allAImeanMs=battleTimings.Count==0?0:battleTimings.Average(),allAImaxMs=battleTimings.Count==0?0:battleTimings.Max(),allAIp95Ms=battleTimings.Count==0?0:battleTimings.OrderBy(x=>x).ElementAt((int)((battleTimings.Count-1)*.95f)),mainThreadMeanMs=frameTimings.Count==0?0:frameTimings.Average(),checks},Newtonsoft.Json.Formatting.Indented));mainThread.Dispose();BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void MeasureWeapon()
    {
        var combat=RiverBoardingCombat.Instance;var go=Object.Instantiate(combat.allyPrefab);go.transform.position=BeachBoatQuestSession.Instance.storyCamera.transform.position+Vector3.forward*20;
        var rig=go.GetComponentInChildren<Animator>();rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;var pose=go.AddComponent<RiverFighterPose>();pose.Initialize(rig,combat.swordPrefab,combat.swordController,17);
        var late=typeof(RiverFighterPose).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);var clock=System.Diagnostics.Stopwatch.StartNew();pose.SetSword(true);late.Invoke(pose,null);clock.Stop();coldWeaponMs=clock.Elapsed.TotalMilliseconds;
        long allocation=GC.GetAllocatedBytesForCurrentThread();clock.Restart();for(int i=0;i<60;i++)late.Invoke(pose,null);clock.Stop();warmWeaponMs=clock.Elapsed.TotalMilliseconds/60;weaponBytes=(GC.GetAllocatedBytesForCurrentThread()-allocation)/60;Object.DestroyImmediate(go);
    }
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
                mainThread=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal,"Main Thread",128);
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/WreckPerfQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());BeachBoatStory.BachDangStoryManager.Instance.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act3_BattleInProgress,true);
                var scenario=BachDangSaveSystem.Capture("QA full battle performance");scenario.constructionSites=Array.Empty<BachDangSaveData.ConstructionSite>();scenario.woodCount=BeachBoatStory.BachDangStoryManager.Instance.totalWoodRequired;BachDangSaveSystem.Apply(scenario);
                var zone=BeachBoatStory.RiverStakeZone.Instance;var support=RiverConstructionCamp.Ensure(zone);
                Check(support.Workers==60&&boarding.friendlyBoats.Count(b=>b.IsConstructionSupport)==30,"The performance scene includes the actual thirty-skiff, sixty-soldier support fleet: "+support.Workers+" / "+boarding.friendlyBoats.Count(b=>b.IsConstructionSupport)+" complete="+zone.IsComplete);
                TideSystem.Instance.SetTide(false,true);fleet.enabled=false;boarding.enabled=true;
                foreach(var ship in fleet.enemyShips){var at=ship.targetPoint.position;at.y=TideSystem.CurrentWaterHeight;ship.transform.position=at;ship.gameObject.SetActive(true);ship.enabled=false;typeof(EnemyShip).GetField("battleStarted",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ship,true);ship.PrepareWreck();}
                foreach(var pose in Object.FindObjectsByType<RiverFighterPose>())if(pose.GetComponent<EnemyDeckGuard>()!=null)pose.WarmTraversal();
                Physics.SyncTransforms();phase=1;return;
            }
            if(phase==1)
            {
                boarding.enabled=false;
                var visuals=BeachBoatStory.RiverStakeZone.Instance.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Static_stake_chunk").ToArray();Check(visuals.Length>0&&visuals.All(f=>f.sharedMesh.vertexCount>0),"Performance scenario renders real stake geometry, not empty combined meshes");
                Check(fleet.enemyShips.All(s=>s.GetComponent<ShipwreckPresentation>().Colliders.Length>0),"Every split-hull collision mesh is prepared before the sinking frame");
                var clock=System.Diagnostics.Stopwatch.StartNew();foreach(var ship in fleet.enemyShips)ship.TakeDamage();clock.Stop();activationMs=clock.Elapsed.TotalMilliseconds;BachDangBattleTimeline.Instance.BeginAmbush();
                Check(fleet.enemyShips.All(s=>s.isDestroyed&&s.HasSolidWreck),"All ten wrecks activate together with solid broken hulls");
                Check(boarding.ShipwreckSurvivors>0&&boarding.ShipwreckDeaths>0,"Sinking retains both swimming survivors and visible casualties");phase=2;return;
            }
            if(phase==2)
            {
                var clock=System.Diagnostics.Stopwatch.StartNew();boarding.StepHostiles(.02f);clock.Stop();maxSimulationMs=Math.Max(maxSimulationMs,clock.Elapsed.TotalMilliseconds);timings.Add(clock.Elapsed.TotalMilliseconds);double hostileMs=clock.Elapsed.TotalMilliseconds;
                clock.Restart();boarding.StepAllies(.02f);boarding.StepDefenders();boarding.StepEnemies(.02f);clock.Stop();battleTimings.Add(hostileMs+clock.Elapsed.TotalMilliseconds);
                if(mainThread.Valid&&mainThread.LastValue>0)frameTimings.Add(mainThread.LastValue/1000000.0);
                if(++samples<120)return;
                MeasureWeapon();
                Check(CombatHitFeedback.Instance!=null&&CombatDamageNumbers.Instance!=null,"Blood and damage-number pools are already prepared before the player's first hit");
                var victim=Object.FindObjectsByType<EnemyDeckGuard>().First(g=>g.IsAlive);var impact=System.Diagnostics.Stopwatch.StartNew();bool sword=victim.Hit(1,true,CombatImpactKind.Sword);impact.Stop();firstSwordHitMs=impact.Elapsed.TotalMilliseconds;
                victim=Object.FindObjectsByType<EnemyDeckGuard>().First(g=>g.IsAlive&&g.Health==EnemyDeckGuard.MaximumHealth);impact.Restart();bool arrow=victim.Hit(1,true,CombatImpactKind.Arrow);impact.Stop();firstArrowHitMs=impact.Elapsed.TotalMilliseconds;
                Check(sword&&arrow,"Actual first sword and arrow damage events retain blood, sound and damage numbers after the wreck release");
                Check(Object.FindObjectsByType<Transform>().Count(t=>t.name.Contains("_ManhGo_"))<=120,"Debris uses a bounded prebuilt pool instead of allocating at impact");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
