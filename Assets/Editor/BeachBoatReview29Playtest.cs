using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
public static class BeachBoatReview29Playtest
{
 const string Flag="Review29QA";static int phase,frame;static double began;static float wait,drawSample;static Vector3 firstHand;static RiverFighterPose npc;static string failure;static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
 static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
 static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
 public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/Review29QA/PrePlay");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
 [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
 static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
 static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/Review29Playtest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
 public static void Picture(string name,Vector3 eye,Vector3 focus)
 {
  var source=BeachBoatQuestSession.Instance.storyCamera;var view=new GameObject("Review29Proof").AddComponent<Camera>();view.CopyFrom(source);view.GetUniversalAdditionalCameraData().renderPostProcessing=true;view.enabled=false;view.transform.position=eye;view.transform.LookAt(focus);CinematicCharacterLOD.RefreshForCamera(view);foreach(var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))r.forceMatrixRecalculationPerRender=true;var rt=new RenderTexture(1280,800,24);view.targetTexture=rt;view.Render();RenderTexture.active=rt;var t=new Texture2D(1280,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,800),0,0);t.Apply();File.WriteAllBytes("Logs/Review29_"+name+".png",t.EncodeToPNG());RenderTexture.active=null;view.targetTexture=null;Object.DestroyImmediate(t);Object.DestroyImmediate(rt);Object.DestroyImmediate(view.gameObject);CinematicCharacterLOD.RefreshForCamera(source);
 }
 static void BoatAt(Vector3 point){var boat=QuestRowboat.Instance;boat.RestoreSave(true,point,Quaternion.identity);Physics.SyncTransforms();}
 static void Tick()
 {
  if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
  try
  {
   if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>150)throw new Exception("Review29 timeout");var session=BeachBoatQuestSession.Instance;var player=session.player;
   if(phase==0)
   {
    foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/Review29QA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();phase=1;return;
   }
   if(phase==1)
   {
    if(BeachBoatQuestSession.InputBlocked)return;
    var rice=VillagePreparationQuest.Instance.stalls.First(s=>s.kind==VillageWorkStall.WorkKind.Rice);var laundry=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("San_Phoi_Vai_")).ToArray();
    Check(laundry.Length>0&&laundry.All(t=>QuestRowboat.FlatDistance(t.position,rice.transform.position)>=85),"The rice stall clears every laundry work area");Picture("RiceStall",rice.transform.position+rice.transform.forward*85+rice.transform.right*25+Vector3.up*35,rice.transform.position+Vector3.up*15);
    Story.Instance.TransitionTo(Story.StoryState.Act3_PrepareForBattle,true);ArcheryTraining.Instance.Restore(0,false);var guide=session.GetComponent<StoryRouteGuide>();if(guide==null)guide=session.GetComponentInChildren<StoryRouteGuide>();
    Check(guide.ResolveTarget(out var title)==ArcheryTraining.Instance.GuideTarget&&title.Contains("cung"),"The required-archery route points to the actual firing area instead of the commander");
    Check(!PreparationBriefing.Archery.Contains("bấm")&&!PreparationBriefing.Archery.Contains("phím"),"The commander's spoken reminder contains no control-key instructions");Story.Instance.ShowArcheryReminder();Check(BeachBoatStory.StoryDialogueUI.Instance.CaptureSave().voiceProfile=="commander_brief","The natural reminder resolves to its Vietnamese voice recording");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
    Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);
    foreach(var stall in VillagePreparationQuest.Instance.stalls){BeachBoatPhysicalVillagePlaytest.Place(stall.InteractionPosition);Check(stall.TryOpen(),"Open physical trade: "+stall.kind);var target=GameObject.Find("Active_work_target");Check(target.GetComponentsInChildren<LineRenderer>().Length==4&&target.GetComponentsInChildren<LineRenderer>().All(l=>l.positionCount==3),"The trade uses item corners instead of any circular target: "+stall.kind);stall.Close();}
    BeachBoatStory.StoryDialogueUI.Instance.Show("Ngô Quyền",PreparationBriefing.Stakes);var briefing=BeachBoatStory.StoryDialogueUI.Instance.CaptureSave();Check(briefing.voiceProfile=="commander_brief"&&BeachBoatStory.StoryDialogueUI.Instance.GetComponent<DialogueVoice>().CurrentClip!=null,"The five-location stake order has a matching Vietnamese voice recording");briefing.line="Tốt lắm. Hãy dựng ba cọc mẫu.";BeachBoatStory.StoryDialogueUI.Instance.RestoreSave(briefing);Check(BeachBoatStory.StoryDialogueUI.Instance.CaptureSave().line==PreparationBriefing.Stakes,"Loading the previous three-stake briefing migrates to the five-location order");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
    var sound=BeachBoatSoundscape.Instance;var clip=sound.StakeImpact;Check(clip!=null&&clip==Resources.Load<AudioClip>("BachDang938/WorkAudio/Stake_Hammer"),"Stake impacts use a real wooden mallet recording instead of cloth placement");var samples=new float[clip.samples*clip.channels];clip.GetData(samples,0);float rms=Mathf.Sqrt(samples.Average(v=>v*v));Check(rms>.05f&&samples.Max(v=>Mathf.Abs(v))<.85f,"The wood strike has audible energy and bounded peaks: "+rms);int before=sound.StakeHitsPlayed;sound.PlayStake();Check(sound.StakeHitsPlayed==before+1,"A real strike event triggers the stake audio");
    npc=Object.FindObjectsByType<RiverFighterPose>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(p=>p.name.StartsWith("Cung_Thu_Mai_Phuc_Tren_Bo_"));npc.gameObject.SetActive(true);npc.BeginBowShot(npc.transform.position+npc.transform.forward*300);wait=Time.time+.16f;phase=2;return;
   }
   if(phase==2){if(Time.time<wait)return;firstHand=npc.Animator.GetBoneTransform(HumanBodyBones.RightHand).position;drawSample=npc.BowDrawAmount;wait=Time.time+.35f;phase=3;return;}
   if(phase==3)
   {
    if(Time.time<wait)return;Check(npc.BowDrawAmount>drawSample&&Vector3.Distance(firstHand,npc.Animator.GetBoneTransform(HumanBodyBones.RightHand).position)>.6f,"The allied archer visibly raises the bow and pulls the string");Picture("AlliedBow",npc.transform.position+npc.transform.forward*45+npc.transform.right*8+Vector3.up*13,npc.transform.position+Vector3.up*12);wait=Time.time+.45f;phase=4;return;
   }
   if(phase==4)
   {
    if(Time.time<wait)return;Check(npc.BowShots==1&&npc.BowDrawAmount<.05f&&!npc.transform.Find("DaiViet_Boarding_Bow/Ten_dang_giu").gameObject.activeSelf,"The archer releases the string and the nocked arrow after the draw");npc.gameObject.SetActive(false);
    var waterPoint=BeachBoatStory.RiverStakeZone.Instance.FieldBounds.center;waterPoint.y=TideSystem.CurrentWaterHeight+5;player.transform.position=waterPoint;player.GetComponent<Rigidbody>().position=waterPoint;Physics.SyncTransforms();var pose=player.GetComponent<RiverFighterPose>();pose.Traversal=RiverFighterPose.Motion.Swim;pose.SwimMoving=true;var rig=pose.Animator;float maxBend=0,maxAbove=-999;
    for(int i=0;i<120;i++){pose.EvaluateTraversal(.02f);foreach(bool left in new[]{true,false}){var hip=rig.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var knee=rig.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var ankle=rig.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);maxBend=Mathf.Max(maxBend,Vector3.Angle(knee.position-hip.position,ankle.position-knee.position));maxAbove=Mathf.Max(maxAbove,ankle.position.y-hip.position.y);}}
    Check(maxBend<58&&maxAbove<1,"Flutter kicks keep knees bounded and feet below the hips instead of snapping behind the back: bend="+maxBend+" above="+maxAbove);Picture("Swim",player.transform.position+player.transform.right*35+Vector3.up*12,rig.GetBoneTransform(HumanBodyBones.Hips).position);pose.FinishTraversal();
    BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());Story.Instance.TransitionTo(Story.StoryState.Act3_BattleInProgress,true);var fleet=EnemyFleetDirector.Instance;fleet.StartBattle();RiverInvasionIntro.Instance.Cancel();fleet.Pursuit.enabled=false;var field=BeachBoatStory.RiverStakeZone.Instance.FieldBounds;var spot=new Vector3(field.center.x,TideSystem.CurrentWaterHeight,field.max.z+500);BoatAt(spot);QuestRowboat.Instance.RestoreDurability(100);var chase=fleet.Pursuit.Chase;chase.Reset();chase.Step(.1f);Check(chase.Pending&&chase.Volleys==1&&chase.Resolved==0,"Enemy detection starts a telegraphed real-water arrow volley");phase=5;return;
   }
   if(phase==5)
   {
    var fleet=EnemyFleetDirector.Instance;var chase=fleet.Pursuit.Chase;var boat=QuestRowboat.Instance;
    for(int i=0;i<21;i++)chase.Step(.1f);Check(chase.Resolved==1&&chase.Hits==1&&boat.Durability==84,"Staying in the warned water area applies exactly one 16-point hull hit");
    chase.BeginVolley();var locked=chase.Landing;chase.Step(.2f);BoatAt(boat.transform.position+Vector3.right*100);for(int i=0;i<21;i++)chase.Step(.1f);Check(chase.Resolved==2&&chase.Hits==1&&boat.Durability==84,"Steering outside the warned area dodges the fixed volley without homing arrows");
    chase.BeginVolley();chase.Step(.35f);var save=BachDangSaveSystem.Capture("Pending enemy arrows");var target=chase.Landing;chase.Reset();chase.RestoreSave(save.fleet,false);Check(chase.Pending&&chase.Volleys==3&&chase.Resolved==2&&chase.Landing==target,"Saving mid-warning restores the same pending volley and landing area");
    save.fleet.lureStage=1;fleet.Pursuit.RestoreSave(save.fleet);var field=BeachBoatStory.RiverStakeZone.Instance.FieldBounds;BoatAt(new Vector3(field.center.x,TideSystem.CurrentWaterHeight,field.center.z-5));fleet.Pursuit.Step(.01f);Check(!fleet.Pursuit.HasEnteredTrap,"Entering the stake field cannot skip unresolved enemy volleys");
    for(int wave=0;wave<3;wave++){if(!chase.Pending)chase.BeginVolley();BoatAt(chase.Landing+Vector3.right*100);for(int i=0;i<21;i++)chase.Step(.1f);}Check(chase.Completed&&chase.Resolved==5,"Five resolved volleys finish the chase exercise without skipping the real boat controls");
    var entered=new BachDangSaveData.Fleet{active=true,lureVersion=1,lureStage=1,lureAnchor=fleet.Pursuit.FormationAnchor,baitVersion=1,baitVolleys=5,baitResolved=5};fleet.Pursuit.RestoreSave(entered);var bounds=BeachBoatStory.RiverStakeZone.Instance.FieldBounds;BoatAt(new Vector3(bounds.center.x,TideSystem.CurrentWaterHeight,bounds.center.z-5));fleet.Pursuit.Step(.1f);Check(fleet.Pursuit.HasEnteredTrap,"Completing the volley exercise and physically entering the field commits the fleet to the trap");
    fleet.Pursuit.RestoreSave(new BachDangSaveData.Fleet{active=true});Check(fleet.Pursuit.HasEnteredTrap&&fleet.Pursuit.Chase.Completed,"A legacy committed battle save does not restart or block the new chase minigame");
    foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
   }
  }catch(Exception e){Finish(e.ToString());}
 }
}
