using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using BeachBoatStoryEditor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Zone=BeachBoatStory.RiverStakeZone;

public static class BeachBoatStakeAccessRevision
{
    public const string Marker="BaiCoc_LoiVeBen_TuNhien_938";
    public const float LaneHalfWidth=84;
    public static readonly Vector3[] ServiceLane={new Vector3(-3180,0,1120),new Vector3(-3290,0,1480),new Vector3(-3230,0,1780),new Vector3(-3350,0,2100),new Vector3(-3430,0,2440),new Vector3(-3520,0,2780),new Vector3(-3600,0,3030)};
    public static readonly Vector3[] DockLane={new Vector3(-3270,0,1880),new Vector3(-3400,0,1880),new Vector3(-3505,0,1880)};
    public static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
    {p.y=a.y=b.y=0;var d=b-a;return Vector3.Distance(p,a+d*(d.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude):0));}
    public static float DistanceToLane(Vector3 p)
    {
        float result=float.PositiveInfinity;
        foreach(var lane in new[]{ServiceLane,DockLane})for(int i=1;i<lane.Length;i++)result=Mathf.Min(result,SegmentDistance(p,lane[i-1],lane[i]));
        return result;
    }
    static float Radius(Bounds b)=>new Vector2(b.extents.x,b.extents.z).magnitude;
    public static void Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/Scenes/beachBoat.unity"||scene.isDirty)throw new Exception("Save beachBoat and use Edit Mode first.");
        if(GameObject.Find(Marker)!=null)throw new Exception("Natural stake access layout is already applied; no duplicate changes made.");
        var zone=Object.FindAnyObjectByType<Zone>();var tide=Object.FindAnyObjectByType<TideSystem>();var terrain=Terrain.activeTerrain;
        if(zone==null||zone.RequiredCount!=1246||zone.ClusterCount!=5)throw new Exception("Expected the existing complete 1,246-stake field.");
        var stakes=zone.fieldStakes;var bounds=stakes.Select(t=>BeachBoatWorldAudit.BoundsOf(t)).ToArray();
        var movable=Enumerable.Range(0,stakes.Length).Where(i=>stakes[i].name.StartsWith("CocGo_CuaSong_",StringComparison.Ordinal)||stakes[i].name.StartsWith("CocGo_DaiRong_",StringComparison.Ordinal)||DistanceToLane(bounds[i].center)<LaneHalfWidth+Radius(bounds[i])+10).ToArray();
        var moving=new HashSet<int>(movable);var placed=Enumerable.Range(0,stakes.Length).Where(i=>!moving.Contains(i)).Select(i=>bounds[i]).ToList();
        var originalPositions=stakes.Select(t=>t.position).ToArray();var rotations=stakes.Select(t=>t.rotation).ToArray();var scales=stakes.Select(t=>t.localScale).ToArray();
        var random=new System.Random(9381026);float Range(float a,float b)=>a+(float)random.NextDouble()*(b-a);
        BeachBoatToolSafety.Begin("NaturalStakesAndDockAccess");int relocated=0;
        try
        {
            foreach(int index in movable)
            {
                var stake=stakes[index];var initial=bounds[index];bool broad=stake.name.StartsWith("CocGo_CuaSong_",StringComparison.Ordinal),wasInLane=DistanceToLane(initial.center)<LaneHalfWidth+Radius(initial)+10;
                bool chosen=false;Bounds candidate=default;
                for(int attempt=0;attempt<480&&!chosen;attempt++)
                {
                    var p=initial.center+new Vector3(Range(broad?-32:-22,broad?32:22),0,Range(broad?-112:-42,broad?112:42));
                    if(wasInLane)p.x+=Range(160,attempt>80?500:320);
                    if(attempt>80)p+=new Vector3(Range(-180,180),0,Range(-160,160));
                    float ground=terrain.SampleHeight(p)+terrain.transform.position.y;
                    if(ground>tide.lowTideY-3||zone.placements.Any(a=>QuestRowboat.FlatDistance(p,a.approach.position)<68))continue;
                    float top=Mathf.Lerp(tide.lowTideY,tide.highTideY,Range(.48f,.69f)),bottom=ground-8;
                    stake.localScale=scales[index];stake.rotation=Quaternion.Euler(0,Range(-32,32),0)*rotations[index]*Quaternion.Euler(Range(-7,7),Range(-5,5),0);
                    var b=BeachBoatWorldAudit.BoundsOf(stake);stake.position+=p-b.center;
                    b=BeachBoatWorldAudit.BoundsOf(stake);stake.localScale*=(top-bottom)/Mathf.Max(.01f,b.size.y);b=BeachBoatWorldAudit.BoundsOf(stake);stake.position+=Vector3.up*(bottom-b.min.y);
                    candidate=BeachBoatWorldAudit.BoundsOf(stake);
                    if(DistanceToLane(candidate.center)<LaneHalfWidth+Radius(candidate)+10)continue;
                    if(placed.Any(other=>QuestRowboat.FlatDistance(candidate.center,other.center)<Mathf.Max(18,Radius(candidate)+Radius(other)+2)))continue;
                    chosen=true;
                }
                if(!chosen)throw new Exception("Unable to place stake safely: "+stake.name);
                if(wasInLane)relocated++;placed.Add(candidate);EditorUtility.SetDirty(stake);
            }
            var root=new GameObject(Marker).transform;root.SetParent(zone.transform,false);
            void Path(string name,Vector3[] points)
            {
                var parent=new GameObject(name).transform;parent.SetParent(root,false);
                for(int i=0;i<points.Length;i++){var t=new GameObject("Point_"+i.ToString("00")).transform;t.SetParent(parent,false);var p=points[i];p.y=tide.lowTideY;t.position=p;}
            }
            Path("Loi_Mep_Bai_Coc",ServiceLane);Path("Loi_Vao_Ben_Bao_Cao",DockLane);
            RiverWaterNavigation.Reset();Physics.SyncTransforms();BeachBoatToolSafety.Save(scene);
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/StakeAccessRevision.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=true,total=stakes.Length,scattered=movable.Length,relocatedFromAccess=relocated,laneWidth=LaneHalfWidth*2,seed=9381026},Newtonsoft.Json.Formatting.Indented));
        }
        catch
        {
            for(int i=0;i<stakes.Length;i++){stakes[i].SetPositionAndRotation(originalPositions[i],rotations[i]);stakes[i].localScale=scales[i];}
            var root=GameObject.Find(Marker);if(root!=null)Object.DestroyImmediate(root);RiverWaterNavigation.Reset();throw;
        }
    }
}
