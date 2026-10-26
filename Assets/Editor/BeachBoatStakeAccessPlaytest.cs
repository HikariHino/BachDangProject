using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Zone=BeachBoatStory.RiverStakeZone;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatStakeAccessPlaytest
{
    const string Flag="StakeAccess938QA";static double began;static int phase,lastFrame=-1;static string runtimeError;
    static readonly List<string> checks=new List<string>();
    public static void RunInEditor(){began=0;phase=0;lastFrame=-1;runtimeError=null;checks.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=text+"\n"+stack;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    static void Finish(string error){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/StakeAccessPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));SessionState.SetBool(Flag,false);EditorApplication.isPlaying=false;}
    static void Setup()
    {
        BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        Story.Instance.collectedWood=5;Zone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());Story.Instance.TransitionTo(Story.StoryState.Act2_CompleteReturnToCamp,true);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        foreach(var boat in RiverBoardingCombat.Instance.friendlyBoats)boat.gameObject.SetActive(false);RiverBoardingCombat.Instance.enabled=false;RiverPlayerCombat.Instance.enabled=false;
        var zone=Zone.Instance;var tide=TideSystem.Instance;
        Check(zone.RequiredCount==1246&&zone.IsComplete,"Preserve all 1,246 stakes and five saved work groups");
        Check(zone.placements.SelectMany(p=>p.stakeIndices).Distinct().Count()==1246,"Scatter keeps every original stake identity and cluster assignment");
        var bounds=zone.fieldStakes.Select(t=>BeachBoatWorldAudit.BoundsOf(t)).ToArray();
        Check(bounds.All(b=>BeachBoatStakeAccessRevision.DistanceToLane(b.center)>BeachBoatStakeAccessRevision.LaneHalfWidth+new Vector2(b.extents.x,b.extents.z).magnitude),"Entire service passage and dock branch are clear of actual timber footprints");
        Check(bounds.All(b=>b.max.y<tide.highTideY-1&&b.max.y>tide.lowTideY+5),"Scattered stakes stay hidden at flood and exposed at ebb");
        var added=zone.fieldStakes.Where(s=>s.name.StartsWith("CocGo_CuaSong_",StringComparison.Ordinal)).ToArray();
        Check(added.Select(t=>Mathf.RoundToInt(t.eulerAngles.y)).Distinct().Count()>40,"Stake directions vary instead of repeating an identical straight-row rotation");
        Check(added.Select(t=>Mathf.RoundToInt(BeachBoatWorldAudit.BoundsOf(t).center.z/4)).Distinct().Count()>100,"Stake positions spread through irregular bands instead of fifteen straight lines");
        foreach(float z in BeachBoatRiverMouthRevision.Belts)
        {
            float worst=0;
            for(float x=-5000;x<0;x+=30)
            {
                var p=new Vector3(x,0,z);if(Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y>=tide.lowTideY-3||BeachBoatStakeAccessRevision.DistanceToLane(p)<140)continue;
                worst=Mathf.Max(worst,bounds.Min(b=>QuestRowboat.FlatDistance(b.center,p)));
            }
            Check(worst<135,"Irregular stake belt still covers river width apart from the narrow access passage: "+z+" ("+worst.ToString("0")+")");
        }
    }
    static void Passage(float water)
    {
        var boat=QuestRowboat.Instance;var tide=TideSystem.Instance;tide.HoldTide(Zone.Instance,water>tide.lowTideY+1,true);Physics.SyncTransforms();
        var carrier=BoatHullCollision.Get(boat.transform);int poses=0;
        foreach(var path in new[]{BeachBoatStakeAccessRevision.ServiceLane,BeachBoatStakeAccessRevision.DockLane})
        {
            for(int i=1;i<path.Length;i++)
            {
                var from=path[i-1];var to=path[i];from.y=to.y=water;var rotation=Quaternion.LookRotation(to-from);int count=Mathf.CeilToInt(Vector3.Distance(from,to)/10);
                for(int n=0;n<=count;n++)
                {
                    var p=Vector3.Lerp(from,to,n/(float)count);
                    if(!boat.CanOccupy(p,rotation)||!carrier.CanMove(p,rotation,p+rotation*Vector3.forward*.1f,rotation,false))throw new Exception("Access blocked at water "+water+" near "+p+" by "+carrier.LastBlocker?.name);
                    poses++;
                }
            }
        }
        Check(poses>200,"Whole real hull can enter and return through both access paths at water "+water+" ("+poses+" sampled poses)");
        foreach(var turn in BeachBoatStakeAccessRevision.ServiceLane.Skip(1).Take(5).Concat(BeachBoatStakeAccessRevision.DockLane.Skip(1)))
        {
            var p=turn;p.y=water;
            for(int angle=0;angle<360;angle+=15)if(!boat.CanOccupy(p,Quaternion.Euler(0,angle,0)))throw new Exception("Boat cannot turn at "+p+" heading "+angle);
        }
        Check(true,"Player boat has turning room at access bends and dock at water "+water);
    }
    static void Dock()
    {
        var boat=QuestRowboat.Instance;TideSystem.Instance.HoldTide(Zone.Instance,false,true);
        RiverBoardingCombat.Instance.RestoreSave(new BachDangSaveData.Boarding{state=(int)RiverBoardingCombat.PlayerState.Rowboat,sword=false});
        var p=BeachBoatStakeAccessRevision.DockLane.Last();p.y=TideSystem.CurrentWaterHeight;boat.RestoreSave(true,p,Quaternion.Euler(0,-90,0));BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        Check(boat.AtDock&&boat.TryDisembark(),"Actual docking interaction returns the player to the reporting pier");
        Check(QuestRowboat.FlatDistance(BeachBoatQuestSession.Instance.player.transform.position,boat.boardingPoint.position)<1&&!boat.IsAboard,"Player stands on the authored pier after returning through the passage");
        Capture();
    }
    static void Capture()
    {
        var camera=new GameObject("QA natural stakes and access").AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.fieldOfView=65;camera.farClipPlane=10000;
        var rt=new RenderTexture(1400,900,24);var image=new Texture2D(1400,900,TextureFormat.RGB24,false);camera.targetTexture=rt;var old=RenderTexture.active;
        void Shot(string name,Vector3 from,Vector3 to){camera.transform.position=from;camera.transform.LookAt(to);camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,900),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
        Shot("NaturalStakeField_Overview",new Vector3(-2600,2200,350),new Vector3(-2400,70,2100));
        Shot("DockAccess_Clear",new Vector3(-3010,235,1730),new Vector3(-3505,80,1880));
        Shot("NaturalStakes_PlayerView",new Vector3(-2850,90,1130),new Vector3(-2250,82,1850));
        RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(began==0)began=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-began<5||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Stake access QA timed out");
            if(phase==0)Setup();else if(phase==1)Passage(TideSystem.Instance.lowTideY);else if(phase==2)Passage(TideSystem.Instance.highTideY);else if(phase==3){Dock();Finish(null);return;}phase++;
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
