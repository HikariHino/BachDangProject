using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Refines the six existing mountain interiors without regenerating the river or battlefield.</summary>
public static class BachDangKarstRefinement
{
    const string ArtPath = "Assets/BachDangAtmosphere";
    const string RootName = "BachDang_Karst_Refined";
    const string TerrainPath = ArtPath + "/BachDang_KarstTerrain.asset";
    const string RockPath = "Assets/Visual Design Cafe/Nature Renderer Demo/Realistic/Art/Rocks/Rock_C_01.prefab";

    struct Range
    {
        public string name;
        public Vector2 center, direction;
        public float length, radius, height;
        public int seed;
        public Range(string n, float x, float z, float dx, float dz, float l, float r, float h, int s)
        { name = n; center = new Vector2(x, z); direction = new Vector2(dx, dz).normalized; length = l; radius = r; height = h; seed = s; }
    }

    static readonly Range[] Ranges = {
        new Range("Trang Kenh", -1920, 1920, .5f, .866f, 420, 510, 256, 101),
        new Range("Thuy Nguyen", -1800, -120, .707f, -.707f, 480, 480, 249, 202),
        new Range("Vong Trieu", -1920, -2160, .95f, .31f, 360, 450, 231, 303),
        new Range("Yen Duc", 1560, 1920, -.6f, .8f, 360, 450, 268, 404),
        new Range("Phuong Hoang", 900, 240, .85f, -.52f, 540, 420, 248, 505),
        new Range("Vong Hai", 900, -1380, .8f, .6f, 300, 390, 220, 606)
    };

    static float Smooth(float from, float to, float value)
    { return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value)); }

    static float Distance(Range range, Vector2 point)
    {
        Vector2 relative = point - range.center;
        float along = Mathf.Clamp(Vector2.Dot(relative, range.direction), -range.length * .5f, range.length * .5f);
        return (relative - range.direction * along).magnitude;
    }

    static bool InMountain(Vector2 point)
    {
        foreach (var range in Ranges) if (Distance(range, point) < range.radius) return true;
        return false;
    }

    static Vector2 PeakCenter(Range range, int index)
    {
        Vector2 cross = new Vector2(-range.direction.y, range.direction.x);
        float along = (index / 5f - .5f) * (range.length + 90);
        float sideways = Mathf.Sin(index * 2.37f + range.seed) * 94;
        return range.center + range.direction * along + cross * sideways;
    }

    static float SculptHeight(Range range, Vector2 point, float original)
    {
        float footprint = 1 - Smooth(.65f, 1f, Distance(range, point) / range.radius);
        // All riverbeds, beaches and low ground remain numerically identical.
        float blend = footprint * Smooth(28, 65, original);
        if (blend <= 0) return original;
        float peaks = 0;
        for (int i = 0; i < 6; i++)
        {
            Vector2 local = point - PeakCenter(range, i);
            float angle = Mathf.Atan2(local.y, local.x);
            float radius = 118 + 24 * Mathf.Sin(i * 1.9f + range.seed);
            float flutes = 1 + .085f * Mathf.Sin(angle * 7 + i) + .045f * Mathf.Sin(angle * 13 - i);
            float distortion = (Mathf.PerlinNoise(point.x * .013f + range.seed, point.y * .013f) - .5f) * 17;
            float distance = (local.magnitude + distortion) / (radius * flutes);
            // A broad eroded crown, steep walls and an irregular saddle between neighbours.
            float shoulder = 1 - Smooth(.30f, 1.23f, distance);
            float crown = Mathf.PerlinNoise(point.x * .024f + i * 11, point.y * .024f + range.seed);
            float variation = Mathf.Repeat(Mathf.Sin(i * 12.9898f + range.seed * .017f) * 43758.5453f, 1);
            float height = range.height * (.61f + .40f * variation);
            float shape = shoulder * (height - 35 + (crown - .5f) * 42);
            peaks = Mathf.Max(peaks, shape);
        }
        float foothill = 18 + (original - 18) * .24f;
        return Mathf.Lerp(original, Mathf.Min(285, foothill + peaks), blend);
    }

    [MenuItem("Bach Dang/Refine Existing Karst Mountains")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Open beachBoat in Edit Mode first.");
        if (GameObject.Find(RootName) != null)
        { Debug.Log("Karst refinement already exists; terrain was left unchanged."); return; }
        var terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null) throw new InvalidOperationException("Battlefield terrain is missing.");
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath) != null)
            throw new InvalidOperationException("A refined terrain asset already exists without its scene root. Restore the corresponding scene before applying again.");
        Directory.CreateDirectory(".utmp/SkyMountains");
        string backup = ".utmp/SkyMountains/beachBoat-before-karst-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Scene backup failed.");
        if (!AssetDatabase.IsValidFolder(ArtPath)) AssetDatabase.CreateFolder("Assets", "BachDangAtmosphere");

        var source = terrain.terrainData;
        var sourceSplat = source.GetAlphamaps(0, 0, source.alphamapWidth, source.alphamapHeight);
        var data = Object.Instantiate(source);
        data.name = "BachDang_KarstTerrain";
        // Persist before painting: creating TerrainData's native splat subassets can reset them.
        AssetDatabase.CreateAsset(data, TerrainPath);
        data.SetAlphamaps(0, 0, sourceSplat);
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        int resolution = data.heightmapResolution;
        var heights = data.GetHeights(0, 0, resolution, resolution);
        int changed = 0;
        float largestChange = 0;
        foreach (var range in Ranges)
        {
            float reach = range.radius + range.length * .5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((range.center.x - reach - origin.x) / size.x * (resolution - 1)), 0, resolution - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((range.center.x + reach - origin.x) / size.x * (resolution - 1)), 0, resolution - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((range.center.y - reach - origin.z) / size.z * (resolution - 1)), 0, resolution - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((range.center.y + reach - origin.z) / size.z * (resolution - 1)), 0, resolution - 1);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                Vector2 point = new Vector2(origin.x + x * size.x / (resolution - 1), origin.z + z * size.z / (resolution - 1));
                float old = heights[z, x] * size.y;
                float next = SculptHeight(range, point, old);
                if (Mathf.Abs(old - next) > .001f)
                {
                    changed++;
                    largestChange = Mathf.Max(largestChange, Mathf.Abs(old - next));
                    heights[z, x] = next / size.y;
                }
            }
        }
        data.SetHeights(0, 0, heights);
        PaintCliffs(data, origin);
        int treesBefore = data.treeInstanceCount;
        ThinMountainVegetation(data, origin);

        Undo.RecordObject(terrain, "Assign refined mountain terrain");
        terrain.terrainData = data;
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null) { Undo.RecordObject(collider, "Assign refined terrain collision"); collider.terrainData = data; }
        var oldRoot = GameObject.Find("--- HỆ THỐNG NÚI ĐÁ VÔI PHOTOSCANNED (PBR) ---");
        if (oldRoot != null) { Undo.RecordObject(oldRoot, "Preserve previous mountain ornaments"); oldRoot.SetActive(false); }
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Add mountain rock accents");
        AddRockAccents(root.transform, terrain);
        data.SetBaseMapDirty();
        terrain.basemapDistance = Mathf.Max(terrain.basemapDistance, 2500);
        terrain.Flush();
        var natureRenderer = terrain.GetComponent<VisualDesignCafe.Rendering.Nature.NatureRenderer>();
        if (natureRenderer != null && natureRenderer.isActiveAndEnabled) natureRenderer.Restart();
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Karst refinement saved: {changed} height samples; max change {largestChange:F1}m; trees {treesBefore} -> {data.treeInstanceCount}. River/low ground <=28m preserved. Backup: {backup}");
    }

    static void PaintCliffs(TerrainData data, Vector3 origin)
    {
        int width = data.alphamapWidth, height = data.alphamapHeight;
        var weights = data.GetAlphamaps(0, 0, width, height);
        if (weights.GetLength(2) < 6) throw new InvalidOperationException("Expected the six original terrain layers.");
        for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
        {
            float u = x / (float)(width - 1), v = z / (float)(height - 1);
            var point = new Vector2(origin.x + u * data.size.x, origin.z + v * data.size.z);
            if (!InMountain(point)) continue;
            float elevation = data.GetInterpolatedHeight(u, v);
            if (elevation <= 30) continue;
            float slope = data.GetSteepness(u, v);
            float mask = Smooth(30, 62, elevation);
            float exposed = Mathf.Max(Smooth(24, 49, slope), Smooth(90, 190, elevation) * .73f);
            float moss = (1 - exposed) * .55f;
            float rock = .12f + exposed * .82f;
            float soil = .10f;
            float grass = Mathf.Max(0, 1 - rock - moss - soil);
            float total = rock + moss + soil + grass;
            for (int layer = 0; layer < weights.GetLength(2); layer++)
            {
                float target = layer == 0 ? grass : layer == 3 ? moss : layer == 4 ? rock : layer == 5 ? soil : 0;
                weights[z, x, layer] = Mathf.Lerp(weights[z, x, layer], target / total, mask);
            }
        }
        data.SetAlphamaps(0, 0, weights);
    }

    static void ThinMountainVegetation(TerrainData data, Vector3 origin)
    {
        var kept = new List<TreeInstance>(data.treeInstanceCount);
        foreach (var original in data.treeInstances)
        {
            var tree = original;
            float u = tree.position.x, v = tree.position.z;
            var point = new Vector2(origin.x + u * data.size.x, origin.z + v * data.size.z);
            if (InMountain(point))
            {
                float elevation = data.GetInterpolatedHeight(u, v), slope = data.GetSteepness(u, v);
                float random = Mathf.Repeat(Mathf.Sin(u * 12763 + v * 37819) * 43758.5453f, 1);
                if (elevation > 32 && (slope > 34 || elevation > 163 || (elevation > 65 && random < .68f))) continue;
                if (elevation > 28) tree.position.y = elevation / data.size.y;
            }
            kept.Add(tree);
        }
        data.SetTreeInstances(kept.ToArray(), false);
        int width = data.detailWidth, height = data.detailHeight;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            var details = data.GetDetailLayer(0, 0, width, height, layer);
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width, v = (z + .5f) / height;
                var point = new Vector2(origin.x + u * data.size.x, origin.z + v * data.size.z);
                if (!InMountain(point)) continue;
                float elevation = data.GetInterpolatedHeight(u, v);
                if (elevation > 28 && (elevation > 130 || data.GetSteepness(u, v) > 32)) details[z, x] = 0;
            }
            data.SetDetailLayer(0, 0, layer, details);
        }
    }

    static void AddRockAccents(Transform parent, Terrain terrain)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockPath);
        if (prefab == null) throw new InvalidOperationException("Closed rock prefab is missing.");
        string materialPath = ArtPath + "/BachDang_KarstRock.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BachDang_KarstRock" };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainSampleAssets/Textures/Terrain/Rock_BaseColor.tif"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainSampleAssets/Textures/Terrain/Rock_Normal.tif"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", .12f);
            material.SetColor("_BaseColor", new Color(.66f, .68f, .64f));
            material.SetTextureScale("_BaseMap", new Vector2(3, 3));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        foreach (var range in Ranges)
        {
            var group = new GameObject(range.name + " - limestone crowns");
            group.transform.SetParent(parent, false);
            var random = new System.Random(range.seed);
            for (int i = 0; i < 12; i++)
            {
                Vector2 center = PeakCenter(range, i / 2);
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                center += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 21 : 77);
                float y = terrain.SampleHeight(new Vector3(center.x, 0, center.y)) + terrain.transform.position.y;
                float u = (center.x - terrain.transform.position.x) / terrain.terrainData.size.x;
                float v = (center.y - terrain.transform.position.z) / terrain.terrainData.size.z;
                if (y < 75 || terrain.terrainData.GetSteepness(u, v) > 28) continue;
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                rock.name = "Karst_Crown_" + i.ToString("00");
                rock.transform.SetParent(group.transform, false);
                rock.transform.position = new Vector3(center.x, y, center.y);
                rock.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
                rock.transform.localScale = new Vector3(8 + (float)random.NextDouble() * 6, 9 + (float)random.NextDouble() * 9, 8 + (float)random.NextDouble() * 7);
                var renderers = rock.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) { renderer.sharedMaterial = material; bounds.Encapsulate(renderer.bounds); }
                rock.transform.position += Vector3.up * (y - bounds.min.y - bounds.size.y * .38f);
                rock.isStatic = true;
            }
        }
    }
}
