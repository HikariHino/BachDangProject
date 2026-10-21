using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Measure torso alignment through idle and walking, with actual Play Mode skin renders.</summary>
public static class BeachBoatNPCVisualPlaytest
{
    const string Flag="NPCVisualPlaytest";
    static double started,posed;
    static int view,samples;
    static float largestLean,largestShoulder;
    static Animator actor;
    static Camera camera;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<3)return;
        try
        {
            if(actor==null)
            {
                foreach(var opening in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))opening.SkipOpening();
                BeachBoatStory.StoryDialogueUI.Instance?.Close();
                actor=GameObject.Find("Camp_Guard_Mid_Right").GetComponent<Animator>();
                foreach(var t in actor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                var wander=actor.GetComponent<NPCWander>();wander.enabled=false;actor.speed=1;actor.SetBool("isWalking",false);actor.Play("Idle",0,0);actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                actor.transform.rotation=Quaternion.identity;
                var source=BeachBoatQuestSession.Instance.storyCamera;
                camera=new GameObject("NPCVisualQACamera").AddComponent<Camera>();camera.CopyFrom(source);source.enabled=false;camera.enabled=true;camera.fieldOfView=35;camera.farClipPlane=250;
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.25f,.28f);
                Place(new Vector3(0,16,60));
                var daylight=new GameObject("NPCQALight").AddComponent<Light>();daylight.type=LightType.Directional;daylight.cullingMask=1<<31;daylight.intensity=1.5f;daylight.transform.rotation=Quaternion.Euler(40,180,0);
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
                posed=EditorApplication.timeSinceStartup;return;
            }
            var axis=actor.transform.InverseTransformDirection(actor.GetBoneTransform(HumanBodyBones.Head).position-actor.GetBoneTransform(HumanBodyBones.Hips).position);
            largestLean=Mathf.Max(largestLean,Mathf.Abs(Mathf.Atan2(axis.x,axis.y)*Mathf.Rad2Deg));
            var left=actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;var right=actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
            largestShoulder=Mathf.Max(largestShoulder,Mathf.Abs(actor.transform.InverseTransformDirection(right-left).y));samples++;
            if(EditorApplication.timeSinceStartup-posed<3)return;
            Shot(new[]{"NPC_Shoulders_Front","NPC_Shoulders_Side","NPC_Walking_Front","NPC_Walking_Side"}[view]);
            if(++view<4)
            {
                if(view==2)actor.SetBool("isWalking",true);
                Place(view%2==0?new Vector3(0,16,60):new Vector3(32,16,48));posed=EditorApplication.timeSinceStartup;return;
            }
            bool success=largestShoulder<.012f&&largestLean<2&&samples>60;
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/NPCVisualPlaytest.json",JsonConvert.SerializeObject(new{success,samples,largestLean,largestShoulder,
                bones=new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Neck,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm}
                    .Select(b=>new{bone=b.ToString(),position=actor.transform.InverseTransformPoint(actor.GetBoneTransform(b).position).ToString("F4")}).ToArray()},Formatting.Indented));
            SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(success?0:1);
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(1);}
    }
    static void Place(Vector3 offset)
    {
        camera.transform.position=actor.transform.position+offset;camera.transform.LookAt(actor.transform.position+Vector3.up*14);camera.ResetWorldToCameraMatrix();camera.ResetProjectionMatrix();
    }
    static void Shot(string name)
    {
        Directory.CreateDirectory("Logs");
        var rt=new RenderTexture(800,1000,24);var pixels=new Texture2D(800,1000,TextureFormat.RGB24,false);camera.targetTexture=rt;
        camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,800,1000),0,0);pixels.Apply();File.WriteAllBytes("Logs/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
    }
}

