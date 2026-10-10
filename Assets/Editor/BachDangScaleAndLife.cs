using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Converts the improved battlefield's units, preserving its geographic layout.</summary>
public static class BachDangScaleAndLife
{
    public const string Folder = "Assets/BachDangScaleAndLife";
    public const float Factor = 7f;

    [MenuItem("Bach Dang/Fit Improved Map And Add Village Life")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Only the improved beachBoat scene can be converted, in Edit Mode.");
        var roots = scene.GetRootGameObjects();
        if (roots.Any(g => g.GetComponent<BachDangWorldScale>() != null))
        { Debug.Log("The improved map has already been fitted. No repeated scaling was applied."); return; }
        var settlements = roots.FirstOrDefault(g => g.name == "BachDang_Living_Settlements");
        var terrain = roots.Select(g => g.GetComponent<Terrain>()).FirstOrDefault(t => t != null);
        if (settlements == null || terrain == null || settlements.transform.childCount != 8)
            throw new InvalidOperationException("The complete improved map is required.");
        if (AssetDatabase.IsValidFolder(Folder) && AssetDatabase.FindAssets("", new[] { Folder }).Length > 0)
            throw new InvalidOperationException("Scale-and-life assets already exist. Restore their scene instead of overwriting them.");
        Directory.CreateDirectory(".utmp/ScaleAndLife");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/ScaleAndLife/beachBoat-before-scale-and-life.unity", true))
            throw new IOException("Scene backup failed.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "BachDangScaleAndLife");

        var oldSkin = roots.SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            .ToDictionary(r => r, r => new ActorState(r));
        var oldPoses = roots.ToDictionary(g => g, g => (position: g.transform.position, scale: g.transform.localScale));
        var generated = new List<string>();
        float oldFogStart = RenderSettings.fogStartDistance, oldFogEnd = RenderSettings.fogEndDistance;
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Fit improved world and add village life");
        try
        {
            var scaleObject = new GameObject("BachDang_WorldScale_Player7");
            Undo.RegisterCreatedObjectUndo(scaleObject, "Add world unit metadata");
            var worldScale = scaleObject.AddComponent<BachDangWorldScale>();
            worldScale.unitsPerReferenceUnit = Factor;

            BachDangScaledTerrain.Create(terrain, Factor, generated);
            foreach (var go in roots)
            {
                if (go == terrain.gameObject) continue;
                Undo.RecordObject(go.transform, "Convert world units");
                go.transform.position = oldPoses[go].position * Factor;
                // Camera and directional-light scale has no geometry to convert.
                if (go.GetComponent<Camera>() == null && go.GetComponent<Light>() == null)
                    go.transform.localScale = oldPoses[go].scale * Factor;
                Override(go.transform);
            }
            foreach (var camera in Components<Camera>(roots))
            {
                Undo.RecordObject(camera, "Convert camera range");
                camera.nearClipPlane *= Factor; camera.farClipPlane *= Factor;
                if (camera.orthographic) camera.orthographicSize *= Factor;
                Override(camera);
            }
            foreach (var camera in Components<BattleCamera>(roots))
                ScaleFields(camera, "distance", "height", "zoomSpeed", "minDistance", "maxDistance", "flySpeed");
            foreach (var tide in Components<TideSystem>(roots)) ScaleFields(tide, "lowTideY", "highTideY");
            foreach (var boat in Components<BoatCrash>(roots)) ScaleFields(boat, "speed", "waterFloatOffset", "sinkSpeed", "forwardCheckDistance");
            foreach (var boat in Components<TidalBoatFloat>(roots)) ScaleFields(boat, "waterlineOffset", "bobAmplitude");
            foreach (var light in Components<Light>(roots))
            {
                if (light.type == LightType.Directional) continue;
                Undo.RecordObject(light, "Convert local light reach");
                light.range *= Factor;
                light.intensity *= Factor * Factor;
                light.shadowNearPlane *= Factor;
                Override(light);
            }
            foreach (var particle in Components<ParticleSystem>(roots))
            {
                Undo.RecordObject(particle, "Scale fire with its world");
                var main = particle.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                Override(particle);
            }
            foreach (var volume in Components<Volume>(roots))
            { Undo.RecordObject(volume, "Convert volume blending"); volume.blendDistance *= Factor; Override(volume); }
            foreach (var cycle in Components<DayNightCycle>(roots))
            {
                Undo.RecordObject(cycle, "Keep atmosphere relative to world size");
                cycle.dayFogDensity /= Factor; cycle.nightFogDensity /= Factor;
                cycle.ApplyTimeOfDay(); Override(cycle);
            }
            RenderSettings.fogStartDistance *= Factor;
            RenderSettings.fogEndDistance *= Factor;
            ScaleWater(roots, generated);
            AddTourAnchors(settlements.transform);

            var clearings = new List<Bounds>();
            BachDangVillageLifeBuilder.Build(terrain, settlements.transform, Factor, clearings, generated);
            BachDangScaledTerrain.ClearNewDetails(terrain, clearings, Factor);
            Physics.SyncTransforms();

            if (Components<SkinnedMeshRenderer>(scene.GetRootGameObjects()).Count() != oldSkin.Count)
                throw new InvalidOperationException("The number of character meshes changed.");
            foreach (var pair in oldSkin)
            {
                var renderer = pair.Key; var state = pair.Value;
                if (renderer == null || renderer.sharedMesh != state.mesh || !renderer.sharedMaterials.SequenceEqual(state.materials)
                    || Vector3.Distance(renderer.transform.position, state.position * Factor) > .05f
                    || Vector3.Distance(renderer.transform.lossyScale, state.scale * Factor) > .05f
                    || Quaternion.Angle(renderer.transform.rotation, state.rotation) > .02f)
                    throw new InvalidOperationException("Character model/reference preservation failed.");
            }
            var transforms = settlements.GetComponentsInChildren<Transform>(true);
            if (transforms.Count(t => t.name.StartsWith("Nha_Dan_")) != 60 || transforms.Count(t => t.name.StartsWith("Lan_Quan_")) != 18)
                throw new InvalidOperationException("Existing village buildings were lost.");
            foreach (var sync in Components<TidalWaterSurface>(roots)) sync.Synchronize();
            worldScale.ApplyRenderScale();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            Undo.CollapseUndoOperations(undo);
            Debug.Log($"Improved beachBoat fitted at {Factor} world units per reference unit. 60 houses/18 barracks retained, {oldSkin.Count} existing character meshes retained, {clearings.Count} new living-detail clearings. TestQuest untouched.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            RenderSettings.fogStartDistance = oldFogStart;
            RenderSettings.fogEndDistance = oldFogEnd;
            var nature = terrain.GetComponent<VisualDesignCafe.Rendering.Nature.NatureRenderer>();
            if (nature != null && nature.isActiveAndEnabled) nature.Restart();
            foreach (string path in generated.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
            throw;
        }
    }

    static IEnumerable<T> Components<T>(IEnumerable<GameObject> roots) where T : Component => roots.SelectMany(g => g.GetComponentsInChildren<T>(true));
    static void Override(Object target)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
    }
    static void ScaleFields(Object component, params string[] names)
    {
        Undo.RecordObject(component, "Convert dimensional fields");
        var serialized = new SerializedObject(component);
        foreach (var name in names)
        {
            var property = serialized.FindProperty(name);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
                throw new InvalidOperationException(component.name + " is missing a dimensional field " + name);
            property.floatValue *= Factor;
        }
        serialized.ApplyModifiedProperties(); Override(component);
    }
    static void AddTourAnchors(Transform settlements)
    {
        string[] groups = { "01_Lang_Cho_Ben_Song", "02_Xom_Chai_Bo_Dong", "04_Trai_Tiep_Van", "03_Xom_Vuon_Bo_Tay" };
        Vector3[] cameras = { new Vector3(-805, 60, 0), new Vector3(20, 82, -20), new Vector3(-1040, 74, 120), new Vector3(-1220, 68, 155) };
        Vector3[] targets = { new Vector3(-680, 20, 110), new Vector3(180, 20, 75), new Vector3(-935, 20, 220), new Vector3(-1090, 20, 255) };
        for (int i = 0; i < groups.Length; i++)
        {
            var group = settlements.Find(groups[i]);
            for (int j = 0; j < 2; j++)
            {
                var anchor = new GameObject(j == 0 ? "Tour_Camera" : "Tour_Target");
                Undo.RegisterCreatedObjectUndo(anchor, "Add settlement viewpoint");
                anchor.transform.SetParent(group, false);
                anchor.transform.position = (j == 0 ? cameras[i] : targets[i]) * Factor;
            }
        }
    }
    static void ScaleWater(GameObject[] roots, List<string> generated)
    {
        var renderer = roots.First(g => g.GetComponent<TideSystem>() != null).GetComponent<Renderer>();
        var original = renderer.sharedMaterial;
        var water = new Material(original) { name = "BachDang_Water_Player7" };
        string path = Folder + "/BachDang_Water_Player7.mat";
        AssetDatabase.CreateAsset(water, path); generated.Add(path);
        string[] metres = { "_ShallowDeepBlendDepth", "_AlphaClearDepth", "_AlphaFullDepth", "_AlphaEdgeWidth", "_AlphaEdgeMaxDepth", "_WaveAmplitude", "_WaveSpeed", "_NormalWorldScale", "_ShorelineDepthFade", "_FoamDepthThreshold", "_ShoreWaveStart", "_ShoreWaveRange", "_ShoreWaveSlopeReach", "_ShoreWaveFoamTexTiling", "_DeepFoamStart", "_DeepFoamFade", "_BottomDistortDepth", "_WaterSurfaceHeight", "_WaterClipThreshold" };
        foreach (string name in metres) if (water.HasProperty(name)) water.SetFloat(name, water.GetFloat(name) * Factor);
        foreach (string name in new[] { "_WaveFrequency", "_WaveDirSpeed", "_WaveFreqSpeed", "_ShoreWaveFrequency" })
            if (water.HasProperty(name)) water.SetFloat(name, water.GetFloat(name) / Factor);
        foreach (string name in new[] { "_FoamTex", "_CausticsTex" })
            if (water.HasProperty(name)) water.SetTextureScale(name, water.GetTextureScale(name) / Factor);
        Undo.RecordObject(renderer, "Use unit-scaled water material");
        renderer.sharedMaterial = water; Override(renderer); EditorUtility.SetDirty(water);
    }
    readonly struct ActorState
    {
        public readonly Vector3 position, scale;
        public readonly Quaternion rotation;
        public readonly Mesh mesh;
        public readonly Material[] materials;
        public ActorState(SkinnedMeshRenderer r)
        { position = r.transform.position; scale = r.transform.lossyScale; rotation = r.transform.rotation; mesh = r.sharedMesh; materials = r.sharedMaterials; }
    }
}
