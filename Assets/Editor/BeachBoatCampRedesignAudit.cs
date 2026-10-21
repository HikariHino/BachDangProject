using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public static class BeachBoatCampRedesignAudit
{
    static Bounds BoundsOf(GameObject go)
    {
        var rr=go.GetComponentsInChildren<Renderer>(true);
        var b=rr.Length>0?rr[0].bounds:new Bounds(go.transform.position,Vector3.zero);
        foreach(var r in rr.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }
    public static void AuditAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);
        var camp=GameObject.Find("TestQuest_Camp").transform;
        var stage=camp.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage"));
        var session=UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>();
        var roots=new[]{"TestQuest_Camp","Camp_Soldiers","NgoQuyen_QuestGiver","CampToVillage","CampToRiver",
            "Lo_Ren_Quan_Do","Kho_Khi_Gioi_Nho","Central_Supply_Store"};
        var objects=roots.Select(GameObject.Find).Where(g=>g!=null).Concat(new[]{stage.gameObject})
            .Select(g=>new{g.name,parent=g.transform.parent?.name,position=g.transform.position.ToString("F2"),
                rotation=g.transform.eulerAngles.ToString("F2"),scale=g.transform.lossyScale.ToString("F2"),
                bounds=BoundsOf(g).ToString()}).ToArray();
        var nearby=UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(a=>Vector3.Distance(a.transform.position,stage.position)<500)
            .Select(a=>new{a.name,position=a.transform.position.ToString("F2"),rotation=a.transform.eulerAngles.ToString("F2"),
                scale=a.transform.lossyScale.ToString("F2"),avatar=AssetDatabase.GetAssetPath(a.avatar),
                parent=a.transform.parent?.name,pose=a.GetComponent<NPCCampPose>()?.pose.ToString(),
                wander=a.GetComponent<NPCWander>()!=null}).ToArray();
        var landmarks=session.GetComponentsInChildren<Transform>(true)
            .Where(t=>Vector3.Distance(t.position,stage.position)<220).Select(t=>new{t.name,
                position=t.position.ToString("F2"),parent=t.parent?.name,types=t.GetComponents<Component>().Select(c=>c?.GetType().Name).ToArray()}).ToArray();
        var panels=GameObject.Find("Camp_Palisade_Reused").transform.Cast<Transform>()
            .Select(t=>new{t.name,position=t.position.ToString("F2"),rotation=t.eulerAngles.ToString("F2"),scale=t.localScale.ToString("F2")}).ToArray();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CampRedesignLayoutAudit.json",JsonConvert.SerializeObject(new{objects,nearby,landmarks,panels},Formatting.Indented));
        PoseAudit(session.storyCamera);
        EditorApplication.Exit(0);
    }

    public static void PoseAudit(Camera sourceCamera)
    {
        string basePath="Assets/Main_Character/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped/";
        string[] paths={"Assets/Animation/Meshy_AI_Character_output@Idle.fbx","Assets/Animation/Meshy_AI_Character_output@Walking (1).fbx",
            basePath+"Meshy_AI_Steppe_Ironclad_biped_Animation_walking_2_inplace_withSkin.fbx",
            basePath+"Meshy_AI_Steppe_Ironclad_biped_Animation_Casual_Walk_withSkin.fbx",
            "Assets/Model_NPC/OldSoldier/Meshy_AI_Dũng_the_Veteran_biped/Meshy_AI_Dũng_the_Veteran_biped_Animation_Running_withSkin.fbx",
            "Assets/BeachBoatStory/Camp/Camp_Balanced_Idle.anim","Assets/BeachBoatStory/Camp/Camp_Balanced_Walk.anim"};
        var source=GameObject.Find("Camp_Worker_01");
        var reports=new List<object>();
        var cameraGo=new GameObject("PoseAuditCamera");var camera=cameraGo.AddComponent<Camera>();camera.CopyFrom(sourceCamera);
        camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.25f,.28f);
        camera.fieldOfView=40;camera.farClipPlane=150;
        var rt=new RenderTexture(700,900,24);var pixels=new Texture2D(700,900,TextureFormat.RGB24,false);camera.targetTexture=rt;
        for(int index=0;index<paths.Length;index++)
        {
            var clip=AssetDatabase.LoadAllAssetsAtPath(paths[index]).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
            if(clip==null)continue;
            var actor=UnityEngine.Object.Instantiate(source);actor.name="PoseAuditActor";
            foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
            foreach(var t in actor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            actor.transform.SetPositionAndRotation(new Vector3(-50000,0,0),Quaternion.identity);
            var animator=actor.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController=null;animator.Rebind();animator.applyRootMotion=false;
            var graph=PlayableGraph.Create("CampPoseAudit");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable=AnimationClipPlayable.Create(graph,clip);
            AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(playable);graph.Play();
            float maximum=0,worstTime=0;
            var samples=new List<object>();
            for(int frame=0;frame<20;frame++)
            {
                float at=clip.length*frame/20f;playable.SetTime(at);graph.Evaluate(0);
                if(index>=5)actor.GetComponent<NPCUprightPosture>()?.Balance();
                var hips=actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
                var head=actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Head).position);
                var delta=head-hips;
                float lateral=Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg;
                float forward=Mathf.Atan2(delta.z,delta.y)*Mathf.Rad2Deg;
                if(Mathf.Abs(lateral)>maximum){maximum=Mathf.Abs(lateral);worstTime=at;}
                samples.Add(new{at,lateral,forward});
            }
            playable.SetTime(worstTime);graph.Evaluate(0);
            if(index>=5)actor.GetComponent<NPCUprightPosture>()?.Balance();
            cameraGo.transform.position=actor.transform.position+new Vector3(0,14, 58);
            cameraGo.transform.LookAt(actor.transform.position+Vector3.up*13);
            camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,700,900),0,0);pixels.Apply();
            File.WriteAllBytes("Logs/NPCPoseCandidate_"+index+".png",pixels.EncodeToPNG());
            if(index>=5)
            for(int frame=0;frame<4;frame++)
            {
                playable.SetTime(clip.length*frame/4f);graph.Evaluate(0);actor.GetComponent<NPCUprightPosture>()?.Balance();
                camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,700,900),0,0);pixels.Apply();
                File.WriteAllBytes("Logs/NPC_Arm_"+(index==5?"Idle":"Walk")+"_"+frame+".png",pixels.EncodeToPNG());
            }
            var curves=AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Animator))
                .Select(b=>new{b.propertyName,min=AnimationUtility.GetEditorCurve(clip,b).keys.Min(k=>k.value),
                    max=AnimationUtility.GetEditorCurve(clip,b).keys.Max(k=>k.value)}).ToArray();
            reports.Add(new{path=paths[index],clip=clip.name,maximum,samples,curves});
            graph.Destroy();UnityEngine.Object.DestroyImmediate(actor);
        }
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(cameraGo);
        File.WriteAllText("Logs/CampNPCPoseAudit.json",JsonConvert.SerializeObject(reports,Formatting.Indented));
    }
}
