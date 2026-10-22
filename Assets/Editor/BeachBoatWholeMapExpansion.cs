using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class BeachBoatWholeMapExpansion
{
    public const string RootName="BeachBoat_Whole_Map_Life";
    const string Folder="Assets/BeachBoatStory/WholeMap";
    static Terrain terrain;
    static float high;
    static Material timber,beams,roof,earth,crop,stone,water;
    static readonly List<Bounds> occupied=new List<Bounds>();
    static readonly Dictionary<Vector2Int,List<Vector3>> treeCells=new Dictionary<Vector2Int,List<Vector3>>();
    static int houses,people,animals,stalls,serial;
    static Transform Group(string name,Transform parent,Vector3 position)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=position;return t;}
    static Material LoadMat(string path)=>AssetDatabase.LoadAssetAtPath<Material>(path);
    static bool Clear(Vector3 p,float radius,float slope=3)
    {
        float y=Ground(p);if(y<high+6||Mathf.Abs(p.x)>20000||Mathf.Abs(p.z)>20000)return false;
        foreach(var d in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            if(Mathf.Abs(Ground(p+d*radius)-y)>slope)return false;
        if(occupied.Any(b=>p.x>b.min.x-radius&&p.x<b.max.x+radius&&p.z>b.min.z-radius&&p.z<b.max.z+radius))return false;
        var cell=new Vector2Int(Mathf.FloorToInt(p.x/128),Mathf.FloorToInt(p.z/128));int r=Mathf.CeilToInt((radius+10)/128);
        for(int x=-r;x<=r;x++)for(int z=-r;z<=r;z++)
            if(treeCells.TryGetValue(cell+new Vector2Int(x,z),out var trees)&&trees.Any(t=>QuestRowboat.FlatDistance(p,t)<radius+9))return false;
        return true;
    }
    static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool collider=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static Mesh SaveMesh(Mesh mesh,string name)
    {
        mesh.name=name;mesh.RecalculateNormals();mesh.RecalculateBounds();var path=Folder+"/"+name+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(previous==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,previous);EditorUtility.SetDirty(previous);Object.DestroyImmediate(mesh);return previous;
    }
    static Transform MeshObject(string name,Transform parent,Mesh mesh,Material mat)
    {
        var t=Group(name,parent,parent.position);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;return t;
    }
    static Transform Stall(Transform parent,Vector3 p,Quaternion rotation,string name="Open_Market_Stall")
    {
        p.y=Ground(p);var t=Group(name,parent,p);t.rotation=rotation;
        foreach(float x in new[]{-14f,14f})foreach(float z in new[]{-10f,10f})
        {
            var basePoint=t.TransformPoint(new Vector3(x,0,z));float bottom=Ground(basePoint);float top=p.y+28;
            Box("Grounded_Post",t,new Vector3(basePoint.x,(bottom+top)/2,basePoint.z),new Vector3(1.5f,top-bottom,1.5f),beams).transform.rotation=rotation;
        }
        var shared=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Shared_Stall_Roof.asset");
        if(shared==null)
        {
            shared=new Mesh{vertices=new[]{new Vector3(-17,27,-13),new Vector3(17,27,-13),new Vector3(-17,34,0),new Vector3(17,34,0),new Vector3(-17,27,13),new Vector3(17,27,13)},triangles=new[]{0,2,1,1,2,3,2,4,3,3,4,5,1,2,0,3,2,1,3,4,2,5,4,3},uv=new[]{new Vector2(0,0),new Vector2(2,0),new Vector2(0,1),new Vector2(2,1),new Vector2(0,2),new Vector2(2,2)}};
            shared=SaveMesh(shared,"Shared_Stall_Roof");
        }
        MeshObject("Pitched_Roof",t,shared,roof);
        Box("Counter",t,t.TransformPoint(new Vector3(0,9,5)),new Vector3(24,2,7),timber).transform.rotation=rotation;
        foreach(float x in new[]{-10f,10f})Box("Counter_Leg",t,t.TransformPoint(new Vector3(x,4,5)),new Vector3(2,8,5),beams).transform.rotation=rotation;
        for(int i=0;i<3;i++)Box("Wrapped_Stock",t,t.TransformPoint(new Vector3(-7+i*7,11,5)),new Vector3(5,2,4),earth).transform.rotation=rotation;
        stalls++;return t;
    }
    static void FixStalls()
    {
        var broken=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name=="Market_Canopy").ToArray();
        foreach(var old in broken){var p=old.position;var q=old.rotation;var parent=old.parent;Object.DestroyImmediate(old.gameObject);Stall(parent,p,q);}
    }
    static void FixShoreGate()
    {
        var gate=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.RiverGate);
        var old=gate.transform.Find("Visible_Story_Gate");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var p=gate.transform.position;p.x=-3850;p.z=1920;p.y=Ground(p);gate.transform.SetPositionAndRotation(p,Quaternion.identity);gate.interactDistance=16;
        var guard=Group("Riverside_Guard_Post",gate.transform,p);
        foreach(float x in new[]{-20f,20f})
        {
            var b=p+new Vector3(x,0,0);b.y=Ground(b);
            Box("Gate_Post",guard,b+Vector3.up*19,new Vector3(3,38,3),beams,true);
        }
        Box("Gate_Crossbeam",guard,p+Vector3.up*36,new Vector3(46,4,4),timber);
        for(int side=-1;side<=1;side+=2)for(int i=0;i<10;i++)
        {
            var b=p+new Vector3(side*(25+i*5),0,0);b.y=Ground(b);
            Box("Stockade_Picket",guard,b+Vector3.up*11,new Vector3(2,22,2),beams,true);
            if(i%3==0)Box("Stockade_Rail",guard,b+Vector3.up*14,new Vector3(17,1.5f,2),timber);
        }
        Stall(guard,p+new Vector3(-65,0,-30),Quaternion.identity,"Guard_Shelter");
        // The return point stays inside the open lane, clear of posts and the tide.
        var arrival=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.CampRiverGate).destinationTarget;
        var landing=p+Vector3.back*26;landing.y=Ground(landing)+3.2f;arrival.SetPositionAndRotation(landing,Quaternion.identity);
    }
    public static Transform House(Transform parent,Vector3 p,float yaw,int variant,bool military)
    {
        if(terrain==null)terrain=Terrain.activeTerrain;
        p.y=Ground(p);
        string path=military?"Assets/BeachBoatStory/Camp/WalkIn_Barracks.prefab":"Assets/Prefabs/Buildings/"+new[]{"pf_build_small_house_straw_roof_01.prefab","pf_build_small_house_tall_roof_01.prefab","pf_build_small_house_01.prefab"}[variant%3];
        var go=BachDangSettlementAssets.Place(path,"Regional_House_"+houses,parent,p,yaw);
        var b=BeachBoatWorldAudit.BoundsOf(go.transform);float height=military?47:42;go.transform.localScale*=height/b.size.y;
        b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.position+=new Vector3(p.x-b.center.x,p.y-b.min.y,p.z-b.center.z);b=BeachBoatWorldAudit.BoundsOf(go.transform);
        if(!military)
        {
            // Closed native houses have solid walls; retain an honest collision footprint.
            foreach(var c in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            var collider=go.AddComponent<BoxCollider>();collider.center=go.transform.InverseTransformPoint(b.center);collider.size=Vector3.Scale(b.size,new Vector3(1/go.transform.lossyScale.x,1/go.transform.lossyScale.y,1/go.transform.lossyScale.z));
        }
        var rr=go.GetComponentsInChildren<Renderer>(true);foreach(var lod in go.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(lod);
        var group=go.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.002f,rr)});group.RecalculateBounds();occupied.Add(b);houses++;return go.transform;
    }
    static void Field(Transform parent,Vector3 center,int index)
    {
        if(!Clear(center,100,6))return;
        var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
        for(int x=-5;x<5;x++)for(int z=-4;z<4;z++)
        {
            int n=vertices.Count;foreach(var d in new[]{new Vector3(x*20,0,z*20),new Vector3(x*20,0,(z+1)*20),new Vector3((x+1)*20,0,z*20),new Vector3((x+1)*20,0,(z+1)*20)})
            {var p=center+d;p.y=Ground(p)+.15f;vertices.Add(p-parent.position);uv.Add(new Vector2(p.x/14,p.z/14));}
            triangles.AddRange(new[]{n,n+1,n+2,n+2,n+1,n+3});
        }
        var mesh=new Mesh{vertices=vertices.ToArray(),triangles=triangles.ToArray(),uv=uv.ToArray()};mesh=SaveMesh(mesh,"Regional_Field_"+index);MeshObject("Cultivated_Field",parent,mesh,earth);
        var rice=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BachDangRuralLandscape/000_Shared_Rice_Clump.asset");
        var patches=new List<CombineInstance>();
        for(int x=-9;x<=9;x++)for(int z=-7;z<=7;z++)
        {var p=center+new Vector3(x*10,0,z*10);p.y=Ground(p)+.2f;patches.Add(new CombineInstance{mesh=rice,transform=Matrix4x4.TRS(p-parent.position,Quaternion.Euler(0,(x+z)*17,0),Vector3.one*5)});}
        var plants=new Mesh{indexFormat=IndexFormat.UInt32};plants.CombineMeshes(patches.ToArray());plants=SaveMesh(plants,"Regional_Crops_"+index);var t=MeshObject("Growing_Rice_Rows",parent,plants,crop);
        var lod=t.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.06f,t.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
    }
    static void Region(Transform root,Vector3 center,int index)
    {
        center.y=Ground(center);bool military=index%5==0;var region=Group((military?"Trai_Tien_Tieu_":"Xom_Dan_")+index.ToString("00"),root,center);
        int before=houses,occupiedBefore=occupied.Count;
        for(int i=0;i<7;i++)
        {
            float angle=i*360f/7+index*19;var p=center+Quaternion.Euler(0,angle,0)*Vector3.forward*(160+i%2*60);
            if(!Clear(p,65,2.8f))continue;House(region,p,angle+180,i,military);
        }
        if(houses-before<2){Object.DestroyImmediate(region.gameObject);houses=before;occupied.RemoveRange(occupiedBefore,occupied.Count-occupiedBefore);return;}
        Stall(region,center+Vector3.right*42,Quaternion.Euler(0,index*30,0));
        Physics.SyncTransforms();people+=BeachBoatBusyWorld.PopulateRegion(region,center,6,military);
        for(int i=0;i<3;i++)Field(region,center+Quaternion.Euler(0,i*120+index*19,0)*Vector3.forward*420,serial++);
        var source=Object.FindObjectsByType<VillageChicken>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
        for(int i=0;i<4;i++)
        {
            var p=center+new Vector3(-45+i*10,0,-25);if(!Clear(p,8,1))continue;p.y=Ground(p)+.1f;
            var hen=Object.Instantiate(source,region);hen.name="Regional_Hen_"+i;hen.transform.position=p;animals++;
        }
    }
    static void Waterfall(Transform root,Vector3 top,Vector3 bottom,int index)
    {
        var direction=bottom-top;direction.y=0;direction.Normalize();var side=Vector3.Cross(Vector3.up,direction);float width=12;
        var parent=Group("Thac_Suoi_"+index,root,top);var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        var source=top+Vector3.up*36;var lip=source+direction*19;
        var foot=top+direction*52;foot.y=Ground(foot)+1.2f;
        var course=new List<Vector3>{source-direction*8,source,lip,Vector3.Lerp(lip,foot,.32f)+Vector3.up*5,Vector3.Lerp(lip,foot,.67f)+Vector3.up*3,foot};
        for(int i=1;i<=26;i++){var p=Vector3.Lerp(foot,bottom,i/26f)+side*Mathf.Sin(i*.35f)*3;p.y=Ground(p)+.6f;course.Add(p);}
        float travelled=0;
        for(int i=0;i<course.Count;i++)
        {
            var center=course[i];if(i>0)travelled+=Vector3.Distance(course[i-1],center);
            for(int edge=-1;edge<=1;edge+=2){var p=center+side*(width*.5f*edge);if(i>5)p.y=Ground(p)+.7f;v.Add(p-top);uv.Add(new Vector2((edge+1)*.5f,travelled/14));}
            if(i>0){int n=i*2;tri.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});}
        }
        var mesh=new Mesh{vertices=v.ToArray(),triangles=tri.ToArray(),uv=uv.ToArray()};mesh=SaveMesh(mesh,"Cascade_"+index);MeshObject("Flowing_Cascade",parent,mesh,water);
        foreach(var end in new[]{source,foot,bottom})
        {
            var pool=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pool.name="Stream_Pool";pool.transform.SetParent(parent,false);pool.transform.position=end+Vector3.up*.65f;pool.transform.localScale=new Vector3(23,.15f,29);pool.GetComponent<Renderer>().sharedMaterial=water;Object.DestroyImmediate(pool.GetComponent<Collider>());
        }
        var pebble=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BachDangRuralLandscape/003_Shared_River_Stone.asset");
        // A grounded rocky spring shelf supports the source pool; the falling sheet clears
        // the cliff face before meeting a winding stream, rather than painting water on grass.
        var cliff=MeshObject("Spring_Cliff",parent,pebble,stone);cliff.position=top;cliff.localScale=new Vector3(83,82,80);
        var shelf=GameObject.CreatePrimitive(PrimitiveType.Cylinder);shelf.name="Rock_Spring_Ledge";shelf.transform.SetParent(parent,false);shelf.transform.position=source-Vector3.up*1.4f;shelf.transform.localScale=new Vector3(32,1.2f,39);shelf.GetComponent<Renderer>().sharedMaterial=stone;Object.DestroyImmediate(shelf.GetComponent<Collider>());
        for(int i=0;i<34;i++)
        {
            var p=Vector3.Lerp(top,bottom,i/33f)+side*((i%2==0?-1:1)*(9+i%4));p.y=Ground(p);
            var t=MeshObject("Wet_River_Rock",parent,pebble,stone);t.position=p;t.localScale=i<10?new Vector3(20+i%5,42-i*2,25+i%7):new Vector3(13+i%5,13+i%4,15+i%7);t.rotation=Quaternion.Euler(0,i*73,0);
        }
        var mist=Group("Cascade_Spray",parent,foot+Vector3.up*3).gameObject.AddComponent<ParticleSystem>();var main=mist.main;main.startLifetime=1.6f;main.startSpeed=3;main.startSize=5;main.startColor=new Color(.8f,.92f,.9f,.3f);main.maxParticles=30;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=mist.emission;emission.rateOverTime=12;var shape=mist.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=55;shape.radius=5;
        mist.GetComponent<ParticleSystemRenderer>().sharedMaterial=LoadMat("Assets/Materials/Effects/mat_fx_smoke_01.mat");
        var audio=parent.gameObject.AddComponent<AudioSource>();audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/BeachBoatStory/Audio/River_Water_Loop.wav");audio.loop=true;audio.playOnAwake=true;audio.spatialBlend=1;audio.volume=.45f;audio.minDistance=25;audio.maxDistance=260;audio.rolloffMode=AudioRolloffMode.Linear;
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");if(GameObject.Find(RootName)!=null)throw new Exception("Whole map expansion already exists");
        terrain=Terrain.activeTerrain;high=Object.FindAnyObjectByType<TideSystem>().highTideY;
        Directory.CreateDirectory(".utmp/WholeMap");EditorSceneManager.SaveScene(scene,".utmp/WholeMap/before.unity",true);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/BeachBoatStory","WholeMap");
        timber=LoadMat("Assets/BeachBoatStory/Camp/Barracks_Timber.mat");beams=LoadMat("Assets/BeachBoatStory/Camp/Barracks_Dark_Beams.mat");roof=LoadMat("Assets/BeachBoatStory/Camp/Camp_Shingle_Roof.mat");earth=LoadMat("Assets/BachDangRuralLandscape/001_Village_Natural_Earth.mat");crop=LoadMat("Assets/BachDangRuralLandscape/Rice_Leaf.mat");stone=LoadMat("Assets/BachDangRuralLandscape/River_Pebble.mat");
        water=new Material(Shader.Find("BachDang/Cascade")){name="Cascade_Water"};AssetDatabase.CreateAsset(water,Folder+"/Cascade_Water.mat");
        foreach(var tree in terrain.terrainData.treeInstances)
        {
            var p=terrain.transform.position+Vector3.Scale(tree.position,terrain.terrainData.size);var cell=new Vector2Int(Mathf.FloorToInt(p.x/128),Mathf.FloorToInt(p.z/128));
            if(!treeCells.TryGetValue(cell,out var list))treeCells[cell]=list=new List<Vector3>();list.Add(p);
        }
        occupied.AddRange(Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.GetComponentInParent<Animator>()==null&&r.bounds.size.y>20&&r.bounds.size.x<600).Select(r=>r.bounds));
        FixStalls();FixShoreGate();var root=Group(RootName,null,Vector3.zero);
        var survey=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Logs/WholeMapSurvey.json"));
        var candidates=survey["candidates"].Select(t=>new Vector3((float)t["position"]["x"],(float)t["position"]["y"],(float)t["position"]["z"])).Where(p=>Clear(p,100,5)).ToList();
        var selected=new List<Vector3>();
        // Spread inhabited clearings through every quadrant, with extra density around the
        // river campaign. Never create a house in floodwater or remove existing forest.
        foreach(var p in candidates.OrderBy(p=>QuestRowboat.FlatDistance(p,new Vector3(-3500,0,0))))
        {
            float separation=QuestRowboat.FlatDistance(p,new Vector3(-3500,0,0))<10000?1500:2300;
            if(selected.Any(s=>QuestRowboat.FlatDistance(s,p)<separation))continue;
            selected.Add(p);Region(root,p,selected.Count);if(selected.Count>=64)break;
        }
        var falls=new List<Vector3>();
        foreach(var slope in survey["slopes"].OrderBy(t=>Mathf.Abs((float)t["top"]["x"])+(Mathf.Abs((float)t["top"]["z"]))))
        {
            var top=new Vector3((float)slope["top"]["x"],(float)slope["top"]["y"],(float)slope["top"]["z"]);
            var bottom=new Vector3((float)slope["bottom"]["x"],(float)slope["bottom"]["y"],(float)slope["bottom"]["z"]);
            if(top.y<high+60||top.y>900||falls.Any(p=>QuestRowboat.FlatDistance(p,top)<5000))continue;
            bool descending=true;float last=top.y;
            for(int i=1;i<=24;i++){float y=Ground(Vector3.Lerp(top,bottom,i/24f));if(y>last+.5f){descending=false;break;}last=y;}
            if(!descending)continue;Waterfall(root,top,bottom,falls.Count+1);falls.Add(top);if(falls.Count==4)break;
        }
        if(root.childCount<25||falls.Count<3||people<100)throw new Exception("Insufficient whole-map coverage: "+root.childCount+" regions, "+falls.Count+" waterfalls, "+people+" residents");
        var streamer=Object.FindAnyObjectByType<MapZoneStreamer>();streamer.zones=streamer.zones.Concat(root.Cast<Transform>().Select(t=>new MapZoneStreamer.Zone{content=t.gameObject,center=t.position,visibleDistance=t.name.StartsWith("Thac_")?2600:6500})).ToArray();EditorUtility.SetDirty(streamer);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/WholeMapBuild.json",JsonConvert.SerializeObject(new{regions=root.childCount-falls.Count,houses,people,animals,stalls,waterfalls=falls.Select(Point),districts=root.Cast<Transform>().Select(t=>new{t.name,position=Point(t.position),people=t.GetComponentsInChildren<NPCWander>(true).Length})},Formatting.Indented));EditorApplication.Exit(0);
    }
    public static void PolishAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");var root=GameObject.Find(RootName).transform;terrain=Terrain.activeTerrain;
        foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=LoadMat("Assets/Materials/Effects/mat_fx_smoke_01.mat");
        // New remote residents share four native prefabs instead of serializing hundreds
        // of copies of their full skeletons in this already large terrain scene.
        var budget=Object.FindAnyObjectByType<AmbientPopulationBudget>();
        var actors=root.GetComponentsInChildren<NPCWander>(true);
        foreach(var group in actors.GroupBy(a=>a.GetComponent<Animator>().avatar.name+"_"+(a.GetComponent<NPCAmbientDialogue>().role==NPCAmbientDialogue.Role.Guard?"Guard":"Civilian")))
        {
            var source=Object.Instantiate(group.First().gameObject);source.name="Regional_"+group.Key;
            foreach(var t in source.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Carried_Work_Supplies").ToArray())Object.DestroyImmediate(t.gameObject);
            var w=source.GetComponent<NPCWander>();w.waypoints=Array.Empty<Transform>();source.transform.position=Vector3.zero;
            var prefab=PrefabUtility.SaveAsPrefabAsset(source,Folder+"/"+source.name+".prefab");Object.DestroyImmediate(source);
            foreach(var old in group.ToArray())
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,old.transform.parent);go.name=old.name;go.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);go.transform.localScale=old.transform.localScale;
                var wander=go.GetComponent<NPCWander>();EditorUtility.CopySerialized(old,wander);wander.animator=go.GetComponent<Animator>();
                var talk=go.GetComponent<NPCAmbientDialogue>();EditorUtility.CopySerialized(old.GetComponent<NPCAmbientDialogue>(),talk);talk.dailyLife=wander;
                for(int i=0;i<budget.citizens.Length;i++)if(budget.citizens[i]==old)budget.citizens[i]=wander;
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(wander);PrefabUtility.RecordPrefabInstancePropertyModifications(talk);
                Object.DestroyImmediate(old.gameObject);
            }
        }
        EditorUtility.SetDirty(budget);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    static void YardProp(string asset,Transform parent,Vector3 p,float height,float yaw)
    {
        p.y=Ground(p);var go=BachDangSettlementAssets.Place("Assets/Prefabs/Props/pf_"+asset+".prefab","Household_"+asset,parent,p,yaw);
        var b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.localScale*=height/b.size.y;b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.position+=new Vector3(p.x-b.center.x,p.y-b.min.y,p.z-b.center.z);
        foreach(var collider in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
        var lod=go.GetComponent<LODGroup>();if(lod==null)lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.02f,go.GetComponentsInChildren<Renderer>(true))});lod.RecalculateBounds();
    }
    static bool RouteNear(Transform region,Vector3 p,float radius)
    {
        foreach(var npc in region.GetComponentsInChildren<NPCWander>(true))
        for(int i=0;i<npc.waypoints.Length;i++)
        {
            var a=npc.waypoints[i].position;var b=npc.waypoints[(i+1)%npc.waypoints.Length].position;var d=b-a;d.y=0;var offset=p-a;offset.y=0;
            float t=Mathf.Clamp01(Vector3.Dot(offset,d)/Mathf.Max(.01f,d.sqrMagnitude));if((offset-d*t).sqrMagnitude<radius*radius)return true;
        }
        return false;
    }
    public static void DressAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");terrain=Terrain.activeTerrain;var root=GameObject.Find(RootName).transform;
        high=Object.FindAnyObjectByType<TideSystem>().highTideY;
        if(root.Cast<Transform>().Any(r=>r.Find("Domestic_Yards")!=null))throw new Exception("Domestic yards already installed");
        foreach(var path in AssetDatabase.FindAssets("",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(p),@"^\d{3}_(Village_Natural_|Village_Footpath_|Communal_Square)")))AssetDatabase.DeleteAsset(path);
        var shared=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Shared_Stall_Roof.asset");
        var front=shared.vertices.Take(6).ToArray();shared.vertices=front.Concat(front).ToArray();shared.uv=shared.uv.Take(6).Concat(shared.uv.Take(6)).ToArray();
        shared.triangles=new[]{0,2,1,1,2,3,2,4,3,3,4,5,7,8,6,9,8,7,9,10,8,11,10,9};shared.RecalculateNormals();shared.RecalculateBounds();EditorUtility.SetDirty(shared);
        timber=LoadMat("Assets/BeachBoatStory/Camp/Barracks_Timber.mat");beams=LoadMat("Assets/BeachBoatStory/Camp/Barracks_Dark_Beams.mat");stone=LoadMat("Assets/BachDangRuralLandscape/River_Pebble.mat");
        var generated=new List<string>();var painter=new BachDangVillageGround(terrain,7,Folder,generated);int paths=0,props=0;
        foreach(var region in root.Cast<Transform>().Where(t=>!t.name.StartsWith("Thac_")))
        {
            if(region.Find("Domestic_Yards")!=null)throw new Exception("Already dressed "+region.name);
            var details=Group("Domestic_Yards",region,region.position);int index=0;
            foreach(var house in region.Cast<Transform>().Where(t=>t.name.StartsWith("Regional_House_")).ToArray())
            {
                var bounds=BeachBoatWorldAudit.BoundsOf(house);var direction=house.position-region.position;direction.y=0;direction.Normalize();var side=Vector3.Cross(Vector3.up,direction);
                float distance=QuestRowboat.FlatDistance(house.position,region.position);
                var end=house.position-direction*(Mathf.Max(bounds.extents.x,bounds.extents.z)+8);var start=region.position+direction*8;
                painter.CreatePath(details,"Village_Footpath_"+index,new[]{start,Vector3.Lerp(start,end,.5f)+side*8,end},1.9f,painter.Earth,index,new List<Bounds>());paths++;
                var yard=house.position+side*(Mathf.Max(bounds.extents.x,bounds.extents.z)+13);
                foreach(var item in new[]{("barrels_02",8f,0f),("logpile_01",6f,20f),("buckets_01",5f,-15f)})
                {
                    var p=yard+direction*item.Item3;p.y=Ground(p);
                    if(p.y<high+5||Mathf.Abs(p.y-house.position.y)>3||RouteNear(region,p,15))continue;
                    var b=new Bounds(p+Vector3.up*8,new Vector3(18,16,18));if(root.GetComponentsInChildren<BoxCollider>(true).Any(c=>c.bounds.Intersects(b)))continue;
                    YardProp(item.Item1,details,p,item.Item2,index*31);props++;
                }
                // Short rear fences define household yards, leaving the communal lane open.
                var back=house.position+direction*(Mathf.Max(bounds.extents.x,bounds.extents.z)+18);
                if(!RouteNear(region,back,18)&&Mathf.Abs(Ground(back)-house.position.y)<2)
                {YardProp("fence_01_double",details,back,10,index*360f/7);props++;}
                index++;
            }
            // A shallow communal stone well, away from the authored pedestrian routes.
            var well=region.position+new Vector3(-28,0,20);well.y=Ground(well);
            if(!RouteNear(region,well,15))
            {
                for(int i=0;i<16;i++)
                {
                    var p=well+Quaternion.Euler(0,i*22.5f,0)*Vector3.forward*6;
                    var block=Box("Well_Stone_Rim",details,p+Vector3.up*3,new Vector3(2.8f,6,2),stone);block.transform.rotation=Quaternion.Euler(0,i*22.5f,0);
                }
                painter.CreatePatch(details,"Communal_Square",well,0,6,5,painter.Pebbles,19,new List<Bounds>());
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText("Logs/WholeMapDressing.json",JsonConvert.SerializeObject(new{paths,props},Formatting.Indented));EditorApplication.Exit(0);
    }
    public static void LandscapeAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");terrain=Terrain.activeTerrain;high=Object.FindAnyObjectByType<TideSystem>().highTideY;var root=GameObject.Find(RootName).transform;
        stone=LoadMat("Assets/BachDangRuralLandscape/River_Pebble.mat");water=LoadMat(Folder+"/Cascade_Water.mat");
        var trees=terrain.terrainData.treeInstances.Select(t=>terrain.transform.position+Vector3.Scale(t.position,terrain.terrainData.size)).ToArray();
        var streamer=Object.FindAnyObjectByType<MapZoneStreamer>();int fallIndex=0,planted=0;
        foreach(var old in root.Cast<Transform>().Where(t=>t.name.StartsWith("Thac_")).ToArray())
        {
            var top=old.position;var bounds=old.Find("Flowing_Cascade").GetComponent<Renderer>().bounds;
            var oldMesh=old.Find("Flowing_Cascade").GetComponent<MeshFilter>().sharedMesh;var oldVertices=oldMesh.vertices;
            var bottom=old.position+(oldVertices[oldVertices.Length-2]+oldVertices[oldVertices.Length-1])*.5f;
            var direction=bottom-top;direction.y=0;direction.Normalize();var side=Vector3.Cross(Vector3.up,direction);
            Vector3 best=top;int bestCount=int.MaxValue;
            for(int i=-3;i<=3;i++)
            {
                var start=top+side*(i*35);var end=bottom+side*(i*35);var delta=end-start;delta.y=0;
                int count=trees.Count(t=>{var offset=t-start;offset.y=0;float along=Mathf.Clamp01(Vector3.Dot(offset,delta)/delta.sqrMagnitude);return(offset-delta*along).sqrMagnitude<16*16;});
                if(count<bestCount){best=start;bestCount=count;}
            }
            var shift=best-top;top=best;top.y=Ground(top);bottom+=shift;bottom.y=Ground(bottom);
            var zone=streamer.zones.First(z=>z.content==old.gameObject);Object.DestroyImmediate(old.gameObject);
            Waterfall(root,top,bottom,++fallIndex);var replacement=root.Find("Thac_Suoi_"+fallIndex);zone.content=replacement.gameObject;zone.center=top;
        }
        var prototypes=terrain.terrainData.treePrototypes.Where(t=>t.prefab!=null).Take(3).ToArray();
        foreach(var region in root.Cast<Transform>().Where(t=>!t.name.StartsWith("Thac_")))
        {
            if(region.Find("Shade_Trees")!=null)throw new Exception("Shade trees already installed");var grove=Group("Shade_Trees",region,region.position);
            for(int i=0;i<12;i++)
            {
                var p=region.position+Quaternion.Euler(0,i*137.5f,0)*Vector3.forward*(285+i%3*35);p.y=Ground(p);
                if(p.y<high+4||trees.Any(t=>QuestRowboat.FlatDistance(t,p)<35)||Mathf.Abs(Ground(p+Vector3.forward*12)-p.y)>4)continue;
                var regionHouses=region.Cast<Transform>().Where(t=>t.name.StartsWith("Regional_House_")).ToArray();
                if(regionHouses.Any(h=>QuestRowboat.FlatDistance(h.position,p)<90))continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prototypes[i%prototypes.Length].prefab,grove);go.name="Village_Shade_Tree";go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,i*73,0));
                var b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.localScale*= (75+i%4*8)/b.size.y;b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.position+=new Vector3(p.x-b.center.x,p.y-b.min.y,p.z-b.center.z);
                foreach(var collider in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);planted++;
            }
        }
        var gate=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsSortMode.None).First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.RiverGate);
        var guard=gate.transform.Find("Riverside_Guard_Post");guard.localRotation=Quaternion.Euler(0,90,0);
        var arrival=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsSortMode.None).First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.CampRiverGate).destinationTarget;
        var landing=gate.transform.position+guard.forward*30;landing.y=Ground(landing)+3.2f;
        arrival.SetPositionAndRotation(landing,Quaternion.LookRotation(guard.forward));
        var details=Group("Dock_Checkpoint_Yard",gate.transform,gate.transform.position);
        var generated=new List<string>();var painter=new BachDangVillageGround(terrain,7,Folder,generated);
        painter.CreatePath(details,"Pier_To_Guard_Post",new[]{new Vector3(-3675,0,1880),new Vector3(-3770,0,1880),gate.transform.position,new Vector3(-3910,0,1920)},2.1f,painter.Earth,900,new List<Bounds>());
        YardProp("barrels_02",details,gate.transform.position+new Vector3(-28,0,-40),9,10);YardProp("logpile_01",details,gate.transform.position+new Vector3(-35,0,48),8,80);
        int guards=BeachBoatBusyWorld.PopulateRegion(details,gate.transform.position+Vector3.left*40,2,true);
        streamer.zones=streamer.zones.Concat(new[]{new MapZoneStreamer.Zone{content=details.gameObject,center=details.position,visibleDistance=1800}}).ToArray();
        EditorUtility.SetDirty(streamer);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/WholeMapLandscape.json",JsonConvert.SerializeObject(new{planted,guards,waterfalls=fallIndex},Formatting.Indented));EditorApplication.Exit(0);
    }
    static void NaturalRock(Transform parent,Vector3 p,float height,float yaw,int variant)
    {
        p.y=Ground(p);string file=variant%2==0?"Rock_C_01":"Rock_A_02";
        var go=BachDangSettlementAssets.Place("Assets/Visual Design Cafe/Nature Renderer Demo/Realistic/Art/Rocks/"+file+".prefab","Natural_Cascade_Rock",parent,p,yaw);
        var b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.localScale*=height/b.size.y;b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.position+=new Vector3(p.x-b.center.x,p.y-b.min.y,p.z-b.center.z);
        foreach(var collider in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
    }
    public static void NaturalFallsAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");terrain=Terrain.activeTerrain;var root=GameObject.Find(RootName).transform;
        foreach(var fall in root.Cast<Transform>().Where(t=>t.name.StartsWith("Thac_")))
        {
            foreach(var old in fall.Cast<Transform>().Where(t=>t.name=="Spring_Cliff"||t.name=="Rock_Spring_Ledge"||t.name=="Wet_River_Rock").ToArray())Object.DestroyImmediate(old.gameObject);
            var mesh=fall.Find("Flowing_Cascade").GetComponent<MeshFilter>().sharedMesh;var v=mesh.vertices;var bottom=fall.position+(v[v.Length-2]+v[v.Length-1])*.5f;var direction=bottom-fall.position;direction.y=0;direction.Normalize();var side=Vector3.Cross(Vector3.up,direction);
            NaturalRock(fall,fall.position-side*16-direction*5,43,35,0);
            NaturalRock(fall,fall.position+side*16-direction*4,41,127,1);
            NaturalRock(fall,fall.position-direction*22,48,216,0);
            var foot=fall.position+direction*52;
            for(int i=0;i<18;i++)
            {
                float t=i/17f;var p=Vector3.Lerp(foot,bottom,t)+side*((i%2==0?-1:1)*(8+i%4*2));
                NaturalRock(fall,p,7+i%5*1.7f,i*137.5f,i);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    public static void BalanceGrovesAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");terrain=Terrain.activeTerrain;var root=GameObject.Find(RootName).transform;
        bool Rock(string path)=>System.Text.RegularExpressions.Regex.IsMatch(path,"rock|boulder|cliff|mountain",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        bool RockModel(GameObject model)
        {
            var original=PrefabUtility.GetCorrespondingObjectFromOriginalSource(model);
            return Rock(model.name)||Rock(original!=null?AssetDatabase.GetAssetPath(original):AssetDatabase.GetAssetPath(model))||
                model.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh!=null&&Rock(f.sharedMesh.name));
        }
        var tree=terrain.terrainData.treePrototypes.Select(p=>p.prefab).First(p=>p!=null&&!RockModel(p));
        int stones=0,replaced=0;
        foreach(var grove in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Shade_Trees").ToArray())
        foreach(var old in grove.Cast<Transform>().ToArray())
        {
            if(!RockModel(old.gameObject))continue;
            var p=old.position;var before=BeachBoatWorldAudit.BoundsOf(old);p.x=before.center.x;p.z=before.center.z;p.y=Ground(p);
            Transform item;float height;
            if(stones++%3==0){item=old;height=10+stones%4*2;}
            else
            {
                var rotation=old.rotation;Object.DestroyImmediate(old.gameObject);var go=(GameObject)PrefabUtility.InstantiatePrefab(tree,grove);go.name="Village_Shade_Tree";item=go.transform;item.SetPositionAndRotation(p,rotation);height=72+stones%4*6;replaced++;
                foreach(var collider in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            }
            var bounds=BeachBoatWorldAudit.BoundsOf(item);item.localScale*=height/bounds.size.y;bounds=BeachBoatWorldAudit.BoundsOf(item);item.position+=new Vector3(p.x-bounds.center.x,p.y-bounds.min.y,p.z-bounds.center.z);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/WholeMapGroveBalance.json",JsonConvert.SerializeObject(new{treeSource=tree.name,replaced,smallStones=(stones+2)/3},Formatting.Indented));EditorApplication.Exit(0);
    }
    public static void ClearFieldTreesAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");var root=GameObject.Find(RootName).transform;int removed=0;
        foreach(var region in root.Cast<Transform>().Where(t=>!t.name.StartsWith("Thac_")))
        {
            var fields=region.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Cultivated_Field").Select(r=>r.bounds).ToArray();var grove=region.Find("Shade_Trees");
            foreach(var tree in grove.Cast<Transform>().ToArray())
            {
                var p=BeachBoatWorldAudit.BoundsOf(tree).center;
                if(!fields.Any(b=>p.x>b.min.x-18&&p.x<b.max.x+18&&p.z>b.min.z-18&&p.z<b.max.z+18))continue;
                Object.DestroyImmediate(tree.gameObject);removed++;
            }
        }
        int remaining=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Shade_Trees").Sum(t=>t.childCount);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("Logs/WholeMapFieldClearance.json",JsonConvert.SerializeObject(new{removed,remaining},Formatting.Indented));EditorApplication.Exit(0);
    }
    static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
    static object Point(Vector3 p)=>new{x=p.x,y=p.y,z=p.z};
    public static void AuditAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");terrain=Terrain.activeTerrain;
        float high=Object.FindAnyObjectByType<TideSystem>().highTideY;
        var towns=GameObject.Find("BeachBoat_Busy_Settlements").transform.Cast<Transform>().Select(t=>t.position).ToArray();
        var candidates=new List<object>();var slopes=new List<object>();
        for(float x=-19500;x<=19500;x+=1300)for(float z=-19500;z<=19500;z+=1300)
        {
            var p=new Vector3(x,0,z);p.y=Ground(p);if(p.y<high+6)continue;
            float[] heights={Ground(p+Vector3.right*180),Ground(p+Vector3.left*180),Ground(p+Vector3.forward*180),Ground(p+Vector3.back*180)};
            float nearest=towns.Min(t=>QuestRowboat.FlatDistance(p,t));
            if(heights.Max()-heights.Min()<22&&nearest>750)candidates.Add(new{position=Point(p),heightRange=heights.Max()-heights.Min(),nearestTown=nearest});
            for(int i=0;i<8;i++)
            {
                var lower=p+Quaternion.Euler(0,i*45,0)*Vector3.forward*180;lower.y=Ground(lower);
                float drop=p.y-lower.y;if(drop<30||drop>180||lower.y<high+3)continue;
                slopes.Add(new{top=Point(p),bottom=Point(lower),drop});break;
            }
        }
        var gates=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var boat=Object.FindAnyObjectByType<QuestRowboat>();
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WholeMapSurvey.json",JsonConvert.SerializeObject(new{
            terrain=Point(terrain.transform.position),size=Point(terrain.terrainData.size),high,
            candidates,slopes,gates=gates.Select(g=>new{g.name,type=g.gateType.ToString(),position=Point(g.transform.position),visuals=g.GetComponentsInChildren<Renderer>(true).Select(r=>new{r.name,size=Point(r.bounds.size),center=Point(r.bounds.center)})}),
            boarding=Point(boat.boardingPoint.position),dock=Point(boat.dockPoint.position),
            canopies=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name=="Market_Canopy").Select(t=>new{position=Point(t.position),bounds=Point(BeachBoatWorldAudit.BoundsOf(t).size)}).Take(6)
        },Formatting.Indented));EditorApplication.Exit(0);
    }
}
