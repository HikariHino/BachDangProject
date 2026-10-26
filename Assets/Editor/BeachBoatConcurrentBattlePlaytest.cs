using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
using StakeZone=BeachBoatStory.RiverStakeZone;

public static class BeachBoatConcurrentBattlePlaytest
{
    const string Flag="ConcurrentFleet938QA";
    static double started;
    static int phase;
    static float launched,ebbTime,ambushTime,highWater;
    static string runtimeError;
    static Vector3[] berths;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
    public static void RunInEditor(){phase=0;started=0;runtimeError=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string m,string stack,LogType t){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(t==LogType.Error||t==LogType.Exception))runtimeError=m+"\n"+stack;}
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    static void Capture()
    {
        var bounds=StakeZone.Instance.FieldBounds;var camera=new GameObject("QA_Concurrent_Fleet_Camera").AddComponent<Camera>();
        camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.fieldOfView=70;
        camera.transform.position=bounds.center+new Vector3(-850,850,-1000);camera.transform.LookAt(bounds.center+Vector3.up*30);
        var rt=new RenderTexture(1280,800,24);var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
        File.WriteAllBytes("Logs/Fleet_Concurrent_Exposed_Stakes.png",pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
        Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    static void Finish(string error)
    {
        var fleet=EnemyFleetDirector.Instance;Time.timeScale=1;
        File.WriteAllText("Logs/ConcurrentBattlePlaytest.json",JsonConvert.SerializeObject(new{success=error==null,error,phase,checks,launched,ebbTime,ambushTime,water=TideSystem.CurrentWaterHeight,exposed=StakeZone.Instance?.ExposedStakeCount,
            ships=fleet?.enemyShips.Select(s=>new{s.name,active=s.gameObject.activeSelf,s.isDestroyed,s.HasSettled,inside=s.IsHullInsideStakeField,p=s.transform.position.ToString(),goal=s.targetPoint.position.ToString(),blocker=s.GetComponent<BoatHullCollision>()?.LastBlocker?.name})},Formatting.Indented));
        SessionState.SetBool(Flag,false);BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
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
            if(EditorApplication.timeSinceStartup-started>120)throw new Exception("Concurrent battle timed out at phase "+phase);
            var fleet=EnemyFleetDirector.Instance;var zone=StakeZone.Instance;var story=Story.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(p.EndsWith(".json")||p.EndsWith(".bak"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/ConcurrentQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                zone.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.collectedWood=story.totalWoodRequired;
                story.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);
                berths=fleet.enemyShips.Select(s=>s.targetPoint.position).ToArray();
                // Reproduce the former deadlock: a hull can be deep inside the field
                // while more than 55 units away from its exact parking marker.
                var probe=fleet.enemyShips[0];var old=probe.transform.position;var rotation=probe.transform.rotation;
                probe.transform.SetPositionAndRotation(probe.targetPoint.position+Vector3.forward*100,Quaternion.Euler(0,180,0));Physics.SyncTransforms();
                Check(probe.IsHullInsideStakeField,"Full hull inside the stake field is recognized before the final 55-unit waypoint radius");
                probe.transform.SetPositionAndRotation(old,rotation);Physics.SyncTransforms();
                RiverPlayerCombat.Instance.ReturnFireEnabled=false;
                RiverBoardingCombat.Instance.enabled=false;fleet.StartBattle();fleet.GetComponent<RiverInvasionIntro>().Cancel();fleet.GetComponent<RiverBattleVoices>().enabled=false;highWater=TideSystem.CurrentWaterHeight;launched=Time.time;Time.timeScale=4;phase=1;return;
            }
            if(phase==1)
            {
                int active=fleet.enemyShips.Count(s=>s.gameObject.activeInHierarchy);if(active==0)return;
                Check(active==fleet.ShipCount,"All ten enemy vessels activate in the same fleet entry");
                Check(fleet.enemyShips.All(s=>Vector3.Angle(s.transform.forward,s.targetPoint.position-s.transform.position)<1),"Every ship starts facing into its own lane without a blocked 180-degree turn");
                Check(fleet.enemyShips.Select((s,i)=>Vector3.Distance(s.targetPoint.position,berths[i])<.01f).All(v=>v),"Existing dispersed enemy wreck berths stay unchanged");
                Check(!fleet.BattleCaption.Contains("Đợt"),"Fleet entry no longer announces timed waves");phase=2;
            }
            if(phase==2)
            {
                if(!fleet.EbbStarted)return;ebbTime=Time.time;
                Check(fleet.ShipsInside==fleet.ShipCount,"The complete physical formation enters deeply before the ebb begins");
                Check(TideSystem.Instance.IsControlledEbb&&TideSystem.Instance.TargetWaterHeight==TideSystem.Instance.lowTideY,"Entire fleet inside actually starts controlled falling water");phase=3;
            }
            if(phase==3)
            {
                if(!fleet.AmbushStarted)return;ambushTime=Time.time;
                Check(TideSystem.CurrentWaterHeight<highWater-5&&zone.ExposedStakeCount>0,"Falling water visibly exposes the planted stakes");
                Check(fleet.enemyShips.All(s=>s.isDestroyed),"The whole fleet is caught and starts sinking in one ambush event");phase=4;
            }
            if(phase==4)
            {
                if(!fleet.AllShipsSunk)return;
                Check(fleet.enemyShips.All(s=>s.gameObject.activeInHierarchy&&s.HasSolidWreck),"All ten physical wrecks are retained after sinking");
                Check(zone.ExposedStakeCount>0,"Stakes remain exposed after the fleet settles");Capture();
                foreach(var guard in RiverPlayerCombat.Instance.guards)guard.DefeatFromVolley();phase=5;return;
            }
            if(phase==5)
            {
                if(fleet.BattleStarted)return;
                Check(RiverBoardingCombat.Instance.friendlyBoats.All(b=>b.IsReturningToHarbor||b.HasReturnedToHarbor),"Allied ships receive their withdrawal order after victory");
                foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
