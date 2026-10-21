using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Extend villages into the empty foreground with connected household lanes and garden hedges.</summary>
public static class BeachBoatVillageInfill
{
    const string Folder="Assets/BeachBoatStory/VillageInfill";
    static Terrain terrain;
    static float high;
    static readonly System.Random random=new System.Random(871120);
    static readonly List<Bounds> hard=new List<Bounds>(),houseBounds=new List<Bounds>();
    static readonly List<(Vector3 a,Vector3 b)> lanes=new List<(Vector3,Vector3)>();
    static readonly List<(Vector3 p,int prototype,float height,float width)> plants=new List<(Vector3,int,float,float)>();
    static Vector3[] originalTrees;
    static readonly Dictionary<Vector2Int,List<Vector3>> treeCells=new Dictionary<Vector2Int,List<Vector3>>();
    static readonly Dictionary<Vector2Int,List<(Vector3 a,Vector3 b)>> laneCells=new Dictionary<Vector2Int,List<(Vector3,Vector3)>>();
    static Vector3 cachedCenter,cachedOrigin;
    static int[] cachedPrevious;
    static bool[] cachedClear;
    static Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/128),Mathf.FloorToInt(p.z/128));
    static bool NearTree(Vector3 p,float radius)
    {
        var cell=Cell(p);for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)if(treeCells.TryGetValue(cell+new Vector2Int(x,z),out var list)&&list.Any(t=>QuestRowboat.FlatDistance(t,p)<radius))return true;return false;
    }
    static float G(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
    static float R(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
    static bool Inside(Vector3 p,Bounds b,float radius)=>p.x>b.min.x-radius&&p.x<b.max.x+radius&&p.z>b.min.z-radius&&p.z<b.max.z+radius;
    static bool Dry(Vector3 p,float radius)
    {
        float y=G(p);return y>high+7&&y<700&&Mathf.Abs(G(p+Vector3.right*radius)-y)<3&&Mathf.Abs(G(p+Vector3.forward*radius)-y)<3;
    }
    static Transform Group(string name,Transform parent,Vector3 p)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=p;return t;}
    static void Prop(Transform parent,string asset,Vector3 p,float height)
    {
        p.y=G(p);var go=BachDangSettlementAssets.Place("Assets/Prefabs/Props/pf_"+asset+".prefab","Household_"+asset,parent,p,R(0,360));
        var b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.localScale*=height/b.size.y;b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.position+=new Vector3(p.x-b.center.x,p.y-b.min.y,p.z-b.center.z);
        foreach(var c in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
        var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.025f,go.GetComponentsInChildren<Renderer>(true))});lod.RecalculateBounds();
    }
    static float Segment(Vector3 p,Vector3 a,Vector3 b)
    {var d=b-a;d.y=0;var v=p-a;v.y=0;return(v-d*Mathf.Clamp01(Vector3.Dot(v,d)/Mathf.Max(.01f,d.sqrMagnitude))).magnitude;}
    static List<Vector3> Route(Vector3 center,Vector3 destination,List<Bounds> obstacles)
    {
        const int n=91;const float step=24;var origin=center-new Vector3(45*step,0,45*step);var clear=new bool[n*n];var previous=new int[n*n];
        Vector3 P(int i)=>origin+new Vector3((i%n)*step,0,(i/n)*step);
        int Cell(Vector3 p)=>Mathf.Clamp(Mathf.RoundToInt((p.x-origin.x)/step),0,n-1)+n*Mathf.Clamp(Mathf.RoundToInt((p.z-origin.z)/step),0,n-1);
        if(cachedPrevious!=null&&cachedCenter==center){previous=cachedPrevious;clear=cachedClear;}
        else for(int i=0;i<clear.Length;i++)
        {var p=P(i);clear[i]=Dry(p,10)&&!obstacles.Any(b=>Inside(p,b,12))&&!NearTree(p,17);previous[i]=-2;}
        int start=Cell(center),goal=Cell(destination);
        if(!clear[start])start=Enumerable.Range(0,clear.Length).Where(i=>clear[i]&&QuestRowboat.FlatDistance(P(i),center)<110).OrderBy(i=>QuestRowboat.FlatDistance(P(i),center)).DefaultIfEmpty(-1).First();
        if(start<0||!clear[goal])return null;var queue=new Queue<int>();if(cachedPrevious==null||cachedCenter!=center){queue.Enqueue(start);previous[start]=-1;}
        while(queue.Count>0)
        {
            int i=queue.Dequeue();foreach(int offset in new[]{-1,1,-n,n})
            {int j=i+offset;if(j<0||j>=clear.Length||!clear[j]||previous[j]!=-2||QuestRowboat.FlatDistance(P(i),P(j))>25)continue;previous[j]=i;queue.Enqueue(j);}
        }
        cachedCenter=center;cachedPrevious=previous;cachedClear=clear;
        if(previous[goal]==-2)return null;var path=new List<Vector3>();for(int i=goal;i>=0;i=previous[i]){var p=P(i);p.y=G(p);path.Add(p);}path.Reverse();
        for(int i=path.Count-2;i>0;i--)if(Vector3.Cross((path[i]-path[i-1]).normalized,(path[i+1]-path[i]).normalized).sqrMagnitude<.01f)path.RemoveAt(i);
        return path;
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");terrain=Terrain.activeTerrain;high=Object.FindAnyObjectByType<TideSystem>().highTideY;
        var root=GameObject.Find(BeachBoatWholeMapExpansion.RootName).transform;if(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Expanded_Household_Lanes"))throw new Exception("Village lanes already installed");
        Directory.CreateDirectory(".utmp/VillageInfill");EditorSceneManager.SaveScene(scene,".utmp/VillageInfill/before.unity",true);File.Copy(AssetDatabase.GetAssetPath(terrain.terrainData),".utmp/VillageInfill/terrain-before.asset",true);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/BeachBoatStory","VillageInfill");
        var report=BeachBoatWildernessDensity.ReadReport();int original=(int)report["original"],firstShrub=(int)report["firstShrub"];
        var data=terrain.terrainData;var instances=data.treeInstances;var heights=new float[data.treePrototypes.Length];
        for(int i=0;i<heights.Length;i++){var go=Object.Instantiate(data.treePrototypes[i].prefab);heights[i]=BeachBoatWorldAudit.BoundsOf(go.transform).size.y;Object.DestroyImmediate(go);}
        originalTrees=instances.Take(original).Where(t=>heights[t.prototypeIndex]*t.heightScale>25).Select(t=>terrain.transform.position+Vector3.Scale(t.position,data.size)).ToArray();
        foreach(var p in originalTrees){var cell=Cell(p);if(!treeCells.TryGetValue(cell,out var list))treeCells[cell]=list=new List<Vector3>();list.Add(p);}
        foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(renderer.GetComponentInParent<Animator>()!=null)continue;var b=renderer.bounds;
            if(b.size.x>1500||b.size.z>1500)continue;
            if(b.size.y>9||renderer.name=="Cultivated_Field"||renderer.name.Contains("Field_Soil"))hard.Add(b);
        }
        var keepouts=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(g=>g.transform.position)
            .Concat(Object.FindObjectsByType<VillageWorkStall>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(s=>s.InteractionPosition)).ToArray();
        var generated=new List<string>();var painter=new BachDangVillageGround(terrain,7,Folder,generated);int houses=0,people=0,roads=0;
        foreach(var region in root.Cast<Transform>().Where(t=>!t.name.StartsWith("Thac_")))
        {
            var group=Group("Expanded_Household_Lanes",region,region.position);var parcels=new List<(Transform home,Vector3 approach,Bounds bounds)>();
            var local=hard.Where(b=>QuestRowboat.FlatDistance(b.center,region.position)<1700).ToList();
            for(int attempt=0;attempt<160&&parcels.Count<12;attempt++)
            {
                var p=region.position+new Vector3(R(-850,850),0,R(-1000,500));p.y=G(p);float distance=QuestRowboat.FlatDistance(p,region.position);
                if(distance<310||distance>1020||!Dry(p,65)||local.Any(b=>Inside(p,b,85))||keepouts.Any(k=>QuestRowboat.FlatDistance(k,p)<180)||NearTree(p,85))continue;
                var direction=region.position-p;direction.y=0;var q=Quaternion.LookRotation(direction);
                var home=BeachBoatWholeMapExpansion.House(group,p,q.eulerAngles.y,attempt,region.name.StartsWith("Trai_"));home.name="Riverside_Home_"+houses;
                var bounds=BeachBoatWorldAudit.BoundsOf(home);var approach=p+direction.normalized*(Mathf.Max(bounds.extents.x,bounds.extents.z)+38);approach.y=G(approach);
                local.Add(bounds);hard.Add(bounds);houseBounds.Add(bounds);parcels.Add((home,approach,bounds));houses++;
            }
            Physics.SyncTransforms();
            painter.PathPointAllowed=(p,r)=>!local.Any(b=>Inside(p,b,r+1));
            foreach(var parcel in parcels)
            {
                var path=Route(region.position,parcel.approach,local);
                if(path==null){hard.Remove(parcel.bounds);local.Remove(parcel.bounds);houseBounds.Remove(parcel.bounds);Object.DestroyImmediate(parcel.home.gameObject);houses--;continue;}
                painter.CreatePath(group,"Residential_Lane_"+roads,path.ToArray(),2.2f,painter.Earth,roads,new List<Bounds>());roads++;
                for(int i=1;i<path.Count;i++)lanes.Add((path[i-1],path[i]));
                var p=parcel.home.position;var away=p-region.position;away.y=0;away.Normalize();var side=Vector3.Cross(Vector3.up,away);float radius=Mathf.Max(parcel.bounds.extents.x,parcel.bounds.extents.z);
                var stock=p+side*(radius+17);if(Dry(stock,10)&&!local.Any(b=>Inside(stock,b,5)))
                {Prop(group,"logpile_01",stock,7);Prop(group,"barrels_02",stock+away*18,9);Prop(group,"buckets_01",stock-away*16,5);}
                people+=BeachBoatBusyWorld.PopulateRegion(region,parcel.approach,1,region.name.StartsWith("Trai_"));
                for(int edge=0;edge<3;edge++)for(int k=-4;k<=4;k++)
                {
                    var v=p+away*(radius+28)+side*(k*12);if(edge<2)v=p+side*((edge==0?-1:1)*(radius+28))+away*(k*12);
                    v.y=G(v);if(Dry(v,8)&&!local.Any(b=>Inside(v,b,6)))plants.Add((v,firstShrub+(k%2==0?0:1),R(13,21),1.7f));
                }
            }
            // Ground cover occupies the open spaces between parcels, with bare walking lanes.
            for(float x=-950;x<950;x+=28)for(float z=-1000;z<650;z+=28)
            {
                var p=region.position+new Vector3(x+R(-9,9),0,z+R(-9,9));p.y=G(p);
                if(!Dry(p,8)||local.Any(b=>Inside(p,b,12))||keepouts.Any(k=>QuestRowboat.FlatDistance(k,p)<45)||R(0,1)>.65f)continue;
                plants.Add((p,firstShrub+2,R(2.5f,5.5f),R(1,1.7f)));
            }
            Debug.Log(region.name+": expanded to "+parcels.Count+" household parcels");
        }
        if(houses<180)throw new Exception("Too few connected homes: "+houses);
        foreach(var lane in lanes)
        {
            var bounds=new Bounds(lane.a,Vector3.zero);bounds.Encapsulate(lane.b);bounds.Expand(64);var min=Cell(bounds.min);var max=Cell(bounds.max);
            for(int x=min.x;x<=max.x;x++)for(int z=min.y;z<=max.y;z++){var cell=new Vector2Int(x,z);if(!laneCells.TryGetValue(cell,out var list))laneCells[cell]=list=new List<(Vector3,Vector3)>();list.Add(lane);}
        }
        var camp=GameObject.Find("CampLayoutOrigin").transform.position+Vector3.forward*100;var parade=new Bounds(camp,new Vector3(600,100,820));
        bool Lane(Vector3 p,float radius)=>laneCells.TryGetValue(Cell(p),out var list)&&list.Any(l=>Segment(p,l.a,l.b)<radius);
        bool ClearForPlant(Vector3 p,float radius)=>!Inside(p,parade,0)&&!houseBounds.Any(b=>Inside(p,b,radius))&&!Lane(p,11);
        var keptOriginal=new List<TreeInstance>();var keptNew=new List<TreeInstance>();int cleared=0;
        for(int i=0;i<instances.Length;i++)
        {
            var t=instances[i];var p=terrain.transform.position+Vector3.Scale(t.position,data.size);float h=heights[t.prototypeIndex]*t.heightScale;
            if(!ClearForPlant(p,h>25?25:5)){cleared++;continue;}
            (i<original?keptOriginal:keptNew).Add(t);
        }
        int planted=0;
        foreach(var item in plants)
        {
            if(!ClearForPlant(item.p,5))continue;var local=item.p-terrain.transform.position;
            keptNew.Add(new TreeInstance{position=new Vector3(local.x/data.size.x,local.y/data.size.y,local.z/data.size.z),prototypeIndex=item.prototype,heightScale=item.height/heights[item.prototype],widthScale=item.height/heights[item.prototype]*item.width,rotation=R(0,Mathf.PI*2),color=Color.white,lightmapColor=Color.white});planted++;
        }
        data.SetTreeInstances(keptOriginal.Concat(keptNew).ToArray(),false);EditorUtility.SetDirty(data);
        var mature=new HashSet<int>(report["treeSpecies"].Select(t=>(int)t["prototype"]));
        report["original"]=keptOriginal.Count;report["total"]=data.treeInstanceCount;report["gardenGroundCover"]=planted;
        report["treeCount"]=keptNew.Count(t=>mature.Contains(t.prototypeIndex));report["bushCount"]=keptNew.Count(t=>t.prototypeIndex>=firstShrub);report["rockCount"]=keptNew.Count(t=>t.prototypeIndex<firstShrub&&!mature.Contains(t.prototypeIndex));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();report.Remove("samples");BeachBoatWildernessDensity.SaveReport(report);
        File.WriteAllText("Logs/VillageInfillBuild.json",JsonConvert.SerializeObject(new{houses,people,roads,cleared,planted,totalPlants=data.treeInstanceCount},Formatting.Indented));EditorApplication.Exit(0);
    }
}
