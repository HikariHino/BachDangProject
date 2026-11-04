using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

/// <summary>Exercises the connected preparation, actual last-post work, swept raft test and both lure decisions.</summary>
public static class BeachBoatEngagementPlaytest
{
    const string Flag="EngagementQA";static int phase,index,frame;static double began;static string failure;static float volume;static bool initialized;static float captureAt;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=index=0;frame=-1;began=0;failure=null;initialized=false;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/Engagement/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=text+"\n"+stack;}
    static void Finish(string error){if(initialized)BachDangPreferences.SetVolume(volume);SessionState.SetBool(Flag,false);File.WriteAllText("Logs/EngagementPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,index,checks},Newtonsoft.Json.Formatting.Indented));VillageWorkStall.Active?.Close();StakeProofTrial.Active?.Close();BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Place(Vector3 p)=>BeachBoatPhysicalVillagePlaytest.Place(p);
    static void Late(object work)=>work.GetType().GetMethod("LateUpdate",Private).Invoke(work,null);
    static Rect ScreenRect(RectTransform r,Camera camera){var corners=new Vector3[4];r.GetWorldCorners(corners);var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
    static void Capture(string name,int width=1280,int height=720)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isActiveAndEnabled&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();var saved=canvases.Select(c=>new{c,c.worldCamera,c.planeDistance}).ToArray();
        var rt=new RenderTexture(width,height,24){hideFlags=HideFlags.HideAndDontSave};var image=new Texture2D(width,height,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};camera.targetTexture=rt;
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}Canvas.ForceUpdateCanvases();
        var vital=GameObject.Find("BeachStoryHUD/Vitals")?.GetComponent<RectTransform>();var medicine=GameObject.Find("HealingItemHUD/Healing_item_slot")?.GetComponent<RectTransform>();
        if(vital!=null&&medicine!=null)Check(!ScreenRect(vital,camera).Overlaps(ScreenRect(medicine,camera)),"Health and medicine do not overlap at "+width+"x"+height);
        camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Logs/Engagement_"+name+".png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;
        foreach(var s in saved){s.c.renderMode=RenderMode.ScreenSpaceOverlay;s.c.worldCamera=s.worldCamera;s.c.planeDistance=s.planeDistance;}Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>160)throw new Exception("Engagement QA timeout");var session=BeachBoatQuestSession.Instance;var story=Story.Instance;var zone=BeachBoatStory.RiverStakeZone.Instance;var boat=QuestRowboat.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);BachDangAutoSave.Instance.enabled=false;volume=BachDangPreferences.Volume;initialized=true;BachDangPreferences.SetVolume(0);
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();story.collectedWood=5;story.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);phase=1;return;
            }
            if(phase==1)
            {
                if(BeachBoatQuestSession.InteractionBlocked)return;var quest=VillagePreparationQuest.Instance;
                Check(!Application.isBatchMode||AudioListener.volume==0,"Native CLI tests are muted without changing game sound preferences");Check(CampaignHUDSkin.Ready&&story.objectiveUIPanel.GetComponent<Image>().sprite!=null,"The world HUD uses the shared framed visual theme");
                Check(quest.SelectWork(3)&&quest.SelectedStall.kind==VillageWorkStall.WorkKind.Provisions,"The player may choose provisions before the other three trades");
                Place(quest.SelectedStall.InteractionPosition+Vector3.back*25);Capture("SupplyHUD");Capture("SupplyHUD_4by3",1024,768);Capture("SupplyHUD_Wide",1920,1080);phase=2;return;
            }
            if(phase==2)
            {
                if(BeachBoatQuestSession.InteractionBlocked){BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();return;}
                var quest=VillagePreparationQuest.Instance;int chosen=3-index;Check(quest.SelectWork(chosen),"Select trade in reverse order "+chosen);var stall=quest.stalls[chosen];Place(stall.InteractionPosition);Check(stall.TryOpen()&&stall.Goal==1,"Start one complete, non-repeating physical order at "+stall.kind);BeachBoatPhysicalVillagePlaytest.SolveAttempt(stall);Check(stall.ClaimReward()&&stall.Completed,"Claim the real delivered order at "+stall.kind);
                if(++index<4)return;Check(quest.CompletedCount==4&&PreparationCampaign.Instance.Data.preparedMask==15,"All four freely ordered jobs finish the supply objective");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();phase=3;return;
            }
            if(phase==3)
            {
                var record=CampaignProgress.Instance.Record;record.minigames=new[]{new BachDangSaveData.MiniRecord{id=CampaignProgress.MiniId(1),seconds=55,limit=75}};
                Check(CampaignProgress.Instance.MeetsMini(1,2),"A historical longer timber lesson keeps its saved A deadline after shortening the new mission");
                boat.LeaveForCheckpoint();var done=new[]{true,true,true,true,false};zone.RestoreCheckpoint(done,false);story.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver,true);Place(boat.boardingPoint.position);Check(boat.TryBoard(),"Board for the last physical sample");boat.RestoreSave(true,zone.placements[4].approach.position,Quaternion.identity);Check(zone.TryPlant(4),"Begin the actual fifth site instead of bypassing its work");
                var work=StakeConstruction.Active;Check(work.Interact(),"Pick up the pointed sample timber");work.WalkBundle(2);Late(work);work.AimStake(Vector2.right*.05f);Late(work);Check(work.Interact(),"Set the real sample direction before hammering");captureAt=Time.time+.7f;phase=4;return;
            }
            if(phase==4)
            {
                if(Time.time<captureAt)return;var work=StakeConstruction.Active;if(work!=null&&work.Step!=StakeConstruction.WorkStep.Finished)
                {
                    if(work.Misses>3)throw new Exception("Hammer not progressing: lift="+work.HammerLift+", aim="+work.AimError+", beat="+work.BeatPosition+", message="+work.Feedback);Late(work);if(index==4){Check(work.PartnerHolding,"A visible second worker holds the same sample while the player drives it");Check(work.PartnerContactError<2,"The support worker grips the sample without stretching its arm bones: "+work.PartnerContactError+" "+work.PartnerDiagnosis);Check(work.PartnerHammerClearance>8.5f,"The helper holds the post below the hammer handle, leaving the strike clear");Capture("HammerPartner");index++;}
                    if(!work.PartnerReady)work.BracePartner();typeof(StakeConstruction).GetField("workStarted",Private).SetValue(work,Time.time-.65f);
                    if(!work.IsSwinging){while(work.HammerLift<.7f)work.LiftHammer(.15f);work.AimHammer(-work.HammerAim);work.Strike();}return;
                }
                if(StakeProofTrial.Active==null)return;captureAt=Time.time+.5f;phase=41;return;
            }
            if(phase==41)
            {
                if(Time.time<captureAt)return;var proof=StakeProofTrial.Active;Check(!proof.Released&&!zone.IsComplete,"The last post waits for the player's raft signal before the days-later montage");Check(!BachDangSaveSystem.CanSave,"Saving cannot serialize half of a raft demonstration");
                proof.Step(20);Check(!proof.Contacted&&proof.Travelled==0,"The test raft cannot move before the player releases it");Time.timeScale=0;Check(!proof.ReleaseRaft(),"Pause prevents releasing the test raft");Time.timeScale=1;Check(proof.ReleaseRaft(),"E releases the real floating test raft");for(int i=0;i<200&&!proof.Contacted;i++)proof.Step(.02f);Check(proof.Contacted&&proof.Travelled>35,"The actual sample's solid capsule intercepts the swept raft hull");captureAt=Time.time+.4f;phase=42;return;
            }
            if(phase==42)
            {
                if(Time.time<captureAt)return;var proof=StakeProofTrial.Active;Capture("RaftCaught");for(int i=0;i<120&&!proof.Completed;i++)proof.Step(.02f);Check(proof.Completed,"The collision settles before the montage is allowed");phase=5;return;
            }
            if(phase==5)
            {
                if(!zone.IsComplete||BeachBoatQuestSession.InteractionBlocked)return;Check(zone.CompletedClusterCount==5&&zone.IsFieldRevealed,"After the physical raft test the complete field is revealed once");
                var pursuit=EnemyFleetDirector.Instance.Pursuit;var route=pursuit.Routes;var start=new Vector3(zone.FieldBounds.center.x,TideSystem.CurrentWaterHeight,zone.FieldBounds.max.z+1700);route.Reset();route.Begin(start);
                for(int i=0;i<3;i++){var point=route.Gate(1,i);Check(RiverWaterNavigation.ClearWater(point,25),"The safe-route gate is in navigable river water "+i);route.Step(.02f,point);}
                Check(route.Choice==1&&route.Completed,"Actually steering through all three green gates completes the longer safe route");var save=new BachDangSaveData.Fleet();route.FillSave(save);route.Reset();route.RestoreSave(save,false);Check(route.Choice==1&&route.Completed,"Saving preserves the completed route and its chosen risk");
                route.Reset();route.Begin(start);route.Step(.02f,route.Gate(2,0));Check(route.Choice==2&&!route.Completed,"The amber gate chooses the shorter dangerous route");pursuit.Chase.Reset();pursuit.Chase.BeginVolley();Check(pursuit.Chase.WarningCount==3&&Mathf.Abs(pursuit.Chase.Telegraph-1.2f)<.01f,"The direct route exposes three simultaneous arrow zones");pursuit.Chase.Reset();
                save=new BachDangSaveData.Fleet();route.FillSave(save);route.Reset();route.RestoreSave(save,false);Check(route.Passed==1&&route.Choice==2&&!route.Completed,"Loading mid-route does not skip the remaining physical gate");route.Step(.02f,route.Gate(2,1));Check(route.Completed,"The direct route requires its second gate too");
                route.Reset();route.Begin(start);route.Step(.02f,route.Gate(1,0));Check(pursuit.Chase.WarningCount==1&&pursuit.Chase.Telegraph>1.5f,"The safe route genuinely reduces the arrow coverage and allows more reaction time");route.RestoreSave(new BachDangSaveData.Fleet{routeVersion=0},true);Check(route.Completed,"Legacy committed battles do not gain a new route lock");
                phase=6;return;
            }
            if(phase==6)
            {
                if(BeachBoatQuestSession.InteractionBlocked)return;boat.LeaveForCheckpoint();story.TransitionTo(Story.StoryState.Act1_Training,true);var drum=Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None).First(s=>s.kind==BeachBoatMinigameStation.ActivityKind.WarDrum);Place(drum.transform.position);drum.BeginActivity();
                bool checkedVolley=false;BeachBoatPhysicalDrumPlaytest.Solve(drum,g=>{if(g.CommandIndex!=2||g.SquadLanded<2||checkedVolley)return;Check(g.SquadGuarding==2&&g.SquadArrows==2&&g.SquadLanded==2,"The final command produces two real archer draw/release animations and two target impacts behind the front guard");checkedVolley=true;});Check(checkedVolley,"The response phase lasts long enough to see arrows land before closing");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();phase=7;captureAt=Time.unscaledTime+.1f;return;
            }
            if(phase==7)
            {
                if(Time.unscaledTime<captureAt)return;Check(PhysicalDrumGame.Active==null&&StakeProofTrial.Active==null,"Both physical demonstrations restore their cameras and player controls");foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
            }
        }
        catch(Exception e){Time.timeScale=1;Finish(e.ToString());}
    }
}
