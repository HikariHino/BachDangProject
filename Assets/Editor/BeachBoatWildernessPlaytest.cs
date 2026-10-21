using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatWildernessPlaytest
{
    const string Flag="WildernessVisualQA";
    static Camera camera;
    static Terrain terrain;
    static Transform[] towns;
    static double started,viewStarted;
    static int view;
    static readonly List<object> checks=new List<object>();
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Finish(string failure)
    {
        File.WriteAllText("Logs/WildernessPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,terrainInstances=terrain?.terrainData.treeInstanceCount,
            allocatedMiB=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()/1048576f,checks},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Capture(string name)
    {
        var rt=new RenderTexture(1280,800,24);var png=new Texture2D(1280,800,TextureFormat.RGB24,false);camera.targetTexture=rt;
        camera.Render();RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,1280,800),0,0);png.Apply();
        var colors=png.GetPixels32();int pink=colors.Count(p=>p.r>200&&p.b>200&&p.g<60);if(pink>colors.Length*.02f)throw new Exception("Missing shader in "+name);
        File.WriteAllBytes("Logs/"+name+".png",png.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(png);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<4)return;
        try
        {
            foreach(var intro in Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();BeachBoatStory.StoryDialogueUI.Instance?.Close();
            if(camera==null)
            {
                terrain=Terrain.activeTerrain;camera=BeachBoatQuestSession.Instance.storyCamera;
                var orbit=camera.GetComponent<BeachBoatStory.Camera_Script>();if(orbit!=null)orbit.enabled=false;
                foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
                towns=GameObject.Find(BeachBoatWholeMapExpansion.RootName).transform.Cast<Transform>().Where(t=>!t.name.StartsWith("Thac_")).ToArray();
                var light=new GameObject("Wilderness_QA_Daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(50,130,0);RenderSettings.sun=light;
                var report=BeachBoatWildernessDensity.ReadReport();int original=(int)report["original"],firstShrub=(int)report["firstShrub"];
                var added=terrain.terrainData.treeInstances.Skip(original).ToArray();
                if(added.Length<40000||terrain.terrainData.treeInstanceCount!=(int)report["total"])throw new Exception("Terrain vegetation did not persist");
                foreach(var prototype in terrain.terrainData.treePrototypes.Skip(firstShrub))
                foreach(var material in prototype.prefab.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r=>r.sharedMaterials))
                    if(material==null||material.shader.name!="Universal Render Pipeline/Lit"||material.GetTexture("_BaseMap")==null)throw new Exception("Shrub has no supported textured material");
                int flooded=added.Count(t=>terrain.transform.position.y+t.position.y*terrain.terrainData.size.y<TideSystem.Instance.highTideY+6.9f);
                if(flooded>0)throw new Exception("New vegetation entered the river: "+flooded);
            }
            int[] indices={0,3,8,15,22,30,38,45};
            var town=towns[Mathf.Min(towns.Length-1,indices[Mathf.Min(view,indices.Length-1)])];
            if(viewStarted==0)
            {
                var at=town.position+new Vector3(-350,0,-650);at.y=terrain.SampleHeight(at)+terrain.transform.position.y;
                camera.transform.position=at+new Vector3(-250,450,-400);camera.transform.LookAt(town.position+Vector3.up*15);camera.farClipPlane=9500;viewStarted=EditorApplication.timeSinceStartup;return;
            }
            if(EditorApplication.timeSinceStartup-viewStarted<2)return;
            var focus=town.position+new Vector3(-350,0,-650);var reportData=BeachBoatWildernessDensity.ReadReport();int oldCount=(int)reportData["original"];
            int nearby=terrain.terrainData.treeInstances.Skip(oldCount).Count(t=>QuestRowboat.FlatDistance(terrain.transform.position+Vector3.Scale(t.position,terrain.terrainData.size),focus)<500);
            int total=terrain.terrainData.treeInstances.Count(t=>QuestRowboat.FlatDistance(terrain.transform.position+Vector3.Scale(t.position,terrain.terrainData.size),focus)<500);
            int drySamples=0;for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)if(terrain.SampleHeight(focus+new Vector3(x*140,0,z*140))+terrain.transform.position.y>TideSystem.Instance.highTideY+7)drySamples++;
            if(drySamples>12&&total<20)throw new Exception("Empty ground still lacks coverage near "+town.name+": "+total);
            Capture("Wilderness_Wide_"+town.name);checks.Add(new{town=town.name,newVegetationWithin500=nearby,totalWithin500=total,drySamples});view++;viewStarted=0;
            if(view==indices.Length)Finish(null);
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
