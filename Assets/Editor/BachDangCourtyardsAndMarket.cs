using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Bounded, shared-asset dressing. Never rebuilds terrain or vegetation.</summary>
public static class BachDangCourtyardsAndMarket
{
    const string Folder = "Assets/BachDangCourtyards";
    const string Marker = "11_San_Nha_Va_Cho";
    const float F = 7f;

    [MenuItem("Bach Dang/Dress Courtyards And Market")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/beachBoat.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open beachBoat in Edit Mode.");
        var settlement = scene.GetRootGameObjects().First(g => g.name == "BachDang_Living_Settlements").transform;
        if (settlement.Find(Marker) != null) { Debug.Log("Courtyards and markets already dressed."); return; }
        if (settlement.Find("10_Lang_Tu_Nhien") == null || !Mathf.Approximately(BachDangWorldScale.ForScene(scene), F))
            throw new InvalidOperationException("The completed natural villages at scale seven are required.");
        if (AssetDatabase.IsValidFolder(Folder) && AssetDatabase.FindAssets("", new[] { Folder }).Length > 0)
            throw new InvalidOperationException("Owned courtyard assets exist without their scene group.");
        var terrain = Terrain.activeTerrain;
        int trees = terrain.terrainData.treeInstanceCount;
        var actors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            .ToDictionary(r => r, r => r.transform.localToWorldMatrix);
        Directory.CreateDirectory(".utmp/Courtyards");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/Courtyards/beachBoat-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true))
            throw new IOException("Could not back up the scene.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "BachDangCourtyards");
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Dress household yards and village markets");
        var assets = new List<string>();
        try
        {
            var group = Group(Marker, settlement);
            var builder = new Builder(terrain, settlement, group, assets);
            foreach (var village in settlement.Cast<Transform>().Where(t => t.name.StartsWith("01_") || t.name.StartsWith("02_") || t.name.StartsWith("03_")))
                builder.Village(village);
            ArrangeCountersAndShades(settlement, terrain);
            var meshes = group.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).Distinct().ToArray();
            long meshBytes = meshes.Sum(m => Profiler.GetRuntimeMemorySizeLong(m));
            int renderers = group.GetComponentsInChildren<MeshRenderer>().Length;
            if (meshBytes > 1024 * 1024 || renderers > 260)
                throw new InvalidOperationException("Courtyard dressing exceeds its 1 MiB shared-mesh or 260-renderer budget.");
            if (builder.yards < 50 || builder.tables < 10)
                throw new InvalidOperationException("Too few courtyards or market stalls have safe room for dressing: " + builder.yards + "/" + builder.tables);
            if (terrain.terrainData.treeInstanceCount != trees || group.GetComponentsInChildren<Light>(true).Length != 0 ||
                group.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Vegetation, light or collider preservation failed.");
            foreach (var actor in actors)
                if (actor.Key == null || actor.Key.transform.localToWorldMatrix != actor.Value)
                    throw new InvalidOperationException("An existing character moved.");
            var report = new { yards = builder.yards, tables = builder.tables, canopies = builder.canopies,
                movedGoods = builder.movedGoods, seats = builder.seats, renderers, uniqueMeshes = meshes.Length,
                meshBytes, trees, actors = actors.Count, newLights = 0, newTextures = 0, newColliders = 0 };
            File.WriteAllText(".utmp/Courtyards/result.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the dressed scene.");
            Undo.CollapseUndoOperations(undo);
            Debug.Log("Courtyards and markets saved: " + Newtonsoft.Json.JsonConvert.SerializeObject(report));
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            foreach (string path in assets.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
            throw;
        }
    }

    static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Courtyard details");
        go.transform.SetParent(parent, false); return go.transform;
    }

    public static void PolishExisting()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/beachBoat.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open beachBoat in Edit Mode.");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/Courtyards/beachBoat-before-counter-polish.unity", true))
            throw new IOException("Could not back up market scene.");
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        try
        {
            ArrangeCountersAndShades(GameObject.Find("BachDang_Living_Settlements").transform, Terrain.activeTerrain);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save market scene.");
            Undo.CollapseUndoOperations(undo);
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    static void ArrangeCountersAndShades(Transform settlement, Terrain terrain)
    {
        var root = settlement.Find(Marker);
        if (root == null) throw new InvalidOperationException("Courtyard group is missing.");
        foreach (Transform villageDetails in root)
        {
            var village = settlement.Find(villageDetails.name);
            int index = 0;
            foreach (var stall in village.Cast<Transform>().Where(t => t.name == "Mai_Che_Hang_Cho" || t.name == "Quay_Ban_Ca"))
            {
                var display = villageDetails.Find("Sap_Hang_" + index++);
                var counter = display.Find("Ban_Bay_Hang");
                if (counter == null) continue;
                Undo.RegisterFullObjectHierarchyUndo(stall.gameObject, "Face goods toward market square");
                var goods = stall.Cast<Transform>().Where(t => t.name == "Hang_Hoa_Cho").ToArray();
                var positions = new[] { new Vector2(-3f,.4f), new Vector2(3f,.5f), new Vector2(3f,-1.1f) };
                for (int item = 0; item < goods.Length; item++)
                {
                    var offset = positions[item % positions.Length];
                    var point = stall.TransformPoint(new Vector3(offset.x,0,offset.y));
                    var bounds = BachDangSettlementAssets.BoundsOf(goods[item].gameObject);
                    point.y = terrain.SampleHeight(point) + terrain.transform.position.y + .025f*F + goods[item].position.y - bounds.min.y;
                    goods[item].position = point; EditorUtility.SetDirty(goods[item]);
                }
                var front = stall.TransformPoint(new Vector3(0,0,1f));
                front.y = terrain.SampleHeight(front) + terrain.transform.position.y + .025f*F;
                Undo.RecordObject(display, "Face counter toward square");
                display.position += front-counter.position; EditorUtility.SetDirty(display);
                var counterBounds = BachDangSettlementAssets.BoundsOf(counter.gameObject);
                foreach(var item in goods)
                {
                    var b = BachDangSettlementAssets.BoundsOf(item.gameObject);
                    if(counterBounds.min.x < b.max.x && counterBounds.max.x > b.min.x && counterBounds.min.z < b.max.z && counterBounds.max.z > b.min.z)
                        throw new InvalidOperationException("Market goods overlap the counter: " + village.name);
                }
            }
        }
        var table = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Shared_Market_Table.asset");
        var basket = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Shared_Woven_Basket.asset");
        var wood = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Courtyard_Weathered_Wood.mat");
        var straw = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Courtyard_Woven_Fibre.mat");
        foreach (var shade in root.GetComponentsInChildren<Transform>().Where(t => t.name == "Khung_Mai_Hien").ToArray())
        {
            if (shade.parent.Find("Ban_Lam_Nghe_Duoi_Hien") != null) continue;
            var bench = SharedDetail("Ban_Lam_Nghe_Duoi_Hien", shade.parent, table, wood);
            bench.SetPositionAndRotation(shade.position, shade.rotation); bench.localScale = new Vector3(.58f,.65f,.70f);
            var goods = SharedDetail("Gio_Duoi_Hien", shade.parent, basket, straw);
            goods.SetPositionAndRotation(bench.TransformPoint(new Vector3(.7f,.95f,0)),shade.rotation);
            goods.localScale = Vector3.one*.8f;
        }
    }

    static Transform SharedDetail(string name, Transform parent, Mesh mesh, Material material)
    {
        var result = Group(name,parent);
        result.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = result.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.lightProbeUsage=LightProbeUsage.Off;
        renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        return result;
    }

    sealed class Builder
    {
        readonly Terrain terrain;
        readonly Transform settlement, root;
        readonly List<string> assets;
        readonly Mesh jar, basket, table, canopy, frame;
        readonly Material clay, straw, wood, linen, indigo;
        readonly List<MeshRenderer> obstacles = new List<MeshRenderer>();
        readonly List<Bounds> paths = new List<Bounds>();
        public int yards, tables, canopies, movedGoods, seats;

        public Builder(Terrain terrain, Transform settlement, Transform root, List<string> assets)
        {
            this.terrain = terrain; this.settlement = settlement; this.root = root; this.assets = assets;
            clay = Material("Courtyard_Earthenware", "Cloth_Clay_Brown", new Color(.43f, .22f, .125f));
            straw = Material("Courtyard_Woven_Fibre", "Village_Hemp_Rope", new Color(.48f, .37f, .20f));
            wood = Material("Courtyard_Weathered_Wood", "Village_Hemp_Rope", new Color(.26f, .205f, .13f));
            linen = AssetDatabase.LoadAssetAtPath<Material>("Assets/BachDangScaleAndLife/Cloth_Undyed_Linen.mat");
            indigo = AssetDatabase.LoadAssetAtPath<Material>("Assets/BachDangScaleAndLife/Cloth_Muted_Indigo.mat");
            if (linen == null || indigo == null) throw new InvalidOperationException("Existing cloth materials are missing.");
            jar = Lathe("Shared_Water_Jar", 16, new[] {
                new Vector2(.23f,0),new Vector2(.32f,.08f),new Vector2(.43f,.30f),new Vector2(.39f,.55f),
                new Vector2(.24f,.69f),new Vector2(.23f,.75f),new Vector2(.19f,.75f),new Vector2(.19f,.68f),new Vector2(.31f,.49f),new Vector2(.30f,.12f),new Vector2(0,.10f) });
            basket = Lathe("Shared_Woven_Basket", 16, new[] {
                new Vector2(.24f,0),new Vector2(.29f,.06f),new Vector2(.30f,.12f),new Vector2(.33f,.14f),new Vector2(.32f,.18f),
                new Vector2(.35f,.24f),new Vector2(.38f,.26f),new Vector2(.37f,.30f),new Vector2(.41f,.39f),new Vector2(.42f,.43f),
                new Vector2(.37f,.43f),new Vector2(.24f,.06f),new Vector2(0,.06f) });
            var boxes = new List<(Vector3, Vector3)> { (new Vector3(0,.87f,0), new Vector3(3.2f,.14f,1.15f)) };
            foreach (int x in new[]{-1,1}) foreach (int z in new[]{-1,1})
                boxes.Add((new Vector3(x*1.32f,.4f,z*.39f),new Vector3(.14f,.8f,.14f)));
            boxes.Add((new Vector3(0,.26f,0),new Vector3(2.7f,.12f,.12f)));
            table = Boxes("Shared_Market_Table", boxes);
            boxes.Clear();
            foreach (int x in new[]{-1,1}) foreach(int z in new[]{-1,1})
                boxes.Add((new Vector3(x*1.5f,1.2f,z*1.05f),new Vector3(.10f,2.4f,.10f)));
            foreach(int z in new[]{-1,1}) boxes.Add((new Vector3(0,2.38f,z*1.05f),new Vector3(3.25f,.10f,.10f)));
            frame = Boxes("Shared_Yard_Shade_Frame", boxes);
            canopy = new Mesh { name = "Shared_Linen_Shade" };
            canopy.vertices = new[] {new Vector3(-1.7f,2.38f,-1.3f),new Vector3(0,2.66f,-1.3f),new Vector3(1.7f,2.38f,-1.3f),
                new Vector3(-1.7f,2.38f,1.3f),new Vector3(0,2.66f,1.3f),new Vector3(1.7f,2.38f,1.3f)};
            canopy.uv = new[]{new Vector2(0,0),new Vector2(.5f,0),new Vector2(1,0),new Vector2(0,1),new Vector2(.5f,1),new Vector2(1,1)};
            canopy.triangles = new[]{0,3,1,1,3,4,1,4,2,2,4,5}; Finish(canopy);
        }

        float Height(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
        Vector3 Ground(Vector3 p) { p.y = Height(p) + .025f*F; return p; }
        static bool Overlap(Bounds a, Bounds b, float margin) =>
            a.min.x < b.max.x+margin && a.max.x > b.min.x-margin && a.min.z < b.max.z+margin && a.max.z > b.min.z-margin;

        public void Village(Transform village)
        {
            var detail = Group(village.name, root); detail.SetPositionAndRotation(village.position,village.rotation);
            obstacles.Clear(); paths.Clear();
            obstacles.AddRange(village.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.sharedMaterial != null && r.sharedMaterial.shader.name!="BachDang/Village Path"));
            var life = settlement.Find("09_Sinh_Hoat/"+village.name+"_Sinh_Hoat");
            obstacles.AddRange(life.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.sharedMaterial != null && r.sharedMaterial.shader.name!="BachDang/Village Path"));
            var natural = settlement.Find("10_Lang_Tu_Nhien/"+village.name+"_Canh_Quan");
            obstacles.AddRange(natural.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Rao_") || r.transform.parent.name.StartsWith("Rao_")));
            foreach(var filter in natural.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Duong_")||f.name.StartsWith("Ngo_")||f.name.StartsWith("Loi_")))
            {
                var vertices=filter.sharedMesh.vertices;
                for(int i=4;i<vertices.Length;i+=4)
                {
                    var b=new Bounds(filter.transform.TransformPoint(vertices[i-4]),Vector3.zero);
                    b.Encapsulate(filter.transform.TransformPoint(vertices[i-1]));
                    b.Encapsulate(filter.transform.TransformPoint(vertices[i])); b.Encapsulate(filter.transform.TransformPoint(vertices[i+3]));
                    paths.Add(b);
                }
            }
            int h=0;
            foreach(var house in village.Cast<Transform>().Where(t=>t.name.StartsWith("Nha_Dan_")).OrderBy(t=>t.name)) Yard(detail,house,h++);
            int s=0;
            foreach(var stall in village.Cast<Transform>().Where(t=>t.name=="Mai_Che_Hang_Cho" || t.name=="Quay_Ban_Ca")) Stall(detail,stall,s++);
            foreach(var goods in life.Cast<Transform>().Where(t=>t.name=="Hang_Hoa_Canh_Cho")) BringGoodsToMarket(village,goods);
            foreach(int side in new[]{-1,1})
            {
                foreach(float z in new[]{89f,93f,87f})
                {
                    var seat=TryMesh(detail,"Ghe_Nghi_San_Chung",table,wood,Ground(village.TransformPoint(new Vector3(side*9,0,z))),village.rotation,new Vector3(.7f,.53f,.65f));
                    if(seat!=null){seats++;break;}
                }
            }
        }

        void Yard(Transform parent, Transform house, int index)
        {
            var group=Group("San_"+house.name,parent); group.SetPositionAndRotation(house.position,house.rotation);
            int placed=0;
            // Props cluster beside the existing doorway goods; the middle approach stays empty.
            var candidates=new[]{new Vector2(4.8f,7.3f),new Vector2(-4.9f,7.2f),new Vector2(3.1f,6.7f),new Vector2(-3.0f,6.8f),
                new Vector2(5.8f,8.4f),new Vector2(-5.7f,8.2f),new Vector2(4.9f,9.6f),new Vector2(-4.8f,9.5f),new Vector2(6.3f,5.7f),new Vector2(-6.3f,5.8f)};
            foreach(var offset in candidates.Skip(index%2).Concat(candidates.Take(index%2)))
            {
                bool water=(placed+index)%3!=1;
                var point=Ground(house.TransformPoint(new Vector3(offset.x,0,offset.y)));
                float scale=water ? .95f+(index%3)*.1f : 1.05f;
                var prop=TryMesh(group,water?"Chum_Nuoc":"Gio_Do_Sinh_Hoat",water?jar:basket,water?clay:straw,
                    point,Quaternion.Euler(0,house.eulerAngles.y+index*37,0),Vector3.one*scale);
                if(prop!=null && ++placed==3)break;
            }
            if(placed>0)yards++;
            if(index%8==0)
            {
                foreach(int side in new[]{-1,1})
                {
                    Vector3 point=Ground(house.TransformPoint(new Vector3(side*7.6f,0,2.4f)));
                    var pose=Matrix4x4.TRS(point,house.rotation,Vector3.one*F);
                    var bounds=WorldBounds(new Bounds(new Vector3(0,1.3f,0),new Vector3(3.6f,2.7f,2.8f)),pose);
                    if(!Safe(bounds,null,true))continue;
                    MeshObject(group,"Khung_Mai_Hien",frame,wood,point,house.rotation,Vector3.one);
                    MeshObject(group,"Mai_Vai_San_Nha",canopy,index%16==0?linen:indigo,point,house.rotation,Vector3.one);
                    canopies++;break;
                }
            }
        }

        void Stall(Transform parent, Transform stall, int index)
        {
            var group=Group("Sap_Hang_"+index,parent); group.SetPositionAndRotation(stall.position,stall.rotation);
            var roof=stall.GetChild(0);
            Transform placed=null;
            foreach(float z in new[]{-.55f,-1.0f,-1.45f})
            {
                var point=Ground(stall.TransformPoint(new Vector3(0,0,z)));
                placed=TryMesh(group,"Ban_Bay_Hang",table,wood,point,stall.rotation,Vector3.one,roof);
                if(placed!=null)break;
            }
            if(placed==null)return;
            tables++;
            for(int n=0;n<3;n++)
            {
                bool vessel=(index+n)%3==0;
                var point=placed.TransformPoint(new Vector3(-1.0f+n,.95f,0));
                // These items intentionally sit on the table, fully inside its tested footprint.
                MeshObject(group,vessel?"Chum_Hang_Cho":"Gio_Hang_Cho",vessel?jar:basket,vessel?clay:straw,
                    point,stall.rotation,Vector3.one*(vessel?.65f:.80f));
            }
        }

        void BringGoodsToMarket(Transform village, Transform goods)
        {
            var original=goods.position; var originalBounds=BachDangSettlementAssets.BoundsOf(goods.gameObject);
            float side=Mathf.Sign(village.InverseTransformPoint(original).x);
            foreach(var offset in new[]{new Vector2(side*23,69),new Vector2(side*24,75),new Vector2(side*24,80),new Vector2(side*24,62)})
            {
                Vector3 point=Ground(village.TransformPoint(new Vector3(offset.x,0,offset.y)));
                var bounds=originalBounds;bounds.center+=point-original;
                if(!Safe(bounds,goods,true))continue;
                Undo.RegisterFullObjectHierarchyUndo(goods.gameObject,"Gather goods around the market square");
                goods.position=point;
                foreach(Transform child in goods)
                {
                    if(child.GetComponent<MeshFilter>()!=null)continue; // Ground patch moves with the whole yard.
                    var b=BachDangSettlementAssets.BoundsOf(child.gameObject);
                    child.position+=Vector3.up*(Height(b.center)+.025f*F-b.min.y);
                    EditorUtility.SetDirty(child);
                }
                EditorUtility.SetDirty(goods);movedGoods++;break;
            }
        }

        bool Safe(Bounds bounds, Transform ignore, bool avoidPaths)
        {
            if(bounds.min.y<16.4f*F)return false;
            float low=float.MaxValue,high=float.MinValue;
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            {float y=Height(bounds.center+new Vector3(x*bounds.extents.x,0,z*bounds.extents.z));low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
            if(high-low>.4f*F)return false;
            foreach(var obstacle in obstacles)
            {
                if(obstacle==null || !obstacle.enabled || (ignore!=null && obstacle.transform.IsChildOf(ignore)))continue;
                if(Overlap(bounds,obstacle.bounds,.07f*F))return false;
            }
            return !avoidPaths || !paths.Any(b=>Overlap(bounds,b,.08f*F));
        }

        Transform TryMesh(Transform parent,string name,Mesh mesh,Material mat,Vector3 point,Quaternion rotation,Vector3 scale,Transform ignore=null)
        {
            var bounds=WorldBounds(mesh.bounds,Matrix4x4.TRS(point,rotation,scale*F));
            if(!Safe(bounds,ignore,true))return null;
            return MeshObject(parent,name,mesh,mat,point,rotation,scale);
        }

        Transform MeshObject(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Quaternion rotation,Vector3 scale)
        {
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Dress courtyard");
            go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go,0);obstacles.Add(renderer);
            return go.transform;
        }

        static Bounds WorldBounds(Bounds local,Matrix4x4 matrix)
        {
            var result=new Bounds(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z))));
            return result;
        }

        Material Material(string name,string source,Color tint)
        {
            var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/BachDangScaleAndLife/"+source+".mat");
            if(original==null)throw new InvalidOperationException("Missing shared material: "+source);
            var result=new Material(original){name=name,enableInstancing=true};result.SetColor("_BaseColor",tint);
            result.SetFloat("_Smoothness",.06f);Save(result,name+".mat");return result;
        }

        Mesh Lathe(string name,int sides,Vector2[] profile)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int row=0;row<profile.Length;row++)for(int side=0;side<=sides;side++)
            {
                float angle=side*Mathf.PI*2/sides;
                vertices.Add(new Vector3(Mathf.Cos(angle)*profile[row].x,profile[row].y,Mathf.Sin(angle)*profile[row].x));
                uv.Add(new Vector2((float)side/sides,profile[row].y));
                if(row==0 || side==sides)continue;
                int a=(row-1)*(sides+1)+side,b=a+1,c=row*(sides+1)+side,d=c+1;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);Finish(mesh);return mesh;
        }

        Mesh Boxes(string name,List<(Vector3 center,Vector3 size)> boxes)
        {
            var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var combined=boxes.Select(b=>new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(b.center,Quaternion.identity,b.size)}).ToArray();
            var mesh=new Mesh{name=name};mesh.CombineMeshes(combined,true,true,false);Finish(mesh);return mesh;
        }

        void Finish(Mesh mesh){mesh.RecalculateNormals();mesh.RecalculateBounds();Save(mesh,mesh.name+".asset");}
        void Save(UnityEngine.Object asset,string name){string path=Folder+"/"+name;AssetDatabase.CreateAsset(asset,path);assets.Add(path);}
    }
}
