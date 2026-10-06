using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisualDesignCafe.Rendering.Nature;

/// <summary>Corrects Nature Renderer's unscaled final-LOD cutoff using owned prefab variants.</summary>
public static class BachDangScaledVegetation
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";
    const string TerrainPath = "Assets/BachDangScaleAndLife/BeachBoat_Player7Terrain.asset";
    const string Folder = "Assets/BachDangScaleAndLife/Vegetation";
    const string SettingsName = "BachDang_ScaledVegetation_Defaults_Player7";

    [MenuItem("Bach Dang/Restore Scaled Forest Visibility")]
    public static void Polish()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Scaled vegetation polishing requires beachBoat in Edit Mode.");
        float factor = BachDangWorldScale.ForScene(scene);
        if (!Mathf.Approximately(factor, 7f))
            throw new InvalidOperationException("The completed seven-times world conversion is required.");
        var roots = scene.GetRootGameObjects();
        var terrains = roots.SelectMany(g => g.GetComponentsInChildren<Terrain>(true))
            .Where(t => AssetDatabase.GetAssetPath(t.terrainData) == TerrainPath).ToArray();
        if (terrains.Length != 1) throw new InvalidOperationException("Exactly one owned scaled terrain is required.");
        var terrain = terrains[0];
        var data = terrain.terrainData;
        var nature = terrain.GetComponent<NatureRenderer>();
        if (nature == null) throw new InvalidOperationException("The scaled terrain has no Nature Renderer.");
        var settingsRoot = roots.FirstOrDefault(g => g.name == SettingsName);
        if (settingsRoot != null && settingsRoot.GetComponent<NatureRendererDefaultSettings>() == null)
            throw new InvalidOperationException("The owned vegetation settings object is incomplete.");

        var prototypes = data.treePrototypes;
        if (prototypes.Any(p => p.prefab == null)) throw new InvalidOperationException("A tree prototype has no prefab.");
        if (settingsRoot != null && prototypes.All(p => AssetDatabase.GetAssetPath(p.prefab).StartsWith(Folder + "/", StringComparison.Ordinal)))
        {
            Debug.Log("Scaled vegetation variants and defaults are already present; no repeated scaling was applied.");
            return;
        }
        var oldTrees = data.treeInstances;
        var oldHeights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        var oldAlphas = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        var previousSettings = roots.SelectMany(g => g.GetComponentsInChildren<NatureRendererDefaultSettings>(true))
            .LastOrDefault(s => s.isActiveAndEnabled);
        Directory.CreateDirectory(".utmp/ScaleAndLife");
        const string sceneBackup = ".utmp/ScaleAndLife/beachBoat-before-scaled-vegetation.unity";
        const string terrainBackup = ".utmp/ScaleAndLife/terrain-before-scaled-vegetation.asset";
        if (!File.Exists(sceneBackup) && !EditorSceneManager.SaveScene(scene, sceneBackup, true))
            throw new IOException("Could not back up the scaled scene.");
        AssetDatabase.SaveAssetIfDirty(data);
        if (!File.Exists(terrainBackup)) File.Copy(TerrainPath, terrainBackup);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/BachDangScaleAndLife", "Vegetation");

        var generated = new List<string>();
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore scaled forest visibility");
        bool replaced = false;
        try
        {
            var replacements = new TreePrototype[prototypes.Length];
            var variants = new Dictionary<GameObject, GameObject>();
            for (int i = 0; i < replacements.Length; i++)
            {
                var source = prototypes[i].prefab;
                if (!variants.TryGetValue(source, out var variant))
                {
                    variant = OwnedVariant(source, factor, generated);
                    variants.Add(source, variant);
                }
                replacements[i] = new TreePrototype(prototypes[i]) { prefab = variant };
                replaced |= variant != source;
            }
            if (replaced)
            {
                Undo.RegisterCompleteObjectUndo(data, "Use scaled tree LOD variants");
                data.treePrototypes = replacements;
                data.RefreshPrototypes();
                // Rebuilding prototype metadata must not snap or resize planted instances.
                data.SetTreeInstances(oldTrees, false);
            }

            if (settingsRoot == null)
            {
                settingsRoot = new GameObject(SettingsName);
                Undo.RegisterCreatedObjectUndo(settingsRoot, "Add scaled vegetation defaults");
                var settings = Undo.AddComponent<NatureRendererDefaultSettings>(settingsRoot);
                if (previousSettings != null)
                {
                    settings.DetailRenderDistance = previousSettings.DetailRenderDistance;
                    settings.DetailShadowDistance = previousSettings.DetailShadowDistance;
                    settings.DetailDensityInDistance = previousSettings.DetailDensityInDistance;
                    settings.DetailDensityInDistanceFalloff = previousSettings.DetailDensityInDistanceFalloff;
                    settings.TreeRenderDistance = previousSettings.TreeRenderDistance;
                    settings.TreeShadowDistance = previousSettings.TreeShadowDistance;
                    settings.TreeDensityInDistance = previousSettings.TreeDensityInDistance;
                    settings.TreeDensityInDistanceFalloff = previousSettings.TreeDensityInDistanceFalloff;
                }
                // Zero render/shadow distances retain Nature Renderer's automatic/unlimited meaning.
                if (settings.DetailRenderDistance > 0f) settings.DetailRenderDistance *= factor;
                if (settings.DetailShadowDistance > 0f) settings.DetailShadowDistance *= factor;
                if (settings.TreeRenderDistance > 0f) settings.TreeRenderDistance *= factor;
                if (settings.TreeShadowDistance > 0f) settings.TreeShadowDistance *= factor;
                // These are relative screen heights, converted internally using unscaled prefab size.
                settings.DetailDensityInDistanceFalloff /= factor;
                settings.TreeDensityInDistanceFalloff /= factor;
                EditorUtility.SetDirty(settings);
            }

            RequireUnchangedSamples(data, oldHeights, oldAlphas);
            var afterTrees = data.treeInstances;
            if (afterTrees.Length != oldTrees.Length || !afterTrees.SequenceEqual(oldTrees))
                throw new InvalidOperationException("Tree instance preservation failed.");
            terrain.Flush();
            if (nature.isActiveAndEnabled) nature.Restart();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            foreach (var path in generated) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save scaled vegetation settings.");
            Undo.CollapseUndoOperations(undo);
            Debug.Log($"Scaled vegetation saved: {prototypes.Length} tree prototypes, {generated.Count} new owned variants; {oldTrees.Length} tree instances and terrain height/alpha samples preserved.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            if (replaced)
            {
                data.treePrototypes = prototypes;
                data.RefreshPrototypes();
                data.SetTreeInstances(oldTrees, false);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            terrain.Flush();
            if (nature != null && nature.isActiveAndEnabled) nature.Restart();
            foreach (var path in generated.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
            throw;
        }
    }

    static GameObject OwnedVariant(GameObject source, float factor, List<string> generated)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (sourcePath.StartsWith(Folder + "/", StringComparison.Ordinal)) return source;
        if (string.IsNullOrEmpty(sourcePath) || !PrefabUtility.IsPartOfPrefabAsset(source))
            throw new InvalidOperationException("A tree prototype is not a persistent prefab: " + source.name);
        string guid = AssetDatabase.AssetPathToGUID(sourcePath);
        string path = Folder + "/Tree_" + guid + "_Player7.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            ValidateVariant(source, existing, factor);
            return existing;
        }
        if (File.Exists(path)) throw new IOException("Cannot overwrite an unrecognized vegetation asset: " + path);

        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            var lodGroup = instance.GetComponent<LODGroup>();
            if (lodGroup != null)
            {
                var lods = lodGroup.GetLODs();
                if (lods.Length == 0) throw new InvalidOperationException("An existing tree LODGroup has no LODs: " + sourcePath);
                if (lods[lods.Length - 1].screenRelativeTransitionHeight > 0f)
                    lods[lods.Length - 1].screenRelativeTransitionHeight /= factor;
                lodGroup.SetLODs(lods);
                PrefabUtility.RecordPrefabInstancePropertyModifications(lodGroup);
            }
            else
            {
                var renderers = instance.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) throw new InvalidOperationException("A tree prototype has no render geometry: " + sourcePath);
                lodGroup = instance.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(.0001f / factor, renderers) });
                lodGroup.RecalculateBounds();
            }
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool saved);
            if (saved) generated.Add(path);
            if (!saved || variant == null) throw new IOException("Could not save scaled tree variant: " + path);
            ValidateVariant(source, variant, factor);
            return variant;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static void ValidateVariant(GameObject source, GameObject variant, float factor)
    {
        var original = PrefabUtility.GetCorrespondingObjectFromSource(variant);
        if (original != source) throw new InvalidOperationException("Owned vegetation variant has a different source: " + variant.name);
        var sourceGroup = source.GetComponent<LODGroup>();
        var variantGroup = variant.GetComponent<LODGroup>();
        if (variantGroup == null) throw new InvalidOperationException("Owned vegetation variant has no root LODGroup.");
        var lods = variantGroup.GetLODs();
        if (sourceGroup == null)
        {
            if (lods.Length != 1 || !Mathf.Approximately(lods[0].screenRelativeTransitionHeight, .0001f / factor))
                throw new InvalidOperationException("Owned vegetation variant has unexpected final LOD settings.");
            return;
        }
        var expected = sourceGroup.GetLODs();
        if (lods.Length != expected.Length || lods.Length == 0)
            throw new InvalidOperationException("Owned vegetation variant changed its LOD count.");
        for (int i = 0; i < lods.Length; i++)
        {
            float threshold = expected[i].screenRelativeTransitionHeight / (i == lods.Length - 1 ? factor : 1f);
            if (!Mathf.Approximately(lods[i].screenRelativeTransitionHeight, threshold))
                throw new InvalidOperationException("Owned vegetation variant has unexpected LOD thresholds.");
        }
    }

    static void RequireUnchangedSamples(TerrainData data, float[,] heights, float[,,] alphas)
    {
        var currentHeights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        var currentAlphas = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        if (currentHeights.Length != heights.Length || currentAlphas.Length != alphas.Length)
            throw new InvalidOperationException("Terrain sample dimensions changed during prototype replacement.");
        for (int y = 0; y < heights.GetLength(0); y++)
            for (int x = 0; x < heights.GetLength(1); x++)
                if (currentHeights[y, x] != heights[y, x]) throw new InvalidOperationException("Terrain height samples changed.");
        for (int y = 0; y < alphas.GetLength(0); y++)
            for (int x = 0; x < alphas.GetLength(1); x++)
                for (int layer = 0; layer < alphas.GetLength(2); layer++)
                    if (currentAlphas[y, x, layer] != alphas[y, x, layer])
                        throw new InvalidOperationException("Terrain alpha samples changed.");
    }
}
