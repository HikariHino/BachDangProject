using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Fill the empty land between settlements, using native terrain instances and shared LODs.</summary>
public static class BeachBoatWildernessDensity
{
    public const string Marker="BeachBoat_Dense_Wilderness_V1";
    const string Folder="Assets/BeachBoatStory/Wilderness";
    static Terrain terrain;
    static float high;
    static readonly Dictionary<Vector2Int,List<Bounds>> protectedCells=new Dictionary<Vector2Int,List<Bounds>>();
    static readonly Dictionary<Vector2Int,List<Vector3>> stems=new Dictionary<Vector2Int,List<Vector3>>();
    static readonly List<TreeInstance> added=new List<TreeInstance>();
    static readonly List<object> samples=new List<object>();
    static readonly System.Random random=new System.Random(128893);
    static readonly Dictionary<int,float> prototypeHeight=new Dictionary<int,float>();
    public static Newtonsoft.Json.Linq.JObject ReadReport()=>Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(File.Exists(Folder+"/PlacementSummary.json")?Folder+"/PlacementSummary.json":"Logs/WildernessBuild.json"));
    public static void SaveReport(object value)
    {var json=JsonConvert.SerializeObject(value,Formatting.Indented);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WildernessBuild.json",json);File.WriteAllText(Folder+"/PlacementSummary.json",json);}
    static float R(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
    static Vector2Int Cell(Vector3 p,int size)=>new Vector2Int(Mathf.FloorToInt(p.x/size),Mathf.FloorToInt(p.z/size));
    static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
    static void Protect(Bounds bounds)
    {
        var a=Cell(bounds.min,128);var b=Cell(bounds.max,128);
        for(int x=a.x;x<=b.x;x++)for(int z=a.y;z<=b.y;z++)
        {var cell=new Vector2Int(x,z);if(!protectedCells.TryGetValue(cell,out var list))protectedCells[cell]=list=new List<Bounds>();list.Add(bounds);}
    }
    static bool Free(Vector3 p,float radius)
    {
        var a=Cell(p-Vector3.one*radius,128);var b=Cell(p+Vector3.one*radius,128);
        for(int x=a.x;x<=b.x;x++)for(int z=a.y;z<=b.y;z++)
        if(protectedCells.TryGetValue(new Vector2Int(x,z),out var list))foreach(var bounds in list)
            if(p.x>bounds.min.x-radius&&p.x<bounds.max.x+radius&&p.z>bounds.min.z-radius&&p.z<bounds.max.z+radius)return false;
        return true;
    }
    static void Stem(Vector3 p)
    {var cell=Cell(p,96);if(!stems.TryGetValue(cell,out var list))stems[cell]=list=new List<Vector3>();list.Add(p);}
    static bool NearStem(Vector3 p,float radius)
    {
        var cell=Cell(p,96);int n=Mathf.CeilToInt(radius/96);
        for(int x=-n;x<=n;x++)for(int z=-n;z<=n;z++)
            if(stems.TryGetValue(cell+new Vector2Int(x,z),out var list)&&list.Any(t=>QuestRowboat.FlatDistance(t,p)<radius))return true;
        return false;
    }
    static bool Dry(Vector3 p,float slopeProbe)
    {
        if(p.y<high+7||p.y>750)return false;
        return Mathf.Abs(Ground(p+Vector3.right*slopeProbe)-p.y)<slopeProbe*.18f&&
            Mathf.Abs(Ground(p+Vector3.forward*slopeProbe)-p.y)<slopeProbe*.18f;
    }
    static bool Rock(GameObject model)
    {
        bool Match(string s)=>System.Text.RegularExpressions.Regex.IsMatch(s??"","rock|stone|boulder|cliff|mountain",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(model);
        return Match(model.name)||Match(source!=null?AssetDatabase.GetAssetPath(source):"")||model.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh!=null&&Match(m.sharedMesh.name));
    }
    static float Measure(GameObject prefab)
    {var go=Object.Instantiate(prefab);go.transform.position=Vector3.zero;float height=BeachBoatWorldAudit.BoundsOf(go.transform).size.y;Object.DestroyImmediate(go);if(height<=0)throw new Exception("No geometry in "+prefab.name);return height;}
    static void Plant(Vector3 p,int prototype,float height,float width=1)
    {
        var local=p-terrain.transform.position;var size=terrain.terrainData.size;
        added.Add(new TreeInstance{position=new Vector3(local.x/size.x,local.y/size.y,local.z/size.z),prototypeIndex=prototype,
            heightScale=height/prototypeHeight[prototype],widthScale=height/prototypeHeight[prototype]*width,rotation=R(0,Mathf.PI*2),
            color=new Color(R(.88f,1),R(.91f,1),R(.85f,1),1),lightmapColor=Color.white});
    }
    static GameObject Shrub(string name,string source)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));go.name=name;
        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;
        foreach(var collider in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
        foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats=renderer.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                var original=mats[i];var path=Folder+"/"+name+"_"+i+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null)
                {
                    material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name+"_Leaves",enableInstancing=true};
                    AssetDatabase.CreateAsset(material,path);
                }
                var texture=original.HasProperty("Base_Map")?original.GetTexture("Base_Map"):original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap"):original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null;
                if(texture==null)
                {
                    // TerrainGrass shader revisions renamed their properties; the authored
                    // albedo remains in the material's serialized texture environments.
                    var saved=new SerializedObject(original).FindProperty("m_SavedProperties.m_TexEnvs");
                    for(int entry=0;entry<saved.arraySize;entry++)
                    {
                        var item=saved.GetArrayElementAtIndex(entry);
                        if(item.FindPropertyRelative("first").stringValue=="Base_Map")texture=item.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                    }
                }
                if(texture==null)throw new Exception("Missing leaf texture in "+original.name);
                material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.42f);material.SetFloat("_Cull",0);material.SetFloat("_Smoothness",.05f);material.EnableKeyword("_ALPHATEST_ON");material.doubleSidedGI=true;material.renderQueue=2450;material.SetOverrideTag("RenderType","TransparentCutout");EditorUtility.SetDirty(material);
                mats[i]=material;
            }
            renderer.sharedMaterials=mats;
        }
        var lod=go.GetComponent<LODGroup>();if(lod==null)lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.008f,go.GetComponentsInChildren<Renderer>(true))});lod.RecalculateBounds();
        var result=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+name+".prefab");Object.DestroyImmediate(go);return result;
    }
    static void KeepRoutesClear()
    {
        foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(renderer.GetComponentInParent<Animator>()!=null)continue;var bounds=renderer.bounds;
            bool path=System.Text.RegularExpressions.Regex.IsMatch(renderer.name,"Footpath|Cultivated_Field|Field_|Path|Duong|Loi_|San_",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if(bounds.size.x>1400||bounds.size.z>1400||(!path&&bounds.size.y<6))continue;
            bounds.Expand(new Vector3(path?12:8,0,path?12:8));Protect(bounds);
        }
        foreach(var wander in Object.FindObjectsByType<NPCWander>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var stops=wander.waypoints;if(stops==null)continue;
            for(int i=0;i<stops.Length;i++)
            {
                var a=stops[i];var b=stops[(i+1)%stops.Length];if(a==null||b==null)continue;
                int steps=Mathf.Max(1,Mathf.CeilToInt(QuestRowboat.FlatDistance(a.position,b.position)/14));
                for(int k=0;k<=steps;k++)Protect(new Bounds(Vector3.Lerp(a.position,b.position,k/(float)steps),new Vector3(28,30,28)));
            }
        }
        var points=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(g=>g.destinationTarget==null?new[]{g.transform.position}:new[]{g.transform.position,g.destinationTarget.position})
            .Concat(Object.FindObjectsByType<BeachBoatStory.WoodCollectible>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(w=>w.transform.position))
            .Concat(Object.FindObjectsByType<VillageWorkStall>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(w=>w.InteractionPosition))
            .Concat(Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(s=>new[]{s.transform.position}.Concat(s.checkpoints.Where(t=>t!=null).Select(t=>t.position))));
        foreach(var p in points)Protect(new Bounds(p,new Vector3(100,50,100)));
        var boat=Object.FindAnyObjectByType<QuestRowboat>();Protect(new Bounds(boat.boardingPoint.position,new Vector3(160,80,150)));
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");if(GameObject.Find(Marker)!=null)throw new Exception("Dense countryside already installed");
        terrain=Terrain.activeTerrain;high=Object.FindAnyObjectByType<TideSystem>().highTideY;var data=terrain.terrainData;var original=data.treeInstances;var asset=AssetDatabase.GetAssetPath(data);
        Directory.CreateDirectory(".utmp/Wilderness");File.Copy(asset,".utmp/Wilderness/terrain-before.asset",true);EditorSceneManager.SaveScene(scene,".utmp/Wilderness/scene-before.unity",true);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/BeachBoatStory","Wilderness");
        KeepRoutesClear();foreach(var instance in original)Stem(terrain.transform.position+Vector3.Scale(instance.position,data.size));
        var prototypes=data.treePrototypes.ToList();
        for(int i=0;i<prototypes.Count;i++)prototypeHeight[i]=Measure(prototypes[i].prefab);
        var trees=Enumerable.Range(0,prototypes.Count).Where(i=>!Rock(prototypes[i].prefab)&&prototypeHeight[i]>4).ToArray();var rocks=Enumerable.Range(0,prototypes.Count).Where(i=>Rock(prototypes[i].prefab)).ToArray();
        if(trees.Length<2||rocks.Length==0)throw new Exception("Expected native tree and rock varieties");
        int firstShrub=prototypes.Count;
        prototypes.Add(new TreePrototype{prefab=Shrub("Wilderness_Bush_A","Assets/TerrainSampleAssets/Prefabs/Bush_A.prefab")});
        prototypes.Add(new TreePrototype{prefab=Shrub("Wilderness_Bush_B","Assets/TerrainSampleAssets/Prefabs/Bush_B.prefab")});
        prototypes.Add(new TreePrototype{prefab=Shrub("Wilderness_Fern","Assets/TerrainSampleAssets/Prefabs/Fern_A.prefab")});
        for(int i=0;i<prototypes.Count;i++)prototypeHeight[i]=Measure(prototypes[i].prefab);
        int treeCount=0,bushCount=0,rockCount=0;
        // Two spatial frequencies make dense groves alternating with loose meadow trees.
        for(float x=-20100;x<20100;x+=105)for(float z=-20100;z<20100;z+=105)
        {
            var p=new Vector3(x+R(-39,39),0,z+R(-39,39));p.y=Ground(p);
            if(!Dry(p,20)||!Free(p,23)||NearStem(p,40))continue;
            float patch=Mathf.PerlinNoise((p.x+23100)/780,(p.z+18900)/780);
            float density=patch>.49f?.83f:.20f;if(R(0,1)>density)continue;
            Plant(p,trees[random.Next(trees.Length)],R(65,126),R(.78f,1.15f));Stem(p);treeCount++;
        }
        for(float x=-20200;x<20200;x+=69)for(float z=-20200;z<20200;z+=69)
        {
            var p=new Vector3(x+R(-26,26),0,z+R(-26,26));p.y=Ground(p);
            if(!Dry(p,12)||!Free(p,10)||NearStem(p,13)||R(0,1)>.57f)continue;
            int prototype=firstShrub+random.Next(3);Plant(p,prototype,prototype==firstShrub+2?R(4,8):R(8,17),R(.85f,1.5f));bushCount++;
            if(R(0,1)<.34f)
            {
                var q=p+new Vector3(R(-18,18),0,R(-18,18));q.y=Ground(q);
                if(Dry(q,10)&&Free(q,9)&&!NearStem(q,12)){Plant(q,firstShrub+random.Next(2),R(6,11),1.2f);bushCount++;}
            }
        }
        for(float x=-19900;x<19900;x+=240)for(float z=-19900;z<19900;z+=240)
        {
            var p=new Vector3(x+R(-90,90),0,z+R(-90,90));p.y=Ground(p);
            if(!Dry(p,15)||!Free(p,14)||NearStem(p,19)||R(0,1)>.22f)continue;
            Plant(p,rocks[random.Next(rocks.Length)],R(4,12),R(.9f,1.3f));rockCount++;
        }
        if(treeCount<5000||bushCount<15000||rockCount<500)throw new Exception("Insufficient ground coverage: "+treeCount+", "+bushCount+", "+rockCount);
        data.treePrototypes=prototypes.ToArray();data.RefreshPrototypes();data.SetTreeInstances(original.Concat(added).ToArray(),false);terrain.Flush();
        var verified=data.treeInstances;
        for(int i=0;i<original.Length;i++)if(!original[i].Equals(verified[i]))throw new Exception("Existing forest changed");
        var marker=new GameObject(Marker);marker.transform.position=Vector3.zero;
        EditorUtility.SetDirty(data);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        var town=GameObject.Find(BeachBoatWholeMapExpansion.RootName).transform;
        foreach(var t in town.Cast<Transform>().Where(t=>!t.name.StartsWith("Thac_")))
        {
            var p=t.position+new Vector3(-400,0,-500);
            int nearby=added.Count(a=>QuestRowboat.FlatDistance(terrain.transform.position+Vector3.Scale(a.position,data.size),p)<500);
            samples.Add(new{t.name,p.x,p.z,nearby});
        }
        SaveReport(new{asset,original=original.Length,treeCount,bushCount,rockCount,total=data.treeInstanceCount,firstShrub,
            treeSpecies=trees.Select(i=>new{prototype=i,prototypes[i].prefab.name,height=prototypeHeight[i]}),samples});EditorApplication.Exit(0);
    }
    public static void NormalizeTreeSpeciesAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");terrain=Terrain.activeTerrain;var data=terrain.terrainData;var prototypes=data.treePrototypes;
        var report=ReadReport();int original=(int)report["original"],count=(int)report["treeCount"],firstShrub=(int)report["firstShrub"];
        for(int i=0;i<prototypes.Length;i++)prototypeHeight[i]=Measure(prototypes[i].prefab);
        var mature=Enumerable.Range(0,firstShrub).Where(i=>!Rock(prototypes[i].prefab)&&prototypeHeight[i]>4).ToArray();
        var instances=data.treeInstances;
        for(int i=original;i<original+count;i++)
        {
            var t=instances[i];int p=mature[random.Next(mature.Length)];float scale=R(65,126)/prototypeHeight[p];t.prototypeIndex=p;t.heightScale=scale;t.widthScale=scale*R(.78f,1.15f);instances[i]=t;
        }
        data.SetTreeInstances(instances,false);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        report["treeSpecies"]=Newtonsoft.Json.Linq.JArray.FromObject(mature.Select(i=>new{prototype=i,prototypes[i].prefab.name,height=prototypeHeight[i]}));SaveReport(report);EditorApplication.Exit(0);
    }
}
