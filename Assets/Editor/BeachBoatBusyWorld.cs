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

/// <summary>BeachBoat-only civilian work clusters, clear walking routes and corrected dorm roofs.</summary>
public static class BeachBoatBusyWorld
{
    const string RootName="BeachBoat_Busy_Settlements",Folder="Assets/BeachBoatStory/Camp/";
    static Terrain terrain;
    static Transform root;
    static readonly List<Vector3> yards=new List<Vector3>();
    static readonly List<NPCWander> citizens=new List<NPCWander>();
    static Vector3[] questKeepouts;
    static Bounds[] buildings;
    static GameObject[] civilianSources;
    static GameObject soldierSource;
    static float highWater;
    static bool expanding;
    static readonly Dictionary<Vector2Int,List<Vector3>> treeCells=new Dictionary<Vector2Int,List<Vector3>>();
    static readonly List<Bounds> occupiedDetails=new List<Bounds>();
    static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
    static Transform Group(string name,Transform parent,Vector3 position)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=position;return t;}
    static bool Free(Vector3 p,float radius=7)
    {
        p.y=Ground(p);
        var local=p-terrain.transform.position;var size=terrain.terrainData.size;
        if(local.x<0||local.z<0||local.x>size.x||local.z>size.z||p.y<highWater+3)return false;
        if(buildings.Any(b=>p.x>b.min.x-radius&&p.x<b.max.x+radius&&p.z>b.min.z-radius&&p.z<b.max.z+radius))return false;
        if(expanding)
        {
            if(occupiedDetails.Any(b=>p.x>b.min.x-radius&&p.x<b.max.x+radius&&p.z>b.min.z-radius&&p.z<b.max.z+radius))return false;
            var cell=new Vector2Int(Mathf.FloorToInt(p.x/64),Mathf.FloorToInt(p.z/64));
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                if(treeCells.TryGetValue(cell+new Vector2Int(x,z),out var trees)&&trees.Any(t=>QuestRowboat.FlatDistance(p,t)<radius+7))return false;
        }
        foreach(var hit in Physics.OverlapCapsule(p+Vector3.up*6,p+Vector3.up*23,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            if(!(hit is TerrainCollider)&&hit.GetComponentInParent<Animator>()==null)return false;
        return true;
    }
    static bool RouteClear(Vector3 a,Vector3 b)
    {
        int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/5);
        for(int i=0;i<=steps;i++)if(!Free(Vector3.Lerp(a,b,i/(float)Mathf.Max(1,steps)),5))return false;
        return true;
    }
    static GameObject Prop(string asset,string name,Transform parent,Vector3 position,float height,float yaw=0)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/pf_"+asset+".prefab");
        if(source==null)throw new InvalidOperationException("Missing workyard asset "+asset);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);go.name=name;
        go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));go.transform.localScale=Vector3.one;
        var b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.localScale*=height/b.size.y;
        b=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.position+=Vector3.up*(Ground(position)+.15f-b.min.y);
        foreach(var c in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        var rr=go.GetComponentsInChildren<Renderer>();foreach(var r in rr){r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
        var lod=go.GetComponent<LODGroup>();if(lod==null)lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.018f,rr)});lod.RecalculateBounds();
        if(expanding)occupiedDetails.Add(BeachBoatWorldAudit.BoundsOf(go.transform));
        return go;
    }
    static void Cargo(Transform actor,bool fish)
    {
        // A small shared prop follows the hands; no rigidbody or collision on carried cargo.
        var a=actor.GetComponent<Animator>();var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/pf_"+(fish?"bucket_01":"plankpile_01")+".prefab");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source,hand);go.name="Carried_Work_Supplies";
        var scale=hand.lossyScale;go.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        var bounds=BeachBoatWorldAudit.BoundsOf(go.transform);go.transform.localScale*=3.5f/Mathf.Max(.1f,bounds.size.y);
        go.transform.position=hand.position+Vector3.down*2;
        foreach(var c in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
    }
    static bool Person(Transform district,Vector3 center,int index,NPCAmbientDialogue.Role role,bool soldier)
    {
        Vector3[] route=null;
        for(int attempt=0;attempt<24;attempt++)
        {
            float angle=attempt*137.5f+index*72;var delta=Quaternion.Euler(0,angle,0)*Vector3.forward*(22+attempt*.8f);
            var a=center+delta;var b=a+Quaternion.Euler(0,angle,0)*new Vector3(16,0,20);var c=a+Quaternion.Euler(0,angle,0)*new Vector3(-10,0,30);
            if(!RouteClear(a,b)||!RouteClear(b,c)||!RouteClear(c,a))continue;
            route=new[]{a,b,c};break;
        }
        if(route==null)return false;
        bool military=soldier||role==NPCAmbientDialogue.Role.Guard;
        var source=military?soldierSource:civilianSources[index%civilianSources.Length];
        var actor=UnityEngine.Object.Instantiate(source,district);actor.name="Ambient_"+role+"_"+citizens.Count.ToString("00");
        foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))
            if(!(b is NPCWander)&&!(b is NPCAmbientDialogue)&&!(b is NPCUprightPosture))UnityEngine.Object.DestroyImmediate(b);
        foreach(var c in actor.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var body in actor.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(body);
        actor.transform.localScale=source.transform.lossyScale*(1+((index%5)-2)*.018f);actor.tag="Untagged";
        var animator=actor.GetComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullCompletely;
        var wander=actor.GetComponent<NPCWander>();if(wander==null)wander=actor.AddComponent<NPCWander>();wander.enabled=true;wander.animator=animator;
        var stops=Group("Daily_Route_"+actor.name,district,Vector3.zero);
        wander.waypoints=route.Select((p,i)=>{p.y=Ground(p)+3.2f;return Group("Stop_"+i,stops,p);}).ToArray();
        wander.autoGenerateWaypoints=false;wander.followWaypointsInOrder=true;wander.waypointWaitSeconds=new[]{3f+index%4,5f+index%5,2f};
        wander.activityLabels=role==NPCAmbientDialogue.Role.Fisher?new[]{"Đang mang giỏ ra bến cá","Đang phân loại mẻ cá","Đang trở về quầy cá"}:
            role==NPCAmbientDialogue.Role.Merchant?new[]{"Đang dọn hàng ra chợ","Đang kiểm hàng và trao đổi","Đang chuyển hàng về quầy"}:
            role==NPCAmbientDialogue.Role.Farmer?new[]{"Đang chuẩn bị nông cụ","Đang kiểm tra rau và thóc","Đang mang nông cụ về sân"}:
            military?new[]{"Đang chuyển vật tư trong trại","Đang kiểm tra quân nhu","Đang tuần tra sân trại"}:new[]{"Đang mang hàng ra sân","Đang chuẩn bị vật tư","Đang trở về kho"};
        wander.walkSpeed=soldier?7:6;wander.minIdleTime=1;wander.maxIdleTime=3;wander.sitLayChance=0;
        actor.transform.SetPositionAndRotation(wander.waypoints[0].position,Quaternion.LookRotation(route[1]-route[0]));
        var talk=actor.GetComponent<NPCAmbientDialogue>();if(talk==null)talk=actor.AddComponent<NPCAmbientDialogue>();talk.role=role;talk.displayName=role==NPCAmbientDialogue.Role.Guard?"Lính gác":soldier?"Lính tiếp vận":role==NPCAmbientDialogue.Role.Fisher?"Ngư dân":role==NPCAmbientDialogue.Role.Farmer?"Người làm ruộng":role==NPCAmbientDialogue.Role.Porter?"Người chuyển hàng":"Người trong chợ";talk.dailyLife=wander;talk.interactDistance=10;
        if(index%3==0&&role!=NPCAmbientDialogue.Role.Guard)Cargo(actor.transform,role==NPCAmbientDialogue.Role.Fisher);
        citizens.Add(wander);return true;
    }
    // Reuse the same source rigs and clear-route rules for remote settlements.
    public static int PopulateRegion(Transform district, Vector3 center, int count, bool military)
    {
        terrain=Terrain.activeTerrain;highWater=UnityEngine.Object.FindAnyObjectByType<TideSystem>().highTideY;
        if(civilianSources==null)
        {
            var all=UnityEngine.Object.FindObjectsByType<NPCWander>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            civilianSources=all.Where(w=>w.GetComponent<Animator>()!=null&&AssetDatabase.GetAssetPath(w.GetComponent<Animator>().avatar).Contains("Pea")).GroupBy(w=>w.GetComponent<Animator>().avatar).Select(g=>g.First().gameObject).Take(3).ToArray();
            soldierSource=GameObject.Find("Camp_Worker_01");
        }
        buildings=Array.Empty<Bounds>();expanding=false;
        var budget=UnityEngine.Object.FindAnyObjectByType<AmbientPopulationBudget>();
        citizens.Clear();citizens.AddRange(budget.citizens);int initial=citizens.Count;
        for(int i=0;i<count;i++)
        {
            var p=center+Quaternion.Euler(0,i*360f/count,0)*Vector3.forward*65;
            Person(district,p,i,military?NPCAmbientDialogue.Role.Guard:i%3==0?NPCAmbientDialogue.Role.Merchant:NPCAmbientDialogue.Role.Farmer,military);
        }
        budget.citizens=citizens.ToArray();EditorUtility.SetDirty(budget);return citizens.Count-initial;
    }
    static void District(string name,Vector3 center,int count,float radius,NPCAmbientDialogue.Role role,bool soldiers=false)
    {
        var district=root.Find(name);if(district==null)district=Group(name,root,center);
        var random=new System.Random(name.Length*93+(int)center.z);int built=0;
        int firstYard=district.Cast<Transform>().Count(t=>t.name.StartsWith("Workyard_"));
        for(int attempt=0;attempt<1800&&built<count;attempt++)
        {
            float angle=(float)random.NextDouble()*Mathf.PI*2;float distance=40+(float)random.NextDouble()*radius;
            var p=center+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*distance;p.y=Ground(p);
            if(!Free(p,24)||yards.Any(y=>Vector3.Distance(p,y)<65)||questKeepouts.Any(q=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(q.x,q.z))<50))continue;
            // Keep the main parade avenue and gate approach clear.
            var origin=GameObject.Find("CampLayoutOrigin").transform.position;var d=p-origin;
            if(soldiers&&(Mathf.Abs(d.x)<35||Mathf.Abs(d.z-150)<25||Mathf.Abs(d.x)>290||d.z< -250||d.z>500))continue;
            if(!Person(district,p,built*2,role,soldiers))continue;
            Person(district,p,built*2+1,role,soldiers);
            var yard=Group("Workyard_"+(firstYard+built).ToString("00"),district,p);
            Prop(role==NPCAmbientDialogue.Role.Fisher?"barrel_fish_01":"barrels_02","Working_Stock",yard,p,11,built*37);
            Prop("plankpile_01","Low_Work_Bench",yard,p+new Vector3(11,0,6),4,built*37+90);
            Prop("buckets_01","Water_And_Baskets",yard,p+new Vector3(-10,0,9),6);
            if(built%2==0)Prop("shed_02","Market_Canopy",yard,p+new Vector3(0,0,-16),42,built%2*180);
            else Prop("logpile_01","Stored_Firewood",yard,p+new Vector3(0,0,-16),9,built*20);
            yards.Add(p);built++;
        }
        if(built<count/2)throw new InvalidOperationException("Too few clear workyards in "+name+": "+built);
        Debug.Log(name+": "+built+" workyards");
    }
    static void Roofs()
    {
        foreach(string name in new[]{"Nha_Nghi_Linh_Tay","Nha_Nghi_Linh_Dong"})
        {
            var house=GameObject.Find(name).transform;house.Find("Mái tranh trái").localRotation=Quaternion.Euler(-34,0,0);house.Find("Mái tranh phải").localRotation=Quaternion.Euler(34,0,0);
            var ridge=GameObject.CreatePrimitive(PrimitiveType.Cube);ridge.name="Corrected_Roof_Ridge";ridge.transform.SetParent(house,false);ridge.transform.localPosition=new Vector3(0,57.5f,0);ridge.transform.localScale=new Vector3(70,2.5f,3);
            ridge.GetComponent<Renderer>().sharedMaterial=house.Find("Vách sau").GetComponent<Renderer>().sharedMaterial;UnityEngine.Object.DestroyImmediate(ridge.GetComponent<Collider>());
        }
    }
    static bool DecorationFits(Transform prop,Transform district)
    {
        var bounds=BeachBoatWorldAudit.BoundsOf(prop);
        foreach(var hit in Physics.OverlapBox(bounds.center,bounds.extents+Vector3.one,Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            if(!(hit is TerrainCollider)&&hit.GetComponentInParent<Animator>()==null&&!hit.transform.IsChildOf(prop))return false;
        bounds.Expand(new Vector3(7,0,7));
        foreach(var actor in district.GetComponentsInChildren<NPCWander>(true))
        {
            var stops=actor.waypoints;
            for(int segment=1;segment<stops.Length;segment++)
            {
                var a=stops[segment-1].position;var b=stops[segment].position;
                int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/3));
                for(int i=0;i<=steps;i++)
                {
                    var p=Vector3.Lerp(a,b,i/(float)steps);
                    if(p.x>bounds.min.x&&p.x<bounds.max.x&&p.z>bounds.min.z&&p.z<bounds.max.z)return false;
                }
            }
        }
        return true;
    }
    static void PolishWorkyards(Transform settlements)
    {
        int relocated=0,removed=0;
        foreach(Transform district in settlements)
        foreach(var yard in district.Cast<Transform>().Where(t=>t.name.StartsWith("Workyard_")))
        foreach(var prop in yard.Cast<Transform>().ToArray())
        {
            if(prop.name=="Market_Canopy")
            {
                var old=BeachBoatWorldAudit.BoundsOf(prop);prop.localScale*=42/old.size.y;
                var resized=BeachBoatWorldAudit.BoundsOf(prop);prop.position+=Vector3.up*(Ground(prop.position)+.15f-resized.min.y);
            }
            if(DecorationFits(prop,district))continue;
            bool fitted=false;
            for(int attempt=0;attempt<32;attempt++)
            {
                var offset=Quaternion.Euler(0,attempt*137.5f,0)*Vector3.forward*(18+(attempt/8)*10);
                var p=yard.position+offset;p.y=Ground(p);
                if(p.y<highWater+3)continue;
                prop.position=p;var b=BeachBoatWorldAudit.BoundsOf(prop);prop.position+=Vector3.up*(Ground(p)+.15f-b.min.y);
                if(!DecorationFits(prop,district))continue;
                // Keep props in separate piles instead of allowing decoration to intersect.
                if(yard.Cast<Transform>().Any(t=>t!=prop&&BeachBoatWorldAudit.BoundsOf(t).Intersects(BeachBoatWorldAudit.BoundsOf(prop))))continue;
                fitted=true;relocated++;break;
            }
            if(!fitted){UnityEngine.Object.DestroyImmediate(prop.gameObject);removed++;}
        }
        Debug.Log("Workyard clearance: relocated "+relocated+", removed "+removed+" obstructing decorations");
    }
    public static void PolishAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");terrain=Terrain.activeTerrain;highWater=UnityEngine.Object.FindFirstObjectByType<TideSystem>().highTideY;
        PolishWorkyards(GameObject.Find(RootName).transform);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        if(GameObject.Find(RootName)!=null)throw new InvalidOperationException("Busy world already exists");
        Directory.CreateDirectory(".utmp/BusyWorld");EditorSceneManager.SaveScene(scene,".utmp/BusyWorld/before.unity",true);
        terrain=Terrain.activeTerrain;highWater=UnityEngine.Object.FindFirstObjectByType<TideSystem>().highTideY;
        Roofs();
        var all=UnityEngine.Object.FindObjectsByType<NPCWander>(FindObjectsSortMode.None);
        civilianSources=all.Where(w=>w.GetComponent<Animator>()!=null&&AssetDatabase.GetAssetPath(w.GetComponent<Animator>().avatar).Contains("Pea")).Select(w=>w.gameObject).ToArray();
        soldierSource=GameObject.Find("Camp_Worker_01");
        if(civilianSources.Length<3)throw new InvalidOperationException("Expected three civilian models");
        buildings=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Nha_Dan_")||t.name.StartsWith("Wooden_Barracks_")||t.name.StartsWith("Nha_Nghi_Linh_")||t.name.StartsWith("Barracks_Tent_")||t.name.StartsWith("Lan_Quan_"))
            .Where(t=>t.GetComponentsInChildren<Renderer>().Length>0).Select(BeachBoatWorldAudit.BoundsOf).ToArray();
        questKeepouts=UnityEngine.Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsSortMode.None).Select(g=>g.transform.position)
            .Concat(UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None).SelectMany(s=>new[]{s.transform.position}.Concat(s.checkpoints.Where(t=>t!=null).Select(t=>t.position))))
            .Concat(UnityEngine.Object.FindObjectsByType<BeachBoatStory.WoodCollectible>(FindObjectsSortMode.None).Select(w=>w.transform.position)).ToArray();
        root=Group(RootName,null,Vector3.zero);yards.Clear();citizens.Clear();Physics.SyncTransforms();
        District("Cho_Ben_Song",new Vector3(-6650,0,-4910),12,390,NPCAmbientDialogue.Role.Merchant);
        District("Xom_Chai",new Vector3(1260,0,525),7,400,NPCAmbientDialogue.Role.Fisher);
        District("Xom_Ruong_Vuon",new Vector3(-6405,0,7665),7,400,NPCAmbientDialogue.Role.Farmer);
        District("Doanh_Trai",GameObject.Find("CampLayoutOrigin").transform.position+Vector3.forward*60,5,260,NPCAmbientDialogue.Role.Porter,true);
        var dock=UnityEngine.Object.FindFirstObjectByType<QuestRowboat>().transform.position;
        var shore=dock;shore.x-=180;District("Ben_Tiep_Van",shore,4,200,NPCAmbientDialogue.Role.Porter);
        var budget=root.gameObject.AddComponent<AmbientPopulationBudget>();budget.citizens=citizens.ToArray();
        PolishWorkyards(root);
        var streamer=UnityEngine.Object.FindFirstObjectByType<MapZoneStreamer>();
        if(streamer!=null)streamer.zones=streamer.zones.Concat(root.Cast<Transform>().Select(t=>new MapZoneStreamer.Zone{content=t.gameObject,center=t.position,visibleDistance=1600})).ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BusyWorldBuild.json",JsonConvert.SerializeObject(new{workyards=yards.Count,people=citizens.Count,maximumAnimated=budget.maximumAnimated},Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void DensifyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        root=GameObject.Find(RootName).transform;
        if(root.Find("Ngo_Cho_Moc")!=null)throw new InvalidOperationException("The denser population is already installed.");
        Directory.CreateDirectory(".utmp/DenseWorld");EditorSceneManager.SaveScene(scene,".utmp/DenseWorld/before.unity",true);
        terrain=Terrain.activeTerrain;highWater=UnityEngine.Object.FindFirstObjectByType<TideSystem>().highTideY;
        var budget=root.GetComponent<AmbientPopulationBudget>();citizens.Clear();citizens.AddRange(budget.citizens);int original=citizens.Count;
        var all=UnityEngine.Object.FindObjectsByType<NPCWander>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        civilianSources=all.Where(w=>w.GetComponent<Animator>()!=null&&AssetDatabase.GetAssetPath(w.GetComponent<Animator>().avatar).Contains("Pea")).Select(w=>w.gameObject).Take(3).ToArray();
        soldierSource=GameObject.Find("Camp_Worker_01");
        if(civilianSources.Length<3||soldierSource==null)throw new InvalidOperationException("Existing textured civilian/soldier sources are missing.");
        buildings=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(t=>t.name.StartsWith("Nha_Dan_")||t.name.StartsWith("Wooden_Barracks_")||t.name.StartsWith("Nha_Nghi_Linh_")||t.name.StartsWith("Barracks_Tent_")||t.name.StartsWith("Lan_Quan_")||t.name=="Central_Supply_Store"||t.name=="Lo_Ren_Quan_Do"||t.name=="Kho_Khi_Gioi_Nho")
            .Where(t=>t.GetComponentsInChildren<Renderer>(true).Length>0).Select(BeachBoatWorldAudit.BoundsOf).ToArray();
        questKeepouts=UnityEngine.Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsSortMode.None).Select(g=>g.transform.position)
            .Concat(UnityEngine.Object.FindObjectsByType<BeachBoatStory.WoodCollectible>(FindObjectsSortMode.None).Select(w=>w.transform.position))
            .Concat(UnityEngine.Object.FindObjectsByType<VillageWorkStall>(FindObjectsSortMode.None).Select(s=>s.InteractionPosition))
            .Concat(UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None).SelectMany(s=>new[]{s.transform.position}.Concat(s.checkpoints.Where(t=>t!=null).Select(t=>t.position))))
            .Concat(new[]{UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.NPCQuestGiver>().transform.position}).ToArray();
        occupiedDetails.Clear();yards.Clear();treeCells.Clear();
        foreach(Transform district in root)foreach(var yard in district.Cast<Transform>().Where(t=>t.name.StartsWith("Workyard_")))
        {yards.Add(yard.position);foreach(Transform prop in yard)if(prop.GetComponentsInChildren<Renderer>().Length>0)occupiedDetails.Add(BeachBoatWorldAudit.BoundsOf(prop));}
        foreach(var tree in terrain.terrainData.treeInstances)
        {
            var p=terrain.transform.position+Vector3.Scale(tree.position,terrain.terrainData.size);
            var cell=new Vector2Int(Mathf.FloorToInt(p.x/64),Mathf.FloorToInt(p.z/64));
            if(!treeCells.TryGetValue(cell,out var list))treeCells[cell]=list=new List<Vector3>();list.Add(p);
        }
        expanding=true;Physics.SyncTransforms();
        District("Cho_Ben_Song",root.Find("Cho_Ben_Song").position,12,440,NPCAmbientDialogue.Role.Merchant);
        District("Xom_Chai",root.Find("Xom_Chai").position,8,420,NPCAmbientDialogue.Role.Fisher);
        District("Xom_Ruong_Vuon",root.Find("Xom_Ruong_Vuon").position,8,420,NPCAmbientDialogue.Role.Farmer);
        District("Doanh_Trai",root.Find("Doanh_Trai").position,8,280,NPCAmbientDialogue.Role.Porter,true);
        District("Ben_Tiep_Van",root.Find("Ben_Tiep_Van").position,5,240,NPCAmbientDialogue.Role.Porter);
        var stalls=UnityEngine.Object.FindObjectsByType<VillageWorkStall>(FindObjectsSortMode.None).OrderBy(s=>s.kind).ToArray();
        var names=new[]{"Ngo_Cho_Moc","Ngo_Cho_Day","Ngo_Cho_Thoc","Ngo_Cho_Luong"};
        for(int i=0;i<stalls.Length;i++)District(names[i],stalls[i].transform.position,5,180,NPCAmbientDialogue.Role.Merchant);
        var settlements=GameObject.Find("BachDang_Living_Settlements").transform;
        District("Trai_Bo_Dong_Dong_Duc",settlements.Find("06_Trai_Bo_Dong").position,8,260,NPCAmbientDialogue.Role.Guard);
        var oldCamp=GameObject.Find("1_DaiBanDoanh_NgoQuyen");
        if(oldCamp!=null&&QuestRowboat.FlatDistance(oldCamp.transform.position,root.Find("Doanh_Trai").position)>650)
            District("Trai_Quan_Bo_Song",oldCamp.transform.position,7,300,NPCAmbientDialogue.Role.Guard);
        PolishWorkyards(root);
        budget.citizens=citizens.ToArray();budget.maximumAnimated=44;budget.activeDistance=700;budget.visibleDistance=850;
        if(citizens.Count<160)throw new InvalidOperationException("Expected at least 160 ambient residents after densifying; got "+citizens.Count);
        int animals=BeachBoatVillageAnimals.Build(root,terrain,p=>Free(p,18));
        var streamer=UnityEngine.Object.FindAnyObjectByType<MapZoneStreamer>();
        var tracked=new HashSet<GameObject>(streamer.zones.Where(z=>z.content!=null).Select(z=>z.content));
        streamer.zones=streamer.zones.Concat(root.Cast<Transform>().Where(t=>!tracked.Contains(t.gameObject)).Select(t=>new MapZoneStreamer.Zone{content=t.gameObject,center=t.position,visibleDistance=1700})).ToArray();
        EditorUtility.SetDirty(budget);EditorUtility.SetDirty(streamer);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DenseWorldBuild.json",JsonConvert.SerializeObject(new{original,people=citizens.Count,animals,maximumAnimated=budget.maximumAnimated,
            districts=root.Cast<Transform>().Select(d=>new{d.name,people=d.GetComponentsInChildren<NPCWander>(true).Length,workyards=d.Cast<Transform>().Count(t=>t.name.StartsWith("Workyard_"))}).ToArray()},Formatting.Indented));
        EditorApplication.Exit(0);
    }
    public static void EquipOutpostGuardsAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var budget=UnityEngine.Object.FindAnyObjectByType<AmbientPopulationBudget>();
        var source=GameObject.Find("Camp_Worker_01");int replaced=0;
        for(int i=0;i<budget.citizens.Length;i++)
        {
            var old=budget.citizens[i];var dialogue=old.GetComponent<NPCAmbientDialogue>();
            if(dialogue.role!=NPCAmbientDialogue.Role.Guard)continue;
            var actor=UnityEngine.Object.Instantiate(source,old.transform.parent);actor.name=old.name;
            foreach(var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))
                if(!(behaviour is NPCWander)&&!(behaviour is NPCAmbientDialogue)&&!(behaviour is NPCUprightPosture))UnityEngine.Object.DestroyImmediate(behaviour);
            foreach(var collider in actor.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            foreach(var body in actor.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(body);
            actor.transform.localScale=source.transform.lossyScale;actor.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);actor.tag="Untagged";
            var wander=actor.GetComponent<NPCWander>()??actor.AddComponent<NPCWander>();EditorUtility.CopySerialized(old,wander);wander.animator=actor.GetComponent<Animator>();wander.animator.applyRootMotion=false;wander.animator.cullingMode=AnimatorCullingMode.CullCompletely;
            var talk=actor.GetComponent<NPCAmbientDialogue>()??actor.AddComponent<NPCAmbientDialogue>();EditorUtility.CopySerialized(dialogue,talk);talk.dailyLife=wander;talk.displayName="Lính gác";
            wander.activityLabels=new[]{"Đang tuần tra quanh trại","Đang kiểm tra hàng rào và kho","Đang trở về vị trí gác"};
            budget.citizens[i]=wander;UnityEngine.Object.DestroyImmediate(old.gameObject);replaced++;
        }
        EditorUtility.SetDirty(budget);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Equipped outpost patrols: "+replaced);EditorApplication.Exit(0);
    }
}
