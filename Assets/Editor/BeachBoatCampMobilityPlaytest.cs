using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatCampMobilityPlaytest
{
    const string Flag="CampMobility938QA";
    static double started,until;
    static bool running;
    static string runtimeError;
    static readonly List<string> checks=new List<string>();
    static int lastFrame=-1,movingSamples;
    static float simulationStart;
    static float nextSample;
    static readonly Dictionary<NPCWander,Vector3> starts=new Dictionary<NPCWander,Vector3>();
    static object[] initial;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    public static void RunInEditor()
    {
        if(EditorSceneManager.GetActiveScene().path!="Assets/Scenes/beachBoat.unity")throw new InvalidOperationException("Open beachBoat first.");
        SessionState.SetBool(Flag+"KeepEditor",true);SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string m,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Exception||type==LogType.Error))runtimeError=m+"\n"+stack;}
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    static void CheckDetour()
    {
        var w=GameObject.Find("Camp_Guard_Front_Left").GetComponent<NPCWander>();
        var nav=new NPCWalkNavigation(w,Terrain.activeTerrain,BachDangWorldScale.ForScene(w.gameObject.scene),false);
        var start=w.transform.position;var destination=start+Vector3.forward*60;
        var wall=new GameObject("QA_Solid_Walking_Obstacle");var box=wall.AddComponent<BoxCollider>();
        wall.transform.position=start+new Vector3(0,14,25);box.size=new Vector3(18,28,5);Physics.SyncTransforms();
        try
        {
            Check(!nav.Clear(start,destination,false),"A solid prop blocks direct walking");
            var route=new List<Vector3>();Check(nav.Route(start,destination,route)&&route.Count>1,"Guard plans a real detour around a solid prop");
            var previous=start;foreach(var point in route){Check(nav.Clear(previous,point,false),"Every detour segment clears physical geometry");previous=point;}
            Check(route.Any(p=>Mathf.Abs(p.x-start.x)>box.size.x*.5f),"Detour passes beside the prop instead of through it");
            Check(nav.FindSpawn(wall.transform.position-Vector3.up*14,out var safe)&&!nav.Occupied(safe,false),"An actor authored inside a prop receives a clear initial spawn");
        }
        finally{Object.DestroyImmediate(wall);Physics.SyncTransforms();}
    }
    static void CheckRunningAudio()
    {
        float listener=AudioListener.volume;AudioListener.volume=0;
        try
        {
        var audio=BeachBoatSoundscape.Instance;
        var clips=Resources.LoadAll<AudioClip>("BachDang938/RunSteps");
        Check(clips.Length==6,"Six recorded left/right running contacts replace generated impacts");
        for(int i=0;i<12;i++)
        {
            var previous=audio.LastFootstep;audio.PlayFootstep(true);
            Check(audio.LastFootstep!=previous,"Successive running contacts use different recordings");
        }
        audio.PlayFootstep(false);Check(!clips.Contains(audio.LastFootstep),"Walking retains its own separate sound");
        }
        finally{AudioListener.volume=listener;}
    }
    static void Capture(string name)
    {
        var camera=new GameObject("QA_Camp_Mobility_Camera").AddComponent<Camera>();
        camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.fieldOfView=60;
        camera.transform.position=new Vector3(-5510,175,1480);camera.transform.LookAt(new Vector3(-5510,165,1560));
        var rt=new RenderTexture(1000,700,24);var pixels=new Texture2D(1000,700,TextureFormat.RGB24,false);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1000,700),0,0);pixels.Apply();
        File.WriteAllBytes("Logs/"+name+".png",pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
        Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    static object Describe(NPCWander w)
    {
        float scale=BachDangWorldScale.ForScene(w.gameObject.scene);
        var bottom=w.transform.position+Vector3.up*scale*.65f;
        var top=w.transform.position+Vector3.up*(w.NominalStrideSpeed/.85f);
        var target=(Vector3)typeof(NPCWander).GetField("target",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w);
        var direction=target-w.transform.position;direction.y=0;
        var hits=Physics.CapsuleCastAll(bottom,top,scale*.4f,direction.normalized,scale,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        return new{w.name,enabled=w.isActiveAndEnabled,position=w.transform.position.ToString("F2"),target=target.ToString("F2"),w.CurrentSpeed,w.DistanceTravelled,w.CurrentActivity,w.NominalStrideSpeed,
            raised=typeof(NPCWander).GetField("raisedPatrol",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w),
            hits=hits.Select(h=>new{path=PathOf(h.transform),h.distance,point=h.point.ToString("F2"),normal=h.normal.ToString("F2"),size=h.collider.bounds.size.ToString("F2"),own=h.transform.IsChildOf(w.transform)||w.transform.IsChildOf(h.transform)}),
            waypoints=w.waypoints?.Where(p=>p!=null).Select(p=>new{p.name,position=p.position.ToString("F2")})};
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<4)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            if(!running)
            {
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance?.DismissForCheckpoint();
                foreach(var w in Object.FindObjectsByType<NPCWander>(FindObjectsSortMode.None).Where(w=>w.name.StartsWith("Camp_Guard_")))starts[w]=w.transform.position;
                Check(starts.Count==6&&starts.Keys.All(w=>w.isActiveAndEnabled),"All six command/courtyard guard walkers are active");
                CheckDetour();CheckRunningAudio();
                foreach(var w in starts.Keys)
                {
                    Check(w.GetComponents<Collider>().All(c=>c.isTrigger||!c.enabled),w.name+" no longer has a duplicate legacy body collider");
                    Check(w.GetComponent<ActorSolidBody>().SolidCollider.enabled,w.name+" has one active physical capsule");
                }
                // Reproduce two patrols meeting at the stair approach, rather than
                // checking each NPC only against scenery in isolation.
                var left=GameObject.Find("Camp_Guard_Front_Left").transform;
                GameObject.Find("Camp_Guard_Back_1").transform.position=left.position+Vector3.right*.5f;Physics.SyncTransforms();
                Capture("Camp_Guards_Patrol_Start");initial=starts.Keys.Select(Describe).ToArray();running=true;simulationStart=Time.time;until=EditorApplication.timeSinceStartup+75;return;
            }
            if(Time.frameCount!=lastFrame){lastFrame=Time.frameCount;movingSamples+=starts.Keys.Count(w=>w.CurrentSpeed>w.NominalStrideSpeed*.65f);}
            if(Time.time>=nextSample)
            {
                nextSample=Time.time+.5f;
                foreach(var w in starts.Keys)
                {
                    var nav=new NPCWalkNavigation(w,Terrain.activeTerrain,BachDangWorldScale.ForScene(w.gameObject.scene),w.name=="Camp_Guard_Back_2"||w.name=="Camp_Guard_Back_3");
                    if(nav.Occupied(w.transform.position,false))throw new Exception("Guard intersects solid scenery: "+w.name);
                    if(!nav.Project(w.transform.position,out _))throw new Exception("Guard left the supported walking surface: "+w.name);
                }
            }
            if(Time.time-simulationStart<24&&EditorApplication.timeSinceStartup<until)return;
            Check(Time.time-simulationStart>=24,"Observed guards through 24 seconds of actual gameplay");
            foreach(var w in starts.Keys)Check(w.DistanceTravelled>20,w.name+" patrols instead of remaining stuck");
            var a=GameObject.Find("Camp_Guard_Front_Left").GetComponent<ActorSolidBody>().SolidCollider;
            var b=GameObject.Find("Camp_Guard_Back_1").GetComponent<ActorSolidBody>().SolidCollider;
            Check(!Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out var depth)||depth<.2f,"Meeting guards separate instead of standing inside each other");
            foreach(var w in starts.Keys.Where(w=>w.name!="Camp_Guard_Back_2"&&w.name!="Camp_Guard_Back_3"))
            {
                var bottom=w.GetComponent<ActorSolidBody>().SolidCollider.bounds.min.y;
                float floor=Terrain.activeTerrain.SampleHeight(w.transform.position)+Terrain.activeTerrain.transform.position.y;
                Check(Mathf.Abs(bottom-floor)<.5f,w.name+" stands at ground level without hovering");
            }
            Check(movingSamples>100,"Guard movement sustains normal walking speed");
            Capture("Camp_Guards_Patrol_End");
            Finish(null);
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void Finish(string error)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CampMobilityPlaytest.json",JsonConvert.SerializeObject(new{success=error==null,error,checks,movingSamples,seconds=Time.time-simulationStart,initial,
            final=starts.Keys.Select(Describe),excursions=starts.Select(p=>new{p.Key.name,distance=QuestRowboat.FlatDistance(p.Key.transform.position,p.Value)})},Formatting.Indented));
        SessionState.SetBool(Flag,false);
        if(SessionState.GetBool(Flag+"KeepEditor",false)){SessionState.SetBool(Flag+"KeepEditor",false);EditorApplication.isPlaying=false;}
        else BeachBoatQAExit.AfterPlayMode(error==null?0:1);
    }
}
