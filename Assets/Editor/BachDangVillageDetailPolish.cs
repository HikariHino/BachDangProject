using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Owned materials for civilian gardens and masonry, independent of terrain-detail distance fading.</summary>
public static class BachDangVillageDetailPolish
{
    const string Folder = "Assets/BachDangScaleAndLife";
    const string LifeGroup = "09_Sinh_Hoat";
    const string PrefabFolder = "Assets/TerrainSampleAssets/Prefabs/";
    const string TextureFolder = "Assets/TerrainSampleAssets/Textures/Details/";
    public const float FernMultiplier = 1.2f;
    public const float BushMultiplier = .65f;

    [MenuItem("Bach Dang/Polish Village Gardens And Wells")]
    public static void ApplyDetailPolish()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Open the scaled beachBoat scene in Edit Mode first.");
        float factor = BachDangWorldScale.ForScene(scene);
        if (!Mathf.Approximately(factor, 7f))
            throw new InvalidOperationException("Garden polish is restricted to the fitted scale-7 battlefield.");
        var settlements = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "BachDang_Living_Settlements");
        var terrain = scene.GetRootGameObjects().Select(o => o.GetComponent<Terrain>()).FirstOrDefault(t => t != null);
        var life = settlements != null ? settlements.transform.Find(LifeGroup) : null;
        if (life == null || terrain == null || !AssetDatabase.IsValidFolder(Folder))
            throw new InvalidOperationException("The fitted village-life group, terrain and owned asset folder are required.");
        var plants = life.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("Rau_", StringComparison.Ordinal) && t.parent != null &&
                t.parent.name.StartsWith("Vuon_Rau_", StringComparison.Ordinal)).ToArray();
        var stones = life.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.name.StartsWith("Da_Thanh_Gieng_", StringComparison.Ordinal)).ToArray();
        if (plants.Length == 0 || stones.Length == 0)
            throw new InvalidOperationException("Expected civilian garden plants and well masonry are missing.");

        Directory.CreateDirectory(".utmp/ScaleAndLife");
        string backup = ".utmp/ScaleAndLife/beachBoat-before-detail-polish-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not back up the scene before detail polish.");
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Polish village garden plants and well stone");
        var created = new List<string>();
        try
        {
            var fern = GetGardenMaterial(false, created);
            var bush = GetGardenMaterial(true, created);
            var stone = GetWellStoneMaterial(created);
            foreach (var wrapper in plants)
            {
                var instance = wrapper.Cast<Transform>().FirstOrDefault();
                var source = instance != null ? PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject) : null;
                string path = source != null ? AssetDatabase.GetAssetPath(source) : "";
                bool isBush = path == PrefabFolder + "Bush_A.prefab";
                if (!isBush && path != PrefabFolder + "Fern_A.prefab")
                    throw new InvalidOperationException("Unexpected plant prefab in the owned civilian garden: " + path);
                if (instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
                    throw new InvalidOperationException("Garden polish must not touch skinned models.");
                Undo.RecordObject(wrapper, "Reground garden plant");
                Undo.RecordObject(instance, "Set authored garden plant size");
                Vector3 point = wrapper.position;
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                wrapper.position = point;
                // Assign an absolute authored multiplier, so repeated polish never enlarges plants again.
                instance.localScale = source.transform.localScale * (isBush ? BushMultiplier : FernMultiplier);
                var bounds = BachDangSettlementAssets.BoundsOf(wrapper.gameObject);
                instance.position += new Vector3(point.x - bounds.center.x,
                    point.y + .06f * factor - bounds.min.y, point.z - bounds.center.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                AssignGardenMaterial(wrapper.gameObject, isBush ? bush : fern);
            }
            foreach (var renderer in stones)
            {
                Undo.RecordObject(renderer, "Use tileable well stone");
                renderer.sharedMaterial = stone;
                EditorUtility.SetDirty(renderer);
            }
            AssetDatabase.SaveAssetIfDirty(fern); AssetDatabase.SaveAssetIfDirty(bush); AssetDatabase.SaveAssetIfDirty(stone);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save polished civilian details.");
            Undo.CollapseUndoOperations(undo);
            Debug.Log($"Village polish saved: {plants.Length} visible garden plants, {stones.Length} well stone blocks. Source flora/materials unchanged. Backup: {backup}");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            foreach (string path in created.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
            throw;
        }
    }

    public static Material GetGardenMaterial(bool bush, List<string> generatedAssets)
    {
        string plant = bush ? "Bush_A" : "Fern_A";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + plant + ".prefab");
        var source = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>(true)?.sharedMaterial : null;
        if (source == null) throw new InvalidOperationException("Missing garden source material for " + plant);
        string basename = bush ? "Bush_A" : "Fern";
        var albedo = Texture(source, "Base_Map", "Texture2D_E1B0D043", TextureFolder + basename + "_BaseColor.tif");
        var normal = Texture(source, "Normal_Map", "Texture2D_9DCAAA49", TextureFolder + basename + "_Normal.tif");
        if (albedo == null || normal == null) throw new InvalidOperationException("Missing garden leaf textures for " + plant);

        var material = OwnedMaterial(bush ? "Village_Garden_Bush" : "Village_Garden_Fern", generatedAssets);
        material.SetTexture("_BaseMap", albedo); material.SetTexture("_BumpMap", normal);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_BumpScale", .7f); material.SetFloat("_Smoothness", .16f);
        material.SetFloat("_Metallic", 0); material.SetFloat("_Surface", 0);
        material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", bush ? .5f : .45f);
        material.SetFloat("_Cull", (float)CullMode.Off); material.SetFloat("_ZWrite", 1);
        material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        material.EnableKeyword("_ALPHATEST_ON"); material.EnableKeyword("_NORMALMAP");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.enableInstancing = true; material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    public static Material GetWellStoneMaterial(List<string> generatedAssets)
    {
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainSampleAssets/TerrainLayers/Rock_TerrainLayer.terrainlayer");
        if (layer == null || layer.diffuseTexture == null || layer.normalMapTexture == null)
            throw new InvalidOperationException("The existing tileable rock terrain textures are required for the well.");
        var material = OwnedMaterial("Village_Well_Tileable_Stone", generatedAssets);
        material.SetTexture("_BaseMap", layer.diffuseTexture); material.SetTexture("_BumpMap", layer.normalMapTexture);
        material.SetColor("_BaseColor", new Color(.89f, .87f, .81f, 1));
        material.SetTextureScale("_BaseMap", new Vector2(.55f, .75f));
        material.SetTextureScale("_BumpMap", new Vector2(.55f, .75f));
        material.SetFloat("_BumpScale", .65f); material.SetFloat("_Smoothness", .08f);
        material.SetFloat("_Metallic", 0); material.SetFloat("_Surface", 0);
        material.SetFloat("_AlphaClip", 0); material.SetFloat("_Cull", (float)CullMode.Back);
        material.SetFloat("_ZWrite", 1); material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        material.EnableKeyword("_NORMALMAP"); material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "Opaque"); material.renderQueue = (int)RenderQueue.Geometry;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    public static void AssignGardenMaterial(GameObject plant, Material material)
    {
        foreach (var renderer in plant.GetComponentsInChildren<MeshRenderer>(true))
        {
            Undo.RecordObject(renderer, "Use owned garden leaf material");
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
        }
    }

    static Material OwnedMaterial(string name, List<string> generatedAssets)
    {
        if (generatedAssets == null) throw new ArgumentNullException(nameof(generatedAssets));
        if (!AssetDatabase.IsValidFolder(Folder)) throw new DirectoryNotFoundException(Folder);
        string path = Folder + "/" + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            if (File.Exists(path)) throw new IOException("The owned material path contains an unexpected asset: " + path);
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path); generatedAssets.Add(path);
        }
        else
        {
            Undo.RecordObject(material, "Polish owned civilian material");
            material.shader = shader;
        }
        return material;
    }

    static Texture Texture(Material source, string current, string legacy, string fallback)
    {
        if (source.HasProperty(current) && source.GetTexture(current) != null) return source.GetTexture(current);
        if (source.HasProperty(legacy) && source.GetTexture(legacy) != null) return source.GetTexture(legacy);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(fallback);
    }
}
