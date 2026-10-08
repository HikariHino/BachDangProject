using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VisualDesignCafe.Rendering.Nature;

/// <summary>Owns only the scaled beachBoat terrain and its cloned terrain layers.</summary>
public static class BachDangScaledTerrain
{
    const string Folder = "Assets/BachDangScaleAndLife";
    const string SourcePath = "Assets/BachDangSettlements/BachDang_SettlementTerrain.asset";
    const string TerrainPath = Folder + "/BeachBoat_Player7Terrain.asset";
    const string UndoLabel = "Scale Bạch Đằng terrain";

    public static TerrainData Create(Terrain terrain, float factor, List<string> generatedAssets)
    {
        ValidateTerrain(terrain, factor);
        if (generatedAssets == null) throw new ArgumentNullException(nameof(generatedAssets));
        var source = terrain.terrainData;
        if (AssetDatabase.GetAssetPath(source) != SourcePath)
            throw new InvalidOperationException("Expected the improved settlement TerrainData as the scale source.");
        if (!AssetDatabase.IsValidFolder(Folder)) throw new DirectoryNotFoundException(Folder);
        RequireUnused(TerrainPath);

        var sourceLayers = source.terrainLayers;
        var layerPaths = new string[sourceLayers.Length];
        for (int i = 0; i < sourceLayers.Length; i++)
        {
            if (sourceLayers[i] == null) throw new InvalidOperationException("The source terrain has an empty layer.");
            layerPaths[i] = Folder + "/BeachBoat_Player7Layer_" + i.ToString("00") + ".terrainlayer";
            RequireUnused(layerPaths[i]);
        }

        // Keep the source samples before copying/importing native terrain subassets.
        var heights = source.GetHeights(0, 0, source.heightmapResolution, source.heightmapResolution);
        var alphas = source.GetAlphamaps(0, 0, source.alphamapWidth, source.alphamapHeight);
        Vector3 sourceSize = source.size;
        var trees = source.treeInstances;
        var detailPrototypes = source.detailPrototypes;
        var nature = terrain.GetComponent<NatureRenderer>();
        SerializedObject natureSerialized = null;
        SerializedProperty natureDistance = null;
        if (nature != null)
        {
            natureSerialized = new SerializedObject(nature);
            natureDistance = natureSerialized.FindProperty("_renderingDistanceLimit");
            if (natureDistance == null || natureDistance.propertyType != SerializedPropertyType.Float)
                throw new InvalidOperationException("Nature Renderer rendering-distance field is unavailable.");
        }

        if (!AssetDatabase.CopyAsset(SourcePath, TerrainPath))
            throw new IOException("Could not copy the settlement terrain.");
        generatedAssets.Add(TerrainPath);
        AssetDatabase.ImportAsset(TerrainPath, ImportAssetOptions.ForceSynchronousImport);
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        if (data == null) throw new IOException("The copied terrain could not be loaded.");
        data.name = "BeachBoat_Player7Terrain";

        var layers = new TerrainLayer[sourceLayers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            layers[i] = UnityEngine.Object.Instantiate(sourceLayers[i]);
            layers[i].name = sourceLayers[i].name + "_Player7";
            layers[i].tileSize = sourceLayers[i].tileSize * factor;
            layers[i].tileOffset = sourceLayers[i].tileOffset * factor;
            AssetDatabase.CreateAsset(layers[i], layerPaths[i]);
            generatedAssets.Add(layerPaths[i]);
        }

        data.size = sourceSize * factor;
        data.SetHeights(0, 0, heights);
        data.terrainLayers = layers;
        for (int i = 0; i < trees.Length; i++)
        {
            trees[i].widthScale *= factor;
            trees[i].heightScale *= factor;
        }
        data.SetTreeInstances(trees, false);
        var scaledDetails = new DetailPrototype[detailPrototypes.Length];
        for (int i = 0; i < scaledDetails.Length; i++)
        {
            scaledDetails[i] = new DetailPrototype(detailPrototypes[i]);
            scaledDetails[i].minWidth *= factor;
            scaledDetails[i].maxWidth *= factor;
            scaledDetails[i].minHeight *= factor;
            scaledDetails[i].maxHeight *= factor;
        }
        data.detailPrototypes = scaledDetails;
        // Layer replacement/import can regenerate alphamaps; reapply the original weights last.
        data.SetAlphamaps(0, 0, alphas);
        data.SetBaseMapDirty();
        EditorUtility.SetDirty(data);
        foreach (var alphaTexture in data.alphamapTextures) EditorUtility.SetDirty(alphaTexture);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(terrain, UndoLabel);
        Undo.RecordObject(terrain.transform, UndoLabel);
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null)
        {
            Undo.RecordObject(collider, UndoLabel);
            collider.terrainData = data;
            EditorUtility.SetDirty(collider);
        }
        terrain.terrainData = data;
        terrain.transform.position *= factor;
        terrain.transform.localScale = Vector3.one;
        terrain.detailObjectDistance = Mathf.Clamp(terrain.detailObjectDistance * factor, 60f, 150f);
        terrain.treeDistance = Mathf.Clamp(terrain.treeDistance * factor, 1500f, 2200f);
        terrain.treeBillboardDistance = Mathf.Clamp(terrain.treeBillboardDistance * factor, 500f, 800f);
        terrain.treeCrossFadeLength = Mathf.Clamp(terrain.treeCrossFadeLength * factor, 20f, 40f);
        terrain.basemapDistance = Mathf.Clamp(terrain.basemapDistance * factor, 800f, 1500f);
        if (nature != null)
        {
            Undo.RecordObject(nature, UndoLabel);
            natureDistance.floatValue = Mathf.Clamp(natureDistance.floatValue * factor, 1200f, 2000f);
            natureSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(nature);
        }
        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(terrain.transform);
        Refresh(terrain);
        return data;
    }

    public static void ClearNewDetails(Terrain terrain, List<Bounds> clearings, float factor)
    {
        ValidateTerrain(terrain, factor);
        var data = terrain.terrainData;
        if (AssetDatabase.GetAssetPath(data) != TerrainPath)
            throw new InvalidOperationException("Vegetation clearing is restricted to the owned scaled terrain.");
        if (clearings == null) throw new ArgumentNullException(nameof(clearings));
        if (clearings.Count == 0) return;

        var areas = new List<Rect>(clearings.Count);
        float margin = 1.5f * factor;
        foreach (var bounds in clearings)
        {
            if (!Finite(bounds.min) || !Finite(bounds.max))
                throw new ArgumentException("Vegetation clearing bounds must be finite.");
            areas.Add(Rect.MinMaxRect(bounds.min.x - margin, bounds.min.z - margin,
                bounds.max.x + margin, bounds.max.z + margin));
        }
        Undo.RegisterCompleteObjectUndo(data, "Clear new village detail footprints");
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        var oldTrees = data.treeInstances;
        var keptTrees = new List<TreeInstance>(oldTrees.Length);
        foreach (var tree in oldTrees)
        {
            var point = new Vector2(origin.x + tree.position.x * size.x, origin.z + tree.position.z * size.z);
            bool remove = false;
            foreach (var area in areas)
                if (area.Contains(point)) { remove = true; break; }
            if (!remove) keptTrees.Add(tree);
        }
        if (keptTrees.Count != oldTrees.Length) data.SetTreeInstances(keptTrees.ToArray(), false);

        int clearedCells = 0;
        if (data.detailWidth > 0 && data.detailHeight > 0)
        {
            // Read only intersecting patches, including partial edge cells under each footprint.
            foreach (var area in areas)
            {
                int x0 = Mathf.Clamp(Mathf.FloorToInt((area.xMin - origin.x) / size.x * data.detailWidth), 0, data.detailWidth);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((area.yMin - origin.z) / size.z * data.detailHeight), 0, data.detailHeight);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((area.xMax - origin.x) / size.x * data.detailWidth), 0, data.detailWidth);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((area.yMax - origin.z) / size.z * data.detailHeight), 0, data.detailHeight);
                if (x0 >= x1 || z0 >= z1) continue;
                for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
                {
                    var values = data.GetDetailLayer(x0, z0, x1 - x0, z1 - z0, layer);
                    bool changed = false;
                    for (int z = 0; z < values.GetLength(0); z++)
                        for (int x = 0; x < values.GetLength(1); x++)
                            if (values[z, x] != 0) { values[z, x] = 0; clearedCells++; changed = true; }
                    if (changed) data.SetDetailLayer(x0, z0, layer, values);
                }
            }
        }
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        Refresh(terrain);
        Debug.Log($"Scaled terrain: cleared {oldTrees.Length - keptTrees.Count} trees and {clearedCells} detail cells for new village props.");
    }

    static void ValidateTerrain(Terrain terrain, float factor)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Terrain conversion requires Edit Mode.");
        if (terrain == null || terrain.terrainData == null) throw new ArgumentNullException(nameof(terrain));
        if (!terrain.gameObject.scene.IsValid() || terrain.gameObject.scene.path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Terrain conversion is restricted to the improved beachBoat scene.");
        if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0)
            throw new ArgumentOutOfRangeException(nameof(factor));
        if ((terrain.transform.lossyScale - Vector3.one).sqrMagnitude > .000001f ||
            (terrain.transform.localScale - Vector3.one).sqrMagnitude > .000001f ||
            Quaternion.Angle(terrain.transform.rotation, Quaternion.identity) > .001f)
            throw new InvalidOperationException("Terrain must retain unit transform scale and identity world rotation.");
    }

    static void RequireUnused(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path))
            throw new IOException("Refusing to replace an existing generated asset: " + path);
    }

    static bool Finite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    static void Refresh(Terrain terrain)
    {
        terrain.Flush();
        var nature = terrain.GetComponent<NatureRenderer>();
        if (nature != null && nature.isActiveAndEnabled) nature.Restart();
    }
}
