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
    static int phase,lastFrame,cluster,hammerFrame;static double began;static bool captured,capturedHammer,impactTimingChecked,hammerCaptureReady;static string runtimeError;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static float maxHammerGripError,maxHammerWristAngle;static int hammerSamples;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
    public static void RunInEditor(){phase=cluster=0;lastFrame=-1;began=0;runtimeError=null;hammerFrame=hammerSamples=0;maxHammerGripError=maxHammerWristAngle=0;captured=capturedHammer=impactTimingChecked=hammerCaptureReady=false;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
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
        SessionState.SetBool(Flag,false);File.WriteAllText("Logs/CasualWorkPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,hammerSamples,maxHammerGripError,maxHammerWristAngle,checks},Newtonsoft.Json.Formatting.Indented));
        BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    public static void ObserveHammer()
    {
        if(!SessionState.GetBool(Flag,false)||StakeConstruction.Active?.Step!=StakeConstruction.WorkStep.Hammer)return;
        try
        {
            var player=BeachBoatQuestSession.Instance.player;var rig=player.GetComponentInChildren<Animator>();var profile=RiverPlayerCombat.Instance.GripProfile;
            var hammer=GameObject.Find("Bua_go").transform;var right=rig.GetBoneTransform(HumanBodyBones.RightHand);var left=rig.GetBoneTransform(HumanBodyBones.LeftHand);
            var rc=right.TransformPoint(profile.rightRodCenter);var lc=left.TransformPoint(profile.leftCenter);
            maxHammerGripError=Mathf.Max(maxHammerGripError,Vector3.Distance(rc,hammer.position),Vector3.Distance(lc,hammer.position-hammer.up*1.8f));
            maxHammerWristAngle=Mathf.Max(maxHammerWristAngle,Vector3.Angle(right.TransformDirection(profile.rightForward),right.position-rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position),Vector3.Angle(left.TransformDirection(profile.leftForward),left.position-rig.GetBoneTransform(HumanBodyBones.LeftLowerArm).position));hammerSamples++;
            if(!hammerCaptureReady)return;hammerCaptureReady=false;
            File.WriteAllText("Logs/HammerGripContact.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{right=rc.ToString(),left=lc.ToString(),shaft=hammer.position.ToString(),direction=hammer.up.ToString(),leftShoulder=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.ToString(),rightShoulder=rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position.ToString(),leftWrist=left.position.ToString(),rightWrist=right.position.ToString()},Newtonsoft.Json.Formatting.Indented));
            Check(Vector3.Distance(rc,hammer.position)<.8f,"Upper fist holds the actual hammer shaft");
            Check(Vector3.Distance(lc,hammer.position-hammer.up*1.8f)<.8f,"Lower fist holds the same shaft below the upper fist (error "+Vector3.Distance(lc,hammer.position-hammer.up*1.8f)+")");
            float rightAngle=Vector3.Angle(right.TransformDirection(profile.rightForward),right.position-rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position);
            float leftAngle=Vector3.Angle(left.TransformDirection(profile.leftForward),left.position-rig.GetBoneTransform(HumanBodyBones.LeftLowerArm).position);
            Check(rightAngle<12&&leftAngle<12,"Both hammer wrists follow the forearms without a bent cuff: "+rightAngle+" / "+leftAngle);
            Check(player.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMesh==profile.posed&&r.GetBlendShapeWeight(0)>99&&r.GetBlendShapeWeight(1)>99),"Both visible hands close around the hammer, rather than staying open");
            foreach(var r in player.GetComponentsInChildren<SkinnedMeshRenderer>())r.forceMatrixRecalculationPerRender=true;
            CaptureCarry("Hammer");
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
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>140)throw new Exception("Casual work QA timeout");
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
                    if(phase==2)
                    {
                        Check(Zone.Instance.CompletedClusterCount==cluster+1,"The 3D carry and hammer loop completes cluster "+(cluster+1));
                        if(Zone.Instance.RecoveryRequired)
                        {
                            BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                            QuestRowboat.Instance.RestoreSave(true,Zone.Instance.RecoveryTarget.position,Quaternion.identity);
                            Check(Zone.Instance.TryRecoverLostStake(),"The lost-bundle recovery remains playable between the 3D work clusters");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                        }
                        cluster++;
                        if(cluster<5){StartCluster(cluster);return;}
                        Check(Zone.Instance.IsComplete&&Zone.Instance.PlantedCount==Zone.Instance.RequiredCount,"All physical field stakes and the return-to-camp quest complete after five 3D work clusters");
                        Check(hammerSamples>100&&maxHammerGripError<.8f&&maxHammerWristAngle<12,"Hands stay on the shaft and wrists stay neutral throughout every lift and impact: "+hammerSamples+" samples, grip "+maxHammerGripError+", wrist "+maxHammerWristAngle);
                        var final=BachDangSaveSystem.Capture("QA all physical stakes");BachDangSaveSystem.Apply(final);
                        Check(Zone.Instance.IsComplete&&StakeConstruction.Active==null,"Loading the full completed field restores all stakes without a construction lock");
                        foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);return;
                    }
                    Check(Zone.Instance.CompletedClusterCount==1&&Zone.Instance.PlantedCount==Zone.Instance.placements[0].stakeIndices.Length,"Three carried bundles and nine strikes build exactly the chosen saved cluster");
                    Check(BachDangSaveSystem.CanSave,"Construction releases save and rowing controls after completion");
                    var save=BachDangSaveSystem.Capture("QA finished construction");BachDangSaveSystem.Apply(save);
                    Check(Zone.Instance.CompletedClusterCount==1&&StakeConstruction.Active==null,"Loading a completed cluster does not recreate carried props or lock the player");
                    StartCluster(1);Zone.Instance.RestoreCheckpoint(Zone.Instance.CaptureCheckpoint());
                    Check(!Zone.Instance.IsPlanting&&StakeConstruction.Active==null&&Zone.Instance.CompletedClusterCount==1,"Cancelling unfinished 3D work keeps completed clusters and releases the avatar");
                    cluster=1;phase=2;StartCluster(cluster);return;
                }
                if(work.Step==StakeConstruction.WorkStep.TakeBundle)work.Interact();
                else if(work.Step==StakeConstruction.WorkStep.CarryBundle)
                {
                    work.WalkBundle(Time.deltaTime);
                    if(!captured&&work.CarryProgress>.5f){captured=true;CaptureCarry();Check(GameObject.Find("Bo_coc_dang_vac")!=null,"The player visibly carries a bundle in the live 3D scene");}
                    if(work.CarryProgress>=1)
                    {
                        if(!work.HasAimedStake){Check(!work.Interact(),"Placing a stake requires aiming, not only E");work.AimStake(Vector2.right*.02f);}
                        else if(!work.PlacementValid)work.AimStake(new Vector2(Mathf.Cos(Time.time*2),Mathf.Sin(Time.time*2))*.12f);
                        else work.Interact();
                    }
                }
                else if(work.Step==StakeConstruction.WorkStep.Hammer)
                {
                    if(work.HammerLift<.8f)work.LiftHammer(.15f);
                    else
                    {
                        if(!capturedHammer){if(hammerFrame==0)hammerFrame=Time.frameCount;if(Time.frameCount>hammerFrame+20)hammerCaptureReady=true;return;}
                        float before=work.Progress;
                        if(work.Strike()&&!impactTimingChecked){Check(work.Progress==before,"Releasing the hammer counts progress only when its physical downswing lands");impactTimingChecked=true;}
                    }
                }
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}

[DefaultExecutionOrder(950)]
public sealed class HammerWorkPoseProbe : MonoBehaviour {void LateUpdate(){BeachBoatCasualWorkPlaytest.ObserveHammer();}}
