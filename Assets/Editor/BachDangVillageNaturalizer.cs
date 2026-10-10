using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Reworks the three existing villages without replacing their buildings or actors.</summary>
public static class BachDangVillageNaturalizer
{
    const string Folder = "Assets/BachDangVillageNatural";
    const string Marker = "10_Lang_Tu_Nhien";
    const float F = 7f;
    static readonly string[] Villages = { "01_Lang_Cho_Ben_Song", "02_Xom_Chai_Bo_Dong", "03_Xom_Vuon_Bo_Tay" };

    [MenuItem("Bach Dang/Natural Village Layout And Ground")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/beachBoat.unity" ||
            !Mathf.Approximately(BachDangWorldScale.ForScene(scene), F))
            throw new InvalidOperationException("Open the improved, scale-seven beachBoat in Edit Mode.");
        var root = scene.GetRootGameObjects().First(g => g.name == "BachDang_Living_Settlements").transform;
        if (root.Find(Marker) != null) { Debug.Log("Natural village layout is already present; no repeated moves."); return; }
        if (AssetDatabase.IsValidFolder(Folder) && AssetDatabase.FindAssets("", new[] { Folder }).Length != 0)
            throw new InvalidOperationException("Owned natural-village assets already exist. Restore their scene instead of replacing them.");
        var terrain = scene.GetRootGameObjects().Select(g => g.GetComponent<Terrain>()).First(t => t != null);
        string terrainPath = AssetDatabase.GetAssetPath(terrain.terrainData);
        if (terrainPath != "Assets/BachDangScaleAndLife/BeachBoat_Player7Terrain.asset")
            throw new InvalidOperationException("The completed scaled terrain is required.");
        var actors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            .ToDictionary(r => r, r => r.transform.localToWorldMatrix);
        var protectedGroups = new[] { "04_Trai_Tiep_Van", "05_Trai_Du_Bi", "06_Trai_Bo_Dong", "08_Ben_Ca_Dan_Sinh" }
            .Select(n => root.Find(n)).SelectMany(t => t.GetComponentsInChildren<Transform>(true))
            .ToDictionary(t => t, t => t.localToWorldMatrix);
        Directory.CreateDirectory(".utmp/NaturalVillage");
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/NaturalVillage/beachBoat-before-" + stamp + ".unity", true))
            throw new IOException("Scene backup failed.");
        AssetDatabase.SaveAssetIfDirty(terrain.terrainData);
        File.Copy(terrainPath, ".utmp/NaturalVillage/terrain-before-" + stamp + ".asset");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "BachDangVillageNatural");
        var assets = new List<string>();
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Natural village layout and ground");
        try
        {
            var group = Group(Marker, root);
            var builder = new Builder(terrain, root, group, assets);
            for (int index = 0; index < Villages.Length; index++) builder.Village(root.Find(Villages[index]), index);
            long newMeshBytes = assets.Where(p => p.EndsWith(".asset", StringComparison.Ordinal))
                .Select(p => AssetDatabase.LoadAssetAtPath<Mesh>(p)).Where(m => m != null)
                .Sum(m => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m));
            if (newMeshBytes > 12L * 1024 * 1024)
                throw new InvalidOperationException("New village geometry exceeds its 12 MiB laptop budget.");
            BachDangScaledTerrain.ClearNewDetails(terrain, builder.clearings, F);
            Physics.SyncTransforms();
            foreach (var pair in actors)
                if (pair.Key == null || pair.Key.transform.localToWorldMatrix != pair.Value)
                    throw new InvalidOperationException("An existing actor was changed.");
            foreach (var pair in protectedGroups)
                if (pair.Key == null || pair.Key.localToWorldMatrix != pair.Value)
                    throw new InvalidOperationException("A camp or landing was changed.");
            var all = root.GetComponentsInChildren<Transform>(true);
            if (all.Count(t => t.name.StartsWith("Nha_Dan_")) != 60 || all.Count(t => t.name.StartsWith("Lan_Quan_")) != 18)
                throw new InvalidOperationException("Existing building count changed.");
            string result = $"Natural villages saved: {builder.moved} households shifted/rotated, {builder.paths} curved paths, {builder.yards} house forecourts, {builder.fences} new garden fences, {builder.tufts} grass tufts combined in six meshes. 60 homes, 18 barracks and {actors.Count} actors retained.";
            File.WriteAllText(".utmp/NaturalVillage/result.txt", result);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the natural villages.");
            Undo.CollapseUndoOperations(undo);
            Debug.Log(result);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            EditorUtility.SetDirty(terrain.terrainData);
            AssetDatabase.SaveAssetIfDirty(terrain.terrainData);
            terrain.Flush();
            var nature = terrain.GetComponent<VisualDesignCafe.Rendering.Nature.NatureRenderer>();
            if (nature != null && nature.isActiveAndEnabled) nature.Restart();
            foreach (string asset in assets.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(asset);
            throw;
        }
    }

    static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Natural village details");
        go.transform.SetParent(parent, false); return go.transform;
    }

    sealed class Builder
    {
        readonly Terrain terrain;
        readonly Transform settlements, root;
        readonly BachDangVillageGround painter;
        readonly List<string> assets;
        readonly Material meadow;
        readonly List<Part> parts = new List<Part>();
        readonly List<Bounds> grassExclusions = new List<Bounds>();
        public readonly List<Bounds> clearings = new List<Bounds>();
        public int moved, paths, yards, fences, tufts;
        int seed;

        public Builder(Terrain terrain, Transform settlements, Transform root, List<string> assets)
        {
            this.terrain = terrain; this.settlements = settlements; this.root = root; this.assets = assets;
            painter = new BachDangVillageGround(terrain, F, Folder, assets);
            var grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainSampleAssets/TerrainLayers/Grass_B_TerrainLayer.terrainlayer");
            meadow = new Material(painter.Earth) { name = "Village_Natural_Regrowth" };
            meadow.SetTexture("_BaseMap", grassLayer.diffuseTexture);
            meadow.SetColor("_BaseColor", new Color(.82f, .91f, .70f, .57f));
            AssetDatabase.CreateAsset(meadow, Folder + "/Village_Natural_Regrowth.mat"); assets.Add(Folder + "/Village_Natural_Regrowth.mat");
        }

        float Height(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
        Vector3 Ground(Vector3 p) { p.y = Height(p); return p; }
        static Bounds Bounds(Transform t) => BachDangSettlementAssets.BoundsOf(t.gameObject);
        static float Area(Bounds a, Bounds b, float margin = 0)
        {
            float x = Mathf.Min(a.max.x + margin, b.max.x) - Mathf.Max(a.min.x - margin, b.min.x);
            float z = Mathf.Min(a.max.z + margin, b.max.z) - Mathf.Max(a.min.z - margin, b.min.z);
            return Mathf.Max(0, x) * Mathf.Max(0, z);
        }
        static bool Inside(Bounds b, Vector3 p, float margin) => p.x > b.min.x - margin && p.x < b.max.x + margin && p.z > b.min.z - margin && p.z < b.max.z + margin;

        public void Village(Transform village, int index)
        {
            seed = 9380 + index * 1000;
            var detail = Group(village.name + "_Canh_Quan", root);
            detail.SetPositionAndRotation(village.position, village.rotation);
            var houses = village.Cast<Transform>().Where(t => t.name.StartsWith("Nha_Dan_")).OrderBy(t => t.name).ToArray();
            var life = settlements.Find("09_Sinh_Hoat/" + village.name + "_Sinh_Hoat");
            parts.Clear(); grassExclusions.Clear();
            foreach (var house in houses)
                foreach (Transform child in house)
                    if (child.GetComponentsInChildren<MeshRenderer>(false).Length > 0) parts.Add(new Part(house, child));
            foreach (Transform child in village)
                if (!child.name.StartsWith("Nha_Dan_") && child.GetComponent<MeshFilter>() == null && child.GetComponentsInChildren<MeshRenderer>().Length > 0)
                    parts.Add(new Part(null, child));
            foreach (Transform child in life)
                if (child.GetComponent<MeshFilter>() == null && child.GetComponentsInChildren<MeshRenderer>().Length > 0)
                    parts.Add(new Part(null, child));

            for (int h = 0; h < houses.Length; h++) MoveHouse(village, houses[h], h, index);
            foreach (var part in parts) clearings.Add(Bounds(part.transform));
            foreach (Transform old in village)
                if (old.GetComponent<MeshFilter>() != null && new[] { "Duong_Chinh", "Ngo_Nho", "Loi_Ngang", "San_Cho" }.Contains(old.name))
                { var r = old.GetComponent<MeshRenderer>(); Undo.RecordObject(r, "Replace straight village path"); r.enabled = false; EditorUtility.SetDirty(r); }

            // Smooth lanes stay inside the occupied village, with their old outer connection points retained.
            Path(detail, "Duong_Lang_Uon", 4.6f, Lane(village, 0, -92, 96));
            Path(detail, "Ngo_Tay", 2.6f, Lane(village, -1, -88, 82));
            Path(detail, "Ngo_Dong", 2.6f, Lane(village, 1, -88, 82));
            foreach (float z in index == 2 ? new[] { -50f, 3f, 53f } : new[] { -56f, -10f, 33f })
                Path(detail, "Ngo_Noi_Xom", 2.35f, CrossLane(village, z));
            Path(detail, "Ngo_Cho", 3.0f, new[] { W(village, -61, 84), W(village, -28, 82), W(village, 0, 84), W(village, 29, 85), W(village, 61, 84) });
            Patch(detail, "San_Cho_Dat_Nen", W(village, 0, 73), village.eulerAngles.y, 35, 37, painter.Earth, true);

            foreach (var house in houses)
            {
                var front = house.position + house.forward * (6.5f * F);
                Patch(detail, "San_Truoc_" + house.name, front, house.eulerAngles.y, 10, 7.3f, painter.Earth, true); yards++;
                Vector3 local = village.InverseTransformPoint(house.position);
                int lane = Mathf.Abs(local.x) < 33 ? 0 : (local.x < 0 ? -1 : 1);
                var porch = house.position + house.forward * (5.7f * F);
                var connection = W(village, ChooseLaneX(village, local.z, lane), local.z);
                var middle = Vector3.Lerp(front, connection, .5f) + house.right * (.65f * F);
                Path(detail, "Loi_Cua_" + house.name, 1.6f, new[] { porch, front, middle, connection });
                if (house.Find("Hang_Rao_San_Sau") == null) AddFence(detail, house);
            }
            DressLife(detail, life);
            ScatterMeadow(detail, village, houses, index);
        }

        void MoveHouse(Transform village, Transform house, int index, int villageIndex)
        {
            var originalPosition = house.position; var originalRotation = house.rotation;
            var local = house.localPosition; bool outer = Mathf.Abs(local.x) > 33;
            float sign = Mathf.Sign(local.x);
            float dx = Curve(local.z) * (outer ? .55f : .85f) + Mathf.Sin(index * 1.73f + villageIndex) * 2.8f;
            float dz = (outer ? sign * 4.8f : -sign * 2.5f) + Mathf.Sin(index * 2.13f + .4f) * 3.3f;
            float angle = Mathf.Cos((local.z + 92) * Mathf.PI * 2 / 188) * 9f + Mathf.Sin(index * 1.51f + 1.2f) * 13f;
            var owned = parts.Where(p => p.house == house).ToArray();
            var childPoses = owned.ToDictionary(p => p.transform, p => p.transform.localPosition);
            var before = owned.ToDictionary(p => p.transform, p => Bounds(p.transform));
            Undo.RegisterFullObjectHierarchyUndo(house.gameObject, "Rearrange household with its yard");
            bool accepted = false;
            foreach (float amount in new[] { 1f, .75f, .5f, .25f })
            {
                foreach (var pose in childPoses) pose.Key.localPosition = pose.Value;
                house.SetPositionAndRotation(village.TransformPoint(local + new Vector3(dx, 0, dz) * amount),
                    Quaternion.AngleAxis(angle * amount, Vector3.up) * originalRotation);
                if (!Reground(house, owned)) continue;
                bool blocked = false;
                foreach (var part in owned)
                {
                    var now = Bounds(part.transform);
                    foreach (var other in parts.Where(p => p.house != house))
                    {
                        var otherBounds = Bounds(other.transform);
                        if (Area(now, otherBounds, .25f * F) > Area(before[part.transform], otherBounds, .25f * F) + .03f * F * F)
                        { blocked = true; break; }
                    }
                    if (blocked) break;
                }
                if (!blocked) { accepted = true; break; }
            }
            if (!accepted)
            {
                house.SetPositionAndRotation(originalPosition, originalRotation);
                foreach (var pose in childPoses) pose.Key.localPosition = pose.Value;
            }
            else moved++;
            EditorUtility.SetDirty(house);
            foreach (var part in owned)
            { EditorUtility.SetDirty(part.transform); if (PrefabUtility.IsPartOfPrefabInstance(part.transform)) PrefabUtility.RecordPrefabInstancePropertyModifications(part.transform); }
        }

        bool Reground(Transform house, Part[] owned)
        {
            foreach (var part in owned)
            {
                var bounds = Bounds(part.transform); float low = float.MaxValue, high = float.MinValue;
                for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                { float y = Height(bounds.center + new Vector3(x * bounds.extents.x * .8f, 0, z * bounds.extents.z * .8f)); low = Mathf.Min(low, y); high = Mathf.Max(high, y); }
                if (low < 16.8f * F || high - low > .75f * F) return false;
                if (part.transform == house.GetChild(0)) house.position += Vector3.up * (high + .02f * F - bounds.min.y);
                else part.transform.position += Vector3.up * (high + .02f * F - bounds.min.y);
            }
            return true;
        }

        static float Curve(float z) => 7.5f * Mathf.Sin((z + 92) * Mathf.PI * 2 / 188);
        Vector3 W(Transform village, float x, float z) => Ground(village.TransformPoint(new Vector3(x, 0, z)));
        float ChooseLaneX(Transform village, float z, int side)
        {
            float preferred = side == 0 ? Curve(z) : side * 34 + Curve(z) * .55f;
            if (z > 63 || z < -76) preferred *= side == 0 ? Mathf.Clamp01((96 - z) / 30) : 1;
            float half = side == 0 ? 2.5f : 1.55f;
            float best = preferred, cost = float.MaxValue;
            for (float dx = -5; dx <= 5; dx += .5f)
            {
                float x = preferred + dx; var point = W(village, x, z);
                bool blocked = parts.Any(p => Inside(Bounds(p.transform), point, half * F));
                float score = Mathf.Abs(dx) + (blocked ? 1000 : 0);
                if (score < cost) { cost = score; best = x; }
            }
            return best;
        }
        Vector3[] Lane(Transform village, int side, float start, float end)
        {
            var result = new List<Vector3>();
            for (float z = start; z < end; z += 7) result.Add(W(village, ChooseLaneX(village, z, side), z));
            result.Add(W(village, side * 33, end));
            result[0] = W(village, side * 33, start);
            return result.ToArray();
        }
        Vector3[] CrossLane(Transform village, float z)
        {
            var result = new List<Vector3>();
            for (float x = -61; x <= 61; x += 6.1f)
            {
                float preferred = z + 2.5f * Mathf.Sin(x * .06f), best = preferred, cost = float.MaxValue;
                for (float dz = -5; dz <= 5; dz += .5f)
                {
                    var p = W(village, x, preferred + dz);
                    float score = Mathf.Abs(dz) + (parts.Any(part => Inside(Bounds(part.transform), p, 1.3f * F)) ? 1000 : 0);
                    if (score < cost) { cost = score; best = preferred + dz; }
                }
                result.Add(W(village, x, best));
            }
            result[0] = W(village, -61, z); result[result.Count - 1] = W(village, 61, z);
            return result.ToArray();
        }

        void Path(Transform parent, string name, float width, Vector3[] points)
        {
            var obstacles = parts.Select(p => Bounds(p.transform)).ToArray();
            Vector3[] route = null;
            InvalidOperationException failure = null;
            float requestedWidth = width;
            // Existing yards take priority; an alley can narrow before taking a detour.
            foreach (float fraction in new[] { 1f, .8f, .65f })
            {
                width = Mathf.Max(1.2f, requestedWidth * fraction);
                try { route = BachDangVillageRoutes.Route(points, width * 1.1f * F * .5f, parent, obstacles, F); break; }
                catch (InvalidOperationException error) { failure = error; }
            }
            if (route == null)
            {
                File.WriteAllText(".utmp/NaturalVillage/blocked-route.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    village = parent.name, name, width,
                    points = points.Select(p => new { p.x, p.z }).ToArray(),
                    obstacles = parts.Select(p => new { p.transform.name, minX = Bounds(p.transform).min.x, maxX = Bounds(p.transform).max.x,
                        minZ = Bounds(p.transform).min.z, maxZ = Bounds(p.transform).max.z }).ToArray()
                }, Newtonsoft.Json.Formatting.Indented));
                throw new InvalidOperationException(parent.name + "/" + name + ": " + failure.Message, failure);
            }
            painter.PathPointAllowed = (point, halfWidth) => !obstacles.Any(b => Inside(b, point, halfWidth + .15f * F));
            painter.PathSegmentAllowed = (a, b, halfWidth) => BachDangVillageRoutes.SegmentClear(a, b, obstacles, halfWidth + .15f * F);
            var areas = new List<Bounds>();
            painter.CreatePath(parent, name, route, width, painter.Earth, seed++, areas);
            clearings.AddRange(areas); grassExclusions.AddRange(areas); paths++;
        }
        void Patch(Transform parent, string name, Vector3 point, float yaw, float width, float depth, Material material, bool excludeGrass)
        {
            var areas = new List<Bounds>();
            painter.CreatePatch(parent, name, point, yaw, width, depth, material, seed++, areas);
            if (excludeGrass) { clearings.AddRange(areas); grassExclusions.AddRange(areas); }
        }
        void AddFence(Transform parent, Transform house)
        {
            Vector3 point = Ground(house.position - house.forward * (7.8f * F));
            var fence = BachDangSettlementAssets.Place("Assets/Prefabs/Props/pf_fence_02_double.prefab", "Rao_San_" + house.name,
                parent, point, house.eulerAngles.y + 6, .78f);
            var bounds = Bounds(fence.transform);
            if (parts.Any(p => p.house != house && Area(bounds, Bounds(p.transform), .4f * F) > 0) ||
                grassExclusions.Any(b => Area(bounds, b) > 0))
            { Undo.DestroyObjectImmediate(fence); return; }
            clearings.Add(bounds); parts.Add(new Part(null, fence.transform)); fences++;
        }

        void DressLife(Transform detail, Transform life)
        {
            painter.PathPointAllowed = null; // Crop rows deliberately lie inside their existing garden footprint.
            painter.PathSegmentAllowed = null;
            foreach (Transform area in life)
            {
                if (area.name.StartsWith("Vuon_Rau_"))
                {
                    for (int row = 0; row < 4; row++)
                    {
                        float z = -5.8f + row * 3.8f;
                        painter.CreatePath(detail, "Luong_Rau_" + area.name + "_" + row,
                            new[] { area.TransformPoint(new Vector3(-7, 0, z)), area.TransformPoint(new Vector3(0, 0, z + .12f)), area.TransformPoint(new Vector3(7, 0, z)) },
                            1.35f, painter.Mud, seed++, new List<Bounds>());
                    }
                }
                else if (area.name == "Gieng_Nuoc_Chung")
                {
                    Patch(detail, "Dat_Am_Quanh_Gieng", area.position, area.eulerAngles.y, 9, 7, painter.Mud, true);
                    Patch(detail, "Soi_Quanh_Gieng", area.position, area.eulerAngles.y, 5.3f, 5.3f, painter.Pebbles, true);
                }
                else if (area.name == "San_Bep_Va_Phoi_Thoc" || area.name == "San_Lam_Nghe")
                    Patch(detail, "Dat_Mon_San_Nghe", area.position, area.eulerAngles.y, 17, 14, painter.Earth, true);
            }
        }

        void ScatterMeadow(Transform detail, Transform village, Transform[] houses, int index)
        {
            var random = new System.Random(3981 + index);
            var centers = new List<Vector3>();
            foreach (var house in houses)
            {
                centers.Add(house.position + house.right * (7.4f * F) - house.forward * (1.5f * F));
                centers.Add(house.position - house.right * (7.4f * F) - house.forward * (2.4f * F));
            }
            foreach (int side in new[] { -1, 1 }) foreach (float z in new[] { -74f, -42f, -18f, 12f, 44f, 64f })
                centers.Add(W(village, side * 59, z));
            var points = new List<Vector3>();
            foreach (var center in centers)
            {
                float radius = Mathf.Lerp(2.4f, 4.4f, (float)random.NextDouble());
                var accepted = new List<Vector3>();
                // Small local accents, not another field of streamed vegetation.
                for (int i = 0; i < 8; i++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2, r = Mathf.Sqrt((float)random.NextDouble()) * radius * F;
                    var p = Ground(center + new Vector3(Mathf.Cos(angle) * r, 0, Mathf.Sin(angle) * r * .72f));
                    if (p.y < 16.8f * F || parts.Any(part => Inside(Bounds(part.transform), p, .5f * F)) ||
                        grassExclusions.Any(b => Inside(b, p, .65f * F))) continue;
                    accepted.Add(p);
                }
                if (accepted.Count < 4) continue;
                points.AddRange(accepted);
                // Broad, translucent regrowth islands replace the uniform lawn around the open spaces.
                var patch = Ground(accepted.Aggregate(Vector3.zero, (sum, p) => sum + p) / accepted.Count);
                var footprint = new Bounds(patch, new Vector3(2.1f * radius, 1, 2.1f * radius) * F);
                if (!grassExclusions.Any(b => Area(footprint, b) > 0) && !parts.Any(p => Area(footprint, Bounds(p.transform)) > 0))
                    Patch(detail, "Co_Loang_Ven_Xom", patch, seed % 180, radius * 1.8f, radius * 1.35f, meadow, false);
            }
            // Even a dense village stays within a fixed geometry budget on 16 GB laptops.
            var boundedPoints = points.OrderBy(p => random.Next()).Take(240).ToArray();
            tufts += painter.AddGrass(detail, "Co_Thap_Chan_Rao", boundedPoints, seed++);
        }
        readonly struct Part
        {
            public readonly Transform house, transform;
            public Part(Transform house, Transform transform) { this.house = house; this.transform = transform; }
        }
    }
}
