using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class BeachBoatCinematicPlaytest
{
    const string Flag="Cinematic938QA",Key="BachDang938.Graphics";
    static int phase,lastFrame,oldChoice,captureIndex,captureFrame;static bool hadChoice;static double began,phaseBegan;static string runtimeError;
    static readonly List<string> checks=new List<string>();static readonly List<float> frames=new List<float>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    public static void RunBatch(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");RunInEditor();}
    public static void RunInEditor()
    {
        phase=0;captureIndex=0;lastFrame=-1;began=0;runtimeError=null;checks.Clear();frames.Clear();hashes.Clear();
        hadChoice=PlayerPrefs.HasKey(Key);oldChoice=PlayerPrefs.GetInt(Key,0);PlayerPrefs.SetInt(Key,2);
        foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
        BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/CinematicQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType t){if(SessionState.GetBool(Flag,false)&&(t==LogType.Error||t==LogType.Exception))runtimeError=m+"\n"+stack;}
    static void Capture(string name,Vector3 position,Vector3 target,float fov=58)
    {
        var camera=new GameObject("Cinematic_QA_"+name).AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.fieldOfView=fov;
        CinematicQuality.ConfigureCamera(camera);camera.transform.position=position;camera.transform.LookAt(target);TorchFlame.RefreshForCamera(camera);
        var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
        File.WriteAllBytes("Logs/Cinematic_Play_"+name+".png",pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
        Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    static void Finish(string error)
    {
        SessionState.SetBool(Flag,false);
        var ordered=frames.OrderBy(x=>x).ToArray();
        File.WriteAllText("Logs/CinematicPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks,frameSamples=frames.Count,
            editorBatchMedianFrameMs=ordered.Length==0?0:ordered[ordered.Length/2]*1000,hardware=SystemInfo.graphicsDeviceName},Newtonsoft.Json.Formatting.Indented));
        if(hadChoice)PlayerPrefs.SetInt(Key,oldChoice);else PlayerPrefs.DeleteKey(Key);
        BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Cinematic QA timeout");
            if(phase==0)
            {
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/CinematicQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangAutoSave.Instance.enabled=false;BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                var cycle=Object.FindAnyObjectByType<DayNightCycle>();cycle.autoAdvance=false;cycle.SetTimeOfDay(9);
                Object.FindAnyObjectByType<LaptopLongViewProfile>().Apply();Object.FindAnyObjectByType<BachDangWorldScale>().ApplyRenderScale();phase=1;phaseBegan=EditorApplication.timeSinceStartup;return;
            }
            if(phase==1)
            {
                if(EditorApplication.timeSinceStartup-phaseBegan<5)return;
                var lods=Object.FindObjectsByType<CinematicCharacterLOD>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                Check(lods.Length>=1100,"Authored village and camp characters retain their rigs and use the new materials/LOD");
                foreach(var group in lods.GroupBy(l=>l.closeMesh))
                {
                    var l=group.First();Check(l.middleMesh.GetIndexCount(0)<l.closeMesh.GetIndexCount(0)*.5f&&l.farMesh.GetIndexCount(0)<l.closeMesh.GetIndexCount(0)*.4f,"Crowd triangle budget, with open borders preserved: "+l.closeMesh.name);
                    Check(l.middleMesh.bindposes.Length==l.closeMesh.bindposes.Length&&l.farMesh.bindposes.Length==l.closeMesh.bindposes.Length,"Skin bindings preserved: "+l.closeMesh.name);
                    Check(l.target.sharedMaterials.All(m=>m!=null&&m.shader.isSupported&&m.GetTexture("_BumpMap")!=null&&m.GetTexture("_MetallicGlossMap")!=null),"Normal and physical surface maps: "+l.closeMesh.name);
                }
                Check(BeachBoatQuestSession.Instance.player.GetComponentInChildren<CinematicCharacterLOD>().important,"Player always retains the original full mesh");
                var terrain=Terrain.activeTerrain;Check(terrain.terrainData.terrainLayers.All(l=>l.normalMapTexture!=null&&l.maskMapTexture!=null),"All six ground layers have physical surface maps");
                Check(terrain.detailObjectDistance==100&&terrain.treeBillboardDistance==650,"High mode restores grass and longer detailed tree range");
                Check(QualitySettings.globalTextureMipmapLimit==0,"High mode retains full resolution textures");
                var high=(UniversalRenderPipelineAsset)QualitySettings.renderPipeline;Check(high.name.Contains("CinematicRP")&&high.shadowCascadeCount==4,"High mode uses the dedicated four-cascade renderer");
                PlayerPrefs.SetInt(Key,1);Object.FindAnyObjectByType<BachDangWorldScale>().ApplyRenderScale();Object.FindAnyObjectByType<LaptopLongViewProfile>().Apply();
                Check(!QualitySettings.renderPipeline.name.Contains("CinematicRP")&&terrain.detailObjectDistance==0&&QualitySettings.globalTextureMipmapLimit==1,"Balanced mode switches back to the lighter renderer and detail/texture budget");
                PlayerPrefs.SetInt(Key,2);Object.FindAnyObjectByType<BachDangWorldScale>().ApplyRenderScale();Object.FindAnyObjectByType<LaptopLongViewProfile>().Apply();
                Check(CinematicQuality.High&&terrain.detailObjectDistance==100,"High mode can be restored without restarting");
                var scanned=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.name.StartsWith("Scanned_")).ToArray();
                Check(scanned.Length>=50&&scanned.All(r=>r.sharedMaterial.shader.isSupported),"Scanned rocks, baskets and stumps render with supported materials");
                Check(scanned.All(r=>r.bounds.size.magnitude<180),"Scanned replacements preserve sensible world scale under scaled parents");
                Check(scanned.Where(r=>r.name.Contains("wicker_basket")).All(r=>Mathf.Abs(r.bounds.min.y-r.transform.parent.GetComponent<MeshRenderer>().bounds.min.y)<.03f),"Replacement baskets rest at the original prop base");
                Check(scanned.Where(r=>r.name.Contains("tree_stump")).All(r=>Mathf.Abs(r.bounds.min.y-(terrain.SampleHeight(r.bounds.center)+terrain.transform.position.y))<.03f),"Scanned stumps rest on the terrain");
                var saved=BachDangSaveSystem.Capture("Cinematic QA");BachDangSaveSystem.Apply(saved);
                Check(BeachBoatQuestSession.Instance.player.GetComponentInChildren<CinematicCharacterLOD>().target.sharedMaterial.GetTexture("_BumpMap")!=null,"Loading a campaign preserves the new player surface");
                Object.FindAnyObjectByType<BachDangBattleTimeline>().enabled=false;
                BeachBoatQuestSession.Instance.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;
                phase=2;phaseBegan=EditorApplication.timeSinceStartup;
                return;
            }
            if(phase==2)
            {
                var origin=GameObject.Find("CampLayoutOrigin").transform.position;var ngo=GameObject.Find("NgoQuyen_QuestGiver").transform;
                var stump=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).First(r=>r.name=="Scanned_tree_stump_01");var b=stump.bounds;
                var camera=BeachBoatQuestSession.Instance.storyCamera;Vector3 p,target;
                if(captureIndex==0){p=ngo.position+ngo.forward*34+Vector3.up*14;target=ngo.position+Vector3.up*12;}
                else if(captureIndex==1||captureIndex==2){p=origin+new Vector3(0,23,-115);target=origin+new Vector3(0,14,100);}
                else if(captureIndex==3){p=b.center+new Vector3(25,16,-30);target=b.center;}
                else {p=ngo.position+ngo.forward*(captureIndex==4?200:300)+Vector3.up*(captureIndex==4?100:240);target=ngo.position+Vector3.up*12;}
                Object.FindAnyObjectByType<DayNightCycle>().SetTimeOfDay(captureIndex==2?5.75f:9);
                camera.transform.position=p;camera.transform.LookAt(target);CinematicCharacterLOD.RefreshForCamera(camera);
                captureFrame=Time.frameCount;phase=3;return;
            }
            if(phase==3)
            {
                if(Time.frameCount<captureFrame+3)return;
                // A mesh layout change needs a fresh skinning frame. Never swap LOD
                // meshes between several Camera.Render calls in the same frame.
                phase=4;var camera=BeachBoatQuestSession.Instance.storyCamera;
                Capture(new[]{"Commander","CampDay","CampDawn","ScannedGround","CommanderMiddleLOD","CommanderFarLOD"}[captureIndex],camera.transform.position,camera.transform.position+camera.transform.forward*100,captureIndex<4?58:captureIndex==4?12:6);
                captureIndex++;phase=captureIndex<6?2:4;return;
            }
            if(phase==4)
            {
                if(frames.Count<120){frames.Add(Time.unscaledDeltaTime);return;}
                foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));
                Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
