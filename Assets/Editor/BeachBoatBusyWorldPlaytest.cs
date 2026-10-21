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
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BusyWorldPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,people=budget?.citizens.Length,results=Results},Formatting.Indented));
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
                if(budget==null||budget.citizens.Length<55)throw new Exception("Expected at least 55 extra daily-life NPCs");
                districts=budget.transform.Cast<Transform>().ToArray();
                camera=new GameObject("BusyWorldQACamera").AddComponent<Camera>();camera.CopyFrom(session.storyCamera);
                session.storyCamera.enabled=false;session.storyCamera.tag="Untagged";camera.tag="MainCamera";camera.enabled=true;camera.farClipPlane=1800;camera.fieldOfView=65;
                foreach(var terrain in Terrain.activeTerrains)terrain.drawTreesAndFoliage=true;
                var daylight=new GameObject("PopulationQALight").AddComponent<Light>();daylight.type=LightType.Directional;daylight.intensity=1.3f;daylight.transform.rotation=Quaternion.Euler(50,140,0);
            }
            if(phaseStarted==0)
            {
                var district=districts[phase];district.gameObject.SetActive(true);
                camera.transform.position=district.position+new Vector3(100,155,80);camera.transform.LookAt(district.position+Vector3.up*130);
                Initial.Clear();foreach(var w in district.GetComponentsInChildren<NPCWander>(true))Initial[w]=w.transform.position;
                budget.Refresh();phaseStarted=EditorApplication.timeSinceStartup;return;
            }
            if(EditorApplication.timeSinceStartup-phaseStarted<15)return;
            var current=districts[phase];int moved=Initial.Count(pair=>(pair.Key.transform.position-pair.Value).sqrMagnitude>9);
            var overlaps=new List<string>();
            foreach(var actor in Initial.Keys)
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
            if(moved<Mathf.Max(2,Initial.Count/2))throw new Exception("Too few NPCs moved in "+current.name+": "+moved+"/"+Initial.Count);
            if(budget.ActiveCount>28||overlaps.Count>0)throw new Exception("Population budget or collision failed in "+current.name);
            if(!talked)throw new Exception("Could not speak to a working NPC in "+current.name);
            phase++;phaseStarted=0;
            if(phase>=districts.Length)Finish(null);
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void Capture(string name,Transform district)
    {
        var worker=district.GetComponentsInChildren<NPCWander>().FirstOrDefault(w=>w.enabled);
        if(worker==null)return;
        var at=worker.transform.position;camera.transform.position=at+new Vector3(-125,180,-170);camera.transform.LookAt(at+new Vector3(20,0,30));
        camera.ResetWorldToCameraMatrix();camera.ResetProjectionMatrix();
        var rt=new RenderTexture(1280,800,24);var image=new Texture2D(1280,800,TextureFormat.RGB24,false);camera.targetTexture=rt;
        camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
    }
}
