using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Adds a small fishing landing to the existing civilian settlement.</summary>
public static class BachDangVillageLanding
{
    const string GroupName = "08_Ben_Ca_Dan_Sinh";
    const string Folder = "Assets/BachDangSettlements";
    const string TerrainPath = Folder + "/BachDang_SettlementTerrain.asset";
    const float DeckY = 17.65f;
    const float PathWidth = 3.6f;
    static readonly Vector2[] PathPoints = { new Vector2(115, 55), new Vector2(60, 75), new Vector2(0, 75) };

    [MenuItem("Bach Dang/Add Civilian Fishing Landing")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Open beachBoat in Edit Mode first.");
        var settlements = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "BachDang_Living_Settlements");
        if (settlements == null) throw new InvalidOperationException("The existing settlement root is required.");
        if (settlements.transform.Find(GroupName) != null)
        { Debug.Log("The civilian fishing landing already exists; nothing was added."); return; }
        var terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.gameObject.scene != scene ||
            AssetDatabase.GetAssetPath(terrain.terrainData) != TerrainPath)
            throw new InvalidOperationException("Only the owned settlement TerrainData may be edited.");
        var atmosphere = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "BachDang_Atmosphere_And_Shore_Detail");
        var oldPier = atmosphere != null ? atmosphere.transform.Find("Ben_Go_Ven_Song") : null;
        var timberRenderer = oldPier != null ? oldPier.GetComponentInChildren<MeshRenderer>() : null;
        var earth = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Village_Packed_Earth.mat");
        if (timberRenderer == null || timberRenderer.sharedMaterial == null || earth == null)
            throw new InvalidOperationException("The existing timber and village path materials are required.");

        Directory.CreateDirectory(".utmp/Settlements");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/Settlements/before-landing.unity", true))
            throw new IOException("Could not back up the scene.");
        File.Copy(TerrainPath, ".utmp/Settlements/terrain-before-landing.asset", true);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add civilian fishing landing");
        string meshPath = null;
        var data = terrain.terrainData;
        var oldTrees = data.treeInstances;
        var oldDetails = new List<DetailPatch>();
        bool vegetationChanged = false;
        try
        {
            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Add fishing landing");
            group.transform.SetParent(settlements.transform, false);
            group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var pier = new GameObject("Cau_Go_Dan_Sinh").transform;
            pier.SetParent(group.transform, false);
            const int plankCount = 121;
            float step = 58f / plankCount;
            for (int i = 0; i < plankCount; i++)
                Primitive("Van_Ben_" + i.ToString("000"), PrimitiveType.Cube, pier,
                    new Vector3(-(i + .5f) * step, DeckY - .08f, 75),
                    new Vector3(step - .022f, .16f, 3.4f), timberRenderer.sharedMaterial);
            for (int i = 0; i <= 7; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = new Vector2(-i * 8f, 75 + side * 1.5f);
                    float bottom = Height(terrain, p) - .35f, top = DeckY + .6f;
                    if (bottom >= top) throw new InvalidOperationException("The landing pier crosses high ground.");
                    Primitive("Tru_Ben_" + i + "_" + side, PrimitiveType.Cylinder, pier,
                        new Vector3(p.x, (bottom + top) * .5f, p.y),
                        new Vector3(.28f, (top - bottom) * .5f, .28f), timberRenderer.sharedMaterial);
                }

            var mesh = CreatePath(terrain);
            meshPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Landing_Path.asset");
            AssetDatabase.CreateAsset(mesh, meshPath);
            var path = new GameObject("Loi_Xuong_Ben_Ca");
            path.transform.SetParent(group.transform, false);
            path.AddComponent<MeshFilter>().sharedMesh = mesh;
            var pathRenderer = path.AddComponent<MeshRenderer>();
            pathRenderer.sharedMaterial = earth;
            pathRenderer.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(path, StaticEditorFlags.BatchingStatic);

            var propBounds = new List<Bounds>();
            AddProp("barrel_fish_01", "Thung_Ca_01", new Vector2(8, 71), 20);
            AddProp("barrel_fish_01", "Thung_Ca_02", new Vector2(10, 71), -15);
            AddProp("bucket_01", "Xo_Nuoc_01", new Vector2(8, 79), 30);
            AddProp("bucket_01", "Xo_Nuoc_02", new Vector2(10, 79), -30);
            AddProp("fish_stick_01", "Gian_Phoi_Ca_01", new Vector2(5, 83), 90);
            AddProp("fish_stick_01", "Gian_Phoi_Ca_02", new Vector2(8, 83), 90);

            void AddProp(string prefab, string name, Vector2 p, float yaw)
            {
                float y = Height(terrain, p);
                if (y < 16.8f) throw new InvalidOperationException("Fishing props require dry ground.");
                var go = BachDangSettlementAssets.Place("Assets/Prefabs/Props/pf_" + prefab + ".prefab", name,
                    group.transform, new Vector3(p.x, y, p.y), yaw);
                propBounds.Add(BachDangSettlementAssets.BoundsOf(go));
            }

            bool Clear(Vector2 p, float margin)
            {
                if (p.x < -64 || p.x > 122 || p.y < 48 || p.y > 91) return false;
                if (Distance(p, new Vector2(0, 75), new Vector2(-58, 75)) <= 1.7f + margin) return true;
                for (int i = 1; i < PathPoints.Length; i++)
                    if (Distance(p, PathPoints[i - 1], PathPoints[i]) <= PathWidth * .5f + margin) return true;
                foreach (var bounds in propBounds)
                    if (p.x >= bounds.min.x - margin && p.x <= bounds.max.x + margin &&
                        p.y >= bounds.min.z - margin && p.y <= bounds.max.z + margin) return true;
                return false;
            }

            Undo.RegisterCompleteObjectUndo(data, "Clear landing vegetation");
            vegetationChanged = true;
            data.SetTreeInstances(oldTrees.Where(tree => !Clear(new Vector2(
                terrain.transform.position.x + tree.position.x * data.size.x,
                terrain.transform.position.z + tree.position.z * data.size.z), 2.6f)).ToArray(), false);
            if (data.detailWidth > 0 && data.detailHeight > 0)
            {
                var origin = terrain.transform.position;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((-64 - origin.x) / data.size.x * data.detailWidth), 0, data.detailWidth - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((48 - origin.z) / data.size.z * data.detailHeight), 0, data.detailHeight - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((122 - origin.x) / data.size.x * data.detailWidth), x0 + 1, data.detailWidth);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((91 - origin.z) / data.size.z * data.detailHeight), z0 + 1, data.detailHeight);
                float margin = Mathf.Max(.7f, new Vector2(data.size.x / data.detailWidth, data.size.z / data.detailHeight).magnitude * .5f);
                for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
                {
                    var values = data.GetDetailLayer(x0, z0, x1 - x0, z1 - z0, layer);
                    oldDetails.Add(new DetailPatch { x = x0, z = z0, layer = layer, values = (int[,])values.Clone() });
                    for (int z = 0; z < values.GetLength(0); z++)
                        for (int x = 0; x < values.GetLength(1); x++)
                            if (Clear(new Vector2(origin.x + (x0 + x + .5f) / data.detailWidth * data.size.x,
                                origin.z + (z0 + z + .5f) / data.detailHeight * data.size.z), margin)) values[z, x] = 0;
                    data.SetDetailLayer(x0, z0, layer, values);
                }
            }
            RefreshVegetation(terrain);
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the landing scene.");
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Civilian fishing pier and path saved. Existing people, boats, heights and shore textures are unchanged.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            if (vegetationChanged)
            {
                data.SetTreeInstances(oldTrees, false);
                foreach (var patch in oldDetails) data.SetDetailLayer(patch.x, patch.z, patch.layer, patch.values);
                RefreshVegetation(terrain);
                EditorUtility.SetDirty(data);
            }
            if (meshPath != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.SaveAssets();
            throw;
        }
    }

    static Mesh CreatePath(Terrain terrain)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>();
        var colors = new List<Color>(); var triangles = new List<int>();
        float travelled = 0;
        float[] across = { 0, .19f, .81f, 1 };
        for (int segment = 1; segment < PathPoints.Length; segment++)
        {
            Vector2 a = PathPoints[segment - 1], b = PathPoints[segment];
            float length = Vector2.Distance(a, b);
            Vector2 direction = (b - a).normalized, side = new Vector2(-direction.y, direction.x);
            int steps = Mathf.CeilToInt(length / 2);
            int previous = -1;
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                int row = vertices.Count;
                for (int column = 0; column < across.Length; column++)
                {
                    var q = p + side * ((across[column] - .5f) * PathWidth);
                    float h = Height(terrain, q);
                    if (h < 16.8f) throw new InvalidOperationException("The proposed landing path leaves dry ground.");
                    vertices.Add(new Vector3(q.x, h + .048f, q.y));
                    uv.Add(new Vector2(across[column] * PathWidth / 4, (travelled + length * i / steps) / 4));
                    colors.Add(new Color(1, 1, 1, column == 0 || column == 3 ? 0 : 1));
                }
                if (previous >= 0)
                    for (int column = 0; column < 3; column++)
                        triangles.AddRange(new[] { previous + column, previous + column + 1, row + column,
                            previous + column + 1, row + column + 1, row + column });
                previous = row;
            }
            travelled += length;
        }
        var mesh = new Mesh { name = "Landing_Path" };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    static void Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    static float Height(Terrain terrain, Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y)) + terrain.transform.position.y;
    static float Distance(Vector2 p, Vector2 a, Vector2 b)
    {
        var d = b - a;
        return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.001f, d.sqrMagnitude)));
    }
    static void RefreshVegetation(Terrain terrain)
    {
        terrain.Flush();
        var renderer = terrain.GetComponent<VisualDesignCafe.Rendering.Nature.NatureRenderer>();
        if (renderer != null && renderer.isActiveAndEnabled) renderer.Restart();
    }
    struct DetailPatch { public int x, z, layer; public int[,] values; }
}
