using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Verify visible particle playback, valid URP materials and near/far light budgets.</summary>
public static class BeachBoatTorchPlaytest
{
    const string Flag="BeachBoatTorchPlaytest";
    static double started,viewStarted;
    static Camera camera;
    static TorchFlame target;
    public static void RunAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<3)return;
        try
        {
            var all=UnityEngine.Object.FindObjectsByType<TorchFlame>(FindObjectsSortMode.None);
            if(all.Length==0)throw new InvalidOperationException("No torches configured");
            if(camera==null)
            {
                foreach(var opening in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))opening.SkipOpening();
                BeachBoatStory.StoryDialogueUI.Instance?.Close();
                var session=BeachBoatQuestSession.Instance;var source=session.storyCamera;
                var origin=GameObject.Find("CampLayoutOrigin").transform.position;
                target=all.OrderBy(t=>(t.transform.position-origin).sqrMagnitude).First();
                source.enabled=false;source.tag="Untagged";
                camera=new GameObject("TorchQA_Camera").AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=true;camera.tag="MainCamera";
                camera.fieldOfView=48;camera.farClipPlane=700;
                camera.ResetWorldToCameraMatrix();camera.ResetProjectionMatrix();
                camera.transform.position=target.transform.position+new Vector3(10,3,8);
                camera.transform.LookAt(target.transform.position+Vector3.up*1.5f);
                TorchFlame.RefreshForCamera(camera);viewStarted=EditorApplication.timeSinceStartup;return;
            }
            if(EditorApplication.timeSinceStartup-viewStarted<2)return;
            int particles=target.flame.particleCount;
            int nearLights=all.Count(t=>t.fireLight.enabled);
            bool valid=all.All(t=>t.flame!=null&&t.embers!=null&&t.fireLight.shadows==LightShadows.None&&
                t.flame.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported&&t.flame.main.maxParticles<=4&&t.embers.main.maxParticles<=8);
            camera.transform.position=target.transform.position+new Vector3(10,3,8);
            camera.transform.LookAt(target.transform.position+Vector3.up*1.5f);
            camera.ResetWorldToCameraMatrix();camera.ResetProjectionMatrix();
            var nearCamera=camera.transform.position;
            var viewport=camera.WorldToViewportPoint(target.transform.position+Vector3.up*1.5f);
            Capture("BeachBoat_Torch_Flames");
            var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f));
            var blockers=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r=>!(r is ParticleSystemRenderer)&&r.bounds.IntersectRay(ray,out float distance)&&distance<viewport.z)
                .Select(r=>new{r.name,parent=r.transform.parent?.name,bounds=r.bounds.ToString()}).ToArray();
            target.transform.parent.GetComponent<Renderer>().forceRenderingOff=true;Capture("BeachBoat_Torch_Fire_Only");target.transform.parent.GetComponent<Renderer>().forceRenderingOff=false;
            camera.transform.position=target.transform.position+Vector3.up*5000;TorchFlame.RefreshForCamera(camera);
            bool farStopped=all.All(t=>!t.flame.isPlaying&&!t.embers.isPlaying&&!t.fireLight.enabled);
            bool success=valid&&particles>0&&nearLights>0&&nearLights<=8&&farStopped;
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BeachBoatTorchPlaytest.json",JsonConvert.SerializeObject(new{success,activeTorches=all.Length,
                totalTorches=UnityEngine.Object.FindObjectsByType<TorchFlame>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,
                particles,nearLights,valid,farStopped,position=target.transform.position.ToString("F2"),scale=target.transform.lossyScale.ToString("F2"),
                nearCamera=nearCamera.ToString("F2"),viewport=viewport.ToString("F2"),blockers,host=target.transform.parent.name,bounds=target.transform.parent.GetComponent<Renderer>().bounds.ToString()},Formatting.Indented));
            SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(success?0:1);
        }
        catch(Exception error)
        {
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BeachBoatTorchPlaytest.json",JsonConvert.SerializeObject(new{success=false,error=error.ToString()},Formatting.Indented));
            SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(1);
        }
    }
    static void Capture(string name)
    {
        var rt=new RenderTexture(1280,900,24);var image=new Texture2D(1280,900,TextureFormat.RGB24,false);camera.targetTexture=rt;
        camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,900),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
    }
}
