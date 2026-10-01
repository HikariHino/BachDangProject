using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Additive village and supply-camp dressing; existing actors and buildings are untouched.</summary>
public static class BachDangSettlementBuilder
{
    public const string RootName = "BachDang_Living_Settlements";
    const string Folder = "Assets/BachDangSettlements";
    const string Buildings = "Assets/Prefabs/Buildings/pf_build_";
    const string Props = "Assets/Prefabs/Props/pf_";
    const string TerrainPath = Folder + "/BachDang_SettlementTerrain.asset";

    [MenuItem("Bach Dang/Add Villages And Supply Camps")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Open beachBoat in Edit Mode first.");
        if (scene.GetRootGameObjects().Any(o => o.name == RootName))
        { Debug.Log("The village/camp group already exists; no duplicate buildings were added."); return; }
        var terrain = Terrain.activeTerrain;
        if (terrain == null) throw new InvalidOperationException("Battlefield terrain is missing.");
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath) != null)
            throw new InvalidOperationException("The dedicated settlement terrain already exists. Restore its scene rather than overwriting it.");
        Directory.CreateDirectory(".utmp/Settlements");
        string backup = ".utmp/Settlements/beachBoat-before-villages-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not back up the scene.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "BachDangSettlements");
        var people = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            .ToDictionary(o => o, o => o.transform.localToWorldMatrix);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add civilian settlements and supply camps");
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create settlement group");
        var generatedAssets = new List<string>();
        try
        {
            var builder = new Builder(terrain, root.transform, generatedAssets);
            builder.Village("01_Lang_Cho_Ben_Song", new Vector2(-690, 110), -8, 5, true, false, 938);
            builder.Village("02_Xom_Chai_Bo_Dong", new Vector2(180, 75), 12, 5, false, true, 939);
            builder.Village("03_Xom_Vuon_Bo_Tay", new Vector2(-1090, 255), -12, 4, false, false, 940);
            builder.Camp("04_Trai_Tiep_Van", new Vector2(-935, 220), 0);
            builder.Camp("05_Trai_Du_Bi", new Vector2(-1020, -410), 12);
            builder.Camp("06_Trai_Bo_Dong", new Vector2(180, 320), -10);
            builder.Connections();
            builder.ClearVegetationUnderSettlements();
            int after = scene.GetRootGameObjects().Sum(o => o.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length);
            if (after != people.Count || people.Any(pair => pair.Key == null || pair.Key.transform.localToWorldMatrix != pair.Value))
                throw new InvalidOperationException("Actor preservation check failed.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Settlements saved: {builder.buildings} buildings, {builder.props} props/fences, {builder.roads.Count} paths/yards, {builder.rejected} rejected unsuitable placements. All {after} existing skinned renderers unchanged. Backup: {backup}");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            foreach (string path in generatedAssets.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
            throw;
        }
    }

    sealed class Builder
    {
        readonly Terrain terrain;
        readonly Transform root;
        readonly List<Bounds> footprints = new List<Bounds>();
        readonly List<Bounds> vegetationClearings = new List<Bounds>();
        public readonly List<Road> roads = new List<Road>();
        readonly Material earth;
        readonly List<string> generatedAssets;
        readonly List<Vector2> protectedCenters = new List<Vector2> { new Vector2(-820, 320), new Vector2(-680, 260), new Vector2(-530, 250), new Vector2(-510, 430), new Vector2(-480, 150) };
        readonly float[] protectedRadius = { 53, 32, 24, 28, 28 };
        public int buildings, props, rejected;
        int meshIndex;

        public Builder(Terrain terrain, Transform root, List<string> generatedAssets)
        {
            this.terrain = terrain;
            this.root = root;
            this.generatedAssets = generatedAssets;
            var pathShader = Shader.Find("BachDang/Village Path");
            if (pathShader == null) throw new InvalidOperationException("Village Path shader must be imported before construction.");
            earth = new Material(pathShader) { name = "Village_Packed_Earth" };
            var layer = terrain.terrainData.terrainLayers[5];
            earth.SetTexture("_BaseMap", layer.diffuseTexture);
            earth.SetFloat("_Smoothness", .08f);
            earth.SetColor("_BaseColor", new Color(1.35f, 1.21f, .96f, .86f));
            SaveNewAsset(earth, Folder + "/Village_Packed_Earth.mat");
        }

        void SaveNewAsset(Object asset, string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new IOException("Generated asset path already exists: " + path);
            AssetDatabase.CreateAsset(asset, path);
            generatedAssets.Add(path);
        }

        float Height(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y)) + terrain.transform.position.y;
        static Vector2 Rotate(Vector2 p, float angle)
        {
            var v = Quaternion.Euler(0, angle, 0) * new Vector3(p.x, 0, p.y);
            return new Vector2(v.x, v.z);
        }
        static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);
        static bool IntersectsXZ(Bounds a, Bounds b, float margin = 0)
        { return a.min.x - margin < b.max.x && a.max.x + margin > b.min.x && a.min.z - margin < b.max.z && a.max.z + margin > b.min.z; }
        bool Protected(Vector2 p, float radius = 0)
        {
            for (int i = 0; i < protectedCenters.Count; i++)
                if (Vector2.Distance(p, protectedCenters[i]) < protectedRadius[i] + radius) return true;
            return false;
        }

        GameObject Place(string asset, string name, Transform parent, Vector2 point, float yaw, float scale, bool building)
        {
            if (Protected(point) || Height(point) < 16.8f) { rejected++; return null; }
            var go = BachDangSettlementAssets.Place(asset, name, parent, new Vector3(point.x, Height(point), point.y), yaw, scale);
            var bounds = BachDangSettlementAssets.BoundsOf(go);
            float min = float.MaxValue, max = float.MinValue;
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
            {
                float h = Height(point + new Vector2(bounds.extents.x * x * .85f, bounds.extents.z * z * .85f));
                min = Mathf.Min(min, h); max = Mathf.Max(max, h);
            }
            bool blocked = building && (footprints.Any(b => IntersectsXZ(bounds, b, 1.1f)) || Protected(point, Mathf.Max(bounds.extents.x, bounds.extents.z)));
            if (min < 16.8f || max - min > (building ? .75f : 1.0f) || blocked)
            { Undo.DestroyObjectImmediate(go); rejected++; return null; }
            go.transform.position += Vector3.up * (max - Height(point) + .035f);
            bounds = BachDangSettlementAssets.BoundsOf(go);
            if (building) { footprints.Add(bounds); buildings++; }
            else props++;
            bounds.Expand(new Vector3(building ? 3 : 1, 0, building ? 3 : 1));
            vegetationClearings.Add(bounds);
            return go;
        }

        Transform Group(string name, Vector2 center, float yaw)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            group.SetPositionAndRotation(new Vector3(center.x, 0, center.y), Quaternion.Euler(0, yaw, 0));
            return group;
        }

        public void Village(string name, Vector2 center, float yaw, int rows, bool extraHomes, bool fishing, int seed)
        {
            var group = Group(name, center, yaw);
            var random = new System.Random(seed);
            Vector2 World(float x, float z) => center + Rotate(new Vector2(x, z), yaw);
            float Jitter(float amount) => ((float)random.NextDouble() * 2 - 1) * amount;
            var homes = new List<GameObject>();
            int index = 0;
            for (int row = 0; row < rows; row++) for (int col = 0; col < 4; col++)
            {
                float x = new[] { -45f, -21f, 21f, 45f }[col] + Jitter(1.6f);
                float z = (row - (rows - 1) * .5f) * 21 + Jitter(2.6f);
                string model = index % 4 == 1 ? "small_house_01" : "small_house_straw_roof_01";
                var house = Place(Buildings + model + ".prefab", "Nha_Dan_" + (++index).ToString("00"), group,
                    World(x, z), yaw + (x < 0 ? 90 : -90) + Jitter(7), 1.04f + Jitter(.055f), true);
                if (house != null) homes.Add(house);
            }
            if (extraHomes)
                foreach (float x in new[] { -45f, -21f, 21f, 45f })
                {
                    var house = Place(Buildings + "small_house_straw_roof_01.prefab", "Nha_Dan_" + (++index).ToString("00"), group,
                        World(x, -68), yaw + (x < 0 ? 90 : -90) + Jitter(6), 1.04f, true);
                    if (house != null) homes.Add(house);
                }
            foreach (var house in homes)
            {
                Vector2 p = XZ(house.transform.position);
                Vector2 forward = XZ(house.transform.forward), right = XZ(house.transform.right);
                int variant = random.Next(4);
                string[] accessories = { "logpile_01", "buckets_01", "barrels_02", "plankpile_01" };
                Place(Props + accessories[variant] + ".prefab", "Do_Dung_San_Nha", house.transform, p + forward * 8.4f + right * 3.3f,
                    house.transform.eulerAngles.y + Jitter(18), .8f, false);
                Place(Props + (fishing ? "barrel_fish_01" : "bucket_01") + ".prefab", "Vat_Dung_Cua_Nha", house.transform,
                    p + forward * 8.2f - right * 3.7f, Jitter(160), .72f, false);
                if (variant == 1 || variant == 3)
                {
                    Place(Props + "fence_02_double.prefab", "Hang_Rao_San_Sau", house.transform,
                        p - forward * 9.6f, house.transform.eulerAngles.y, .7f, false);
                    Place(Props + "shed_01.prefab", "Mai_Che_Cui", house.transform,
                        p - forward * 8.5f + right * 5, house.transform.eulerAngles.y + 90, .9f, false);
                }
            }

            // Lanes remain open; houses vary slightly around them rather than occupying a uniform giant grid.
            Path(group, "Duong_Chinh", 5.8f, new[] { World(0, -92), World(-3, -40), World(2, 5), World(0, 60), World(0, 96) });
            foreach (float x in new[] { -33f, 33f })
                Path(group, "Ngo_Nho", 3.3f, new[] { World(x, -88), World(x + 1, -25), World(x - 1, 30), World(x, 82) });
            foreach (float z in rows == 4 ? new[] { -50f, 0f, 53f, 84f } : new[] { -56f, -12f, 32f, 84f })
                Path(group, "Loi_Ngang", 3.1f, new[] { World(-61, z), World(-30, z + 2), World(0, z), World(31, z - 2), World(61, z) });
            Path(group, "San_Cho", 24f, new[] { World(0, 60), World(0, 86) });
            for (int side = -1; side <= 1; side += 2) for (int row = 0; row < 2; row++)
            {
                Vector2 p = World(side * 14, 65 + row * 14);
                var shed = Place(Props + "shed_02.prefab", fishing ? "Quay_Ban_Ca" : "Mai_Che_Hang_Cho", group, p, yaw + side * -90, 1.35f, true);
                if (shed == null) continue;
                for (int i = 0; i < 3; i++)
                    Place(Props + (fishing ? "barrel_fish_01" : i == 1 ? "buckets_01" : "barrel_01") + ".prefab", "Hang_Hoa_Cho",
                        shed.transform, p + Rotate(new Vector2(-side * 1.2f, (i - 1) * 1.6f), yaw), yaw, .7f, false);
            }
            Place(Buildings + "storage_01.prefab", "Kho_Thoc_Va_Hang", group, World(-28, 91), yaw, 1.1f, true);
            Place(Buildings + "bighouse_02.prefab", "Nha_Sinh_Hoat_Chung", group, World(28, 91), yaw + 180, .82f, true);
            for (int i = 0; i < 5; i++)
            {
                Vector2 p = World(-65, -30 + i * 8);
                Place(Props + (fishing ? "fish_stick_01" : "logpile_01") + ".prefab", fishing ? "Gian_Phoi_Ca" : "San_Chua_Cui", group, p, yaw + 90, fishing ? .95f : 1.2f, false);
                if (i % 2 == 0) Place(Props + "fence_02_double.prefab", "Rao_Khu_Lam_Nghe", group, World(-71, -30 + i * 8), yaw + 90, .85f, false);
            }
            Torch(group, World(-5, 84), yaw);
            Torch(group, World(5, -65), yaw);
        }

        public void Camp(string name, Vector2 center, float yaw)
        {
            var group = Group(name, center, yaw);
            Vector2 World(float x, float z) => center + Rotate(new Vector2(x, z), yaw);
            for (int side = -1; side <= 1; side += 2) for (int row = 0; row < 3; row++)
            {
                var barrack = Place(Buildings + (row == 1 ? "barracks_single_01" : "barracks_01") + ".prefab",
                    "Lan_Quan_" + (side < 0 ? "Tay_" : "Dong_") + row, group, World(side * 25, (row - 1) * 24), yaw - side * 90, .9f, true);
                if (barrack == null) continue;
                Place(Props + "barrels_01.prefab", "Luong_Thuc", group, World(side * 37, (row - 1) * 24 + 7), yaw, .9f, false);
                Place(Props + "plankpile_02.prefab", "Vat_Lieu_Dung_Trai", group, World(side * 37, (row - 1) * 24 - 6), yaw, 1.1f, false);
            }
            Place(Buildings + "big_storage_01.prefab", "Kho_Tiep_Te", group, World(0, 40), yaw, .62f, true);
            Place(Buildings + "gate_01.prefab", "Cong_Trai", group, World(0, -49), yaw, 1.15f, true);
            Place(Buildings + "tower_01.prefab", "Choi_Canh_Tay", group, World(-45, -45), yaw, .66f, true);
            Place(Buildings + "tower_01.prefab", "Choi_Canh_Dong", group, World(45, 45), yaw + 180, .66f, true);
            // Simple timber perimeter with a deliberately open gate and no new guards.
            for (int i = -7; i <= 7; i++)
            {
                float offset = i * 6.45f;
                Place(Props + "fence_02_double.prefab", "Rao_Go_Phia_Bac", group, World(offset, 52), yaw, 1.16f, false);
                if (Mathf.Abs(i) >= 2) Place(Props + "fence_02_double.prefab", "Rao_Go_Phia_Nam", group, World(offset, -52), yaw, 1.16f, false);
                foreach (int side in new[] { -1, 1 })
                    Place(Props + "fence_02_double.prefab", "Rao_Go_Ben_Trai", group, World(side * 52, offset), yaw + 90, 1.16f, false);
            }
            Path(group, "San_Tap", 20, new[] { World(0, -42), World(0, 30) });
            Path(group, "Loi_Vao_Cong", 8, new[] { World(0, -65), World(0, -40) });
            for (int z = -24; z <= 24; z += 24)
                Path(group, "Loi_Vao_Lan", 3, new[] { World(-25, z), World(25, z) });
            Torch(group, World(-7, -43), yaw);
            Torch(group, World(7, 27), yaw);
        }

        void Torch(Transform parent, Vector2 point, float yaw)
        {
            var torch = Place(Props + "torch_stick_01.prefab", "Duoc_San", parent, point, yaw, 1.0f, false);
            if (torch == null) return;
            var lights = torch.GetComponentsInChildren<Light>(true);
            if (lights.Length == 0)
            {
                var lightObject = new GameObject("Anh_Duoc");
                lightObject.transform.SetParent(torch.transform, false);
                lightObject.transform.position = new Vector3(point.x, BachDangSettlementAssets.BoundsOf(torch).max.y - .1f, point.y);
                lights = new[] { lightObject.AddComponent<Light>() };
            }
            foreach (var light in lights)
            {
                light.type = LightType.Point; light.color = new Color(1, .52f, .19f);
                light.intensity = 2.1f; light.range = 13; light.shadows = LightShadows.None;
                light.enabled = true;
            }
        }

        public void Connections()
        {
            var group = Group("07_Duong_Lang_Va_Tiep_Van", Vector2.zero, 0);
            Path(group, "Duong_Lang_Den_Xuong", 5, new[] { new Vector2(-690, 198), new Vector2(-714, 214), new Vector2(-723, 238) });
            Path(group, "Duong_Xom_Vuon_Tiep_Van", 5, new[] { new Vector2(-1090, 182), new Vector2(-1060, 154), new Vector2(-990, 150), new Vector2(-935, 156) });
            Path(group, "Duong_Bo_Dong", 5, new[] { new Vector2(185, 166), new Vector2(203, 207), new Vector2(188, 247) });
        }

        public void Path(Transform parent, string name, float width, Vector2[] points)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
            float travelled = 0;
            for (int segment = 1; segment < points.Length; segment++)
            {
                Vector2 start = points[segment - 1], end = points[segment];
                float length = Vector2.Distance(start, end);
                Vector2 direction = (end - start).normalized, cross = new Vector2(-direction.y, direction.x);
                int count = Mathf.Max(1, Mathf.CeilToInt(length / 2.5f));
                int previous = -1;
                Vector2 previousPoint = default;
                for (int i = 0; i <= count; i++)
                {
                    Vector2 p = Vector2.Lerp(start, end, i / (float)count);
                    float variation = 1 + (Mathf.PerlinNoise(p.x * .13f, p.y * .13f) - .5f) * .13f;
                    Vector2 a = p - cross * width * .5f * variation, b = p + cross * width * .5f * variation;
                    if (Height(p) < 16.8f || Height(a) < 16.8f || Height(b) < 16.8f) { previous = -1; continue; }
                    int index = vertices.Count;
                    float v = (travelled + length * i / count) / 4;
                    float feather = Mathf.Min(.32f, .85f / width);
                    var across = new[] { 0f, feather, 1 - feather, 1f };
                    for (int column = 0; column < 4; column++)
                    {
                        Vector2 q = Vector2.Lerp(a, b, across[column]);
                        vertices.Add(new Vector3(q.x, Height(q) + .048f, q.y));
                        uv.Add(new Vector2(across[column] * width / 4, v));
                        float alpha = column == 0 || column == 3 ? 0 : 1;
                        colors.Add(new Color(1, 1, 1, alpha));
                    }
                    if (previous >= 0)
                    {
                        for (int column = 0; column < 3; column++)
                            triangles.AddRange(new[] { previous + column, previous + column + 1, index + column,
                                previous + column + 1, index + column + 1, index + column });
                        roads.Add(new Road(previousPoint, p, width));
                    }
                    previous = index;
                    previousPoint = p;
                }
                travelled += length;
            }
            if (vertices.Count < 4) return;
            var mesh = new Mesh { name = name + "_Mesh" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            SaveNewAsset(mesh, Folder + "/Path_" + (++meshIndex).ToString("00") + ".asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = earth; renderer.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        bool ClearAt(Vector2 point, float margin)
        {
            if (point.x < -1240 || point.x > 330 || point.y < -510 || point.y > 410) return false;
            foreach (var bound in vegetationClearings)
                if (point.x >= bound.min.x - margin && point.x <= bound.max.x + margin && point.y >= bound.min.z - margin && point.y <= bound.max.z + margin) return true;
            foreach (var road in roads) if (road.Distance(point) < road.width * .5f + margin) return true;
            return false;
        }

        public void ClearVegetationUnderSettlements()
        {
            var source = terrain.terrainData;
            var splat = source.GetAlphamaps(0, 0, source.alphamapWidth, source.alphamapHeight);
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath) || AssetDatabase.LoadMainAssetAtPath(TerrainPath) != null)
                throw new InvalidOperationException("Settlement vegetation requires a saved source TerrainData and an unused destination.");
            // Copy the whole native asset, including splat subassets. Instantiating a TerrainData
            // can leave newly generated alphamaps pending import and reset them after scene save.
            if (!AssetDatabase.CopyAsset(sourcePath, TerrainPath)) throw new IOException("Cannot copy settlement TerrainData.");
            generatedAssets.Add(TerrainPath);
            AssetDatabase.ImportAsset(TerrainPath, ImportAssetOptions.ForceSynchronousImport);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
            data.name = "BachDang_SettlementTerrain";
            data.SetAlphamaps(0, 0, splat);
            var trees = source.treeInstances.Where(tree => !ClearAt(new Vector2(terrain.transform.position.x + tree.position.x * data.size.x,
                terrain.transform.position.z + tree.position.z * data.size.z), 2.6f)).ToArray();
            data.SetTreeInstances(trees, false);
            for (int layer = 0; layer < source.detailPrototypes.Length; layer++)
            {
                var details = source.GetDetailLayer(0, 0, source.detailWidth, source.detailHeight, layer);
                for (int z = 0; z < source.detailHeight; z++) for (int x = 0; x < source.detailWidth; x++)
                {
                    var p = new Vector2(terrain.transform.position.x + (x + .5f) / source.detailWidth * data.size.x,
                        terrain.transform.position.z + (z + .5f) / source.detailHeight * data.size.z);
                    // Broad rejection keeps the expensive footprint checks within the occupied map area.
                    if (p.x < -1240 || p.x > 330 || p.y < -510 || p.y > 410) continue;
                    if (ClearAt(p, .7f)) details[z, x] = 0;
                }
                data.SetDetailLayer(0, 0, layer, details);
            }
            Undo.RecordObject(terrain, "Assign settlement vegetation terrain");
            terrain.terrainData = data;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null) { Undo.RecordObject(collider, "Assign settlement terrain collider"); collider.terrainData = data; }
            data.SetBaseMapDirty(); terrain.Flush();
            var renderer = terrain.GetComponent<VisualDesignCafe.Rendering.Nature.NatureRenderer>();
            if (renderer != null && renderer.isActiveAndEnabled) renderer.Restart();
            EditorUtility.SetDirty(data);
            Debug.Log($"Settlement footprints cleared {source.treeInstanceCount - data.treeInstanceCount} terrain trees. Heights and shore textures are unchanged.");
        }
    }

    public readonly struct Road
    {
        public readonly Vector2 a, b;
        public readonly float width;
        public Road(Vector2 a, Vector2 b, float width) { this.a = a; this.b = b; this.width = width; }
        public float Distance(Vector2 point)
        {
            var delta = b - a;
            return Vector2.Distance(point, a + delta * Mathf.Clamp01(Vector2.Dot(point - a, delta) / Mathf.Max(.001f, delta.sqrMagnitude)));
        }
    }
}
