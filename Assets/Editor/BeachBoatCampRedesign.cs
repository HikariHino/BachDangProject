using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Authored beachBoat layout: parade court, rear command post and functional canvas tents.</summary>
public static class BeachBoatCampRedesign
{
    const string ScenePath="Assets/Scenes/beachBoat.unity", Folder="Assets/BeachBoatStory/Camp/";
    static Terrain terrain;
    static Vector3 origin;
    static Material wood, mat;
    static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
    static Vector3 Point(float x,float z,float up=0){var p=origin+new Vector3(x,0,z);p.y=Ground(p)+up;return p;}
    static Transform Empty(string name,Transform parent,Vector3 local)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}
    static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=p;go.transform.localScale=size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
        go.isStatic=true;return go;
    }
    static void Prop(string name,Transform parent,Vector3 local,float height)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/"+name+".prefab");
        if(asset==null)throw new InvalidOperationException("Missing tent prop: "+name);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
        go.transform.localPosition=local;go.transform.localRotation=Quaternion.identity;
        var scale=parent.lossyScale;go.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        var rr=go.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);
        go.transform.localScale*=height/Mathf.Max(.01f,b.size.y);
        float floor=parent.TransformPoint(local).y;
        go.transform.position+=Vector3.up*(floor-go.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y));
        foreach(var c in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
        go.isStatic=true;
    }
    static void MoveBuilding(string name,float x,float z)
    {
        var t=GameObject.Find(name)?.transform;if(t==null)throw new InvalidOperationException(name+" missing");
        var before=t.position;var after=Point(x,z);after.y+=before.y-Ground(before);t.position=after;
        var service=GameObject.Find("Use_"+name)?.transform;if(service!=null&&!service.IsChildOf(t))service.position+=after-before;
        // Move authored work routes together with their workplace.
        foreach(var w in UnityEngine.Object.FindObjectsByType<NPCWander>(FindObjectsSortMode.None))
            if(w.waypoints!=null&&w.waypoints.Any(p=>p!=null&&p.IsChildOf(t)))w.transform.position+=after-before;
    }
    static void Fence(Transform root)
    {
        var old=root.Find("Camp_Palisade_Reused");
        var sample=old.GetComponentsInChildren<MeshRenderer>().First().gameObject;
        var panels=Empty("Camp_Palisade_Expanded",root,Vector3.zero);
        var corners=new[]{Point(-330,-285),Point(330,-285),Point(330,550),Point(-330,550)};
        var gate=Point(-330,150);int index=0;
        for(int edge=0;edge<4;edge++)
        {
            var a=corners[edge];var b=corners[(edge+1)%4];var d=b-a;
            int steps=Mathf.CeilToInt(d.magnitude/14);
            for(int i=0;i<steps;i++)
            {
                var p=Vector3.Lerp(a,b,(i+.5f)/steps);if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(gate.x,gate.z))<35)continue;
                var panel=UnityEngine.Object.Instantiate(sample,panels);panel.name="Camp_Palisade_"+(index++).ToString("000");
                // Existing fence parent was stretched vertically. Preserve its world dimensions once.
                var s=sample.transform.lossyScale;panel.transform.localScale=s;
                panel.transform.rotation=Quaternion.LookRotation(d);
                foreach(var c in panel.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
                panel.transform.position=p;panel.transform.position+=Vector3.up*(Ground(p)-panel.GetComponent<Renderer>().bounds.min.y);
            }
        }
        var edges=new[]{(corners[0],corners[1]),(corners[1],corners[2]),(corners[2],corners[3]),
            (corners[3],gate+Vector3.forward*35),(gate-Vector3.forward*35,corners[0])};
        for(int i=0;i<edges.Length;i++)
        {
            var a=edges[i].Item1;var b=edges[i].Item2;
            var t=Empty("Camp_Barrier_"+i,panels,Vector3.zero);t.SetPositionAndRotation((a+b)*.5f+Vector3.up*22,Quaternion.LookRotation(b-a));
            t.gameObject.AddComponent<BoxCollider>().size=new Vector3(5,44,Vector3.Distance(a,b));
        }
        UnityEngine.Object.DestroyImmediate(old.gameObject);panels.name="Camp_Palisade_Reused";
    }
    static void Command(Transform camp)
    {
        var stage=camp.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage"));
        var old=stage.position;var destination=old+Vector3.forward*425;
        destination.y+=Ground(destination)-Ground(old);
        var stairs=GameObject.Find("Stage_Stair_Walkway").transform;
        var approach=stairs.position-old;approach.y=0;
        var turn=Quaternion.FromToRotation(approach.normalized,Vector3.back);
        var actors=UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
            .Where(a=>a.name=="NgoQuyen_QuestGiver"||
                (a.name.StartsWith("Camp_Guard_")&&a.transform.position.y-Ground(a.transform.position)>20)).Select(a=>a.transform).ToArray();
        var moving=new HashSet<Transform>(actors){stage};
        moving.Add(stairs);moving.Add(GameObject.Find("Stage_Stair_Landing").transform);
        foreach(var t in actors)
        {
            var w=t.GetComponent<NPCWander>();
            if(w?.waypoints!=null)foreach(var p in w.waypoints)if(p!=null&&!actors.Any(a=>p.IsChildOf(a))&&!p.IsChildOf(stage))moving.Add(p);
        }
        foreach(var t in moving){t.position=destination+turn*(t.position-old);t.rotation=turn*t.rotation;}
        Empty("CommandApproach",camp,Vector3.zero).position=Point(0,330,2.4f);
        var session=UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.BeachBoatQuestSession>();
        if(session?.playerSpawn==null)throw new InvalidOperationException("Player arrival missing");
        session.playerSpawn.SetPositionAndRotation(Point(-260,150,2.4f),Quaternion.Euler(0,90,0));
        session.player.transform.SetPositionAndRotation(session.playerSpawn.position,session.playerSpawn.rotation);
        foreach(var station in UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None))
            if(station.kind==BeachBoatMinigameStation.ActivityKind.WarDrum)
            {var before=station.transform.position;var p=Point(-62,235);p.y+=before.y-Ground(before);station.transform.position=p;}
    }
    static void Tent(Transform t,int side,Vector2 point,CampFacility.FacilityKind kind,string label)
    {
        t.SetPositionAndRotation(Point(point.x,point.y,.12f),Quaternion.Euler(0,side<0?90:270,0));
        foreach(var c in t.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"CanvasFieldTent.asset");
        t.GetComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        var inside=Empty("Tent_Interior",t,Vector3.zero);
        Box("Sleeping_Mat",inside,new Vector3(-7,.35f,-3),new Vector3(8,.5f,30),mat);
        Box("Headrest",inside,new Vector3(-7,.8f,-16),new Vector3(7,1.3f,3),mat);
        if(kind==CampFacility.FacilityKind.Medical){Prop("pf_bucket_01",inside,new Vector3(10,0,-13),5);}
        else if(kind==CampFacility.FacilityKind.Provisions){Prop("pf_barrels_01",inside,new Vector3(9,0,-12),10);Prop("pf_bucket_01",inside,new Vector3(10,0,7),4);}
        else if(kind==CampFacility.FacilityKind.Armory){Prop("pf_halberd_01",inside,new Vector3(11,0,-8),25);Prop("pf_plankpile_01",inside,new Vector3(8,0,-16),5);}
        else Prop("pf_bucket_01",inside,new Vector3(11,0,-14),4);
        var marker=Empty("Use_Tent",t,new Vector3(0,0,6));var facility=marker.gameObject.AddComponent<CampFacility>();
        facility.kind=kind;facility.displayName=label.ToLowerInvariant();facility.interactDistance=14;
        Box("Service_Sign",inside,new Vector3(0,21,24.8f),new Vector3(27,4,.6f),wood);
        var sign=Empty("Service_Label",inside,new Vector3(0,21,25.2f));sign.localRotation=Quaternion.Euler(0,180,0);
        var text=sign.gameObject.AddComponent<TextMeshPro>();StoryFont.Apply(text);text.text=label;text.fontSize=30f;
        text.alignment=TextAlignmentOptions.Center;text.color=new Color(.95f,.84f,.59f);text.rectTransform.sizeDelta=new Vector2(26,4);
        text.textWrappingMode=TextWrappingModes.NoWrap;
    }
    static void TentSoldier(int worker,Transform tent)
    {
        var actor=GameObject.Find("Camp_Worker_"+worker.ToString("00"));var wander=actor.GetComponent<NPCWander>();
        var route=Empty("Tent_Duty_Route",tent,Vector3.zero);
        var offsets=new[]{new Vector3(0,0,46),new Vector3(0,0,29),new Vector3(0,0,10),new Vector3(-7,0,-3)};
        wander.waypoints=offsets.Select((p,i)=>{var stop=Empty("Stop_"+i,route,p);var at=stop.position;at.y=Ground(at)+3.2f;stop.position=at;return stop;}).ToArray();
        wander.autoGenerateWaypoints=false;wander.followWaypointsInOrder=true;
        wander.activityLabels=new[]{"Đang đổi ca trực","Đang vào lều","Đang tới chiếu nghỉ","Đang ngủ sau ca trực"};
        wander.waypointWaitSeconds=new[]{4f,.1f,.1f,18f};wander.walkSpeed=7;wander.minIdleTime=.2f;wander.maxIdleTime=1;
        actor.transform.SetPositionAndRotation(wander.waypoints[0].position,tent.rotation*Quaternion.Euler(0,180,0));
    }
    static void Paths(Transform root)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
        void Road(Vector2 a,Vector2 b,float width)
        {
            var delta=b-a;var side=new Vector2(-delta.y,delta.x).normalized*width*.5f;
            int steps=Mathf.CeilToInt(delta.magnitude/12);
            for(int i=0;i<steps;i++)
            {
                var p=Vector2.Lerp(a,b,i/(float)steps);var q=Vector2.Lerp(a,b,(i+1f)/steps);int n=vertices.Count;
                foreach(var xy in new[]{p-side,p+side,q+side,q-side}){vertices.Add(Point(xy.x,xy.y,.2f));uv.Add(xy/24);}
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
        }
        Road(new Vector2(0,-265),new Vector2(0,350),32);
        Road(new Vector2(-330,150),new Vector2(0,150),24);
        Road(new Vector2(0,-45),new Vector2(0,255),138);
        foreach(float z in new[]{-170f,-65f,80f,185f})Road(new Vector2(-80,z),new Vector2(80,z),16);
        foreach(int side in new[]{-1,1})
        {Road(new Vector2(side*175,-250),new Vector2(side*175,45),15);Road(new Vector2(side*175,105),new Vector2(side*175,355),15);Road(new Vector2(0,290),new Vector2(side*170,290),16);}
        var mesh=new Mesh{name="CompactCampPaths"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var filter=root.GetComponentsInChildren<MeshFilter>().First(f=>f.sharedMesh!=null&&AssetDatabase.GetAssetPath(f.sharedMesh).EndsWith("CompactCampPaths.asset"));
        var existing=filter.sharedMesh;EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);
        filter.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);filter.transform.localScale=Vector3.one;
    }
    static void MedicalStaff(string name,Transform tent)
    {
        var actor=GameObject.Find(name);var pose=actor.GetComponent<NPCCampPose>();if(pose!=null)UnityEngine.Object.DestroyImmediate(pose);
        var animator=actor.GetComponent<Animator>();animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Folder+"ThaiCampPoses.controller");
        var wander=actor.GetComponent<NPCWander>()??actor.AddComponent<NPCWander>();wander.animator=animator;
        var route=Empty("Medic_Route",tent,Vector3.zero);
        wander.waypoints=new[]{new Vector3(8,0,44),new Vector3(0,0,28),new Vector3(7,0,-3)}.Select((p,i)=>
        {var t=Empty("Stop_"+i,route,p);var at=t.position;at.y=Ground(at)+3.2f;t.position=at;return t;}).ToArray();
        wander.followWaypointsInOrder=true;wander.autoGenerateWaypoints=false;wander.waypointWaitSeconds=new[]{8f,.1f,10f};
        wander.activityLabels=new[]{"Đang chuẩn bị băng vải","Đang vào lều quân y","Đang kiểm tra đồ cứu thương"};
        wander.minIdleTime=.2f;wander.maxIdleTime=2;wander.walkSpeed=6;
        actor.transform.SetPositionAndRotation(wander.waypoints[0].position,tent.rotation*Quaternion.Euler(0,180,0));
    }
    public static void Apply()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);terrain=Terrain.activeTerrain;
        var camp=GameObject.Find("TestQuest_Camp").transform;var root=camp.Find("Compact_Living_Camp");
        if(camp.Find("CampLayoutOrigin")!=null)throw new InvalidOperationException("Camp redesign already applied; refusing duplicate setup.");
        Directory.CreateDirectory(".utmp/CampRedesign");EditorSceneManager.SaveScene(scene,".utmp/CampRedesign/before.unity",true);
        var stage=camp.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage"));origin=stage.position;origin.y=Ground(origin);
        Empty("CampLayoutOrigin",camp,Vector3.zero).position=origin;
        BeachBoatNPCMotionRepair.Apply();BeachBoatVisualRepair.BuildOpenTentAssets();
        wood=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Barracks_Dark_Beams.mat");mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Camp_Sleeping_Mat.mat");
        Fence(root);Command(camp);
        MoveBuilding("Lo_Ren_Quan_Do",245,245);MoveBuilding("Kho_Khi_Gioi_Nho",-270,320);MoveBuilding("Central_Supply_Store",270,355);
        var tents=root.Find("Barracks_Rows").Cast<Transform>().OrderBy(t=>t.name).ToArray();
        if(tents.Length!=14)throw new InvalidOperationException("Expected 14 canvas tents");
        var places=new[]{new Vector2(-245,-205),new Vector2(-245,-125),new Vector2(-245,-35),new Vector2(-245,195),new Vector2(-245,245),new Vector2(-175,410),new Vector2(-230,505),
            new Vector2(245,-205),new Vector2(245,-125),new Vector2(245,-35),new Vector2(245,155),new Vector2(150,320),new Vector2(175,410),new Vector2(230,505)};
        var kinds=new[]{4,4,0,2,2,0,0,3,3,3,2,2,0,0};
        for(int i=0;i<tents.Length;i++)
        {var k=(CampFacility.FacilityKind)kinds[i];string label=k==CampFacility.FacilityKind.Medical?"LỀU QUÂN Y":k==CampFacility.FacilityKind.Armory?"LỀU QUÂN TRANG":k==CampFacility.FacilityKind.Provisions?"LỀU TIẾP TẾ":"LỀU NGHỈ CA";Tent(tents[i],i<7?-1:1,places[i],k,label);}
        for(int i=0;i<4;i++)TentSoldier(9+i,tents[new[]{2,5,12,13}[i]]);
        MedicalStaff("Camp_Laying_Soldier",tents[0]);MedicalStaff("Camp_Sitting_Soldier",tents[1]);
        MoveBuilding("Stake_Timber_1_0",185,-170);MoveBuilding("Stake_Timber_1_1",185,-65);
        foreach(var a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
            if(a.isHuman&&Vector3.Distance(a.transform.position,origin)<1000&&AssetDatabase.GetAssetPath(a.avatar).Contains("OldSoldier"))
                if(a.GetComponent<NPCUprightPosture>()==null)a.gameObject.AddComponent<NPCUprightPosture>();
        // The expanded rear court was previously woodland. Clear only the fenced footprint.
        terrain.terrainData.treeInstances=terrain.terrainData.treeInstances.Where(tree=>
        {var p=Vector3.Scale(tree.position,terrain.terrainData.size)+terrain.transform.position;return p.x<origin.x-340||p.x>origin.x+340||p.z<origin.z-295||p.z>origin.z+560;}).ToArray();
        EditorUtility.SetDirty(terrain.terrainData);
        Paths(root);Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Camp scene save failed");
        Debug.Log("Camp expanded north, command stage relocated, 14 functional open tents and 4 existing tent duty soldiers configured.");
    }
    public static void ApplyAndExit(){Apply();EditorApplication.Exit(0);}
    public static void RefreshEditorPosesAndExit()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        terrain=Terrain.activeTerrain;origin=GameObject.Find("TestQuest_Camp").transform.Find("CampLayoutOrigin").position;
        foreach(string name in new[]{"Camp_Laying_Soldier","Camp_Sitting_Soldier"})
        {var a=GameObject.Find(name).GetComponent<Animator>();a.Rebind();a.Update(.01f);a.GetComponent<NPCUprightPosture>()?.Balance();}
        var moved=new HashSet<GameObject>();
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var p=r.bounds.center-origin;
            if(Mathf.Abs(p.x)>70||p.z< -55||p.z>255)continue;
            var filter=r.GetComponent<MeshFilter>();if(filter?.sharedMesh==null)continue;
            string path=AssetDatabase.GetAssetPath(filter.sharedMesh).ToLowerInvariant();
            if(!path.Contains("rock")&&!path.Contains("boulder")&&!path.Contains("stone"))continue;
            var prop=PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject)??r.gameObject;
            if(prop.GetComponentsInChildren<MonoBehaviour>().Length>0||!moved.Add(prop))continue;
            var before=prop.transform.position;
            var target=Point(p.x<0?-365:365,-220+moved.Count*45);target.y+=before.y-Ground(before);prop.transform.position=target;
        }
        Debug.Log("Moved decorative stones out of the parade court: "+moved.Count);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
}
