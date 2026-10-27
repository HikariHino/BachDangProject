using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;

public static class BeachBoatArcheryPlaytest
{
    const string Flag="ArcheryRange938QA";static int phase,lastFrame,poseFrame,poseStage;static double started;static float nextShot;static bool poseReady;
    static string failure;static int battleShots,battleHits,ammo;static float campaignSeconds;static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string path)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path)));
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
    public static void RunInEditor(){phase=poseStage=0;lastFrame=-1;started=0;failure=null;poseReady=false;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/ArcheryQA/PrePlay");EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType t){if(SessionState.GetBool(Flag,false)&&(t==LogType.Error||t==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error)
    {
        SessionState.SetBool(Flag,false);ArcheryTraining.Active?.Close();
        File.WriteAllText("Logs/ArcheryPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));
        BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    static void Capture(Camera c,string name="Draw")
    {
        var rt=new RenderTexture(1280,800,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;var t=new Texture2D(1280,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,800),0,0);t.Apply();File.WriteAllBytes("Logs/ArcheryTraining_"+name+".png",t.EncodeToPNG());RenderTexture.active=null;c.targetTexture=null;UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(rt);
    }
    public static void ObserveAfterPose()
    {
        if(!poseReady||!SessionState.GetBool(Flag,false))return;poseReady=false;
        try
        {
            var combat=RiverPlayerCombat.Instance;var range=ArcheryTraining.Instance;var player=BeachBoatQuestSession.Instance.player;
            if(poseStage<2)
            {
                var skeleton=player.GetComponentInChildren<Animator>();var grip=combat.GripProfile;var lh=skeleton.GetBoneTransform(HumanBodyBones.LeftHand);var rh=skeleton.GetBoneTransform(HumanBodyBones.RightHand);
                Check(grip!=null&&Vector3.Angle(lh.TransformDirection(grip.leftForward),lh.position-skeleton.GetBoneTransform(HumanBodyBones.LeftLowerArm).position)<15&&Vector3.Angle(rh.TransformDirection(grip.rightForward),rh.position-skeleton.GetBoneTransform(HumanBodyBones.RightLowerArm).position)<15,"Both wrists remain neutral during draw stage "+poseStage);
                Check(Vector3.Dot(rh.position-skeleton.GetBoneTransform(HumanBodyBones.Chest).position,player.transform.forward)>.5f,"Drawing wrist remains in front of the torso during draw stage "+poseStage);
                Capture(BeachBoatQuestSession.Instance.storyCamera,poseStage==0?"Ready":"HalfDraw");
                poseStage++;player.GetComponent<ArcheryPoseProbe>().draw=poseStage==1?.55f:.95f;poseFrame=Time.frameCount;return;
            }
                var rig=player.GetComponentInChildren<Animator>();var bow=GameObject.Find("Cung_Go_Quan_Si").transform;
                Check(Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.LeftHand).position,bow.position)<2.5f,"Left hand supports the bow grip");
                var profile=combat.GripProfile;Check(profile!=null&&profile.posed.blendShapeCount==2,"The fingerless source rig has dedicated grip and string-hook mesh poses");
                var head=rig.GetBoneTransform(HumanBodyBones.Head);var rightHand=rig.GetBoneTransform(HumanBodyBones.RightHand);var leftHand=rig.GetBoneTransform(HumanBodyBones.LeftHand);
                Check(Vector3.Distance(rightHand.TransformPoint(profile.rightCenter),head.position)<4,"The hooked fingers anchor near the cheek");
                Check(Vector3.Angle(leftHand.TransformDirection(profile.leftForward),leftHand.position-rig.GetBoneTransform(HumanBodyBones.LeftLowerArm).position)<15,"Bow wrist follows its forearm without a drooping bend");
                Check(Vector3.Angle(rightHand.TransformDirection(profile.rightForward),rightHand.position-rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position)<15,"Drawing wrist follows its forearm without a drooping bend");
                Check(player.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMesh==profile.posed&&r.GetBlendShapeWeight(0)>99&&r.GetBlendShapeWeight(1)>99),"Both visible mesh hands actually close while the bow is drawn");
                var chest=rig.GetBoneTransform(HumanBodyBones.Chest);var wrist=rig.GetBoneTransform(HumanBodyBones.RightHand);var elbow=rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
                Check(Vector3.Dot(wrist.position-chest.position,player.transform.forward)>.5f,"Drawing wrist stays in front of the chest, never behind the back");
                Check(Vector3.Dot(elbow.position-chest.position,player.transform.right)>1.5f,"Drawing elbow stays outside the torso");
                Check(bow.Find("Nocked_arrow").gameObject.activeSelf,"A nocked arrow is visible while drawing");
                foreach(var r in player.GetComponentsInChildren<SkinnedMeshRenderer>())r.forceMatrixRecalculationPerRender=true;
                Capture(BeachBoatQuestSession.Instance.storyCamera);
                var source=BeachBoatQuestSession.Instance.storyCamera;var preview=new GameObject("ArcheryFrontProof").AddComponent<Camera>();preview.CopyFrom(source);preview.enabled=false;preview.transform.position=chest.position+player.transform.forward*18+player.transform.right*20+Vector3.up*4;preview.transform.LookAt(chest.position+Vector3.up*2);Capture(preview,"Front");UnityEngine.Object.DestroyImmediate(preview.gameObject);
                UnityEngine.Object.DestroyImmediate(player.GetComponent<ArcheryPoseProbe>());combat.enabled=true;
                Check(BachDangFrontEnd.Instance.HandleEscape()&&ArcheryTraining.Active==null&&BachDangFrontEnd.Instance.Page==BachDangFrontEnd.MenuPage.None,"ESC exits training without opening Pause");
                Check(combat.Arrows==ammo,"Training returns the original battle ammunition");
                Check(player.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMesh==profile.source),"Lowering the bow restores the original character mesh and open-hand pose");
                var data=BachDangSaveSystem.Capture("Archery training result");Check(data.archeryBestScore==60&&data.archeryTrainingCompleted,"The save captures the practice record");
                range.Restore(0,false);BachDangSaveSystem.Apply(data);Check(range.BestScore==60&&range.Completed&&ArcheryTraining.Active==null,"Save/load restores the result without reopening a training round");
                foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-started>75)throw new Exception("Archery QA timeout");
            var range=ArcheryTraining.Instance;var combat=RiverPlayerCombat.Instance;var player=BeachBoatQuestSession.Instance.player;
            if(phase==0)
            {
                foreach(var path in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[path]=Hash(path);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/ArcheryQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();BeachBoatStory.BachDangStoryManager.Instance.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act2_Completed,true);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                Check(range!=null&&range.Targets?.Length==3,"Camp contains three physical near/far targets");
                Check(range.Targets.All(t=>t.GetComponentInChildren<MeshCollider>()!=null),"Arrows hit the thin target surface rather than an oversized sphere");
                BeachBoatPhysicalVillagePlaytest.Place(range.Approach+Vector3.right*80);Check(!range.Begin(),"Training cannot start remotely");
                BeachBoatPhysicalVillagePlaytest.Place(range.Approach);ammo=combat.Arrows;battleShots=combat.ShotsFired;battleHits=combat.Hits;
                Check(range.Begin()&&!BachDangSaveSystem.CanSave,"Training starts in the world and defers saving during a round");
                campaignSeconds=CampaignProgress.Instance.Record.seconds;
                Check(!combat.TryFire(new Ray(player.transform.position,Vector3.up),1),"Aiming is required before firing");
                phase=1;return;
            }
            if(phase==1)
            {
                if(range.Shots==6){nextShot=Time.time+1.1f;phase=2;return;}
                if(Time.time<nextShot)return;
                var target=range.NextTarget;var origin=player.transform.position+Vector3.up*14;var direction=(target.transform.position-origin).normalized;
                combat.SetAim(true);Physics.SyncTransforms();var end=range.ResolveShot(new Ray(origin,direction),out var actual,out int points);
                Check(actual==target&&points==10,"A clear shot at the highlighted bullseye scores ten points: "+range.Shots+" (hit "+actual?.name+") obstacles="+string.Join(",",Physics.RaycastAll(new Ray(origin,direction),220).OrderBy(h=>h.distance).Take(5).Select(h=>h.collider.name+":"+h.distance))+" approach="+range.Approach+" target="+target.transform.position);
                Check(combat.TryFire(new Ray(origin,direction),1),"The normal bow launches a training arrow");combat.StepArrows(1);
                nextShot=Time.time+.65f;return;
            }
            if(phase==2)
            {
                if(Time.time<nextShot)return;
                Check(range.Finished&&range.Score==60&&range.Hits==6&&range.Completed&&range.BestScore==60,"Six arriving arrows complete the physical round and record its result");
                combat.SetAim(true);Check(!combat.TryFire(new Ray(player.transform.position,Vector3.up),1),"A completed round cannot fire a seventh scoring arrow");
                Check(combat.ShotsFired==battleShots&&combat.Hits==battleHits,"Training does not inflate battle accuracy or kills");
                Check(CampaignProgress.Instance.Record.seconds==campaignSeconds,"Optional practice does not penalize the campaign rank timer");
                var camera=BeachBoatQuestSession.Instance.storyCamera;var direction=range.Targets[0].transform.position-player.transform.position;direction.y=0;player.transform.rotation=Quaternion.LookRotation(direction);player.GetComponent<Rigidbody>().rotation=player.transform.rotation;
                var orbit=camera.GetComponent<BeachBoatStory.Camera_Script>();orbit.SnapToTarget();orbit.SetElevation(0);combat.enabled=false;var probe=player.AddComponent<ArcheryPoseProbe>();probe.combat=combat;probe.draw=.05f;poseFrame=Time.frameCount;phase=3;return;
            }
            if(phase==3){if(Time.frameCount>=poseFrame+20)poseReady=true;return;}

        }
        catch(Exception e){Finish(e.ToString());}
    }
}

[DefaultExecutionOrder(950)]
public sealed class ArcheryPoseProbe : MonoBehaviour
{
    public RiverPlayerCombat combat;public float draw=.95f;
    void LateUpdate()
    {
        if(combat==null)return;combat.SetAim(true);typeof(RiverPlayerCombat).GetField("charge",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(combat,draw);
        typeof(RiverPlayerCombat).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
        BeachBoatArcheryPlaytest.ObserveAfterPose();
    }
}
