using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeachBoatStory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Replace relay signboards with four grounded, staffed village work stalls.</summary>
public static class BeachBoatVillageWorkSetup
{
    static Terrain terrain;
    static Material wood,rope,food;
    static float highWater;
    static readonly List<Vector3> Used=new List<Vector3>();
    static Bounds[] householdBounds=Array.Empty<Bounds>();
    static Bounds[] rockBounds=Array.Empty<Bounds>();
    static Bounds[] fieldBounds=Array.Empty<Bounds>();
    static Vector3[] villageTrees=Array.Empty<Vector3>();
    public static void AuditLayoutAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var report=new {
            village=UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.BachDangStoryManager>().villageSpawnPoint.position,
            districts=GameObject.Find("BeachBoat_Busy_Settlements").transform.Cast<Transform>().Select(t=>new {t.name,position=t.position}),
            stalls=UnityEngine.Object.FindObjectsByType<VillageWorkStall>(FindObjectsSortMode.None).Select(s=>new{s.name,position=s.transform.position}),
            ships=UnityEngine.Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(s=>new{s.name,s.spawnPoint,target=s.targetPoint.position,bounds=BoundsOf(s.transform),route=s.routeWaypoints.Select(t=>t.position)}),
            stakes=UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.RiverStakeZone>().fieldStakes.Select(t=>BoundsOf(t))
        };
        System.IO.Directory.CreateDirectory("Logs");System.IO.File.WriteAllText("Logs/VillageBattleLayoutAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented,new Newtonsoft.Json.JsonSerializerSettings{ReferenceLoopHandling=Newtonsoft.Json.ReferenceLoopHandling.Ignore}));
        EditorApplication.Exit(0);
    }
    static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
    static Bounds BoundsOf(Transform t)=>BeachBoatWorldAudit.BoundsOf(t);
    static bool Clear(Vector3 p)
    {
        float ground=Ground(p);if(ground<highWater+3)return false;
        if(fieldBounds.Any(b=>p.x>b.min.x-55&&p.x<b.max.x+55&&p.z>b.min.z-55&&p.z<b.max.z+55))return false;
        if(householdBounds.Any(b=>p.x>b.min.x-34&&p.x<b.max.x+34&&p.z>b.min.z-34&&p.z<b.max.z+34))return false;
        if(rockBounds.Any(b=>p.x>b.min.x-38&&p.x<b.max.x+38&&p.z>b.min.z-38&&p.z<b.max.z+38))return false;
        if(villageTrees.Any(t=>QuestRowboat.FlatDistance(t,p)<55))return false;
        foreach(var offset in new[]{new Vector3(25,0,25),new Vector3(-25,0,25),new Vector3(25,0,-25),new Vector3(-25,0,-25)})
            if(Mathf.Abs(Ground(p+offset)-ground)>2.5f)return false;
        foreach(var hit in Physics.OverlapBox(new Vector3(p.x,ground+23,p.z),new Vector3(32,22,32),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            if(!(hit is TerrainCollider)&&hit.GetComponentInParent<Animator>()==null)return false;
        foreach(var gate in UnityEngine.Object.FindObjectsByType<CampGateTransition>(FindObjectsSortMode.None))
            if(QuestRowboat.FlatDistance(p,gate.transform.position)<65)return false;
        foreach(var worker in UnityEngine.Object.FindObjectsByType<NPCWander>(FindObjectsSortMode.None))
        {
            if(worker.waypoints==null)continue;
            for(int i=0;i<worker.waypoints.Length;i++)
            {
                int next=(i+1)%worker.waypoints.Length;
                if(worker.waypoints[i]==null||worker.waypoints[next]==null)continue;
                var a=worker.waypoints[i].position;var b=worker.waypoints[next].position;
                var delta=b-a;delta.y=0;var offset=p-a;offset.y=0;
                float along=delta.sqrMagnitude>.1f?Mathf.Clamp01(Vector3.Dot(offset,delta)/delta.sqrMagnitude):0;
                if(QuestRowboat.FlatDistance(p,a+delta*along)<42)return false;
            }
        }
        return Used.All(old=>QuestRowboat.FlatDistance(old,p)>85);
    }
    public static void ScatterStallsAndExit()
    {ScatterStalls();EditorApplication.Exit(0);}
    public static void ScatterStalls()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        terrain=Terrain.activeTerrain;highWater=UnityEngine.Object.FindFirstObjectByType<TideSystem>().highTideY;
        var village=GameObject.Find("01_Lang_Cho_Ben_Song").transform;
        var houses=village.Cast<Transform>().Where(t=>t.name.StartsWith("Nha_Dan_")).ToArray();
        var stalls=UnityEngine.Object.FindObjectsByType<VillageWorkStall>(FindObjectsSortMode.None).OrderBy(s=>s.kind).ToArray();
        if(houses.Length<8||stalls.Length!=4)throw new InvalidOperationException("Expected four stalls and the populated riverside village");
        Directory.CreateDirectory(".utmp/VillageWork");EditorSceneManager.SaveScene(scene,".utmp/VillageWork/before-scatter.unity",true);
        householdBounds=houses.Select(BoundsOf).ToArray();Used.Clear();
        foreach(var s in stalls)s.gameObject.SetActive(false);Physics.SyncTransforms();
        fieldBounds=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(r=>r.name=="Dat_Ruong"||r.name=="Lua_Non"||r.name.Contains("Field")||r.name.Contains("Rice_Rows"))
            .Select(r=>r.bounds).ToArray();
        var center=houses.Aggregate(Vector3.zero,(sum,t)=>sum+t.position)/houses.Length;
        villageTrees=terrain.terrainData.treeInstances.Select(t=>terrain.transform.position+Vector3.Scale(t.position,terrain.terrainData.size)).Where(p=>QuestRowboat.FlatDistance(p,center)<2200).ToArray();
        rockBounds=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r=>r.GetComponentsInParent<Transform>().Any(t=>System.Text.RegularExpressions.Regex.IsMatch(t.name,"rock|stone|boulder|đá",System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            .Select(r=>r.bounds).Where(b=>QuestRowboat.FlatDistance(b.center,center)<2400).ToArray();
        var random=new System.Random(938210);
        var report=new List<object>();
        for(int sector=0;sector<4;sector++)
        {
            var choices=houses.Where(h=>(h.position.x<center.x?0:1)+(h.position.z<center.z?0:2)==sector).ToArray();
            if(choices.Length==0)throw new InvalidOperationException("No homes in village sector "+sector);
            bool placed=false;var stall=stalls[sector];
            for(int attempt=0;attempt<1800&&!placed;attempt++)
            {
                var house=choices[random.Next(choices.Length)];var bounds=BoundsOf(house);
                float angle=(float)random.NextDouble()*Mathf.PI*2;
                float radius=Mathf.Max(bounds.extents.x,bounds.extents.z)+45+(float)random.NextDouble()*95;
                var p=house.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;p.y=Ground(p);
                if(!Clear(p)||Used.Any(old=>QuestRowboat.FlatDistance(old,p)<180))continue;
                var rotation=Quaternion.LookRotation(new Vector3(house.position.x-p.x,0,house.position.z-p.z));
                bool exitClear=true;
                for(float distance=29;distance<=80;distance+=7)
                {
                    var front=p+rotation*Vector3.back*distance;front.y=Ground(front);
                    if(front.y<highWater+3||Mathf.Abs(front.y-p.y)>3||Physics.OverlapCapsule(front+Vector3.up*6,front+Vector3.up*22,4,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)&&c.GetComponentInParent<Animator>()==null))
                    {exitClear=false;break;}
                }
                if(!exitClear)continue;
                stall.transform.SetPositionAndRotation(p,rotation);stall.gameObject.SetActive(true);
                var interaction=stall.interactionPoint.position;interaction.y=Ground(interaction)+3.2f;stall.interactionPoint.position=interaction;
                Used.Add(p);Physics.SyncTransforms();placed=true;
                report.Add(new{stall.stallName,house=house.name,sector,position=new[]{p.x,p.y,p.z}});
            }
            if(!placed)throw new InvalidOperationException("No clear household stall site in sector "+sector);
        }
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/VillageStallDistribution.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    static Vector3 FindPlace(Vector3 desired)
    {
        for(int ring=0;ring<14;ring++)for(int angle=0;angle<(ring==0?1:20);angle++)
        {var p=desired+Quaternion.Euler(0,angle*18,0)*Vector3.forward*(ring*20);p.y=Ground(p);if(Clear(p)){Used.Add(p);return p;}}
        throw new InvalidOperationException("Could not find unobstructed village stall ground at "+desired);
    }
    static Transform Group(string name,Transform parent,Vector3 position)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
    static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool solid=false)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;if(!solid)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
    static GameObject Prop(string asset,Transform parent,Vector3 local,float height)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/pf_"+asset+".prefab");
        if(source==null)throw new InvalidOperationException("Missing stall prop "+asset);
        var g=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);g.transform.localPosition=local;g.transform.localRotation=Quaternion.identity;g.transform.localScale=Vector3.one;
        var b=BoundsOf(g.transform);g.transform.localScale*=height/b.size.y;b=BoundsOf(g.transform);g.transform.position+=Vector3.up*(parent.TransformPoint(local).y-b.min.y);
        foreach(var c in g.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
        return g;
    }
    static void Coil(Transform parent,float x)
    {
        for(int ring=0;ring<4;ring++)
        {
            var line=Group("Cuon_Day_That_"+ring,parent,new Vector3(x,12.3f+ring*.25f,-5)).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial=rope;line.useWorldSpace=false;line.positionCount=49;line.widthMultiplier=.55f;line.generateLightingData=true;line.shadowCastingMode=ShadowCastingMode.Off;
            for(int n=0;n<49;n++){float angle=n/48f*Mathf.PI*2;line.SetPosition(n,new Vector3(Mathf.Cos(angle)*(3+ring*.5f),0,Mathf.Sin(angle)*(3+ring*.5f)));}
        }
    }
    static void Awning(Transform parent)
    {
        var cover=AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachBoatStory/Camp/CanvasFieldTent.mat");
        var frame=Group("Mai_Che_Mo_Mat_Truoc",parent,Vector3.zero);
        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            Box("Cot_Sap",frame,new Vector3(x*22,14,z*13),new Vector3(1.4f,28,1.4f),wood,true);
        Box("Xa_Ngang",frame,new Vector3(0,28,0),new Vector3(46,1.3f,1.3f),wood);
        foreach(int x in new[]{-1,1})
        {
            var roof=Box("Mai_Vai",frame,new Vector3(x*12,30.5f,0),new Vector3(25.3f,.65f,34),cover);
            roof.transform.localRotation=Quaternion.Euler(0,0,-x*18);
        }
    }
    public static void OpenStallsAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachBoatStory/Camp/Barracks_Dark_Beams.mat");
        foreach(var stall in UnityEngine.Object.FindObjectsByType<VillageWorkStall>(FindObjectsSortMode.None))
        {
            foreach(var child in stall.transform.Cast<Transform>().ToArray())
                if(child.name.Contains("shed_02")||child.name=="Mai_Che_Mo_Mat_Truoc")UnityEngine.Object.DestroyImmediate(child.gameObject);
            Awning(stall.transform);
            stall.transform.Find("Ten_Sap").localPosition=new Vector3(0,36,-17);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");terrain=Terrain.activeTerrain;highWater=UnityEngine.Object.FindFirstObjectByType<TideSystem>().highTideY;
        if(UnityEngine.Object.FindFirstObjectByType<VillagePreparationQuest>()!=null)throw new InvalidOperationException("Village stalls already installed");
        Directory.CreateDirectory(".utmp/VillageWork");EditorSceneManager.SaveScene(scene,".utmp/VillageWork/before.unity",true);
        var old=UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None).Single(s=>s.kind==BeachBoatMinigameStation.ActivityKind.SupplyRelay);
        var positions=old.checkpoints.Select(t=>t.position).ToArray();
        foreach(var marker in old.checkpointMarkers)if(marker!=null)UnityEngine.Object.DestroyImmediate(marker);
        foreach(var child in old.transform.Cast<Transform>().ToArray())if(child.name.StartsWith("SupplyCrate_")||child.name.StartsWith("VisibleCargo_"))UnityEngine.Object.DestroyImmediate(child.gameObject);
        old.checkpointMarkers=Array.Empty<GameObject>();old.checkpoints=Array.Empty<Transform>();old.enabled=false;
        var owner=old.gameObject.AddComponent<VillagePreparationQuest>();owner.stalls=new VillageWorkStall[4];Used.Clear();Physics.SyncTransforms();
        wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachBoatStory/Camp/Barracks_Dark_Beams.mat");
        rope=AssetDatabase.LoadAssetAtPath<Material>("Assets/BachDangScaleAndLife/Village_Hemp_Rope.mat");
        food=AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachBoatStory/SceneryMaterials/Supply_Rations.mat");
        var sources=UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Where(a=>a.isHuman&&AssetDatabase.GetAssetPath(a.avatar).Contains("Pea")).Take(3).ToArray();
        if(sources.Length==0||wood==null||rope==null||food==null)throw new InvalidOperationException("Village art references missing");
        string[] names={"SẠP GỖ","HÀNG DÂY THỪNG","HÀNG GẠO","SẠP LƯƠNG KHÔ"};
        for(int i=0;i<4;i++)
        {
            var p=FindPlace(positions[i]);var root=Group("Village_Work_Stall_"+i,old.transform,Vector3.zero);root.position=p;
            var facing=old.transform.position-p;facing.y=0;if(facing.sqrMagnitude>.1f)root.rotation=Quaternion.LookRotation(-facing);
            Awning(root);
            Box("Ban_Hang_That",root,new Vector3(0,11,-5),new Vector3(32,2,13),wood,true);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("Chan_Ban",root,new Vector3(x*13,5,z*4-5),new Vector3(2,10,2),wood);
            if(i==0){Prop("plankpile_01",root,new Vector3(-4,12,-5),4);Prop("logpile_01",root,new Vector3(23,0,6),10);}
            if(i==1){Coil(root,-7);Coil(root,7);Prop("bucket_01",root,new Vector3(22,0,7),8);}
            if(i==2)
            {
                var basket=Group("Thung_Gao",root,new Vector3(0,12,-5));basket.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BachDangCourtyards/Shared_Woven_Basket.asset");basket.gameObject.AddComponent<MeshRenderer>().sharedMaterial=rope;
                basket.localScale=Vector3.one*(6/BoundsOf(basket).size.y);Box("Gao_Trong_Thung",root,new Vector3(0,15,-5),new Vector3(7,.6f,5),food);
                Prop("barrels_02",root,new Vector3(23,0,7),12);
            }
            if(i==3){Prop("fish_01",root,new Vector3(-7,12,-5),3);Prop("buckets_01",root,new Vector3(6,12,-5),5);Prop("barrel_fish_01",root,new Vector3(24,0,6),12);}
            var vendor=UnityEngine.Object.Instantiate(sources[i%sources.Length].gameObject,root);
            foreach(var b in vendor.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(b);
            foreach(var c in vendor.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
            foreach(var body in vendor.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(body);
            vendor.name="Nguoi_Ban_Hang";vendor.tag="Untagged";vendor.transform.localScale=sources[i%sources.Length].transform.lossyScale;
            vendor.transform.localPosition=new Vector3(0,3.2f,8);vendor.transform.localRotation=Quaternion.Euler(0,180,0);
            var animator=vendor.GetComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullCompletely;
            var sign=Group("Ten_Sap",root,new Vector3(0,36,-17)).gameObject.AddComponent<TextMeshPro>();StoryFont.Apply(sign);sign.text=names[i];sign.fontSize=9;sign.alignment=TextAlignmentOptions.Center;sign.rectTransform.sizeDelta=new Vector2(60,10);sign.color=new Color(1,.84f,.52f);
            var stall=root.gameObject.AddComponent<VillageWorkStall>();stall.quest=owner;stall.kind=(VillageWorkStall.WorkKind)i;stall.stallName=names[i];stall.interactionPoint=Group("Cho_Dung_Tuong_Tac",root,new Vector3(0,3.2f,-29));stall.interactionDistance=25;owner.stalls[i]=stall;
            Physics.SyncTransforms();
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
