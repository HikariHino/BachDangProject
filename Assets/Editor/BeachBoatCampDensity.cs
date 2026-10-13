using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Editor-only dressing. All repeated shelters share the existing optimized meshes/materials.
public static class BeachBoatCampDensity
{
    const string Folder = "Assets/BeachBoatStory/CampLife";
    static Terrain terrain;
    static Vector2[] polygon;
    static readonly List<Vector3> occupied = new List<Vector3>();
    static readonly List<float> radii = new List<float>();
    static readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
    static Vector3 spawn, gate, commander;
    static int tents, props, stores, yards;
    static readonly List<Vector3> vertices = new List<Vector3>();
    static readonly List<Vector2> uvs = new List<Vector2>();
    static readonly List<Color> colors = new List<Color>();
    static readonly List<int> indices = new List<int>();
    static float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
    static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
    static float Segment(Vector2 p, Vector2 a, Vector2 b)
    { var e = b - a; return Vector2.Distance(p, a + e * Mathf.Clamp01(Vector2.Dot(p - a, e) / Mathf.Max(.001f, e.sqrMagnitude))); }
    static bool Interior(Vector3 p, float margin)
    {
        var q = Flat(p); bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[j]; var b = polygon[i];
            if (Segment(q, a, b) < margin) return false;
            if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside && Ground(p) > 111;
    }
    static bool Available(Vector3 p, float radius)
    {
        if (!Interior(p, radius + 22) || Vector2.Distance(Flat(p), Flat(spawn)) < radius + 40 ||
            Vector2.Distance(Flat(p), Flat(commander)) < radius + 65 ||
            Segment(Flat(p), Flat(spawn), Flat(gate)) < radius + 25) return false;
        for (int i = 0; i < occupied.Count; i++) if (Vector2.Distance(Flat(p), Flat(occupied[i])) < radius + radii[i] + 10) return false;
        var samples = new[] { p, p + Vector3.right * radius, p - Vector3.right * radius, p + Vector3.forward * radius, p - Vector3.forward * radius };
        return samples.Max(Ground) - samples.Min(Ground) < 3;
    }
    static void Seat(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        go.transform.position += Vector3.up * (Ground(go.transform.position) - .12f - rs.Min(r => r.bounds.min.y));
    }
    static Material Shared(Material source)
    {
        if (source == null) return null;
        if (source.enableInstancing) return source;
        if (materials.TryGetValue(source, out var value)) return value;
        value = new Material(source) { name = source.name + "_CampLife", enableInstancing = true };
        AssetDatabase.CreateAsset(value, Folder + "/Material_" + materials.Count + ".mat"); materials.Add(source, value); return value;
    }
    static void Clean(GameObject go)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) if (c != null) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(c.gameObject);
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) { GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0); GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject); }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = r.sharedMaterials.Select(Shared).ToArray();
    }
    static void Box(GameObject go)
    {
        var bounds = new Bounds(); bool first = true;
        foreach (var f in go.GetComponentsInChildren<MeshFilter>())
        {
            var b = f.sharedMesh.bounds;
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                var p = go.transform.InverseTransformPoint(f.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
        }
        var box = go.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
    }
    static GameObject Shelter(GameObject template, Transform parent, Vector3 p, float turn)
    {
        if (!Available(p, 42)) return null;
        var go = Object.Instantiate(template); go.name = "Leu_Linh_" + (++tents).ToString("000"); Clean(go);
        go.transform.SetParent(parent, true); go.transform.position = p;
        go.transform.rotation = Quaternion.AngleAxis(turn, Vector3.up) * template.transform.rotation;
        Seat(go); Box(go); occupied.Add(go.transform.position); radii.Add(42); return go;
    }
    static GameObject Prefab(string path, string name, Transform parent, Vector3 p, float scale, float yaw, bool collision)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new Exception("Missing " + path);
        var go = Object.Instantiate(source); go.name = name; Clean(go);
        go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0)); go.transform.localScale = Vector3.one * scale; Seat(go);
        if (go.GetComponentsInChildren<LODGroup>().Length == 0)
        { var group = go.AddComponent<LODGroup>(); group.SetLODs(new[] { new LOD(.035f, go.GetComponentsInChildren<Renderer>()) }); group.RecalculateBounds(); }
        if (collision) Box(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }
    static void Path(Vector3 a, Vector3 b, float width, bool yard = false)
    {
        var delta = Flat(b) - Flat(a); float length = delta.magnitude; if (length < .1f) return;
        var normal = new Vector3(-delta.y, 0, delta.x).normalized; int steps = Mathf.Max(2, Mathf.CeilToInt(length / 24)); int previous = -1;
        for (int i = 0; i <= steps; i++)
        {
            var p = Vector3.Lerp(a, b, (float)i / steps);
            if (!Interior(p, width * .6f)) { previous = -1; continue; }
            if (!yard && occupied.Select((o, index) => Vector2.Distance(Flat(o), Flat(p)) < radii[index] + width * .4f).Any(x => x)) { previous = -1; continue; }
            int offset = vertices.Count;
            foreach (float across in new[] { 0f, .10f, .90f, 1f })
            {
                var q = p + normal * ((across - .5f) * width); q.y = Ground(q) + .32f;
                vertices.Add(q); uvs.Add(new Vector2(q.x / 28, q.z / 28)); colors.Add(new Color(1, 1, 1, across == 0 || across == 1 ? 0 : .86f));
            }
            if (previous >= 0) for (int col = 0; col < 3; col++) indices.AddRange(new[] { previous + col, previous + col + 1, offset + col, previous + col + 1, offset + col + 1, offset + col });
            previous = offset;
        }
    }

    [MenuItem("Bach Dang/Dress Island Military Camp")]
    public static void Run() => Debug.Log(Apply());
    public static int ClearTreesOnPaths()
    {
        if (Application.isPlaying) throw new Exception("Edit Mode required.");
        var camp = GameObject.Find("TestQuest_Camp");
        var filter = camp.transform.Find("Island_Camp_Life/Duong_Dat_Va_San_Tap").GetComponent<MeshFilter>();
        var points = filter.sharedMesh.vertices.Select(v => Flat(filter.transform.TransformPoint(v))).ToArray(); var tris = filter.sharedMesh.triangles;
        Func<Vector2, Vector2, Vector2, float> cross = (a, b, c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        int removed = 0;
        foreach (var tree in Object.FindObjectsByType<LODGroup>().Where(g => g.name == "Conifer" && !g.transform.IsChildOf(camp.transform)).ToArray())
        {
            var p = Flat(tree.transform.position);
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = points[tris[i]]; var b = points[tris[i + 1]]; var c = points[tris[i + 2]];
                float x = cross(a, b, p), y = cross(b, c, p), z = cross(c, a, p);
                if ((x >= 0 && y >= 0 && z >= 0) || (x <= 0 && y <= 0 && z <= 0))
                { Undo.DestroyObjectImmediate(tree.gameObject); removed++; break; }
            }
        }
        return removed;
    }
    public static string Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity") throw new Exception("Open beachBoat in Edit Mode.");
        var camp = GameObject.Find("TestQuest_Camp");
        if (camp == null || camp.transform.Find("Island_Camp_Life") != null || AssetDatabase.IsValidFolder(Folder)) throw new Exception("Camp missing or dressing already exists.");
        terrain = Terrain.activeTerrain; occupied.Clear(); radii.Clear(); materials.Clear(); vertices.Clear(); uvs.Clear(); colors.Clear(); indices.Clear(); tents = props = stores = yards = 0;
        var people = Object.FindObjectsByType<SkinnedMeshRenderer>().ToDictionary(r => r, r => r.transform.localToWorldMatrix);
        var initialMeshes = Object.FindObjectsByType<MeshFilter>().Select(f => f.sharedMesh).Where(m => m != null).Distinct().ToArray();
        var initialTextures = AssetDatabase.GetDependencies(scene.path, true).Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(Texture2D)).ToArray();
        var originals = camp.transform.Cast<Transform>().Where(t => t.name.Contains("Nomad")).OrderBy(t => t.position.x).ToArray();
        var fence = camp.transform.Cast<Transform>().Single(t => t.name.Contains("HANG RAO"));
        polygon = fence.Cast<Transform>().Where(t => t.name.StartsWith("HangRao"))
            .GroupBy(t => int.Parse(t.name.Split('_')[2])).OrderBy(g => g.Key).SelectMany(g => {
                var numbered = g.Where(t => !t.name.Contains("(")).OrderBy(t => int.Parse(t.name.Split('_')[3])).ToArray(); var d = numbered.Last().position - numbered.First().position;
                return g.OrderBy(t => Vector3.Dot(t.position, d)); }).Select(t => Flat(t.position)).ToArray();
        spawn = GameObject.Find("CampArrival").transform.position; gate = GameObject.Find("CampToVillage").transform.position;
        commander = camp.transform.Cast<Transform>().Single(t => t.name.Contains("Rustic_Wooden_Stage")).position;
        foreach (var t in originals) { occupied.Add(t.position); radii.Add(42); }
        occupied.Add(commander); radii.Add(65);
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Dress island military camp");
        var root = new GameObject("Island_Camp_Life"); root.transform.SetParent(camp.transform, false); Undo.RegisterCreatedObjectUndo(root, "Create camp districts");
        AssetDatabase.CreateFolder("Assets/BeachBoatStory", "CampLife");
        try
        {
            var centers = new List<Vector3> { spawn, gate };
            var along = new Vector3(-.96f, 0, .28f).normalized; var across = new Vector3(-along.z, 0, along.x);
            int district = 0;
            foreach (var seed in originals)
            {
                var group = new GameObject("Cum_Quan_" + (++district).ToString("00")); group.transform.SetParent(root.transform, false);
                var origin = seed.position; group.transform.position = origin;
                int added = 0;
                // Several rows form a readable unit around each original shelter, with wide footpaths.
                for (int ring = 1; ring <= 3 && added < 5; ring++)
                foreach (var offset in new[] { across * 115 * ring, -across * 115 * ring, along * 115 * ring, -along * 115 * ring, (along + across) * 115 * ring, (along - across) * 115 * ring })
                {
                    if (added >= 5) break;
                    var tent = Shelter(seed.gameObject, group.transform, origin + offset, added % 2 == 0 ? 0 : 180);
                    if (tent != null) { added++; Path(origin + along * 56, tent.transform.position + along * 56, 16); }
                }
                var center = origin + along * 62; centers.Add(center);
                Path(center - across * 75, center + across * 75, 30);
                var stock = origin - along * 58;
                if (Interior(stock, 25))
                {
                    Prefab("Assets/Prefabs/Props/pf_barrels_01.prefab", "Thung_Luong_Thuc", group.transform, stock, 5, district * 23, true); props++;
                    Prefab("Assets/Prefabs/Props/pf_logpile_01.prefab", "Cui_Dun", group.transform, stock + across * 28, 7, district * 23, false); props++;
                }
            }
            var command = new GameObject("Khu_Chi_Huy_Va_Tiep_Te"); command.transform.SetParent(root.transform, false);
            for (int side = -1; side <= 1; side += 2) for (int row = -2; row <= 5; row++)
                Shelter(originals[0].gameObject, command.transform, commander + new Vector3(side * 170, 0, row * 108), side < 0 ? 0 : 180);
            // Existing storage prefab and texture assets are already used elsewhere on beachBoat.
            foreach (var offset in new[] { new Vector3(-330, 0, -170), new Vector3(330, 0, -170), new Vector3(-330, 0, 230), new Vector3(330, 0, 230) })
            {
                var p = commander + offset; if (!Available(p, 95)) continue;
                var store = Prefab("Assets/Prefabs/Buildings/pf_build_big_storage_01.prefab", "Kho_Tiep_Te_" + (++stores), command.transform, p, 4.2f, 0, true);
                occupied.Add(p); radii.Add(95);
                for (int k = 0; k < 3; k++) { Prefab("Assets/Prefabs/Props/pf_plankpile_02.prefab", "Vat_Lieu_Dong_Thuyen", command.transform, p + new Vector3((k - 1) * 24, 0, 70), 7, 90, false); props++; }
            }
            // Broad packed-earth squares give the otherwise empty interior a clear military purpose.
            foreach (var p in new[] { commander + new Vector3(0, 0, 650), originals[8].position + across * 360, originals[17].position - across * 360 })
            {
                if (!Available(p, 150)) continue;
                Path(p - along * 150, p + along * 150, 210, true); yards++; centers.Add(p);
                for (int k = 0; k < 4; k++) { Prefab("Assets/Prefabs/Props/pf_logpile_01.prefab", "Go_Tap_Luyen", command.transform, p + along * 160 + across * ((k - 1.5f) * 40), 7, 0, false); props++; }
            }
            Path(gate, spawn, 28); Path(spawn, commander + Vector3.forward * 48, 38);
            // A minimal network connects the groups without an expensive runtime road system.
            var linked = new List<Vector3> { centers[0] }; var pending = centers.Skip(1).ToList();
            while (pending.Count > 0)
            {
                var edge = linked.SelectMany(a => pending.Select(b => new { a, b, d = (a - b).sqrMagnitude })).OrderBy(e => e.d).First();
                Path(edge.a, edge.b, 22); linked.Add(edge.b); pending.Remove(edge.b);
            }
            var mesh = new Mesh { name = "Island_Camp_Paths", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "/CampPaths.asset");
            var ground = new GameObject("Duong_Dat_Va_San_Tap"); ground.transform.SetParent(root.transform, false); ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); ground.transform.localScale = Vector3.one;
            ground.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = ground.AddComponent<MeshRenderer>(); renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/BachDangSettlements/Village_Packed_Earth.mat"); renderer.shadowCastingMode = ShadowCastingMode.Off;
            int removedSceneTrees = ClearTreesOnPaths();
            if (people.Any(p => p.Key == null || p.Key.transform.localToWorldMatrix != p.Value) || Object.FindObjectsByType<SkinnedMeshRenderer>().Length != people.Count) throw new Exception("Actor preservation failed.");
            Physics.SyncTransforms(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Undo.CollapseUndoOperations(undo);
            var newMeshes = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).Distinct().Except(initialMeshes).ToArray();
            var report = new { addedTents = tents, totalTents = originals.Length + tents, supplyBuildings = stores, trainingYards = yards, props,
                addedRenderers = root.GetComponentsInChildren<Renderer>().Length, addedColliders = root.GetComponentsInChildren<Collider>().Length,
                newMeshMiB = newMeshes.Sum(m => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m)) / 1048576f,
                newMeshNames = newMeshes.Select(m => m.name).ToArray(), newTextureDependencies = AssetDatabase.GetDependencies(scene.path, true).Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(Texture2D)).Except(initialTextures).ToArray(),
                actors = people.Count, trees = terrain.terrainData.treeInstanceCount, removedSceneTrees, pathVertices = vertices.Count };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented); System.IO.File.WriteAllText(".utmp/CampDensity/result.json", json); return json;
        }
        catch { Undo.RevertAllDownToGroup(undo); AssetDatabase.DeleteAsset(Folder); throw; }
    }
}
