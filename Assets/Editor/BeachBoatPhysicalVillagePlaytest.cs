using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using UnityEngine.Rendering.Universal;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatPhysicalVillagePlaytest
{
    const string Flag="PhysicalVillage938QA";
    static int phase,index,lastFrame,captureFrame;static double began;static string runtimeError,pendingCapture;static bool capturing,captureReady;
    static VillageWorkStall stall;static bool riceOverflow;static HashSet<string> captures=new HashSet<string>();
    static readonly HashSet<VillageWorkStall.WorkKind> challengeChecks=new HashSet<VillageWorkStall.WorkKind>();
    static BachDangSaveData completedCheckpoint;
    static string stationSnapshot;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool ok,string s){if(!ok)throw new Exception(s);checks.Add(s);}
    public static void RunBatch(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");RunInEditor();}
    public static void RunInEditor(){phase=index=0;lastFrame=-1;began=0;runtimeError=pendingCapture=null;capturing=captureReady=false;riceOverflow=false;checks.Clear();hashes.Clear();captures.Clear();challengeChecks.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType t){if(SessionState.GetBool(Flag,false)&&(t==LogType.Error||t==LogType.Exception))runtimeError=m+"\n"+stack;}
    public static void Place(Vector3 p)
    {
        p.y=Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y+3.2f;
        var go=BeachBoatQuestSession.Instance.player;var body=go.GetComponent<Rigidbody>();go.transform.position=p;body.position=p;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
    }
    public static void SolveAttempt(VillageWorkStall s)
    {
        int guard=0;var player=BeachBoatQuestSession.Instance.player;
        while(s.Phase==VillageWorkStall.WorkPhase.Playing&&guard++<1500)
        {
            var work=s.Physical;float distance=QuestRowboat.FlatDistance(player.transform.position,work.RequiredApproach);
            if(distance>1){s.AdvanceTime(distance/30);Place(work.RequiredApproach);}
            bool action=work.Current==VillagePhysicalWork.Step.Saw||work.Current==VillagePhysicalWork.Step.Tie||work.Current==VillagePhysicalWork.Step.Wrap||work.Current==VillagePhysicalWork.Step.FillRice&&work.RiceFill<.70f;
            if(action){s.AdvanceTime(.1f);if(!DriveTool(work))throw new Exception("Cannot perform physical action at "+work.Current);}
            else {s.AdvanceTime(.2f);if(!work.Interact())throw new Exception("Cannot interact at "+work.Current);}
        }
        if(s.Phase!=VillageWorkStall.WorkPhase.Won)throw new Exception("Physical work did not finish: "+s.kind+" / "+s.Feedback);
    }
    public static bool DriveTool(VillagePhysicalWork work)
    {
        if(!work.ToolHeld&&!work.PressTool())return false;
        if(work.Finishing)
        {
            work.AdvanceTool(.1f);
            if(work.Current==VillagePhysicalWork.Step.Tie&&work.ToolPressure>=.6f)work.ReleaseTool();
            return true;
        }
        bool moved=work.DragTool(Vector2.ClampMagnitude(work.ToolGoal-work.ToolPointer,.3f),.1f);return moved;
    }
    static void RequestCapture(string name)
    {
        if(captures.Contains(name)||pendingCapture!=null)return;
        var player=BeachBoatQuestSession.Instance.player;var direction=stall.Physical.TargetPosition-player.transform.position;direction.y=0;
        player.transform.rotation=Quaternion.LookRotation(direction);player.GetComponent<Rigidbody>().rotation=player.transform.rotation;
        var orbit=BeachBoatQuestSession.Instance.storyCamera.GetComponent<BeachBoatStory.Camera_Script>();if(!stall.Physical.ToolStep){float elevation=orbit.Elevation;orbit.SnapToTarget();orbit.SetElevation(elevation);}pendingCapture=name;captureFrame=Time.frameCount;
    }
    static void Capture(string name)
    {
        if(!captures.Add(name))return;capturing=true;var source=BeachBoatQuestSession.Instance.storyCamera;var player=BeachBoatQuestSession.Instance.player;
        if(stall.Physical.Carrying)
        {
            var held=(GameObject)typeof(VillagePhysicalWork).GetField("held",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(stall.Physical);
            var rig=player.GetComponentInChildren<Animator>();
            Check(Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.RightHand).position,held.transform.position+player.transform.right*2)<2.5f,"Right hand reaches the carried item: "+stall.kind);
            Check(Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.LeftHand).position,held.transform.position-player.transform.right*2)<2.5f,"Left hand reaches the carried item: "+stall.kind);
        }
        foreach(var r in player.GetComponentsInChildren<SkinnedMeshRenderer>())r.forceMatrixRecalculationPerRender=true;
        if(stall.Physical.ToolStep)
        {
            var contact=(Vector3)typeof(VillagePhysicalWork).GetMethod("ToolPoint",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stall.Physical,new object[]{stall.Physical.ToolPointer});
            var rig=player.GetComponentInChildren<Animator>();
            var hand=rig.GetBoneTransform(HumanBodyBones.RightHand);var shoulder=rig.GetBoneTransform(HumanBodyBones.RightUpperArm);var elbow=rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Check(Vector3.Distance(hand.position,stall.Physical.RightGripPosition)<2.5f,"Working hand reaches the tool, not the air: "+stall.kind+" hand="+hand.position+" grip="+stall.Physical.RightGripPosition+" arm="+(Vector3.Distance(shoulder.position,elbow.position)+Vector3.Distance(elbow.position,hand.position)));
            Check(stall.Physical.HandsOnly&&stall.Physical.HandTriangles>100,"Only the player's forearms render at the bench; torso and helmet are hidden: "+stall.kind);
            Check(stall.Physical.HandSegmentError<.001f,"Display arm segments keep their original proportions throughout tool motion: "+stall.kind+" error="+stall.Physical.HandSegmentError);
            Check(stall.Physical.HandWristAngle<1,"Display wrists use the neutral mesh bind frame throughout the gesture: "+stall.kind+" angle="+stall.Physical.HandWristAngle);
            Check(StationSignature()==stationSnapshot,"The same table, roof and crates remain in place while manipulating tools: "+stall.kind);
            if(stall.kind==VillageWorkStall.WorkKind.Rope)Check(Object.FindAnyObjectByType<VillageWorkUI>().TensionGaugeVisible,"Large rope tension gauge remains visible below the work area");
            Check(source.GetComponent<BeachBoatStory.Camera_Script>().TableFocusActive&&!VillagePhysicalWork.AllowsMovement,"Bench view locks walking and uses the centered tabletop camera: "+stall.kind);
            var viewport=source.WorldToViewportPoint(contact);
            Check(viewport.z>0&&viewport.x>.08f&&viewport.x<.92f&&viewport.y>.18f&&viewport.y<.88f,"Tool stays inside the unobstructed tabletop frame: "+stall.kind+" "+viewport);
        }
        File.WriteAllText("Logs/VillageCamera_"+name+".json",Newtonsoft.Json.JsonConvert.SerializeObject(new { focus=source.GetComponent<BeachBoatStory.Camera_Script>().WorkFocusActive, distance=QuestRowboat.FlatDistance(player.transform.position,stall.Physical.RequiredApproach), camera=source.transform.position.ToString(), target=stall.Physical.TargetPosition.ToString(), activeCamera=Camera.main?.name, components=source.GetComponents<Behaviour>().Select(c=>c.GetType().FullName+":"+c.enabled).ToArray() },Newtonsoft.Json.Formatting.Indented));
        var ui=Object.FindAnyObjectByType<VillageWorkUI>();typeof(VillageWorkUI).GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(ui,null);
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=source;c.planeDistance=source.nearClipPlane+1;}
        Canvas.ForceUpdateCanvases();
        var rt=new RenderTexture(1600,1000,24);source.targetTexture=rt;source.Render();RenderTexture.active=rt;var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
        File.WriteAllBytes("Logs/Village3D_"+name+".png",pixels.EncodeToPNG());RenderTexture.active=null;source.targetTexture=null;Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;}
        if(name.EndsWith("_Carry"))
        {
            var preview=new GameObject("Carry_pose_QA").AddComponent<Camera>();preview.CopyFrom(source);preview.enabled=false;
            var chest=player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Chest).position;
            preview.transform.position=chest+player.transform.forward*34+player.transform.right*25+Vector3.up*8;preview.transform.LookAt(chest);
            var proof=new RenderTexture(1280,800,24);preview.targetTexture=proof;preview.Render();RenderTexture.active=proof;var proofPixels=new Texture2D(1280,800,TextureFormat.RGB24,false);proofPixels.ReadPixels(new Rect(0,0,1280,800),0,0);proofPixels.Apply();File.WriteAllBytes("Logs/Village3D_"+name+"_Front.png",proofPixels.EncodeToPNG());RenderTexture.active=null;preview.targetTexture=null;Object.DestroyImmediate(proofPixels);Object.DestroyImmediate(proof);Object.DestroyImmediate(preview.gameObject);
        }
        capturing=false;
    }
    static void Finish(string error)
    {
        SessionState.SetBool(Flag,false);VillageWorkStall.Active?.Close();
        File.WriteAllText("Logs/PhysicalVillagePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,index,checks},Newtonsoft.Json.Formatting.Indented));
        BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    public static void CaptureAfterPose()
    {
        if(!captureReady||pendingCapture==null||!SessionState.GetBool(Flag,false))return;
        captureReady=false;string name=pendingCapture;pendingCapture=null;
        try{Capture(name);}catch(Exception e){Finish(e.ToString());}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||capturing)return;EditorApplication.QueuePlayerLoopUpdate();if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Physical village QA timeout");
            if(pendingCapture!=null){if(Time.frameCount>=captureFrame+(stall.Physical.ToolStep?20:3))captureReady=true;return;}
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/PhysicalVillageQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BeachBoatQuestSession.Instance.player.AddComponent<VillageWorkPoseProbe>();
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                CheckCampLighting();
                Story.Instance.collectedWood=5;Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);phase=1;return;
            }
            if(phase==1)
            {
                stall=VillagePreparationQuest.Instance.stalls.OrderBy(s=>s.kind).ElementAt(index);stall.RestoreLearningProgress(0);Place(stall.InteractionPosition);
                Check(stall.Workstation!=null&&stall.Workstation.Environment!=null,"The complete working furniture is visible before pressing E: "+stall.kind);stationSnapshot=StationSignature();CaptureStation(stall.kind+"_Idle");
                var crates=new[]{"Stock_crate","Delivery_crate"}.Select(n=>stall.Workstation.Environment.Find(n).GetComponent<Collider>());
                var posts=stall.transform.Find("Mai_Che_Mo_Mat_Truoc").GetComponentsInChildren<Collider>().Where(c=>c.enabled&&c.name=="Cot_Sap");
                Check(!crates.Any(crate=>posts.Any(post=>Physics.ComputePenetration(crate,crate.transform.position,crate.transform.rotation,post,post.transform.position,post.transform.rotation,out _,out _))),"Permanent canopy posts clear both supply crates: "+stall.kind);
                Check(stall.TryOpen()&&stall.Physical!=null&&stall.Phase==VillageWorkStall.WorkPhase.Playing,"E immediately starts physical work: "+stall.kind);
                Check(StationSignature()==stationSnapshot,"Pressing E does not replace the stall or remove its awning: "+stall.kind);CaptureStation(stall.kind+"_Opened");
                var rig=BeachBoatQuestSession.Instance.player.GetComponentInChildren<Animator>();
                Check(BeachBoatQuestSession.Instance.player.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.bones.Contains(rig.GetBoneTransform(HumanBodyBones.RightUpperArm))),"Work pose drives the skeleton used by the visible character: "+stall.kind);
                Check(!BachDangSaveSystem.CanSave&&VillagePhysicalWork.AllowsMovement&&VillagePhysicalWork.AllowsCamera,"Walking/camera work while saving defers during the attempt: "+stall.kind);
                Check(!stall.SubmitOption(0)&&stall.Progress==0,"Retired screen-puzzle keys cannot complete goods: "+stall.kind);
                var work=stall.Physical;Place(work.RequiredApproach+Vector3.back*100);
                Check(!work.Interact()&&!work.Work(.1f),"Goods and tools cannot be used remotely: "+stall.kind);
                Place(work.RequiredApproach);Check(work.Interact()&&work.Carrying,"Picking up creates a carried 3D object: "+stall.kind);phase=2;return;
            }
            if(phase==2)
            {
                Check(BachDangFrontEnd.Instance.HandleEscape()&&VillageWorkStall.Active==null&&BachDangFrontEnd.Instance.Page==BachDangFrontEnd.MenuPage.None,"ESC cancels the work without opening Pause: "+stall.kind);
                Check(!Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name=="Village_3D_Work_"+stall.kind),"Temporary workstation immediately stops blocking the world: "+stall.kind);
                Check(StationSignature()==stationSnapshot,"Leaving restores supplies while keeping the same furniture and canopy: "+stall.kind);
                Check(!Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name=="Workbench_Hands_Only")&&BeachBoatQuestSession.Instance.player.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>!r.forceRenderingOff),"ESC restores the complete player and removes the hands-only view: "+stall.kind);
                Place(stall.InteractionPosition);phase=3;return;
            }
            if(phase==3)
            {
                Check(stall.TryOpen(),"Cancelled trade can restart: "+stall.kind);stall.AdvanceTime(1);stall.ShowHelp();float elapsed=stall.Elapsed;stall.AdvanceTime(30);stall.StartTimedFromInstructions();
                Check(stall.Elapsed==elapsed&&stall.Phase==VillageWorkStall.WorkPhase.Playing,"Optional help pauses and resumes the same physical attempt: "+stall.kind);
                Time.timeScale=0;stall.AdvanceTime(5);Check(stall.Elapsed==elapsed,"Tab/pause cannot charge the physical work timer: "+stall.kind);Time.timeScale=1;
                phase=4;return;
            }
            if(phase==4)
            {
                if(stall.Phase==VillageWorkStall.WorkPhase.Won)
                {
                    Check(!stall.Physical.HandsOnly&&!BeachBoatQuestSession.Instance.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().TableFocusActive,"Finishing goods restores third-person view before claiming the reward: "+stall.kind);
                    Check(stall.Progress==stall.Goal&&CampaignProgress.Instance.MiniRecord(index+1)?.id==CampaignProgress.MiniId(index+1),"Only actual deliveries finish and record the 3D trade: "+stall.kind);
                    Check(stall.ClaimReward()&&stall.Completed,"Confirming the result grants the required supplies: "+stall.kind);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                    var save=BachDangSaveSystem.Capture("Physical trade QA");BachDangSaveSystem.Apply(save);
                    Check(stall.Completed&&VillageWorkStall.Active==null,"Save/load retains supplies and removes work props: "+stall.kind);
                    index++;phase=index<4?1:5;return;
                }
                var work=stall.Physical;var before=work.Current;
                if(QuestRowboat.FlatDistance(BeachBoatQuestSession.Instance.player.transform.position,work.RequiredApproach)>1)
                {stall.AdvanceTime(QuestRowboat.FlatDistance(BeachBoatQuestSession.Instance.player.transform.position,work.RequiredApproach)/30);Place(work.RequiredApproach);return;}
                if(!challengeChecks.Contains(stall.kind)&&work.ToolStep)
                {
                    if(work.Current==VillagePhysicalWork.Step.Saw)
                    {
                        work.PressTool();for(int n=0;n<3;n++)work.DragTool(Vector2.up*.4f,.1f);for(int n=0;n<8;n++)work.DragTool(Vector2.right*.3f,.1f);
                        Check(work.ToolStages==0,"A saw dragged outside the guide cannot cut wood");challengeChecks.Add(stall.kind);
                    }
                    else if(work.Current==VillagePhysicalWork.Step.Tie&&work.Finishing)
                    {
                        int knots=work.Knots;work.ReleaseTool();Check(work.Knots==knots&&work.ToolStages==0,"Releasing rope tension too early retries only the current knot");challengeChecks.Add(stall.kind);
                    }
                    else if(work.Current==VillagePhysicalWork.Step.FillRice&&work.ScoopLoaded)
                    {
                        for(int n=0;n<7;n++)work.DragTool(Vector2.up*.3f,.1f);
                        Check(!work.ScoopLoaded&&work.RiceFill==0,"Tilting a loaded scoop before reaching the sack spills the scoop, not the entire bag");challengeChecks.Add(stall.kind);
                    }
                    else if(work.Current==VillagePhysicalWork.Step.Wrap&&work.Finishing)
                    {
                        int stages=work.ToolStages;work.ReleaseTool();Check(work.ToolStages==stages&&!work.Finishing,"Releasing a fold before pressing it does not secure the crease");challengeChecks.Add(stall.kind);
                    }
                }
                if(work.Current==VillagePhysicalWork.Step.FillRice&&!riceOverflow)
                {
                    if(work.RiceFill<.95f){stall.AdvanceTime(.1f);DriveTool(work);return;}
                    int errors=stall.Mistakes;work.Interact();Check(stall.Mistakes==errors+1&&work.RiceFill==0&&stall.Progress==0,"Overfilled rice visibly resets the bag, charges one error and grants no delivery");riceOverflow=true;
                }
                bool action=work.Current==VillagePhysicalWork.Step.Saw||work.Current==VillagePhysicalWork.Step.Tie||work.Current==VillagePhysicalWork.Step.Wrap||work.Current==VillagePhysicalWork.Step.FillRice&&work.RiceFill<.70f;
                if(work.Current==VillagePhysicalWork.Step.Tie&&work.Finishing&&work.ToolPressure>.38f&&!captures.Contains("Rope_Tension")){RequestCapture("Rope_Tension");return;}
                if(action)
                {
                    if(!work.ToolHeld)
                    {
                        float beforeProgress=work.ActionProgress;
                        work.PressTool();for(int n=0;n<10;n++)work.DragTool(Vector2.zero,.1f);
                        Check(work.ActionProgress==beforeProgress,"Holding without tool motion grants no work: "+work.Current);
                    }
                    stall.AdvanceTime(.1f);CheckAction(DriveTool(work),work.Current.ToString());
                    if(work.ActionProgress>.35f&&work.ActionProgress<.65f)RequestCapture(stall.kind.ToString());
                }
                else
                {
                    stall.AdvanceTime(.2f);CheckAction(work.Interact(),work.Current.ToString());
                    if(work.Carrying)RequestCapture(stall.kind+"_Carry");
                }
                return;
            }
            if(phase==5)
            {
                Check(challengeChecks.Count==4,"All four light precision challenges reject incorrect gestures and remain completable");
                Check(VillagePreparationQuest.Instance.CompletedCount==4&&CampaignProgress.Instance.RecordedMinigames==4,"All four physical trades satisfy the supply quest and achievement list");
                var completed=BachDangSaveSystem.Capture("Completed physical trades");var replay=VillagePreparationQuest.Instance.stalls[0];Place(replay.InteractionPosition);Check(replay.TryOpen(),"A completed trade can be replayed for save/load cleanup verification");Place(replay.Physical.RequiredApproach);replay.Physical.Interact();BachDangSaveSystem.Apply(completed);
                Check(VillageWorkStall.Active==null&&VillagePreparationQuest.Instance.CompletedCount==4&&!Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name.StartsWith("Village_3D_Work_")),"Loading while carrying cleans temporary goods and preserves completed supplies");
                var progress=CampaignProgress.Instance;var current=progress.Capture();var old=progress.Capture();old.minigames=new[]{new BachDangSaveData.MiniRecord{id="village:0:1",seconds=65,limit=135}};
                progress.Restore(old,(int)Story.StoryState.Act2_SupplyPreparation);
                Check(progress.MiniRecord(1).seconds==65&&progress.MiniRank(1)==3&&progress.TimingResult(1).Contains("bản cũ"),"Legacy puzzle times stay unchanged and use their original rank limits");
                progress.RecordMinigame(CampaignProgress.MiniId(1),70,75);Check(progress.MiniRecord(1).seconds==65&&progress.MiniRank(1)==3,"A slower physical replay cannot erase the player's better historical achievement");
                progress.RecordMinigame(CampaignProgress.MiniId(1),30,75);
                Check(progress.MiniRecord(1).seconds==30&&progress.Record.minigames.Any(m=>m.id=="village:0:1"&&m.seconds==65),"A physical record is separate from the preserved historical puzzle record");progress.Restore(current,(int)Story.Instance.currentState);
                completedCheckpoint=BachDangSaveSystem.Capture("Table view cleanup QA");OpenSawBench();phase=6;return;
            }
            if(phase==6)
            {
                Check(stall.Physical.HandsOnly,"Replay enters the hands-only view before testing ESC");
                Check(BachDangFrontEnd.Instance.HandleEscape()&&VillageWorkStall.Active==null,"ESC closes a hands-only action without Pause");phase=7;return;
            }
            if(phase==7)
            {
                CheckRestored("ESC");OpenSawBench();phase=8;return;
            }
            if(phase==8)
            {
                Check(stall.Physical.HandsOnly,"Replay enters the hands-only view before testing load");BachDangSaveSystem.Apply(completedCheckpoint);phase=9;return;
            }
            if(phase==9)
            {
                CheckRestored("Load");foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static string StationSignature()
    {
        var station=stall.Workstation;var roof=stall.transform.Find("Mai_Che_Mo_Mat_Truoc");
        return station.Environment.GetEntityId()+"|"+string.Join("|",new[]{"Solid_workbench","Stock_crate","Delivery_crate"}.Select(n=>{var t=station.Environment.Find(n);return t.GetEntityId()+":"+t.GetComponent<Renderer>().bounds.ToString("F4");}))+"|"+roof.gameObject.activeInHierarchy+"|"+roof.localToWorldMatrix.ToString("F4");
    }
    static Camera PreviewCamera()
    {
        var camera=new GameObject("Workbench_QA_Camera").AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;CinematicQuality.ConfigureCamera(camera);return camera;
    }
    static float CapturePreview(Camera camera,string path)
    {
        var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;
        var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
        float mean=texture.GetPixels().Average(c=>c.grayscale);RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);return mean;
    }
    static void CaptureStation(string name)
    {
        var camera=PreviewCamera();camera.transform.position=stall.transform.TransformPoint(new Vector3(55,55,-85));camera.transform.LookAt(stall.transform.TransformPoint(new Vector3(0,20,-5)));CapturePreview(camera,"Logs/Stall_"+name+".png");Object.DestroyImmediate(camera.gameObject);
    }
    static void CheckCampLighting()
    {
        var light=Object.FindAnyObjectByType<CampReadability>();Check(light!=null&&light.CampVolume!=null&&!light.CampVolume.isGlobal,"Camp readability uses a bounded volume, not a global exposure change");
        var cycle=Object.FindAnyObjectByType<DayNightCycle>();float hour=cycle.timeOfDay;bool advance=cycle.autoAdvance;cycle.autoAdvance=false;cycle.SetTimeOfDay(19.5f);
        var facility=Object.FindObjectsByType<CampFacility>(FindObjectsSortMode.None).First(f=>f.kind==CampFacility.FacilityKind.Medical);var camera=PreviewCamera();camera.transform.position=facility.transform.position+facility.transform.forward*75+facility.transform.right*30+Vector3.up*28;camera.transform.LookAt(facility.transform.position+Vector3.up*14);
        TorchFlame.RefreshForCamera(camera);
        foreach(var torch in Object.FindObjectsByType<TorchFlame>(FindObjectsSortMode.None))if(torch.fireLight!=null&&torch.fireLight.enabled)torch.fireLight.intensity=torch.baseIntensity;
        try
        {
            light.CampVolume.weight=0;float before=CapturePreview(camera,"Logs/Camp_Readability_Before.png");light.CampVolume.weight=1;float after=CapturePreview(camera,"Logs/Camp_Readability_After.png");
            Check(after>before*1.04f&&after<before*2.3f,"Camp night image gets a modest, measurable brightness lift: "+before+" -> "+after);
            Check(!light.CampBounds.Contains(VillagePreparationQuest.Instance.stalls[0].transform.position),"Camp exposure region does not extend to the village minigames");
        }
        finally{light.CampVolume.weight=1;cycle.SetTimeOfDay(hour);cycle.autoAdvance=advance;Object.DestroyImmediate(camera.gameObject);TorchFlame.RefreshForCamera(Camera.main);}
    }
    static void OpenSawBench()
    {
        stall=VillagePreparationQuest.Instance.stalls.First(s=>s.kind==VillageWorkStall.WorkKind.Timber);Place(stall.InteractionPosition);
        Check(stall.TryOpen(),"Open a completed stall for table cleanup QA");Place(stall.Physical.RequiredApproach);stall.Physical.Interact();Place(stall.Physical.RequiredApproach);stall.Physical.Interact();
    }
    static void CheckRestored(string reason)
    {
        Check(!Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name=="Workbench_Hands_Only")&&BeachBoatQuestSession.Instance.player.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>!r.forceRenderingOff)&&!BeachBoatQuestSession.Instance.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().WorkFocusActive,reason+" destroys display arms and restores body visibility and orbit camera");
    }
    static void CheckAction(bool ok,string step){if(!ok)throw new Exception("Physical input failed at "+step+" phase="+stall.Phase+" elapsed="+stall.Elapsed+" time="+stall.TimeRemaining+" near="+stall.Physical.CanInteract+" held="+stall.Physical.ToolHeld+" pointer="+stall.Physical.ToolPointer+" target="+stall.Physical.RequiredApproach+" player="+BeachBoatQuestSession.Instance.player.transform.position);}
}

[DefaultExecutionOrder(950)]
public sealed class VillageWorkPoseProbe : MonoBehaviour
{
    void LateUpdate(){BeachBoatPhysicalVillagePlaytest.CaptureAfterPose();}
}
