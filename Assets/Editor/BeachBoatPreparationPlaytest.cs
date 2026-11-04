using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
using Dialogue=BeachBoatStory.StoryDialogueUI;
public static class BeachBoatPreparationPlaytest
{
    const string Flag="PreparationCampaignQA";static int phase,index,lastFrame;static double courierDeadline;static float captureAfter;static double began;static string failure;static float volume;static bool initialized;static BachDangSaveData clean,carried;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    public static void RunInEditor(){phase=index=0;lastFrame=-1;began=0;failure=null;initialized=false;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/PreparationQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&(type==LogType.Error||type==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){if(initialized)BachDangPreferences.SetVolume(volume);SessionState.SetBool(Flag,false);File.WriteAllText("Logs/PreparationPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,index,checks},Newtonsoft.Json.Formatting.Indented));VillageWorkStall.Active?.Close();BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Place(Vector3 p)=>BeachBoatPhysicalVillagePlaytest.Place(p);
    static PreparationCampaign Prep=>PreparationCampaign.Instance;
    static Transform Root=>GameObject.Find("Campaign_Supply_Logistics").transform;
    static Transform Fallen(int i)=>Root.Find("Kien_hang_roi_"+i);
    static Transform Choice(int i)=>Root.Find("Xe_tiep_te_ben_song/"+(i==1?"Uu_tien_cung_thu":"Uu_tien_cuu_thuong"));
    static void Gather()
    {
        for(int i=0;i<3;i++)
        {
            Place(Fallen(i).position+Vector3.back*7);Check(Prep.Interact()&&Prep.Carrying,"Pickup fallen supply "+i);Place(Prep.DockPosition+Vector3.back*13);Check(Prep.Interact()&&!Prep.Carrying,"Place recovered supply on the physical freight cart "+i);
        }
    }
    static void Capture(string name,Vector3? eye=null,Vector3? target=null)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;if(eye.HasValue){camera.transform.position=eye.Value;camera.transform.LookAt(target.Value);}var rt=new RenderTexture(1280,800,24){hideFlags=HideFlags.HideAndDontSave};var image=new Texture2D(1280,800,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes("Logs/Preparation_"+name+".png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;camera.transform.SetPositionAndRotation(position,rotation);Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>160)throw new Exception("Preparation QA timed out");var player=BeachBoatQuestSession.Instance.player;var combat=RiverPlayerCombat.Instance;var vitals=player.GetComponent<PlayerVitals>();
            if(phase==0)
            {
                foreach(var file in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[file]=Hash(file);BachDangAutoSave.Instance.enabled=false;volume=BachDangPreferences.Volume;initialized=true;BachDangPreferences.SetVolume(0);BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();Dialogue.Instance.DismissForCheckpoint();Story.Instance.collectedWood=5;Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);phase=1;return;
            }
            if(phase==1)
            {
                if(BeachBoatQuestSession.InteractionBlocked||Prep==null||Prep.DockPosition==Vector3.zero)return;Check(Prep.Data.preparedMask==0&&combat.QuiverCapacity==6,"A fresh campaign starts with no invented freight or arrow bonus");phase=2;return;
            }
            if(phase==2)
            {
                if(BeachBoatQuestSession.InteractionBlocked)return;var stall=VillagePreparationQuest.Instance.stalls.First(s=>(int)s.kind==index);Place(stall.InteractionPosition);Check(stall.TryOpen(),"Open real physical supply trade "+stall.kind);
                if(index==0)
                {Place(stall.Physical.RequiredApproach);stall.Physical.Interact();Place(stall.Physical.RequiredApproach);stall.Physical.Interact();captureAfter=Time.unscaledTime+.7f;phase=21;return;}
                BeachBoatPhysicalVillagePlaytest.SolveAttempt(stall);FinishTrade(stall);return;
            }
            if(phase==21)
            {
                var work=VillageWorkStall.Active.Physical;if(!work.AtTable||Time.unscaledTime<captureAfter)return;var saw=work.Saw;Check(saw!=null&&saw.BladeMesh.vertexCount>200&&VillageHandSaw.Teeth==22,"The saw is a tapered, toothed steel mesh with an open wooden grip");
                var camera=BeachBoatQuestSession.Instance.storyCamera;var screen=(Vector2)camera.WorldToScreenPoint(saw.HandleContact);var before=work.ToolPointer;Check(work.PressPointer(screen),"The actual visible saw handle can be grabbed");work.DragPointer(screen,.03f);Check(Vector2.Distance(work.ToolPointer,before)<.04f,"A stationary pointer on the handle cannot make the saw jump or slide off the hand");work.ReleaseTool();Capture("Saw_Grip");Check(work.HandWristAngle<1&&work.HandSegmentError<.001f&&work.HandContactError<.02f,"Saw hands have neutral wrists, fixed arm proportions and real handle contact");
                var stall=VillageWorkStall.Active;BeachBoatPhysicalVillagePlaytest.SolveAttempt(stall);FinishTrade(stall);return;
            }
            if(phase==3)
            {
                if(BeachBoatQuestSession.InteractionBlocked){Dialogue.Instance.Close();return;}
                Check(Prep.Data.preparedMask==15&&Prep.Data.qualityMask==15,"All four clean physical attempts earn their visible one-time preparation rewards");Check(combat.QuiverCapacity==8&&vitals.HealingItems==5,"Two wood/rope rewards expand quiver to 8; two food rewards grant two medicines");
                clean=BachDangSaveSystem.Capture("Preparation reward QA");Place(VillagePreparationQuest.Instance.stalls[0].InteractionPosition+Vector3.back*30);clean.preparation.deliveredMask=0;clean.preparation.transit=new[]{-1f,-1f,-1f,-1f};Prep.Restore(clean);courierDeadline=EditorApplication.timeSinceStartup+12;phase=4;return;
            }
            if(phase==4)
            {
                if(Prep.ActiveCouriers==0)
                {
                    if(EditorApplication.timeSinceStartup>courierDeadline){File.WriteAllText("Logs/SupplyRouteDiagnosis.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{diagnosis=SupplyCourier.LastDiagnosis,data=Prep.Data,warehouse=Prep.WarehousePosition.ToString(),stalls=VillagePreparationQuest.Instance.stalls.Select(s=>new{s.name,p=s.InteractionPosition.ToString()}),actors=CampDiscipline.Instance.Actors.Where(a=>a.Type==ProtectedCampNPC.Kind.Civilian).Select(a=>new{a.name,p=a.transform.position.ToString(),walker=a.GetComponent<NPCWander>()!=null,enabled=a.GetComponent<NPCWander>()?.enabled,nav=a.GetComponent<NPCWander>()?.Navigation!=null})},Newtonsoft.Json.Formatting.Indented));throw new Exception("No villager acquired a freight route; see SupplyRouteDiagnosis.json");}return;
                }for(int i=0;i<100;i++)Prep.Advance(.02f);Check(Prep.CourierDistance>1,"Existing villagers physically walk their navigation routes to collect freight");Check(Prep.ActiveCouriers<=2,"Freight borrows at most two existing villagers rather than spawning a crowd");
                var courier=Prep.Couriers[0];var actor=courier.Actor;var pickup=(Vector3)typeof(SupplyCourier).GetField("stock",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(courier);actor.transform.position=pickup;Physics.SyncTransforms();Prep.Advance(.02f);actor.GetComponentInChildren<Animator>().Update(0);courier.Pose();Capture("Porter_Grip",actor.transform.position+new Vector3(18,18,-28),actor.transform.position+Vector3.up*12);if(courier.GripError>=1)
                {
                    var r=actor.GetComponentInChildren<Animator>();var c=(GameObject)typeof(SupplyCourier).GetField("cargo",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(courier);
                    File.WriteAllText("Logs/PorterAnatomy.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{actor=actor.name,root=actor.transform.position.ToString(),cargo=c.transform.position.ToString(),bones=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand}.Select(id=>new{id=id.ToString(),p=r.GetBoneTransform(id).position.ToString(),scale=r.GetBoneTransform(id).lossyScale.ToString()}),frames=new[]{"leftGrip","rightGrip"}.Select(name=>{var grip=(HumanoidHandGrip)typeof(SupplyCourier).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(courier);return new{name,contact=grip.Contact.ToString(),center=typeof(HumanoidHandGrip).GetField("center",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(grip).ToString()};})},Newtonsoft.Json.Formatting.Indented));
                }
                Check(courier.Holding&&courier.GripError<1,$"A porter who reaches the supplied crate lifts it with both palms on the cargo: held={courier.Holding}, error={courier.GripError}");
                Capture("Warehouse",Prep.WarehousePosition+new Vector3(50,40,-70),Prep.WarehousePosition+Vector3.up*7);
                Place(Prep.DockPosition+Vector3.back*35);for(int i=0;i<700;i++)Prep.Advance(.02f);Check(Prep.Data.deliveredMask==15,"Offscreen freight arrives as all four persistent visible supply types at the dock");Check(Prep.StartRescue()||Prep.RescueActive,"Approaching the dock after preparation starts the optional supply mishap");Check(Prep.RescueRemaining<=65&&Prep.RescueRemaining>50,"The rescue exposes a real finite deadline");
                var before=Prep.Data.rescueSeconds;CampaignJournalUI.Instance.Open();float scale=Time.timeScale;Prep.Advance(1);Check(Prep.Data.rescueSeconds==before,"The paused journal does not spend rescue time");CampaignJournalUI.Instance.Close();
                Place(Fallen(0).position+Vector3.back*6);Dialogue.TryConsumeInput();Check(Prep.Interact()&&Prep.Carrying,"The same consumed E event used by live input can actually pick up a crate");carried=BachDangSaveSystem.Capture("Carry recovery QA");BachDangSaveSystem.Apply(carried);Check(!Prep.Carrying&&Prep.Data.recoveredMask==0&&Fallen(0).gameObject.activeSelf,"Loading while carrying puts the unfinished crate back without losing or duplicating it");phase=5;return;
            }
            if(phase==5)
            {
                if(BeachBoatQuestSession.InteractionBlocked)return;Gather();Check(Prep.Recovered==3,"All three deliveries are required before selecting reinforcement");Place(Choice(1).position+Vector3.back*7);int medicine=vitals.HealingItems;Check(Prep.Interact()&&Prep.Data.choice==1&&combat.QuiverCapacity==10,"Choosing archers grants two extra quiver slots, reaching 10");Check(vitals.HealingItems==medicine&&!Prep.Interact(),"The mutually exclusive medical choice cannot be claimed as a second reward");
                var rewarded=BachDangSaveSystem.Capture("Archer reward saved");BachDangSaveSystem.Apply(rewarded);Check(Prep.Data.choice==1&&combat.QuiverCapacity==10,"Save/load preserves the selected reinforcement and expanded quiver");
                combat.ResetForCheckpoint();Check(combat.Arrows==10,"Battle/checkpoint ammo refill respects earned capacity instead of resetting it to six");BachDangSaveSystem.Apply(carried);phase=6;return;
            }
            if(phase==6)
            {
                if(BeachBoatQuestSession.InteractionBlocked)return;Gather();Place(Choice(2).position+Vector3.back*7);int medicine=vitals.HealingItems;Check(Prep.Interact()&&Prep.Data.choice==2&&vitals.HealingItems==medicine+2,"The medical branch grants exactly two medicines instead of the archer bonus");Check(combat.QuiverCapacity==8,"Medical reinforcement keeps only previously earned preparation quiver slots");BachDangSaveSystem.Apply(carried);phase=7;return;
            }
            if(phase==7)
            {
                if(BeachBoatQuestSession.InteractionBlocked)return;for(int i=0;i<400;i++)Prep.Advance(.2f);Check(Prep.RescueRemaining==0,"Rescue time can expire without failing the main campaign");Gather();Place(Choice(2).position+Vector3.back*7);int medicine=vitals.HealingItems;Check(Prep.Interact()&&Prep.Data.incidentResolved&&Prep.Data.choice==0&&vitals.HealingItems==medicine,"Late rescue saves the supplies but grants no unearned timed bonus");Check(!Prep.JournalText.Contains("Nhận 2 thuốc"),"The journal does not claim a reward for the missed deadline");
                BachDangSaveSystem.Apply(carried);Prep.SkipRescue();Check(Prep.Data.incidentSkipped&&Story.Instance.currentState!=Story.StoryState.Act3_Celebration,"The player can explicitly skip the optional rescue without being given a victory or blocking the campaign");
                var old=BachDangSaveSystem.Capture("Legacy preparation QA");old.preparation=null;BachDangSaveSystem.Apply(old);Check(Prep.Data.preparedMask==15&&Prep.Data.deliveredMask==15&&Prep.Data.qualityMask==0&&combat.QuiverCapacity==6,"Legacy completed supplies restore visually without inventing new rewards");
                foreach(var file in hashes)Check(File.Exists(file.Key)&&Hash(file.Key)==file.Value,"User save unchanged: "+Path.GetFileName(file.Key));Capture("Dock",Prep.DockPosition+new Vector3(55,35,-65),Prep.DockPosition+Vector3.up*7);Finish(null);return;
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void FinishTrade(VillageWorkStall stall)
    {
        Check(stall.Phase==VillageWorkStall.WorkPhase.Won,"Actually finish deliveries before granting rewards: "+stall.kind);Check(stall.Elapsed<=CampaignProgress.MiniTarget((int)stall.kind+1,2),"A clean 3D attempt can meet its advertised quality deadline: "+stall.kind+" "+stall.Elapsed);int old=Prep.Data.qualityMask;Check(stall.ClaimReward(),"Claim actual prepared material: "+stall.kind);var medicines=BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().HealingItems;Check(!stall.ClaimReward()&&medicines==BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().HealingItems,"An already claimed result cannot duplicate medicine: "+stall.kind);
        if(++index>=4){phase=3;Dialogue.Instance.Close();}else phase=2;
    }
}
