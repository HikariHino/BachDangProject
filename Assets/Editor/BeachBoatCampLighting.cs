using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Authored warm path lighting for the opening camp, using the existing torch assets.</summary>
public static class BeachBoatCampLighting
{
    const string RootName="Camp_Path_Fireposts_938",Flag="CampLighting938QA";
    static readonly Vector3[] Views={new Vector3(0,0,-110),new Vector3(-160,0,145),new Vector3(0,0,310)};
    static double began;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> saveHashes=new Dictionary<string,string>();
    static string Hash(string path)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path)));
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    public static void Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying||scene.path!="Assets/Scenes/beachBoat.unity")throw new Exception("Open beachBoat in Edit Mode");
        var camp=GameObject.Find("TestQuest_Camp");var origin=GameObject.Find("CampLayoutOrigin").transform.position;
        if(camp.transform.Find(RootName)!=null)throw new Exception("Camp lighting already authored");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Props/prop_torch_01.fbx");
        var fire=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BeachBoatStory/Camp/Torch_Flame.prefab");
        if(source==null||fire==null)throw new Exception("Existing torch assets missing");
        var root=new GameObject(RootName).transform;root.SetParent(camp.transform,true);
        var offsets=new List<Vector3>();
        foreach(float z in new[]{-180f,-60f,60f,180f,300f,370f})foreach(float x in new[]{-34f,34f})offsets.Add(new Vector3(x,0,z));
        foreach(float z in new[]{-170f,-30f,150f,300f,430f})foreach(float x in new[]{-185f,185f})offsets.Add(new Vector3(x,0,z));
        offsets.Add(new Vector3(-275,0,130));offsets.Add(new Vector3(-275,0,190));
        offsets.Add(new Vector3(-65,0,455));offsets.Add(new Vector3(65,0,455));
        int index=0;
        foreach(var offset in offsets)
        {
            var requested=origin+offset;Vector3 p=default;bool clear=false;
            for(int option=0;option<17&&!clear;option++)
            {
                var shift=option==0?Vector3.zero:Quaternion.Euler(0,(option-1)%8*45,0)*Vector3.forward*(option<=8?12:24);
                p=requested+shift;p.y=Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y;
                clear=!Physics.OverlapBox(p+Vector3.up*15,new Vector3(3,14,3),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                    .Any(c=>!(c is TerrainCollider)&&c.GetComponentInParent<Animator>()==null&&!(c is MeshCollider&&c.bounds.size.y<2));
            }
            if(!clear)throw new Exception("No clear path shoulder for torch "+offset);
            var pole=(GameObject)PrefabUtility.InstantiatePrefab(source,root);pole.name="Camp_Firepost_"+(++index).ToString("00");
            pole.transform.SetPositionAndRotation(p,Quaternion.identity);
            var filter=pole.GetComponentInChildren<MeshFilter>();var mesh=filter.sharedMesh;
            var vertices=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
            float height=vertices.Max(v=>v.y)-vertices.Min(v=>v.y);pole.transform.localScale*=30/height;
            vertices=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
            pole.transform.position+=Vector3.up*(p.y-vertices.Min(v=>v.y));
            vertices=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();float top=vertices.Max(v=>v.y);
            var head=vertices.Where(v=>v.y>top-2.4f).ToArray();var tip=new Vector3(head.Average(v=>v.x),top,head.Average(v=>v.z));
            if(pole.GetComponentsInChildren<Collider>().Length==0)
            {
                var box=pole.AddComponent<BoxCollider>();var local=vertices.Select(pole.transform.InverseTransformPoint).ToArray();var b=new Bounds(local[0],Vector3.zero);foreach(var v in local)b.Encapsulate(v);
                box.center=b.center;box.size=b.size;
            }
            var effect=(GameObject)PrefabUtility.InstantiatePrefab(fire,pole.transform);effect.name="BeachBoat_Torch_Flame";
            effect.transform.SetPositionAndRotation(tip-Vector3.up*1.5f,Quaternion.identity);
            effect.transform.localScale=Vector3.one*(6/pole.transform.lossyScale.x);
            var torch=effect.GetComponent<TorchFlame>();torch.baseIntensity=750;torch.lightDistance=260;
            torch.fireLight.intensity=750;torch.fireLight.range=110;torch.fireLight.color=new Color(1,.64f,.3f);torch.fireLight.shadows=LightShadows.None;
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(torch);PrefabUtility.RecordPrefabInstancePropertyModifications(torch.fireLight);
            Physics.SyncTransforms();
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CampLightingBuild.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{added=index,origin=origin.ToString()},Newtonsoft.Json.Formatting.Indented));
    }
    public static void CaptureBaseline()=>CaptureViews("Before",false);
    static void CaptureViews(string suffix,bool test)
    {
        var origin=GameObject.Find("CampLayoutOrigin").transform.position;var cycle=Object.FindAnyObjectByType<DayNightCycle>();cycle.SetTimeOfDay(5.75f);
        var source=BeachBoatQuestSession.Instance!=null?BeachBoatQuestSession.Instance.storyCamera:Camera.main;
        var camera=new GameObject("Camp_Lighting_QA_Camera").AddComponent<Camera>();if(source!=null)camera.CopyFrom(source);camera.enabled=false;camera.fieldOfView=65;
        var torches=Object.FindObjectsByType<TorchFlame>();
        for(int i=0;i<Views.Length;i++)
        {
            var at=origin+Views[i];at.y=Terrain.activeTerrain.SampleHeight(at)+Terrain.activeTerrain.transform.position.y;
            camera.transform.position=at+new Vector3(0,22,-40);camera.transform.LookAt(at+new Vector3(0,12,75));
            TorchFlame.RefreshForCamera(camera);
            foreach(var t in torches){t.flame.Simulate(1.2f,true,true);if(t.embers!=null)t.embers.Simulate(1.2f,true,true);}
            var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/CampLighting_"+i+"_"+suffix+".png",pixels.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);
            if(test)Check(torches.Count(t=>t.fireLight.enabled)>0&&torches.Count(t=>t.fireLight.enabled)<=8,"Near camera light budget remains at most eight, view "+i);
        }
        camera.transform.position=origin+Vector3.up*5000;TorchFlame.RefreshForCamera(camera);
        if(test)Check(torches.All(t=>!t.fireLight.enabled&&!t.flame.isPlaying),"Distant camp lights and particles stop");
        Object.DestroyImmediate(camera.gameObject);
    }
    public static void RunInEditor(){began=0;checks.Clear();saveHashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        SessionState.SetBool(Flag,false); // Camera.Render can pump editor callbacks; never re-enter this capture.
        string error=null;
        try
        {
            foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))saveHashes[p]=Hash(p);
            BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/CampLightingQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
            BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
            var root=GameObject.Find("TestQuest_Camp").transform.Find(RootName);
            if(root==null)CaptureViews("Before",true);
            else
            {
            var torches=root.GetComponentsInChildren<TorchFlame>();
            Check(torches.Length==26,"Twenty-six additional fireposts cover the camp paths, tents and command approach");
            Check(torches.All(t=>t.fireLight.shadows==LightShadows.None&&t.fireLight.range==110&&t.baseIntensity==750),"New warm lights have the expected brightness and do not cast costly shadows");
            Check(torches.All(t=>t.flame.main.maxParticles<=4&&t.embers.main.maxParticles<=8),"Each firepost retains the small particle budget");
            Check(torches.All(t=>t.transform.parent.GetComponentsInChildren<Collider>().Any(c=>!c.isTrigger)),"Fireposts have solid poles");
            CaptureViews("After",true);
            }
            foreach(var p in saveHashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));
        }
        catch(Exception e){error=e.ToString();}
        File.WriteAllText("Logs/CampLightingPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,checks},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
}
