using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;
public static class BeachBoatSupportFleetPlaytest
{
    const string Flag="SupportFleet30QA";static int phase,frame;static double began;static float mark;static string failure;
    static Vector3 hand;static RiverFighterPose pose;static EnemyDeckGuard enemy;static BachDangSaveData saved;static int initialHealth;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/SupportFleetPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static Vector3 Hand()=>pose.transform.InverseTransformPoint(pose.Animator.GetBoneTransform(HumanBodyBones.RightHand).position);
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Support QA timed out");
            var boarding=RiverBoardingCombat.Instance;var camp=RiverConstructionCamp.Ensure(BeachBoatStory.RiverStakeZone.Instance);
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/SupportQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());BeachBoatStory.BachDangStoryManager.Instance.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp,true);
                var legacy=BachDangSaveSystem.Capture("QA old missing fleet");legacy.constructionSites=Array.Empty<BachDangSaveData.ConstructionSite>();BachDangSaveSystem.Apply(legacy);
                Check(camp.Workers==60&&boarding.friendlyBoats.Count(b=>b.IsConstructionSupport)==30,"A completed old save with no construction data recovers thirty skiffs and sixty soldiers: "+camp.Workers+" / "+boarding.friendlyBoats.Count(b=>b.IsConstructionSupport));
                saved=BachDangSaveSystem.Capture("QA recovered support");
                Check(saved.boarding.defenders.Count(d=>d.boat.Contains("Thuyen_cong_binh"))==60&&saved.patrols.Count(p=>p.name.Contains("Thuyen_cong_binh"))==30,"Support crew health and each vessel position enter the normal boarding and patrol save data");
                var injured=saved.boarding.defenders.First(d=>d.boat.Contains("Thuyen_cong_binh"));injured.health=2;
                var dead=saved.boarding.defenders.Last(d=>d.boat.Contains("Thuyen_cong_binh"));dead.health=0;dead.active=false;
                BachDangSaveSystem.Apply(saved);BachDangSaveSystem.Apply(saved);boarding.enabled=false;
                var restored=BachDangSaveSystem.Capture("QA repeat load");
                Check(boarding.friendlyBoats.Length==40&&camp.Workers==60&&restored.boarding.defenders.Count(d=>d.boat.Contains("Thuyen_cong_binh"))==60,"Repeated load never duplicates boats or their registered soldiers");
                Check(restored.boarding.defenders.First(d=>d.id==injured.id).health==2&&restored.boarding.defenders.First(d=>d.id==dead.id).health==0&&!restored.boarding.defenders.First(d=>d.id==dead.id).active,"Wounded and killed support soldiers retain health and visibility after load");
                Check(saved.patrols.Where(p=>p.name.Contains("Thuyen_cong_binh")).All(p=>QuestRowboat.FlatDistance(p.position,restored.patrols.First(r=>r.name==p.name).position)<1),"Support boat positions survive repeated load without jumping to new berths");
                Check(!Object.FindObjectsByType<Transform>().Any(t=>t.name.StartsWith("Coc_nhon_cong_binh")||t.name.StartsWith("Coc_mau_vi_tri_")),"Recovering the fleet never restores symbolic construction poles");
                pose=Object.FindObjectsByType<RiverFighterPose>().First(p=>p.GetComponentInParent<AmbientRiverBoat>()?.IsConstructionSupport==true&&p.gameObject.activeInHierarchy&&p.name.EndsWith("_0"));
                foreach(var guard in RiverPlayerCombat.Instance.guards)guard.DefeatFromVolley();enemy=RiverPlayerCombat.Instance.guards.First();enemy.IsIndependentBoarder=true;enemy.gameObject.SetActive(true);enemy.ResetGuard();enemy.enabled=false;enemy.transform.SetParent(null,true);
                enemy.transform.position=pose.transform.position+pose.transform.forward*80;enemy.GetComponent<ActorSolidBody>().Carrier=null;Physics.SyncTransforms();
                BachDangBattleTimeline.Instance.BeginAmbush();foreach(var d in (System.Collections.IEnumerable)typeof(RiverBoardingCombat).GetField("defenders",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(boarding))d.GetType().GetField("shot").SetValue(d,0f);boarding.StepDefenders();initialHealth=enemy.Health;mark=Time.time;hand=Hand();phase=1;return;
            }
            if(phase==1)
            {
                var arrows=(RiverAlliedArrowFlight)typeof(RiverBoardingCombat).GetField("alliedArrows",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(boarding);arrows.Step(Time.deltaTime);
                if(Time.time-mark<.35f)return;
                Check(pose.BowShots>0&&Vector3.Distance(hand,Hand())>.5f,"A support soldier really moves its right arm while drawing the bow");
                Check(enemy.Health==initialHealth,"Drawing a bow does not deal instant invisible damage before release");
                var chest=pose.Animator.GetBoneTransform(HumanBodyBones.Chest);BeachBoatReview29Playtest.Picture("Support_Bow",chest.position+pose.transform.forward*28+pose.transform.right*22+Vector3.up*10,chest.position);phase=2;return;
            }
            if(phase==2)
            {
                var arrows=(RiverAlliedArrowFlight)typeof(RiverBoardingCombat).GetField("alliedArrows",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(boarding);arrows.Step(Time.deltaTime);
                if(Time.time-mark<2)return;
                Check(arrows.Released>0&&enemy.Health<initialHealth,"Released physical deck arrows reach a real enemy collider and deal damage");
                arrows.Reset();enemy.ResetGuard();var carrier=pose.GetComponentInParent<AmbientRiverBoat>().transform;enemy.transform.SetParent(carrier,true);enemy.transform.position=pose.transform.position+pose.transform.forward*15;enemy.GetComponent<ActorSolidBody>().Carrier=carrier;Physics.SyncTransforms();
                boarding.StepDefenders();Check(pose.SwordVisible&&pose.Animator.enabled,"An enemy on the support deck makes its defender draw the sword with the animator enabled");hand=Hand();mark=Time.time;phase=3;return;
            }
            if(phase==3)
            {
                if(Time.time-mark<.35f)return;
                Check(Vector3.Distance(hand,Hand())>.5f,"The deck defender actually animates a sword strike instead of staying frozen");
                var chest=pose.Animator.GetBoneTransform(HumanBodyBones.Chest);BeachBoatReview29Playtest.Picture("Support_Sword",chest.position+pose.transform.forward*30+pose.transform.right*25+Vector3.up*8,chest.position);
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
