using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachBoatBusyWorldPlaytest
{
    const string Flag="BusyWorldPlaytest";
    static double started,phaseStarted;
    static int phase;
    static Transform[] districts;
    static AmbientPopulationBudget budget;
    static readonly Dictionary<NPCWander,Vector3> Initial=new Dictionary<NPCWander,Vector3>();
    static readonly List<object> Results=new List<object>();
    static Camera camera;
    static readonly Dictionary<VillageChicken,Vector3> AnimalStart=new Dictionary<VillageChicken,Vector3>();
    static int animalsMoved;
    const string VisualFlag="DenseWorldVisualOnly";
    const string AnimalFlag="DenseWorldAnimalOnly";
    static Renderer[] environment;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    public static void CaptureAndExit(){SessionState.SetBool(VisualFlag,true);RunAndExit();}
    public static void CaptureAnimalsAndExit(){SessionState.SetBool(AnimalFlag,true);RunAndExit();}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BusyWorldPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,people=budget?.citizens.Length,animals=AnimalStart.Count,animalsMoved,maximumAnimated=budget?.maximumAnimated,allocatedMiB=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()/1048576f,results=Results},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<3)return;
        try
        {
            var session=BeachBoatQuestSession.Instance;
            foreach(var opening in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))opening.SkipOpening();
            BeachBoatStory.StoryDialogueUI.Instance?.Close();
            if(budget==null)
            {
                budget=UnityEngine.Object.FindFirstObjectByType<AmbientPopulationBudget>();
                if(budget==null||budget.citizens.Length<160)throw new Exception("Expected at least 160 daily-life NPCs");
                districts=budget.transform.Cast<Transform>().ToArray();
                camera=new GameObject("BusyWorldQACamera").AddComponent<Camera>();camera.CopyFrom(session.storyCamera);
                environment=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.GetComponentInParent<Animator>()==null&&r.GetComponent<TMPro.TMP_Text>()==null).Cast<Renderer>().ToArray();
                foreach(var animal in UnityEngine.Object.FindObjectsByType<VillageChicken>(FindObjectsInactive.Include,FindObjectsSortMode.None))AnimalStart[animal]=animal.transform.position;
                session.storyCamera.enabled=false;session.storyCamera.tag="Untagged";camera.tag="MainCamera";camera.enabled=true;camera.farClipPlane=1800;camera.fieldOfView=65;
                foreach(var terrain in Terrain.activeTerrains)terrain.drawTreesAndFoliage=true;
                var daylight=new GameObject("PopulationQALight").AddComponent<Light>();daylight.type=LightType.Directional;daylight.intensity=1.3f;daylight.transform.rotation=Quaternion.Euler(50,140,0);
            }
            if(SessionState.GetBool(AnimalFlag,false))
            {
                var animal=AnimalStart.Keys.First(a=>a!=null);animal.transform.parent.parent.gameObject.SetActive(true);
                camera.transform.position=animal.transform.position+new Vector3(-10,6,-16);camera.transform.LookAt(animal.transform.position+Vector3.up*2);
                if(phaseStarted==0){phaseStarted=EditorApplication.timeSinceStartup;return;}
                if(EditorApplication.timeSinceStartup-phaseStarted<8)return;
                var flock=animal.transform.parent.GetComponentsInChildren<VillageChicken>();
                int animalsInMotion=flock.Count(a=>(a.transform.position-AnimalStart[a]).sqrMagnitude>.2f);
                if(animalsInMotion<2||flock.Any(a=>a.leftLeg==null||a.rightLeg==null||a.head.GetComponent<Renderer>().sharedMaterials.Length!=4))throw new Exception("Animal movement/model verification failed");
                SaveAnimalImage();File.WriteAllText("Logs/DenseAnimalCheck.json",JsonConvert.SerializeObject(new{success=true,moved=animalsInMotion,flock=flock.Length},Formatting.Indented));
                SessionState.SetBool(Flag,false);SessionState.SetBool(AnimalFlag,false);BeachBoatQAExit.AfterPlayMode(0);return;
            }
            if(phaseStarted==0)
            {
                var district=districts[phase];district.gameObject.SetActive(true);
                var center=district.position;center.y=Terrain.activeTerrain.SampleHeight(center)+Terrain.activeTerrain.transform.position.y;
                camera.transform.position=center+new Vector3(100,70,80);camera.transform.LookAt(center+Vector3.up*15);
                Initial.Clear();foreach(var w in district.GetComponentsInChildren<NPCWander>(true))Initial[w]=w.transform.position;
                budget.Refresh();phaseStarted=EditorApplication.timeSinceStartup;return;
            }
            if(EditorApplication.timeSinceStartup-phaseStarted<(SessionState.GetBool(VisualFlag,false)?1:10))return;
            if(SessionState.GetBool(VisualFlag,false))
            {
                Capture("BusyWorld_"+districts[phase].name,districts[phase]);phase++;phaseStarted=0;
                if(phase>=districts.Length)
                {
                    SaveAnimalImage();SessionState.SetBool(Flag,false);SessionState.SetBool(VisualFlag,false);BeachBoatQAExit.AfterPlayMode(0);
                }
                return;
            }
            var current=districts[phase];int moved=Initial.Count(pair=>(pair.Key.transform.position-pair.Value).sqrMagnitude>9);
            animalsMoved=AnimalStart.Count(pair=>(pair.Key.transform.position-pair.Value).sqrMagnitude>.25f);
            var overlaps=new List<string>();
            foreach(var actor in Initial.Keys.Where(a=>a.isActiveAndEnabled))
            {
                var p=actor.transform.position;
                foreach(var hit in Physics.OverlapCapsule(p+Vector3.up*6,p+Vector3.up*21,3,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    if(!(hit is TerrainCollider)&&hit.GetComponentInParent<Animator>()==null)overlaps.Add(actor.name+"/"+hit.name);
                foreach(var prop in current.Cast<Transform>().Where(t=>t.name.StartsWith("Workyard_")).SelectMany(t=>t.Cast<Transform>()))
                {
                    var bounds=BeachBoatWorldAudit.BoundsOf(prop);bounds.Expand(new Vector3(5,0,5));
                    if(p.x>bounds.min.x&&p.x<bounds.max.x&&p.z>bounds.min.z&&p.z<bounds.max.z)overlaps.Add(actor.name+"/decoration:"+prop.name);
                }
            }
            Capture("BusyWorld_"+current.name,current);
            var worker=Initial.Keys.First(w=>w.enabled);var visitor=worker.transform.position+worker.transform.forward*6;
            session.player.transform.position=visitor;var body=session.player.GetComponent<Rigidbody>();if(body!=null){body.position=visitor;body.linearVelocity=Vector3.zero;}
            Physics.SyncTransforms();
            bool talked=worker.GetComponent<NPCAmbientDialogue>().Talk()&&BeachBoatStory.StoryDialogueUI.Instance!=null&&BeachBoatStory.StoryDialogueUI.Instance.IsOpen;
            BeachBoatStory.StoryDialogueUI.Instance?.Close();
            Results.Add(new{district=current.name,people=Initial.Count,moved,animated=budget.ActiveCount,overlaps,talked});
            int nearby=Initial.Keys.Count(a=>a.isActiveAndEnabled);
            if(moved<Mathf.Max(2,nearby/2))throw new Exception("Too few nearby NPCs moved in "+current.name+": "+moved+"/"+nearby);
            if(budget.ActiveCount>budget.maximumAnimated||overlaps.Count>0)throw new Exception("Population budget or collision failed in "+current.name);
            if(budget.citizens.Any(a=>!a.enabled&&a.GetComponentsInChildren<Renderer>().Any(r=>r.enabled)))throw new Exception("An unanimated crowd actor remains visible");
            if(!talked)throw new Exception("Could not speak to a working NPC in "+current.name);
            phase++;phaseStarted=0;
            if(phase>=districts.Length){if(animalsMoved<12)throw new Exception("Too few yard animals moved");Finish(null);}
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void SaveAnimalImage()
    {
        var animal=AnimalStart.Keys.First(a=>a!=null);animal.transform.parent.parent.gameObject.SetActive(true);
        animal.GetComponent<LODGroup>().ForceLOD(0);
        camera.transform.position=animal.transform.position+new Vector3(-10,6,-16);camera.transform.LookAt(animal.transform.position+Vector3.up*2);
        SaveImage("Dense_Yard_Animals");
    }
    static void Capture(string name,Transform district)
    {
        var actors=district.GetComponentsInChildren<NPCWander>().Where(w=>w.enabled).ToArray();
        var worker=actors.OrderByDescending(w=>actors.Count(a=>(w.transform.position-a.transform.position).sqrMagnitude<180*180)).FirstOrDefault();
        if(worker==null)return;
        var at=worker.transform.position;var focus=at+Vector3.up*10;
        bool placed=false;
        foreach(float height in new[]{35f,65f,110f})
        {
            for(int angle=0;angle<12;angle++)
            {
                var position=at+Quaternion.Euler(0,angle*30,0)*new Vector3(-80,height,-110);
                var ray=new Ray(position,(focus-position).normalized);float distance=Vector3.Distance(position,focus);
                bool blocked=environment.Any(r=>r!=null&&r.gameObject.activeInHierarchy&&r.enabled&&r.bounds.IntersectRay(ray,out float hit)&&hit<distance-8);
                if(blocked)continue;camera.transform.position=position;placed=true;break;
            }
            if(placed)break;
        }
        if(!placed)camera.transform.position=at+new Vector3(0,170,-80);
        camera.transform.LookAt(focus);
        SaveImage("Dense_"+name);
    }
    static void SaveImage(string name)
    {
        camera.ResetWorldToCameraMatrix();camera.ResetProjectionMatrix();
        var rt=new RenderTexture(1280,800,24);var image=new Texture2D(1280,800,TextureFormat.RGB24,false);camera.targetTexture=rt;
        camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
    }
}
