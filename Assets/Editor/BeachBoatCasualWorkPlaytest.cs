using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Story=BeachBoatStory.BachDangStoryManager;
using Zone=BeachBoatStory.RiverStakeZone;

public static class BeachBoatCasualWorkPlaytest
{
    const string Flag="CasualWork938QA";
    static int phase,lastFrame,cluster,hammerFrame,badStrikePhase;static double began;static bool captured,capturedHammer,impactTimingChecked,hammerCaptureReady,leanChecked;static string runtimeError;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static float maxHammerGripError,maxHammerWristAngle,maxCrewHandMotion;static Vector3 firstCrewHand;static int hammerSamples;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
    public static void RunBatch(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");RunInEditor();}
    public static void RunInEditor(){phase=cluster=badStrikePhase=0;lastFrame=-1;began=0;runtimeError=null;hammerFrame=hammerSamples=0;maxHammerGripError=maxHammerWristAngle=maxCrewHandMotion=0;captured=capturedHammer=impactTimingChecked=hammerCaptureReady=leanChecked=false;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))runtimeError=m+"\n"+stack;}
    static void PlacePlayer(Vector3 p){var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;player.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms();}
    static void StartCluster(int index)
    {
        var z=Zone.Instance;var boat=QuestRowboat.Instance;var at=z.placements[index].approach.position;
        for(int n=0;n<9;n++)
        {
            var p=at+(n==0?Vector3.zero:Quaternion.Euler(0,(n-1)*45,0)*Vector3.forward*18);
            if(!z.HasBoatClearance(p))continue;boat.RestoreSave(true,p,Quaternion.identity);
            if(z.TryPlant(index))return;
        }
        throw new Exception("Cannot start actual construction at cluster "+index);
    }
    static void CaptureCarry(string name="Carry")
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;if(name=="Carry")camera.GetComponent<BeachBoatStory.Camera_Script>().SnapToTarget();
        var rt=new RenderTexture(1280,800,24);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
        File.WriteAllBytes("Logs/StakeConstruction_"+name+".png",pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(rt);
    }
    static void Finish(string error)
    {
        SessionState.SetBool(Flag,false);File.WriteAllText("Logs/CasualWorkPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,hammerSamples,maxHammerGripError,maxHammerWristAngle,maxCrewHandMotion,checks},Newtonsoft.Json.Formatting.Indented));
        BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
        if(Application.isBatchMode)EditorApplication.Exit(error==null?0:1);
    }
    public static void ObserveHammer()
    {
        if(!SessionState.GetBool(Flag,false)||StakeConstruction.Active?.Step!=StakeConstruction.WorkStep.Hammer)return;
        try
        {
            var player=BeachBoatQuestSession.Instance.player;var rig=player.GetComponentInChildren<Animator>();var profile=RiverPlayerCombat.Instance.GripProfile;
            var hammer=GameObject.Find("Bua_go").transform;var right=rig.GetBoneTransform(HumanBodyBones.RightHand);var left=rig.GetBoneTransform(HumanBodyBones.LeftHand);
            var rc=right.TransformPoint(profile.rightRodCenter);var lc=left.TransformPoint(profile.leftCenter);
            maxHammerGripError=Mathf.Max(maxHammerGripError,StakeConstruction.Active.HandContactError);
            var crewHand=UnityEngine.Object.FindObjectsByType<Transform>().First(t=>t.name=="Cong_binh_0_1").GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand).position;
            if(hammerSamples==0)firstCrewHand=crewHand;else maxCrewHandMotion=Mathf.Max(maxCrewHandMotion,Vector3.Distance(crewHand,firstCrewHand));
            maxHammerWristAngle=Mathf.Max(maxHammerWristAngle,StakeConstruction.Active.HandWristAngle);hammerSamples++;
            if(!hammerCaptureReady)return;hammerCaptureReady=false;
            File.WriteAllText("Logs/HammerGripContact.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{right=rc.ToString(),left=lc.ToString(),shaft=hammer.position.ToString(),direction=hammer.up.ToString(),leftShoulder=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.ToString(),rightShoulder=rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position.ToString(),leftWrist=left.position.ToString(),rightWrist=right.position.ToString()},Newtonsoft.Json.Formatting.Indented));
            Check(StakeConstruction.Active.HandsOnly,"Only work hands render while hammering, leaving the stake unobstructed");
            Check(StakeConstruction.Active.SupportedOnDeck,"The work position keeps both boots over the actual plank deck");
            var view=BeachBoatQuestSession.Instance.storyCamera;
            Check(BoatHullCollision.Get(QuestRowboat.Instance.transform).TryDeckSurface(view.transform.position,out _),"Hammer camera stays vertically above the actual boat planks");
            File.WriteAllText("Logs/StakeWorkCamera.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{eye=view.transform.position.ToString(),forward=view.transform.forward.ToString(),head=rig.GetBoneTransform(HumanBodyBones.Head).position.ToString(),chest=rig.GetBoneTransform(HumanBodyBones.Chest).position.ToString(),stake=GameObject.Find("Coc_dang_dong").transform.position.ToString(),player=player.transform.position.ToString(),water=TideSystem.CurrentWaterHeight},Newtonsoft.Json.Formatting.Indented));
            CaptureCarry("Hammer");
            Check(view.transform.forward.y<-.05f,"The hammer view looks down towards the supported work position, not up at the sky • "+view.transform.forward+" eye "+view.transform.position);
            Check(StakeConstruction.Active.StakeMesh==Zone.Instance.fieldStakes[0].GetComponentInChildren<MeshFilter>(true).sharedMesh,"The sampled stake uses the completed field's pointed timber mesh");
            Check(maxCrewHandMotion>.15f,"A construction hammer hand actually moves while the player is hammering, despite the player interaction lock");
            Check(StakeConstruction.Active.WorkingCrew>=12,"More construction soldiers cooperate in holder/hammer pairs on separate boats");
            Check(StakeConstruction.Active.HandContactError<.02f&&StakeConstruction.Active.HandSegmentError<.001f,"Both fixed-length display arms hold the actual hammer shaft");
            Check(StakeConstruction.Active.HandWristAngle<1&&StakeConstruction.Active.DisplayedGripWeight>90,"Hammer wrists stay neutral and fingers close around the shaft");
            Check(StakeConstruction.Active.HammerHeadError<.02f,"The hammer head reaches its real stake target instead of stopping short");
            foreach(var r in player.GetComponentsInChildren<SkinnedMeshRenderer>())r.forceMatrixRecalculationPerRender=true;
            CaptureCarry("Hammer");
            var crewActor=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name=="Cong_binh_0_1");
            var crewView=new GameObject("PairedConstructionProof").AddComponent<Camera>();crewView.CopyFrom(view);crewView.enabled=false;
            crewView.transform.position=crewActor.position+crewActor.forward*34+crewActor.right*35+Vector3.up*28;
            crewView.transform.LookAt(crewActor.position+Vector3.up*5);
            var crewRt=new RenderTexture(1280,800,24);crewView.targetTexture=crewRt;crewView.Render();RenderTexture.active=crewRt;
            var crewPixels=new Texture2D(1280,800,TextureFormat.RGB24,false);crewPixels.ReadPixels(new Rect(0,0,1280,800),0,0);crewPixels.Apply();File.WriteAllBytes("Logs/StakeConstruction_Team.png",crewPixels.EncodeToPNG());
            RenderTexture.active=null;crewView.targetTexture=null;UnityEngine.Object.DestroyImmediate(crewPixels);UnityEngine.Object.DestroyImmediate(crewRt);UnityEngine.Object.DestroyImmediate(crewView.gameObject);
            var source=BeachBoatQuestSession.Instance.storyCamera;var chest=rig.GetBoneTransform(HumanBodyBones.Chest);var preview=new GameObject("HammerGripFrontProof").AddComponent<Camera>();preview.CopyFrom(source);preview.enabled=false;preview.transform.position=chest.position+player.transform.forward*22+player.transform.right*24+Vector3.up*9;preview.transform.LookAt(Vector3.Lerp(chest.position,hammer.position,.7f));
            foreach(bool close in new[]{false,true})
            {
                if(close){preview.transform.position=hammer.position+player.transform.forward*10+player.transform.right*11+Vector3.up*3;preview.transform.LookAt(hammer.position-hammer.up*.5f);}
                var rt=new RenderTexture(1280,800,24);preview.targetTexture=rt;preview.Render();RenderTexture.active=rt;var t=new Texture2D(1280,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,800),0,0);t.Apply();File.WriteAllBytes("Logs/StakeConstruction_Hammer_"+(close?"GripClose":"Front")+".png",t.EncodeToPNG());RenderTexture.active=null;preview.targetTexture=null;UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(rt);
            }
            UnityEngine.Object.DestroyImmediate(preview.gameObject);capturedHammer=true;
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>240)throw new Exception("Casual work QA timeout");
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/CasualWorkQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BeachBoatQuestSession.Instance.player.AddComponent<HammerWorkPoseProbe>();
                BachDangFrontEnd.Instance.Play();UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                Story.Instance.collectedWood=5;Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);
                var solve=typeof(BeachBoatVillageWorkPlaytest).GetMethod("SolveAttempt",BindingFlags.NonPublic|BindingFlags.Static);
                foreach(var s in VillagePreparationQuest.Instance.stalls)
                {
                    s.RestoreLearningProgress(0);PlacePlayer(s.InteractionPosition);
                    Check(s.TryOpen()&&s.Difficulty==1&&s.Phase==VillageWorkStall.WorkPhase.Playing,"Interaction immediately starts one Medium game: "+s.kind);
                    s.AdvanceTime(1);s.ShowHelp();float elapsed=s.Elapsed;s.AdvanceTime(50);s.StartTimedFromInstructions();
                    Check(s.Phase==VillageWorkStall.WorkPhase.Playing&&s.Elapsed==elapsed,"Optional help returns to the existing attempt without resetting or charging time: "+s.kind);
                    solve.Invoke(null,new object[]{s});Check(s.Phase==VillageWorkStall.WorkPhase.Won,"The Medium puzzle is solvable: "+s.kind);
                    Check(s.ClaimReward()&&s.Completed&&VillageWorkStall.Active==null,"One victory grants supplies without unlocking another difficulty: "+s.kind);
                    BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                }
                Check(VillagePreparationQuest.Instance.CompletedCount==4&&CampaignProgress.ScoredMinigames==5,"All four supplies and only five scored minigames are required");
                Check(CampaignProgress.Instance.RecordedMinigames==4,"The four Medium records map to the new achievement requirements");
                var supply=BachDangSaveSystem.Capture("QA medium supplies");BachDangSaveSystem.Apply(supply);
                Check(VillagePreparationQuest.Instance.CompletedCount==4,"Saving and loading retains collected supplies under the single-difficulty rules");
                Story.Instance.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver,true);Zone.Instance.RestoreCheckpoint(new bool[5]);StartCluster(0);
                Check(StakeConstruction.Active!=null&&Zone.Instance.IsPlanting&&!BachDangSaveSystem.CanSave,"Starting construction enters the 3D work session and defers saving until stable");
                Check(!StakeConstruction.Active.Strike(),"Hammering cannot skip picking up and carrying a bundle");phase=1;return;
            }
            if(phase==1||phase==2)
            {
                var work=StakeConstruction.Active;
                if(work==null)
                {
                    if(BeachBoatStory.ScreenFader.Instance.IsTransitioning||Zone.Instance.IsPlanting)return;
                    if(!Zone.Instance.IsComplete)
                    {
                        Check(RiverConstructionCamp.Ensure(Zone.Instance).Workers>=(cluster+1)*12,"Each worked location retains at least six paired teams after leaving the minigame");
                        Check(Zone.Instance.CompletedClusterCount==cluster+1,"Only the finished location advances progress: "+(cluster+1)+"/5");
                        Check(Story.Instance.currentState==Story.StoryState.Act2_PlantStakesAtRiver,"The other locations remain required before the time skip");
                        Check(Zone.Instance.fieldStakes.All(t=>!t.gameObject.activeSelf),"The full stake field stays hidden before all five samples are complete");
                        var partial=BachDangSaveSystem.Capture("QA partial stakes");var posts=Zone.Instance.CaptureSamplePositions();
                        Check(partial.constructionSites.Length==cluster+1&&partial.constructionSites.All(c=>c.posts.Length>=6),"NPC posts and stationed workers are part of partial saves");
                        BachDangSaveSystem.Apply(partial);
                        var restored=RiverConstructionCamp.Ensure(Zone.Instance).Capture();Check(partial.constructionSites.SelectMany(c=>c.posts).Zip(restored.SelectMany(c=>c.posts),(a,b)=>Vector3.Distance(a,b)<.001f).All(v=>v),"NPC poles retain their actual positions across partial load");
                        Check(Zone.Instance.CompletedClusterCount==cluster+1&&!Zone.Instance.IsComplete,"A partial save retains the number of completed locations");
                        Check(Zone.Instance.CaptureSamplePositions().Select((v,i)=>!partial.stakes[i]||Vector3.Distance(v,posts[i])<.001f).All(v=>v),"Planted sample posts retain their exact positions after loading");
                        cluster++;StartCluster(cluster);return;
                    }
                    Check(cluster==4&&Zone.Instance.CompletedClusterCount==Zone.Instance.ClusterCount,"Five separate sample locations precede the narrative reveal");
                    Check(Story.Instance.currentState==Story.StoryState.Act2_CompleteReturnToCamp,"The days-later reveal resumes the return-to-camp mission");
                    Check(BachDangSaveSystem.CanSave,"Construction and the narrative release save and rowing controls");
                    Check(hammerSamples>50&&maxHammerGripError<.02f&&maxHammerWristAngle<1,"Displayed hammer hands stay on the shaft with neutral wrists through the sample exercise");
                    var final=BachDangSaveSystem.Capture("QA five locations and time skip");File.WriteAllText("Logs/ConstructionFieldCheckpoint.json",JsonUtility.ToJson(final));BachDangSaveSystem.Apply(final);
                    Check(final.constructionSites.Length==0&&RiverConstructionCamp.Ensure(Zone.Instance).Workers==0,"The time skip removes symbolic construction crews and their posts");
                    Check(!UnityEngine.Object.FindObjectsByType<Transform>().Any(t=>t.name.StartsWith("Coc_mau_vi_tri_")||t.name.StartsWith("Coc_nhon_cong_binh")),"Player and NPC sample poles disappear after the completed field reveal");
                    Check(Zone.Instance.ExposedStakeCount>=0&&Zone.Instance.fieldStakes.Any(t=>t.gameObject.activeSelf),"The real completed stake field remains present");
                    Check(Zone.Instance.IsComplete&&StakeConstruction.Active==null,"Loading the finished time skip restores the full field without work locks");
                    Check(CampaignProgress.Instance.Record.stakeTargetSeconds>=180&&CampaignProgress.Instance.StakeTarget(3)==final.campaign.stakeTargetSeconds,"Five-location route achievement keeps its realistic recorded time target after load");
                    foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);return;
                }
                if(work.Step==StakeConstruction.WorkStep.TakeBundle)work.Interact();
                else if(work.Step==StakeConstruction.WorkStep.CarryBundle)
                {
                    work.WalkBundle(Time.deltaTime);
                    if(!captured&&work.CarryProgress>.5f){captured=true;CaptureCarry();Check(GameObject.Find("Bo_coc_dang_vac")!=null,"The player carries the sample stake in the actual world");Check(work.SupportedOnDeck,"Carrying stays on supported planks before reaching the work position");}
                    if(work.CarryProgress>=1)
                    {
                        if(!work.HasAimedStake){Check(!work.Interact(),"Placing a stake requires aiming");work.AimStake(Vector2.right*.02f);}
                        else if(!work.PlacementValid)work.AimStake(new Vector2(Mathf.Cos(Time.time*2),Mathf.Sin(Time.time*2))*.12f);
                        else {if(!leanChecked){work.AdjustLean(work.RequiredLean+8-work.StakeLean);Check(!work.Interact(),"A visibly incorrect stake inclination cannot skip placement alignment");leanChecked=true;}work.AdjustLean(work.RequiredLean-work.StakeLean);work.Interact();}
                    }
                }
                else if(work.Step==StakeConstruction.WorkStep.Hammer)
                {
                    if(badStrikePhase==0){for(int n=0;n<3;n++)work.LiftHammer(.15f);if(work.Strike()){badStrikePhase=1;Check(work.Progress==0,"Releasing weakly starts a visible swing without awarding progress");}return;}
                    if(badStrikePhase==1){if(work.IsSwinging)return;Check(work.Misses==1&&work.Progress==0,"A weak impact does not advance the stake");badStrikePhase=2;}
                    if(badStrikePhase==2){work.AimHammer(.2f);if(work.HammerLift<.8f)work.LiftHammer(.15f);else if(work.Strike())badStrikePhase=3;return;}
                    if(badStrikePhase==3){if(work.IsSwinging)return;Check(work.Misses==2&&work.Progress==0,"A strong but off-centre strike also fails to drive the stake");badStrikePhase=4;}
                    work.AimHammer(-work.AimError);
                    if(work.HammerLift<.8f)work.LiftHammer(.15f);
                    else
                    {
                        if(!capturedHammer){if(hammerFrame==0)hammerFrame=Time.frameCount;if(Time.frameCount>hammerFrame+20)hammerCaptureReady=true;return;}
                        float before=work.Progress;
                        if(!work.IsBeatReady)return;
                        if(work.Strike()&&!impactTimingChecked){Check(work.Progress==before,"Progress is awarded only when a well-aimed downswing lands");impactTimingChecked=true;}
                    }
                }
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}

[DefaultExecutionOrder(950)]
public sealed class HammerWorkPoseProbe : MonoBehaviour {void LateUpdate(){BeachBoatCasualWorkPlaytest.ObserveHammer();}}
