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

public static class BeachBoatRiverMouthPlaytest
{
    const string Flag="RiverMouth938QA";
    static readonly List<string> checks=new List<string>();
    static double began;static int phase,steps,lastFrame=-1;static string runtimeError;
    static AmbientRiverBoat[] boats;static Vector3[] starts;static float[] distance;
    public static void RunInEditor(){checks.Clear();began=0;phase=steps=0;lastFrame=-1;runtimeError=null;SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string message,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=message+"\n"+stack;}
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    static void Finish(string error)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RiverMouthPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,steps,checks,boats=boats?.Select((b,i)=>new{b.name,b.lightPatrol,travelled=distance?[i]??0,displacement=starts!=null?QuestRowboat.FlatDistance(b.transform.position,starts[i]):0,b.HasNavigableRoute,b.PlannedDetours})},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);EditorApplication.isPlaying=false;
    }
    static void Setup()
    {
        BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        Story.Instance.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver,true);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        var zone=Zone.Instance;var terrain=Terrain.activeTerrain;var tide=TideSystem.Instance;tide.HoldHighTide(zone,true);
        Check(zone.ClusterCount==5&&zone.RequiredCount>1000,"Five quest work points now build the entire expanded field");
        var indices=zone.placements.SelectMany(p=>p.stakeIndices).ToArray();Check(indices.Length==zone.RequiredCount&&indices.Distinct().Count()==zone.RequiredCount,"Every stake belongs to exactly one saved quest cluster");
        var bounds=zone.fieldStakes.Select(s=>BeachBoatWorldAudit.BoundsOf(s)).ToArray();
        Check(bounds.All(b=>b.max.y<tide.highTideY-1&&b.max.y>tide.lowTideY+5),"All added stakes remain concealed at flood and emerge at ebb");
        foreach(float z in BeachBoatRiverMouthRevision.Belts)
        {
            float worst=0;int wet=0;
            for(float x=-5000;x<=0;x+=22)
            {
                var p=new Vector3(x,0,z);if(terrain.SampleHeight(p)+terrain.transform.position.y>=tide.lowTideY-2)continue;
                wet++;float gap=bounds.Where(b=>Mathf.Abs(b.center.z-z)<65).Min(b=>Mathf.Abs(b.center.x-x));worst=Mathf.Max(worst,gap);
            }
            Check(wet>80&&worst<100,"Bank-to-bank coverage, without an empty left channel, at river section "+z+" (largest gap to a stake "+worst.ToString("0")+")");
        }
        var partial=new[]{true,false,true,false,true};zone.RestoreCheckpoint(partial);Check(zone.CaptureCheckpoint().SequenceEqual(partial),"An older five-cluster save restores the same completed work points");
        zone.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());Check(zone.PlantedCount==zone.RequiredCount&&zone.IsComplete,"Old fully planted saves activate every newly added stake");
        foreach(var approach in zone.placements)Check(zone.HasBoatClearance(approach.approach.position),"Quest planting approach retains hull clearance: "+approach.approach.name);
        boats=RiverBoardingCombat.Instance.friendlyBoats;Check(boats.Length==10&&boats.Count(b=>b.lightPatrol)==8,"Keep ten allied vessels but use eight small patrol boats and only two warships");
        var row=QuestRowboat.Instance;var reference=BeachBoatWorldAudit.BoundsOf(row.transform.Find("Thuyen_Go_Song"));float rowLength=Mathf.Max(reference.size.x,reference.size.z);
        foreach(var b in boats)
        {
            b.enabled=false;var hull=BoatHullCollision.Get(b.transform);float length=Mathf.Max(hull.SolidHull.bounds.size.x,hull.SolidHull.bounds.size.z);
            if(b.lightPatrol)Check(length<rowLength*1.2f,"Small patrol hull matches player boat scale: "+b.name);
            float route=Enumerable.Range(0,b.route.Length).Sum(i=>QuestRowboat.FlatDistance(b.route[i].position,b.route[(i+1)%b.route.Length].position));
            Check(route>2800&&b.route.Length==16&&b.trafficPadding>=80,"Broad long patrol loop and wider traffic spacing: "+b.name);
            var path=new List<Vector3>();
            Check(RiverWaterNavigation.FindPath(b.transform.position,b.route[1].position,hull.Radius,path,b.transform)&&path.Count>0,"First patrol leg is reachable around actual stakes/traffic: "+b.name);
        }
        starts=boats.Select(b=>b.transform.position).ToArray();distance=new float[boats.Length];
        var legacy=boats[0];legacy.RestorePatrol(new BachDangSaveData.Patrol{name=legacy.name,position=new Vector3(-3000,70,2100),rotation=Quaternion.identity});
        Check(QuestRowboat.FlatDistance(legacy.transform.position,starts[0])<1,"Legacy patrol saves migrate onto their new safe route instead of trapping inside the full barrier");
        var current=boats[1];var restored=current.transform.position;current.RestorePatrol(new BachDangSaveData.Patrol{name=current.name,position=restored,rotation=current.transform.rotation,layoutVersion=AmbientRiverBoat.PatrolLayoutVersion});
        Check(QuestRowboat.FlatDistance(current.transform.position,restored)<1,"New patrol saves retain their actual vessel position");
        float nearest=float.PositiveInfinity;for(int i=0;i<boats.Length;i++)for(int j=i+1;j<boats.Length;j++)nearest=Mathf.Min(nearest,QuestRowboat.FlatDistance(starts[i],starts[j]));
        Check(nearest>=280,"Patrol operators start on widely separated vessels: "+nearest.ToString("0")+" units");
        RiverBoardingCombat.Instance.enabled=false;RiverPlayerCombat.Instance.enabled=false;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
    }
    static void Simulate()
    {
        // Simulate actual hull sweeps and navigation for 50 seconds; never teleport past stakes.
        for(int n=0;n<20;n++)
        {
            for(int i=0;i<boats.Length;i++){var before=boats[i].transform.position;boats[i].Step(.05f);distance[i]+=QuestRowboat.FlatDistance(before,boats[i].transform.position);}
            steps++;
        }
    }
    static void Capture(string name,Vector3 position,Vector3 focus)
    {
        var camera=new GameObject("QA_RiverMouth_Camera").AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.fieldOfView=65;camera.farClipPlane=15000;camera.transform.position=position;camera.transform.LookAt(focus);
        var rt=new RenderTexture(1400,900,24);var image=new Texture2D(1400,900,TextureFormat.RGB24,false);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,900),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(began==0)began=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-began<5||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>180)throw new Exception("River mouth QA timed out");
            if(phase==0){Setup();phase=1;return;}
            if(phase==1){Simulate();if(steps<1000)return;phase=2;return;}
            if(phase==2)
            {
                foreach(var b in boats.Select((boat,i)=>new{boat,i}))
                {
                    Check(distance[b.i]>600&&b.boat.HasNavigableRoute,"Patrol actually travels over a broad area without remaining stuck: "+b.boat.name+" ("+distance[b.i].ToString("0")+" units)");
                    Check(BoatHullCollision.Get(b.boat.transform).CanOccupy(b.boat.transform.position,b.boat.transform.rotation),"Patrol stays outside solid stakes and other hulls: "+b.boat.name);
                }
                TideSystem.Instance.HoldTide(Zone.Instance,false,true);foreach(var b in boats)b.Step(.001f);
                Capture("RiverMouth_Stakes_Overview",new Vector3(-2500,2350,350),new Vector3(-2400,70,2100));
                var small=boats.First(b=>b.lightPatrol);Capture("RiverMouth_Light_Patrol",small.transform.position-small.transform.forward*75+small.transform.right*65+Vector3.up*40,small.transform.position+Vector3.up*8);
                Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
