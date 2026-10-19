using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using BeachBoatStory;

/// <summary>Scene-specific audit and compact camp polish for the laptop build.</summary>
public static class BeachBoatCampPolish
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";
    const string RootName = "Compact_Living_Camp";

    static float Ground(Terrain terrain, Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

    static void Seat(GameObject go, Terrain terrain, Vector3 position)
    {
        go.transform.position = new Vector3(position.x, Ground(terrain,position), position.z);
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
            go.transform.position += Vector3.up * (Ground(terrain,position) + .12f - renderers.Min(r => r.bounds.min.y));
    }

    static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

    static bool KeepOriginalCampMesh(GameObject go)
    {
        var group=go.GetComponent<LODGroup>();
        var original=go.GetComponent<Renderer>();
        if(group==null||original==null)return false;
        var broken=go.GetComponentsInChildren<Renderer>(true).Where(r=>r!=original&&r.name.StartsWith("Laptop_LOD")).ToArray();
        if(broken.Length==0)return false;
        group.SetLODs(new[]{new LOD(.015f,new[]{original})});
        group.RecalculateBounds();
        original.enabled=true;
        foreach(var renderer in broken)renderer.gameObject.SetActive(false);
        return true;
    }

    static int UseSharedEarthToneMaterials(Transform camp)
    {
        var timber=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Buildings/Materials/mat_building_01.mat");
        var canvas=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Buildings/Materials/mat_strawroof_01.mat");
        if(timber==null||canvas==null)throw new InvalidOperationException("Shared building materials are missing.");
        int changed=0;
        var stage=camp.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage"));
        var stageRenderer=stage.GetComponent<Renderer>();
        if(stageRenderer!=null){stageRenderer.sharedMaterial=timber;changed++;}
        var rows=camp.Find(RootName+"/Barracks_Rows");
        foreach(Transform tent in rows)
        {
            var renderer=tent.GetComponent<Renderer>();
            if(renderer!=null){renderer.sharedMaterial=canvas;changed++;}
        }
        var gate=GameObject.Find("CampToVillage")?.transform.Find("Visible_Story_Gate");
        var gateRenderer=gate?.GetComponent<Renderer>();
        if(gateRenderer!=null)
        {
            gateRenderer.sharedMaterials=Enumerable.Repeat(timber,gateRenderer.sharedMaterials.Length).ToArray();
            changed++;
        }
        return changed;
    }

    static void Transition(AnimatorState from, AnimatorState to, string parameter, bool value)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = .16f;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
    }

    // The imported camp controller had an isWalking parameter but no Walking state or
    // transitions. NPCWander moved transforms while the animation stayed at Idle.
    static void RepairCampAnimation()
    {
        const string path = "Assets/BeachBoatStory/Camp/ThaiCampPoses.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) throw new InvalidOperationException("Thai camp controller is missing.");
        var machine = controller.layers[0].stateMachine;
        AnimatorState State(string name) => machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
        var idle = State("Idle");
        var walk = State("Walking") ?? machine.AddState("Walking");
        walk.motion = Clip("Assets/Animation/Meshy_AI_Character_output@Walking (1).fbx");
        foreach (var state in machine.states.Select(s => s.state))
            foreach (var transition in state.transitions.ToArray())
                state.RemoveTransition(transition);
        var sit = State("Sitting"); var lay = State("Laying");
        Transition(idle,walk,"isWalking",true);
        Transition(walk,idle,"isWalking",false);
        Transition(idle,sit,"isSitting",true);
        Transition(sit,idle,"isSitting",false);
        Transition(idle,lay,"isLaying",true);
        Transition(lay,idle,"isLaying",false);
        EditorUtility.SetDirty(controller);
    }

    static GameObject AddPrefab(string path, string name, Transform parent, Terrain terrain, Vector3 p, float scale, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        instance.transform.localScale *= scale;
        instance.transform.rotation = Quaternion.Euler(0,yaw,0);
        Seat(instance,terrain,p);
        return instance;
    }

    static void AddCompactPaths(Transform parent, Terrain terrain, Vector3 origin)
    {
        const string meshPath="Assets/BeachBoatStory/Camp/CompactCampPaths.asset";
        if(parent.Find("Compact_Earth_Paths")!=null)return;
        var vertices=new List<Vector3>();var uvs=new List<Vector2>();var triangles=new List<int>();
        void Strip(Vector3 a,Vector3 b,float width)
        {
            a+=origin;b+=origin;
            var direction=(b-a).normalized;
            var side=new Vector3(-direction.z,0,direction.x)*width*.5f;
            var points=new[]{a+side,a-side,b+side,b-side};
            int index=vertices.Count;
            for(int i=0;i<4;i++)
            {
                var p=points[i];p.y=Ground(terrain,p)+.22f;vertices.Add(p);
                uvs.Add(new Vector2(i%2, i<2?0:Vector3.Distance(a,b)/20f));
            }
            triangles.AddRange(new[]{index,index+2,index+1,index+1,index+2,index+3});
        }
        Strip(new Vector3(-265,0,150),new Vector3(0,0,150),18);
        Strip(new Vector3(0,0,300),new Vector3(0,0,30),20);
        Strip(new Vector3(0,0,-35),new Vector3(0,0,-205),16);
        Strip(new Vector3(-205,0,145),new Vector3(205,0,145),11);
        Strip(new Vector3(-205,0,-120),new Vector3(205,0,-120),11);
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(mesh==null){mesh=new Mesh{name="Compact_Camp_Earth_Paths"};AssetDatabase.CreateAsset(mesh,meshPath);}
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var path=new GameObject("Compact_Earth_Paths");path.transform.SetParent(parent,true);
        path.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        path.transform.localScale=Vector3.one;
        path.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=path.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BachDangSettlements/Village_Packed_Earth.mat");
        renderer.shadowCastingMode=ShadowCastingMode.Off;
    }

    static void AddFence(Transform parent, GameObject sample, Terrain terrain, Vector3 origin, Vector3 gate)
    {
        var corners = new[] {
            origin + new Vector3(-265,0,-225), origin + new Vector3(265,0,-225),
            origin + new Vector3(265,0,315), origin + new Vector3(-265,0,315)
        };
        for (int i=0;i<corners.Length;i++) corners[i].y=Ground(terrain,corners[i]);
        int index = 0;
        for (int edge=0; edge<4; edge++)
        {
            var a=corners[edge];var b=corners[(edge+1)%4];
            var direction=(b-a).normalized;
            var length=Vector3.Distance(a,b);
            int steps=Mathf.CeilToInt(length/14f);
            for (int i=0;i<steps;i++)
            {
                var p=a+direction*((i+.5f)*length/steps);
                if (Vector3.Distance(p,gate)<24) continue;
                var panel=UnityEngine.Object.Instantiate(sample,parent);
                panel.name="Camp_Palisade_"+(index++).ToString("000");
                panel.transform.rotation=Quaternion.LookRotation(direction);
                var s=sample.transform.localScale;
                panel.transform.localScale=new Vector3(s.x,s.y*.62f,s.z);
                foreach (var collider in panel.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                Seat(panel,terrain,p);
            }
        }
        // Four long barriers cost much less than a collider on every fence panel.
        var segments = new[] {
            (corners[0],corners[1]), (corners[1],corners[2]),
            (corners[2],corners[3]), (corners[3],gate+Vector3.forward*24),
            (gate+Vector3.back*24,corners[0])
        };
        for(int i=0;i<segments.Length;i++)
        {
            var a=segments[i].Item1;var b=segments[i].Item2;
            var d=b-a;
            var go=new GameObject("Camp_Barrier_"+i);go.transform.SetParent(parent,false);
            go.transform.SetPositionAndRotation((a+b)*.5f+Vector3.up*18,Quaternion.LookRotation(d));
            go.AddComponent<BoxCollider>().size=new Vector3(5,38,d.magnitude);
        }
    }

    static void AddWorker(Transform parent, GameObject source, Terrain terrain, Vector3 origin,
        Vector3 offset, int index, string activity)
    {
        var actor=UnityEngine.Object.Instantiate(source,parent);
        actor.name="Camp_Worker_"+index.ToString("00");
        Seat(actor,terrain,origin+offset);
        actor.transform.rotation=Quaternion.Euler(0,index%2==0?90:270,0);
        var dialogue=actor.GetComponent<NPCDialogue>();
        if(dialogue!=null){dialogue.speakerName="Quân sĩ doanh trại";dialogue.dialogueText=activity+". Việc chuẩn bị trận địa vẫn đang tiếp diễn.";}
        var ambient=actor.GetComponent<NPCAmbientDialogue>();
        if(ambient!=null)ambient.displayName="Quân sĩ doanh trại";
        var route=new GameObject("Worker_Route_"+index.ToString("00")).transform;
        route.SetParent(parent,false);
        var waypoints=new Transform[3];
        for(int i=0;i<waypoints.Length;i++)
        {
            var p=origin+offset+new Vector3((i-1)*13,0,i%2==0?9:-11);
            var marker=new GameObject("Work_Stop_"+i).transform;marker.SetParent(route,false);
            marker.position=new Vector3(p.x,Ground(terrain,p)+.25f,p.z);waypoints[i]=marker;
        }
        var wander=actor.GetComponent<NPCWander>() ?? actor.AddComponent<NPCWander>();
        wander.animator=actor.GetComponent<Animator>();
        wander.enabled=true;wander.autoGenerateWaypoints=false;wander.waypoints=waypoints;
        wander.activityLabels=new[]{activity,"Đang chuyển vật tư","Đang trở về chốt"};
        wander.walkSpeed=4.5f;wander.minIdleTime=1.5f;wander.maxIdleTime=5f;
        wander.sitLayChance=8;
        var animator=actor.GetComponent<Animator>();
        animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
    }

    [MenuItem("BeachBoat/Polish Compact Living Camp")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(Application.isPlaying||scene.path!=ScenePath)throw new InvalidOperationException("Open beachBoat in Edit Mode.");
        var camp=GameObject.Find("TestQuest_Camp");
        if(camp==null||camp.transform.Find(RootName)!=null)throw new InvalidOperationException("Camp missing or already polished.");
        var terrain=Terrain.activeTerrain;
        var stage=camp.transform.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage"));
        var origin=stage.position;
        var fenceRoot=camp.transform.Cast<Transform>().First(t=>t.name.Contains("HANG RAO"));
        var panelSample=fenceRoot.Cast<Transform>().First(t=>t.name.StartsWith("HangRao")).gameObject;
        var shelters=camp.transform.Cast<Transform>().Where(t=>t.name.Contains("Nomad")).OrderBy(t=>t.name).ToArray();
        if(shelters.Length!=25)throw new InvalidOperationException("Expected 25 original tents, found "+shelters.Length);
        var source=GameObject.Find("Camp_Guard_Mid_Right");
        if(source==null||source.GetComponent<Animator>()==null)throw new InvalidOperationException("Camp guard source is missing.");
        RepairCampAnimation();
        var root=new GameObject(RootName);root.transform.SetParent(camp.transform,false);
        var oldLife=camp.transform.Find("Island_Camp_Life");
        if(oldLife!=null)UnityEngine.Object.DestroyImmediate(oldLife.gameObject);
        var tentsRoot=new GameObject("Barracks_Rows").transform;tentsRoot.SetParent(root.transform,false);
        var xValues=new[]{-175f,175f};
        var zValues=new[]{-175f,-105f,-35f,35f,105f,175f};
        var tentSource=shelters[0].gameObject;
        for(int i=0;i<xValues.Length*zValues.Length;i++)
        {
            var shelter=i<shelters.Length?shelters[i].gameObject:UnityEngine.Object.Instantiate(tentSource,tentsRoot);
            shelter.name="Barracks_Tent_"+(i+1).ToString("00");
            shelter.transform.SetParent(tentsRoot,true);
            shelter.transform.localScale*=.5f;
            var p=origin+new Vector3(xValues[i%xValues.Length],0,zValues[i/xValues.Length]);
            shelter.transform.rotation=Quaternion.Euler(0,(i%2)*180,0);
            Seat(shelter,terrain,p);
            KeepOriginalCampMesh(shelter);
        }
        KeepOriginalCampMesh(stage.gameObject);
        foreach(var shelter in shelters.Skip(12))UnityEngine.Object.DestroyImmediate(shelter.gameObject);
        // Remote props belonged to the old island-wide layout. Keep the local stage detail.
        foreach(var child in camp.transform.Cast<Transform>().ToArray())
        {
            if(child==root.transform||child==fenceRoot)continue;
            var d=Vector2.Distance(new Vector2(origin.x,origin.z),new Vector2(child.position.x,child.position.z));
            if(d>500 && child.name!="Island_Fence_Infill" && child.name!="Island_Fence_Collisions")
                child.gameObject.SetActive(false);
        }
        var fence=new GameObject("Camp_Palisade_Reused").transform;fence.SetParent(root.transform,false);
        var gate=origin+new Vector3(-265,0,150);gate.y=Ground(terrain,gate);
        AddFence(fence,panelSample,terrain,origin,gate);
        UnityEngine.Object.DestroyImmediate(fenceRoot.gameObject);
        foreach(var name in new[]{"Island_Fence_Infill","Island_Fence_Collisions"})
        {var old=camp.transform.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);}
        var props=new GameObject("Supply_And_Work").transform;props.SetParent(root.transform,false);
        for(int side=-1;side<=1;side+=2)
        foreach(float z in new[]{-170f,-65f,80f,185f})
            AddPrefab("Assets/Prefabs/Buildings/pf_build_barracks_single_01_low.prefab",
                "Wooden_Barracks_"+side+"_"+z,props,terrain,
                origin+new Vector3(side*90,0,z),5.3f,side<0?90:270);
        AddPrefab("Assets/Prefabs/Buildings/pf_build_storage_01_low.prefab","Central_Supply_Store",props,terrain,
            origin+new Vector3(0,0,265),5f,180);
        foreach(int side in new[]{-1,1})
        {
            for(int i=0;i<4;i++)
            {
                AddPrefab("Assets/Prefabs/Props/pf_logpile_01.prefab","Stake_Timber_"+side+"_"+i,props,terrain,
                    origin+new Vector3(side*130,0,-140+i*75),5.5f,90);
                AddPrefab("Assets/Prefabs/Props/pf_barrels_01.prefab","Food_Stores_"+side+"_"+i,props,terrain,
                    origin+new Vector3(side*130,0,-110+i*75),4f,0);
            }
        }
        var actors=new GameObject("Working_Soldiers").transform;actors.SetParent(root.transform,false);
        var offsets=new[]{new Vector3(-32,0,-145),new Vector3(32,0,-105),new Vector3(-35,0,-55),
            new Vector3(35,0,280),new Vector3(-32,0,65),new Vector3(34,0,125),
            new Vector3(-34,0,185),new Vector3(33,0,245),
            new Vector3(42,0,-185),new Vector3(-42,0,-195),
            new Vector3(-48,0,270),new Vector3(48,0,205)};
        var activities=new[]{"Đang chuyển gỗ cọc","Đang kiểm tra kho lương","Đang khiêng vật tư",
            "Đang thay ca gác","Đang sửa dụng cụ","Đang phân phát lương khô",
            "Đang chuẩn bị xuất quân","Đang kiểm tra lều trại",
            "Đang tập hợp vật tư","Đang thay phiên tuần tra","Đang kiểm kê kho","Đang sửa hàng rào"};
        for(int i=0;i<offsets.Length;i++)AddWorker(actors,source,terrain,origin,offsets[i],i+1,activities[i]);
        AddCompactPaths(root.transform,terrain,origin);
        var lookout=GameObject.Find("Camp_Lookout_Soldier");
        if(lookout!=null)Seat(lookout,terrain,origin+new Vector3(225,0,280));
        foreach(var name in new[]{"CampToVillage","CampToRiver"})
        {
            var gateObject=GameObject.Find(name);
            if(gateObject==null)continue;
            gateObject.transform.position=gate;
            var visual=gateObject.transform.Find("Visible_Story_Gate");
            if(visual!=null){visual.rotation=Quaternion.Euler(0,180,0);visual.localScale*=.45f;Seat(visual.gameObject,terrain,gate);}
        }
        UseSharedEarthToneMaterials(camp.transform);
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var report=new {tentCount=tentsRoot.childCount,workers=actors.GetComponentsInChildren<NPCWander>().Length,
            fencePanels=fence.GetComponentsInChildren<MeshRenderer>().Length,
            campRenderers=camp.GetComponentsInChildren<Renderer>().Length,
            campColliders=camp.GetComponentsInChildren<Collider>().Length,
            terrainTrees=terrain.terrainData.treeInstanceCount};
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CampPolishResult.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        Debug.Log("Compact living camp applied: "+Newtonsoft.Json.JsonConvert.SerializeObject(report));
    }

    public static void ApplyAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        Apply();
        EditorApplication.Exit(0);
    }

    // Converts the first compact pass in place without rebuilding the quest objects.
    public static void RefineAndExit()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var camp=GameObject.Find("TestQuest_Camp");
        var root=camp.transform.Find(RootName);
        if(root==null)throw new InvalidOperationException("Compact camp is missing.");
        var origin=camp.transform.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage")).position;
        var terrain=Terrain.activeTerrain;
        var tents=root.Find("Barracks_Rows");
        var xValues=new[]{-175f,175f};
        var zValues=new[]{-175f,-105f,-35f,35f,105f,175f};
        for(int i=0;i<36;i++)
        {
            var tent=tents.Find("Barracks_Tent_"+(i+1).ToString("00"));
            if(tent==null)throw new InvalidOperationException("Missing tent "+i);
            if(i>=12){UnityEngine.Object.DestroyImmediate(tent.gameObject);continue;}
            tent.localScale*=.5f;
            Seat(tent.gameObject,terrain,origin+new Vector3(xValues[i%2],0,zValues[i/2]));
        }
        var props=root.Find("Supply_And_Work");
        foreach(var old in props.Cast<Transform>().Where(t=>t.name.StartsWith("Supply_Store_")).ToArray())
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        for(int side=-1;side<=1;side+=2)
        {
            foreach(float z in new[]{-170f,-65f,80f,185f})
                AddPrefab("Assets/Prefabs/Buildings/pf_build_barracks_single_01_low.prefab",
                    "Wooden_Barracks_"+side+"_"+z,props,terrain,
                    origin+new Vector3(side*90,0,z),5.3f,side<0?90:270);
            for(int i=0;i<4;i++)
            {
                var logs=props.Find("Stake_Timber_"+side+"_"+i);
                var food=props.Find("Food_Stores_"+side+"_"+i);
                logs.localScale*=5.5f/7f;food.localScale*=4f/5f;
                Seat(logs.gameObject,terrain,origin+new Vector3(side*130,0,-140+i*75));
                Seat(food.gameObject,terrain,origin+new Vector3(side*130,0,-110+i*75));
            }
        }
        AddPrefab("Assets/Prefabs/Buildings/pf_build_storage_01_low.prefab","Central_Supply_Store",props,terrain,
            origin+new Vector3(0,0,265),5f,180);
        var worker=root.Find("Working_Soldiers/Camp_Worker_04");
        var destination=origin+new Vector3(35,0,280);
        var shift=destination-worker.position;
        Seat(worker.gameObject,terrain,destination);
        var route=root.Find("Working_Soldiers/Worker_Route_04");
        foreach(Transform point in route)point.position+=shift;
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/CampPolishRefined.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{
            tents=tents.childCount,woodenBarracks=props.Cast<Transform>().Count(t=>t.name.StartsWith("Wooden_Barracks_")),
            renderers=camp.GetComponentsInChildren<Renderer>().Length,
            colliders=camp.GetComponentsInChildren<Collider>().Length},Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void EnlivenAndExit()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var camp=GameObject.Find("TestQuest_Camp");
        var root=camp.transform.Find(RootName);
        if(root==null)throw new InvalidOperationException("Compact camp is missing.");
        var origin=camp.transform.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage")).position;
        var terrain=Terrain.activeTerrain;
        var actors=root.Find("Working_Soldiers");
        var source=GameObject.Find("Camp_Guard_Mid_Right");
        var offsets=new[]{new Vector3(42,0,-185),new Vector3(-42,0,-195),
            new Vector3(-48,0,270),new Vector3(48,0,205)};
        var labels=new[]{"Đang tập hợp vật tư","Đang thay phiên tuần tra","Đang kiểm kê kho","Đang sửa hàng rào"};
        for(int i=0;i<4;i++)
            if(actors.Find("Camp_Worker_"+(i+9).ToString("00"))==null)
                AddWorker(actors,source,terrain,origin,offsets[i],i+9,labels[i]);
        var data=terrain.terrainData;
        var existing=data.treeInstances;
        var added=new System.Collections.Generic.List<TreeInstance>();
        var random=UnityEngine.Random.state;UnityEngine.Random.InitState(93819);
        for(int cluster=0;cluster<120&&added.Count<460;cluster++)
        {
            var center=origin+new Vector3(UnityEngine.Random.Range(-1550f,1550f),0,UnityEngine.Random.Range(-1150f,1150f));
            for(int k=0;k<6&&added.Count<460;k++)
            {
                var offset=UnityEngine.Random.insideUnitCircle*UnityEngine.Random.Range(8f,90f);
                var p=center+new Vector3(offset.x,0,offset.y);
                if(Mathf.Abs(p.x-origin.x)<335&&p.z>origin.z-295&&p.z<origin.z+395)continue;
                var local=p-terrain.transform.position;
                if(local.x<0||local.z<0||local.x>data.size.x||local.z>data.size.z||Ground(terrain,p)<118)continue;
                var copy=existing[UnityEngine.Random.Range(0,existing.Length)];
                copy.position=new Vector3(local.x/data.size.x,(Ground(terrain,p)-terrain.transform.position.y)/data.size.y,local.z/data.size.z);
                copy.rotation=UnityEngine.Random.Range(0f,6.283185f);
                added.Add(copy);
            }
        }
        UnityEngine.Random.state=random;
        data.SetTreeInstances(existing.Concat(added).ToArray(),false);
        EditorUtility.SetDirty(data);
        terrain.Flush();
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/CampPolishEnlivened.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{
            workers=actors.GetComponentsInChildren<NPCWander>().Length,
            addedTrees=added.Count,totalTrees=data.treeInstanceCount,
            renderers=camp.GetComponentsInChildren<Renderer>().Length},Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void FinalTouchesAndExit()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var gate=GameObject.Find("CampToVillage");
        var visible=gate?.transform.Find("Visible_Story_Gate");
        if(visible==null)throw new InvalidOperationException("Camp gate visual is missing.");
        visible.localScale*=.45f;
        Seat(visible.gameObject,Terrain.activeTerrain,gate.transform.position);
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/CampPolishGate.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{
            scale=visible.localScale.ToString("F2"),bounds=visible.GetComponent<Renderer>()?.bounds.size.ToString("F1"),
            gate=gate.transform.position.ToString("F1")},Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void RepairVisualLODsAndExit()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var camp=GameObject.Find("TestQuest_Camp");
        var root=camp.transform.Find(RootName);
        int repaired=0;
        foreach(Transform tent in root.Find("Barracks_Rows"))if(KeepOriginalCampMesh(tent.gameObject))repaired++;
        var stage=camp.transform.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage"));
        if(KeepOriginalCampMesh(stage.gameObject))repaired++;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/CampPolishLOD.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{repaired},Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void RepairCampMaterialsAndExit()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var camp=GameObject.Find("TestQuest_Camp").transform;
        int changed=UseSharedEarthToneMaterials(camp);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/CampPolishMaterialRepair.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{changed},Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void AddPathsAndExit()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var camp=GameObject.Find("TestQuest_Camp");
        var origin=camp.transform.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage")).position;
        AddCompactPaths(camp.transform.Find(RootName),Terrain.activeTerrain,origin);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        EditorApplication.Exit(0);
    }

    public static void MemoryAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        BachDangMapMemoryReport.Capture("compact-living-camp");
        EditorApplication.Exit(0);
    }

    public static void CaptureGroundAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var stage=GameObject.Find("TestQuest_Camp").transform.Cast<Transform>()
            .First(t=>t.name.Contains("Rustic_Wooden_Stage"));
        var origin=stage.position;
        var source=UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>()?.storyCamera;
        if(source==null)throw new InvalidOperationException("Story camera is missing.");
        var go=UnityEngine.Object.Instantiate(source.gameObject);
        go.name="Camp_Ground_Capture_Only";
        var camera=go.GetComponent<Camera>();camera.enabled=false;
        go.transform.position=new Vector3(origin.x,Ground(Terrain.activeTerrain,origin)+20,origin.z+245);
        go.transform.LookAt(new Vector3(origin.x,Ground(Terrain.activeTerrain,origin)+18,origin.z+20));
        camera.fieldOfView=70;camera.farClipPlane=1200;camera.clearFlags=CameraClearFlags.Skybox;
        var rt=new RenderTexture(1600,1000,24);var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
        Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/CampPolishGround.png",texture.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);
        EditorApplication.Exit(0);
    }

    public static void PlaytestAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        SessionState.SetBool("CampPolishPlaytest",true);
        EditorApplication.isPlaying=true;
    }

    public static void PlaytestVisualAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        SessionState.SetBool("CampPolishPlaytest",true);
        SessionState.SetBool("CampPolishVisual",true);
        EditorApplication.isPlaying=true;
    }

    public static void GateSmokeAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        GateSmokeProbe.EnsureRegistered();
        SessionState.SetBool("CampPolishGateSmoke",true);
        EditorApplication.isPlaying=true;
    }

    public static void CaptureAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var stage=GameObject.Find("TestQuest_Camp").transform.Cast<Transform>()
            .First(t=>t.name.Contains("Rustic_Wooden_Stage"));
        var origin=stage.position;
        var sourceCamera=UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>()?.storyCamera;
        if(sourceCamera==null)throw new InvalidOperationException("Story camera is missing.");
        var cameraObject=UnityEngine.Object.Instantiate(sourceCamera.gameObject);
        cameraObject.name="Camp_Polish_Capture_Only";
        var camera=cameraObject.GetComponent<Camera>();
        camera.enabled=false;
        cameraObject.transform.position=origin+new Vector3(0,310,430);
        cameraObject.transform.LookAt(origin+new Vector3(0,-35,40));
        camera.fieldOfView=64;camera.farClipPlane=1500;
        camera.clearFlags=CameraClearFlags.Skybox;
        var rt=new RenderTexture(1600,1000,24);
        var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
        Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/CampPolishAfter.png",texture.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        EditorApplication.Exit(0);
    }

    public static void MeasureAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var tent=GameObject.Find("Barracks_Tent_01");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/pf_build_barracks_single_01_low.prefab");
        var barracks=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Bounds BoundsOf(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>(true);
            var b=renderers[0].bounds;for(int i=1;i<renderers.Length;i++)b.Encapsulate(renderers[i].bounds);
            return b;
        }
        var report=new{tentScale=tent.transform.localScale.ToString("F2"),tentBounds=BoundsOf(tent).size.ToString("F1"),
            barracksScale1Bounds=BoundsOf(barracks).size.ToString("F1")};
        UnityEngine.Object.DestroyImmediate(barracks);
        File.WriteAllText("Logs/CampPolishMeasure.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void MaterialsAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var targets=new[]{"Barracks_Tent_01","Meshy_AI_Rustic_Wooden_Stage_1009055412_texture",
            "Visible_Story_Gate","Central_Supply_Store","Wooden_Barracks_-1_-170"};
        var report=targets.Select(name=>{
            var go=GameObject.Find(name);
            return new{name,renderers=go==null?null:go.GetComponentsInChildren<Renderer>(true)
                .Select(r=>new{r.name,materials=r.sharedMaterials.Select(m=>new{
                    name=m==null?"null":m.name,path=AssetDatabase.GetAssetPath(m),
                    shader=m==null?"null":m.shader?.name,
                    color=m!=null&&m.HasProperty("_BaseColor")?m.GetColor("_BaseColor").ToString():"none",
                    texture=m!=null&&m.HasProperty("_BaseMap")?AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")):"none"}).ToArray()}).ToArray()};
        }).ToArray();
        File.WriteAllText("Logs/CampPolishMaterials.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }

    [InitializeOnLoad]
    static class PlaytestProbe
    {
        static double started;
        static Vector3[] startPositions;
        static float[] maxTravel;
        static int walkingFrames;
        static int idleFrames;
        static int walkingFlags;
        static bool visualCaptured;
        static PlaytestProbe() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if(!SessionState.GetBool("CampPolishPlaytest",false)||!EditorApplication.isPlaying)return;
            var root=GameObject.Find(RootName);
            if(root==null)return;
            var workers=root.GetComponentsInChildren<NPCWander>();
            if(workers.Length!=12)return;
            if(started==0)
            {
                started=EditorApplication.timeSinceStartup;
                startPositions=workers.Select(x=>x.transform.position).ToArray();
                maxTravel=new float[workers.Length];
                // Batch mode has no visible camera and culls all skinned animators.
                // Force animation updates in this probe only; scene settings stay unchanged.
                foreach(var worker in workers)worker.GetComponent<Animator>().cullingMode=AnimatorCullingMode.AlwaysAnimate;
            }
            for(int i=0;i<workers.Length;i++)
            {
                maxTravel[i]=Mathf.Max(maxTravel[i],Vector3.Distance(startPositions[i],workers[i].transform.position));
                var animator=workers[i].GetComponent<Animator>();
                if(animator.GetCurrentAnimatorStateInfo(0).IsName("Walking")||
                   animator.GetCurrentAnimatorStateInfo(0).IsName("Walking (1)"))walkingFrames++;
                if(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))idleFrames++;
                if(animator.GetBool("isWalking"))walkingFlags++;
            }
            if(!visualCaptured&&SessionState.GetBool("CampPolishVisual",false)&&EditorApplication.timeSinceStartup-started>10)
            {
                visualCaptured=true;
                var camera=UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>()?.storyCamera;
                if(camera!=null)
                {
                    var sceneCamp=GameObject.Find("TestQuest_Camp");
                    var stage=sceneCamp.transform.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage"));
                    var ground=Terrain.activeTerrain.SampleHeight(stage.position)+Terrain.activeTerrain.transform.position.y;
                    var go=UnityEngine.Object.Instantiate(camera.gameObject);
                    var listener=go.GetComponent<AudioListener>();if(listener!=null)listener.enabled=false;
                    var view=go.GetComponent<Camera>();view.enabled=false;
                    go.transform.position=new Vector3(stage.position.x,ground+20,stage.position.z+245);
                    go.transform.LookAt(new Vector3(stage.position.x,ground+18,stage.position.z+20));
                    var rt=new RenderTexture(1600,1000,24);var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
                    view.targetTexture=rt;view.Render();RenderTexture.active=rt;
                    texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
                    Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/CampPolishPlay.png",texture.EncodeToPNG());
                    RenderTexture.active=null;view.targetTexture=null;
                    UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(go);
                }
                BachDangMapMemoryReport.Capture("compact-camp-play");
            }
            if(EditorApplication.timeSinceStartup-started<22)return;
            var terrain=Terrain.activeTerrain;
            var camp=GameObject.Find("TestQuest_Camp");
            var gate=GameObject.Find("CampToVillage");
            var report=new {success=maxTravel.Count(x=>x>3)>=9&&walkingFrames>0,
                workers=workers.Length,workersMovedOver3=maxTravel.Count(x=>x>3),
                maxTravel,walkingFrames,idleFrames,walkingFlags,timeScale=Time.timeScale,
                controllers=workers.Select(x=>x.GetComponent<Animator>().runtimeAnimatorController?.name).ToArray(),
                activities=workers.Select(x=>x.CurrentActivity).ToArray(),
                campRenderers=camp.GetComponentsInChildren<Renderer>().Length,
                terrainTrees=terrain.terrainData.treeInstanceCount,
                gatePosition=gate.transform.position.ToString("F1")};
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CampPolishPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            SessionState.SetBool("CampPolishPlaytest",false);
            SessionState.SetBool("CampPolishVisual",false);
            EditorApplication.Exit(report.success?0:1);
        }
    }

    [InitializeOnLoad]
    static class GateSmokeProbe
    {
        static double started;
        static int phase;
        static string error;
        static GateSmokeProbe(){EditorApplication.update+=Tick;}
        internal static void EnsureRegistered() { }
        static void Finish(bool success,Vector3 position)
        {
            var gate=GameObject.Find("CampToVillage")?.GetComponent<BeachBoatStory.CampGateTransition>();
            File.WriteAllText("Logs/CampPolishGateSmoke.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{
                success,error,gate=gate?.transform.position.ToString("F1"),
                destination=gate?.destinationTarget?.position.ToString("F1"),player=position.ToString("F1")},
                Newtonsoft.Json.Formatting.Indented));
            SessionState.SetBool("CampPolishGateSmoke",false);
            EditorApplication.Exit(success?0:1);
        }
        static void Tick()
        {
            if(!SessionState.GetBool("CampPolishGateSmoke",false)||!EditorApplication.isPlaying)return;
            if(started==0)started=EditorApplication.timeSinceStartup;
            var elapsed=EditorApplication.timeSinceStartup-started;
            var session=BeachBoatQuestSession.Instance;
            var story=BeachBoatStory.BachDangStoryManager.Instance;
            if(session==null||story==null)
            {
                if(elapsed>28)
                {
                    var foundSession=UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>();
                    var foundStory=UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.BachDangStoryManager>();
                    error=$"Quest bootstrap missing: session={session!=null}, story={story!=null}, sceneSession={foundSession!=null}, sceneStory={foundStory!=null}";
                    Finish(false,Vector3.zero);
                }
                return;
            }
            if(elapsed>28){error="Gate transition timed out";Finish(false,session.player.transform.position);return;}
            if(phase==0&&elapsed>2)
            {
                var opening=UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.OpeningEvent>();
                if(opening!=null)opening.SkipOpening();
                var npc=UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.NPCQuestGiver>();
                var p=npc.transform.position+Vector3.forward*8;
                session.player.transform.position=p;
                var rb=session.player.GetComponent<Rigidbody>();if(rb!=null){rb.position=p;rb.linearVelocity=Vector3.zero;}
                Physics.SyncTransforms();
                if(!npc.TryTalk()){error="Cannot receive opening quest";Finish(false,p);return;}
                BeachBoatStory.StoryDialogueUI.Instance?.Close();
                // The normal flow completes a training minigame before the gate unlocks.
                // This probe isolates the moved gate's transition from that separate activity.
                story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act1_GoToCampGate,true);
                phase=1;
            }
            if(phase==1&&elapsed>4)
            {
                var gate=GameObject.Find("CampToVillage").GetComponent<BeachBoatStory.CampGateTransition>();
                var p=gate.transform.position+Vector3.up;
                session.player.transform.position=p;
                var rb=session.player.GetComponent<Rigidbody>();if(rb!=null){rb.position=p;rb.linearVelocity=Vector3.zero;}
                Physics.SyncTransforms();
                if(!gate.TryInteract())
                {
                    error=$"Camp gate rejected: state={story.currentState}, blocked={BeachBoatQuestSession.InteractionBlocked}, " +
                        $"available={gate.IsAvailable(story.currentState)}, inRange={gate.IsInRange(story.GetPlayerTransform())}, " +
                        $"storyPlayer={story.GetPlayerTransform()?.position.ToString("F1")}, gateEnabled={gate.isActiveAndEnabled}";
                    Finish(false,p);return;
                }
                phase=2;
            }
            if(phase==2&&elapsed>7)
            {
                var gate=GameObject.Find("CampToVillage").GetComponent<BeachBoatStory.CampGateTransition>();
                var player=session.player.transform.position;
                if(Vector3.Distance(player,gate.destinationTarget.position)<6){Finish(true,player);return;}
            }
        }
    }

    public static void AuditAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp");
        var stage = camp.transform.Cast<Transform>().First(t => t.name.Contains("Rustic_Wooden_Stage"));
        var origin = stage.position;
        var terrain = Terrain.activeTerrain;
        var children = camp.transform.Cast<Transform>().Select(t => new {
            t.name, pos = t.position.ToString("F0"), scale = t.lossyScale.ToString("F1"),
            distance = Vector2.Distance(new Vector2(t.position.x,t.position.z),new Vector2(origin.x,origin.z)),
            renderers = t.GetComponentsInChildren<Renderer>(true).Length,
            colliders = t.GetComponentsInChildren<Collider>(true).Length
        }).OrderBy(t => t.distance).ToArray();
        var life = camp.transform.Find("Island_Camp_Life");
        var groups = life.Cast<Transform>().Select(t => new {
            t.name, pos = t.position.ToString("F0"), tents = t.GetComponentsInChildren<Transform>(true).Count(x => x.name.Contains("Nomad")),
            renderers = t.GetComponentsInChildren<Renderer>(true).Length
        }).ToArray();
        var walkers = UnityEngine.Object.FindObjectsByType<NPCWander>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene).Select(x => new {
                x.name, pos = x.transform.position.ToString("F0"), x.enabled,
                eligible = NPCWander.IsEligible(x.GetComponent<Animator>()),
                waypoints = x.waypoints == null ? 0 : x.waypoints.Length
            }).ToArray();
        var gates = UnityEngine.Object.FindObjectsByType<CampGateTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene).Select(x => new { x.name, pos = x.transform.position.ToString("F0") }).ToArray();
        var grid = (from x in new[] {-350,-250,-150,0,150,250,350}
                    from z in new[] {-300,-200,-100,0,100,200,300,400}
                    let p = origin + new Vector3(x,0,z)
                    select new {x,z,height=terrain.SampleHeight(p)+terrain.transform.position.y}).ToArray();
        var fenceRoot = camp.transform.Cast<Transform>().First(t=>t.name.Contains("HANG RAO"));
        var panel = fenceRoot.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault();
        var panelInfo = panel == null ? null : new {panel.name,
            pos=panel.transform.position.ToString("F1"),rotation=panel.transform.eulerAngles.ToString("F1"),
            scale=panel.transform.lossyScale.ToString("F1"),bounds=panel.bounds.size.ToString("F1"),
            mesh=panel.GetComponent<MeshFilter>()?.sharedMesh?.name};
        var fenceNodes = fenceRoot.Cast<Transform>().Take(12).Select(t=>new{t.name,
            pos=t.position.ToString("F1"),rot=t.eulerAngles.ToString("F1"),
            size=t.GetComponent<Renderer>()?.bounds.size.ToString("F1")}).ToArray();
        var report = new { stage = origin.ToString("F0"), terrainTrees = terrain.terrainData.treeInstanceCount,
            campRenderers = camp.GetComponentsInChildren<Renderer>(true).Length,
            campColliders = camp.GetComponentsInChildren<Collider>(true).Length,
            children, groups, walkers, gates, grid, panelInfo, fenceNodes };
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CampPolishAudit.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }
}
