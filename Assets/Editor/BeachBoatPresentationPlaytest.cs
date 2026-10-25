using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatPresentationPlaytest
{
    const string Flag="Presentation938QA";
    static double started;
    static int phase,lastFrame=-1,moving,native,backward;
    static float until;
    static string runtimeError;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<NPCWander,Vector3> previous=new Dictionary<NPCWander,Vector3>();
    static readonly Dictionary<NPCWander,float> travel=new Dictionary<NPCWander,float>();
    public static void RunInEditor(){SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string m,string stack,LogType t){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(t==LogType.Error||t==LogType.Exception))runtimeError=m+"\n"+stack;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    static void Move(Vector3 p)
    {
        var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;var body=player.GetComponent<Rigidbody>();body.position=p;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
    }
    static void Capture(string name,int width=1280,int height=720)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;
        var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isActiveAndEnabled&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var saved=canvases.Select(c=>new{c,c.worldCamera,c.planeDistance}).ToArray();
        var rt=new RenderTexture(width,height,24);var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
        camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
        File.WriteAllBytes("Logs/"+name+".png",pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
        foreach(var s in saved){s.c.renderMode=RenderMode.ScreenSpaceOverlay;s.c.worldCamera=s.worldCamera;s.c.planeDistance=s.planeDistance;}
        Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);
    }
    static void TickUI(VillageWorkUI ui)=>typeof(VillageWorkUI).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
    static void CheckTimber()
    {
        Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        var stall=VillagePreparationQuest.Instance.stalls.First(s=>s.kind==VillageWorkStall.WorkKind.Timber);Move(stall.InteractionPosition);
        Check(stall.TryOpen(),"Can open the physical timber stall");stall.StartAttempt(0);
        var ui=Object.FindAnyObjectByType<VillageWorkUI>();TickUI(ui);
        var choices=ui.transform.Find("WorkOverlay/Workbench/Work/TimberChoices");
        var options=Enumerable.Range(0,stall.TimberOptions).Where(stall.IsSoundTimber).Take(2).ToArray();
        choices.Find("Timber"+options[0]).GetComponent<Button>().onClick.Invoke();TickUI(ui);
        Check(stall.TimberSelectedPieces==1&&choices.Find("Timber"+options[0]+"/SelectedTag").gameObject.activeSelf,"Mouse selection shows a persistent selected badge");
        Check(choices.Find("Timber"+options[0]).GetComponent<Outline>().enabled,"Selected wood has a bright outline");
        choices.Find("Timber"+options[1]).GetComponent<Button>().onClick.Invoke();TickUI(ui);
        Check(ui.transform.Find("WorkOverlay/Workbench/Work/Task").GetComponent<TMP_Text>().text.Contains("#FFE29B"),"Full selected count receives a separate highlighted summary color");Capture("Timber_Selected_Visible");
        stall.Undo();TickUI(ui);Check(!choices.Find("Timber"+options[1]+"/SelectedTag").gameObject.activeSelf,"Undo removes the badge from the reverted plank");
        choices.Find("Timber"+options[0]).GetComponent<Button>().onClick.Invoke();TickUI(ui);Check(stall.TimberSelectedPieces==0,"Clicking a selected plank visibly deselects it");
        stall.SubmitOption(options[0]);stall.RestoreCurrentTask();TickUI(ui);
        Check(Enumerable.Range(0,stall.TimberOptions).All(i=>!choices.Find("Timber"+i+"/SelectedTag").gameObject.activeSelf),"Restore clears all selected badges and outlines");stall.Close();
        stall.RestoreLearningProgress(2);Check(stall.TryOpen(),"Can reopen timber selection for hard layout review");stall.StartAttempt(2);TickUI(ui);Check(stall.Difficulty==2,"Timber layout review really uses hard difficulty");
        var hardOption=Enumerable.Range(0,stall.TimberOptions).First(stall.IsSoundTimber);stall.SubmitOption(hardOption);TickUI(ui);
        Check(choices.Find("Timber"+hardOption+"/SelectedTag").gameObject.activeSelf&&ui.transform.Find("WorkOverlay/Workbench/Work/Task").GetComponent<TMP_Text>().text.Contains("#8CF0BD"),"Hard timber selection highlights a partial selection");Capture("Timber_Hard_Selected_Visible");stall.Close();
    }
    static void Finish(string error)
    {
        Time.timeScale=1;File.WriteAllText("Logs/PresentationPlaytest.json",JsonConvert.SerializeObject(new{success=error==null,error,phase,checks,moving,native,backward,
            civilians=travel.Select(p=>new{p.Key.name,distance=p.Value,controller=p.Key.animator.runtimeAnimatorController.name})},Formatting.Indented));
        SessionState.SetBool(Flag,false);EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<4)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            if(EditorApplication.timeSinceStartup-started>150)throw new Exception("Presentation test timeout at phase "+phase);
            var session=BeachBoatQuestSession.Instance;var camera=session.storyCamera;var fleet=EnemyFleetDirector.Instance;
            if(phase==0)
            {
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                var gate=GameObject.Find("VillageToCamp").transform.position;Move(gate+Vector3.forward*25);
                camera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;camera.transform.position=gate+new Vector3(25,55,105);camera.transform.LookAt(gate+new Vector3(0,12,-25));
                AmbientPopulationBudget.Instance.Refresh();until=Time.time+1.5f;phase=1;return;
            }
            if(phase==1)
            {
                if(Time.time<until)return;
                var profile=Resources.Load<CivilianMovementProfile>("BachDang938/CivilianMovement");Check(profile!=null&&profile.walking.isHumanMotion,"Native civilian walk imports as a real humanoid clip");
                var gate=GameObject.Find("VillageToCamp").transform.position;
                foreach(var w in Object.FindObjectsByType<NPCWander>().Where(w=>w.isActiveAndEnabled&&Vector3.Distance(w.transform.position,gate)<180&&profile.avatars.Contains(w.animator.avatar)))
                {Check(w.animator.runtimeAnimatorController==profile.controller,w.name+" uses the compatible civilian walk controller");previous[w]=w.transform.position;travel[w]=0;w.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
                Check(previous.Count>=3,"Gate test observes several real civilian NPCs");Capture("Village_Gate_Movement_Start");until=Time.time+9;phase=2;return;
            }
            if(phase==2)
            {
                if(lastFrame!=Time.frameCount)
                {
                    lastFrame=Time.frameCount;
                    foreach(var w in previous.Keys.ToArray())
                    {
                        var delta=w.transform.position-previous[w];delta.y=0;previous[w]=w.transform.position;travel[w]+=delta.magnitude;
                        if(w.CurrentSpeed>6&&delta.magnitude>.03f&&!w.animator.IsInTransition(0))
                        {
                            moving++;if(w.animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip==Resources.Load<CivilianMovementProfile>("BachDang938/CivilianMovement").walking))native++;
                            if(Vector3.Dot(delta.normalized,w.transform.forward)<.5f)backward++;
                        }
                    }
                }
                if(Time.time<until)return;
                Check(travel.Count(p=>p.Value>12)>=3,"Villagers really travel through the gate neighborhood");
                Check(moving>40&&native>=moving*.90f,"Moving civilians visibly play the native walking clip");
                Check(backward<moving*.05f,"Walking follows the facing direction instead of sliding backward");Capture("Village_Gate_Movement_End");CheckTimber();
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());Story.Instance.collectedWood=Story.Instance.totalWoodRequired;Story.Instance.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);
                var boat=QuestRowboat.Instance;Move(boat.boardingPoint.position);Check(boat.TryBoard(),"Can board the player boat for the compact combat HUD");RiverPlayerCombat.Instance.ReturnFireEnabled=false;
                boat.transform.position=new Vector3(-2400,TideSystem.CurrentWaterHeight,1400);Physics.SyncTransforms();until=Time.time+1;phase=3;return;
            }
            if(phase==3)
            {
                if(Time.time<until)return;
                camera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;camera.transform.position=QuestRowboat.Instance.transform.position+new Vector3(-70,80,-115);camera.transform.LookAt(QuestRowboat.Instance.transform.position+Vector3.up*10);
                var hud=Object.FindAnyObjectByType<CompactRiverCombatHUD>();Check(hud!=null&&hud.ActionIconCount==4,"Compact HUD shows Q/F/SPACE/R icon prompts");
                Check(GameObject.Find("BoardingCombatHUD")==null&&GameObject.Find("CombatStatus")==null,"The former central instruction banners are removed");
                Canvas.ForceUpdateCanvases();var corners=new Vector3[4];hud.Cluster.GetWorldCorners(corners);
                Check(corners[0].x>Screen.width*.65f&&corners[2].y<Screen.height*.40f,"Combat icons sit against the lower right edge and clear the center");
                hud.Refresh("",false);Capture("Combat_HUD_Compact_1280");Capture("Combat_HUD_Compact_960",960,540);
                hud.Refresh("Địch đang nhắm! Di chuyển để né tên. Giữ chuột phải để giương cung và nhắm, chuột trái để bắn.",true);Canvas.ForceUpdateCanvases();
                var notice=hud.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="CombatNotice");Check(notice.maxVisibleLines==2&&notice.overflowMode==TextOverflowModes.Ellipsis,"Long combat notices stay within two lines at the edge");Capture("Combat_HUD_Notice_960",960,540);
                Check(hud.GetComponentsInChildren<CombatHudIcon>().All(i=>i.DrawnVertices>0),"Every combat symbol renders visible vector geometry");
                Check(RiverBoardingCombat.Instance.HandleActionKey(KeyCode.Q)&&RiverBoardingCombat.Instance.SwordDrawn,"Q still draws the sword with the icon HUD");RiverBoardingCombat.Instance.HandleActionKey(KeyCode.Q);
                Time.timeScale=4;phase=4;return;
            }
            if(phase==4)
            {
                if(!fleet.AmbushStarted&&RiverBoardingCombat.Instance.SwimsStarted>0)throw new Exception("Allied swimmers entered the fleet approach before the ambush");
                if(!fleet.EbbStarted)return;
                var hud=Object.FindAnyObjectByType<CompactRiverCombatHUD>();hud.Refresh("",false);
                Check(TideSystem.Instance.EbbProgress>=0&&TideSystem.Instance.EbbSecondsRemaining>0,"HUD receives real ebb progress and remaining seconds");Capture("Combat_HUD_Tide_Falling");phase=5;
            }
            if(phase==5&&fleet.AmbushStarted)
            {
                Object.FindAnyObjectByType<CompactRiverCombatHUD>().Refresh("",false);Capture("Combat_HUD_Stakes_Exposed");
                Check(BeachBoatStory.RiverStakeZone.Instance.ExposedStakeCount>0,"HUD changes to exposed stakes during the real collective ambush");until=Time.time+8;phase=6;
            }
            if(phase==6&&Time.time>=until){Check(RiverBoardingCombat.Instance.SwimsStarted>0,"Allied boarders deploy after the fleet is trapped");Finish(null);}
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
