using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class BeachBoatSolidRiverPlaytest
{
    const string Flag="SolidRiver938QA";
    static double started;
    static string runtimeError;
    static readonly List<string> checks=new List<string>();
    static object[] movements;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    public static void RunInEditor(bool navigationOnly=false){SessionState.SetBool(Flag+"KeepEditor",true);SessionState.SetBool(Flag+"NavigationOnly",navigationOnly);SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string m,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Exception||type==LogType.Error))runtimeError=m+"\n"+stack;}
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    static void Finish(string error)
    {
        File.WriteAllText("Logs/SolidRiverPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,scope=SessionState.GetBool(Flag+"NavigationOnly",false)?"navigation":"full",checks,movements},Newtonsoft.Json.Formatting.Indented));SessionState.SetBool(Flag,false);if(SessionState.GetBool(Flag+"KeepEditor",false)){SessionState.SetBool(Flag+"KeepEditor",false);EditorApplication.isPlaying=false;}else BeachBoatQAExit.AfterPlayMode(error==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<4)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
            BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());
            TideSystem.Instance.SetTide(false,true);
            var boats=RiverBoardingCombat.Instance.friendlyBoats;
            foreach(var boat in boats)boat.enabled=false;
            foreach(var npc in Object.FindObjectsByType<NPCWander>(FindObjectsSortMode.None))npc.enabled=false;
            var starts=boats.Select(b=>b.transform.position).ToArray();
            var travel=new float[boats.Length];var excursion=new float[boats.Length];
            // An independent geometric check verifies the actual hull, not its route labels.
            for(int frame=0;frame<1600;frame++)
            {
                for(int index=0;index<boats.Length;index++)
                {
                    var boat=boats[index];var before=boat.transform.position;boat.Step(.05f);travel[index]+=QuestRowboat.FlatDistance(before,boat.transform.position);excursion[index]=Mathf.Max(excursion[index],QuestRowboat.FlatDistance(starts[index],boat.transform.position));var hull=BoatHullCollision.Get(boat.transform);var c=hull.SolidHull;
                    CheckNoStakeOverlap(boat,c);
                }
            }
            movements=boats.Select((b,i)=>(object)new{b.name,distance=QuestRowboat.FlatDistance(starts[i],b.transform.position),travel=travel[i],excursion=excursion[i],position=b.transform.position.ToString(),b.HasNavigableRoute,b.PlannedDetours,rejected=BoatHullCollision.Get(b.transform).RejectedMoves}).ToArray();
            for(int i=0;i<boats.Length;i++)Check(travel[i]>700&&excursion[i]>500,"Ship actually patrols a broad collision-safe route: "+boats[i].name);
            Check(true,"Every simulated pose of all ten hulls stays outside actual planted stake meshes");
            SweepFixtures();
            foreach(var terrain in Terrain.activeTerrains)
            {Check(new SerializedObject(terrain.GetComponent<TerrainCollider>()).FindProperty("m_EnableTreeColliders").boolValue,"Terrain uses solid tree contacts");Check(terrain.terrainData.treePrototypes.Where(p=>p.prefab!=null).All(p=>p.prefab.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled&&!c.isTrigger)),"Every terrain tree/rock prototype has a solid body");}
            var rowboat=QuestRowboat.Instance;var story=BeachBoatStory.BachDangStoryManager.Instance;story.collectedWood=story.totalWoodRequired;story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act3_WaitForLowTide,true);
            var player=BeachBoatQuestSession.Instance.player;player.transform.position=rowboat.boardingPoint.position;player.GetComponent<Rigidbody>().position=player.transform.position;
            Check(rowboat.TryBoard(),"Player can board a clear launch with full-hull checks");var from=rowboat.transform.position;for(int i=0;i<40;i++)rowboat.Step(1,0,.05f);Check(QuestRowboat.FlatDistance(from,rowboat.transform.position)>15,"Player can actually row away from the dock with solid bodies enabled");
            if(SessionState.GetBool(Flag+"NavigationOnly",false)){Finish(null);return;}
            var clips=Resources.LoadAll<AudioClip>("BachDang938/RunSteps");Check(clips.Length==6,"Six recorded running footfalls are bundled");
            var signatures=new HashSet<string>();
            foreach(var clip in clips)
            {
                var values=new float[clip.samples*clip.channels];Check(clip.GetData(values,0),"Running waveform decodes: "+clip.name);
                float rms=Mathf.Sqrt(values.Sum(v=>v*v)/values.Length),peak=values.Max(v=>Mathf.Abs(v));
                Check(rms>.055f&&rms<.12f&&peak<.8f,"Running impact is clearly audible without clipping: "+clip.name);
                signatures.Add(Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join(",",values.Take(1000).Select(v=>v.ToString("F5")))))));
            }
            Check(signatures.Count==8,"Footfalls contain eight distinct contact/friction variations");Finish(null);
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void CheckNoStakeOverlap(AmbientRiverBoat boat,BoxCollider box)
    {
        foreach(var stake in BeachBoatStory.RiverStakeZone.Instance.fieldStakes)
        {
            var renderer=stake.GetComponentInChildren<Renderer>(true);var p=box.transform.InverseTransformPoint(renderer.bounds.center)-box.center;
            float r=new Vector2(renderer.bounds.extents.x,renderer.bounds.extents.z).magnitude;
            var half=box.size*.5f;var scale=box.transform.lossyScale;
            if(Mathf.Abs(p.x)<half.x+r/scale.x&&Mathf.Abs(p.z)<half.z+r/scale.z)throw new Exception("Hull crossed stake: "+boat.name+" / "+stake.name);
        }
    }
    static void SweepFixtures()
    {
        var root=new GameObject("SweepFixture");root.transform.position=new Vector3(0,2000,0);
        var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="DaiVietPatrolVisual_Rebuilt";visual.transform.SetParent(root.transform,false);visual.transform.localScale=new Vector3(30,12,200);
        var hull=BoatHullCollision.Get(root.transform);var post=GameObject.CreatePrimitive(PrimitiveType.Cube);post.name="ThinSolidPost";post.transform.position=root.transform.position+new Vector3(0,2,150);post.transform.localScale=new Vector3(.5f,40,.5f);Physics.SyncTransforms();
        Check(!hull.CanMove(root.transform.position,Quaternion.identity,root.transform.position+Vector3.forward*300,Quaternion.identity,false),"A large step cannot tunnel completely through a thin solid post");
        post.transform.position=root.transform.position+new Vector3(75,2,60);Physics.SyncTransforms();
        Check(!hull.CanMove(root.transform.position,Quaternion.identity,root.transform.position,Quaternion.Euler(0,90,0),false),"An in-place turn cannot sweep the stern through a post between endpoints");
        post.transform.position=root.transform.position+Vector3.right*60;Physics.SyncTransforms();
        Check(!hull.CanMove(root.transform.position,Quaternion.identity,root.transform.position+Vector3.right*100,Quaternion.identity,false),"Solid obstacles block lateral yielding too");
        post.SetActive(false);Check(hull.CanMove(root.transform.position,Quaternion.identity,root.transform.position+Vector3.forward*5,Quaternion.identity,false),"Free water still permits movement");
        Object.DestroyImmediate(root);Object.DestroyImmediate(post);
    }
}
