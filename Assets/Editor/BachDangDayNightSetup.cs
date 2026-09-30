using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Configures only the already-open battlefield; never opens or replaces a scene.</summary>
public static class BachDangDayNightSetup
{
    private const string ScenePath = "Assets/Scenes/beachBoat.unity";
    private const string ShaderPath = "Assets/Shaders/BachDangDynamicSky.shader";
    private const string SkyFolder = "Assets/BachDangAtmosphere";
    private const string SkyPath = SkyFolder + "/BachDang_DynamicSky.mat";
    private const string PanoramaFolder = "Assets/OptiWater/Demo/Fantasy Skybox FREE/Panoramics/FS003/";
    private const string MoonName = "BachDang_Moon";

    [MenuItem("Bach Dang/Set Up Dynamic Day And Night")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || !scene.isLoaded || scene.path != ScenePath)
            throw new InvalidOperationException("Open beachBoat in Edit Mode before setting up its day/night cycle.");

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        Texture2D day = AssetDatabase.LoadAssetAtPath<Texture2D>(PanoramaFolder + "FS003_Day_Sunless.png");
        Texture2D night = AssetDatabase.LoadAssetAtPath<Texture2D>(PanoramaFolder + "FS003_Night_Moonless.png");
        if (shader == null || day == null || night == null)
            throw new InvalidOperationException("The dynamic sky shader and the existing Sunless/Moonless FS003 textures are required.");

        var lights = new List<Light>();
        var cycles = new List<DayNightCycle>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            lights.AddRange(root.GetComponentsInChildren<Light>(true));
            cycles.AddRange(root.GetComponentsInChildren<DayNightCycle>(true));
        }

        Light sun = null;
        foreach (DayNightCycle existing in cycles)
        {
            Light candidate = existing.sunLight != null ? existing.sunLight : existing.GetComponent<Light>();
            if (candidate != null && candidate.type == LightType.Directional && candidate.gameObject.scene == scene)
            {
                sun = candidate;
                break;
            }
        }
        if (sun == null && RenderSettings.sun != null && RenderSettings.sun.gameObject.scene == scene
            && RenderSettings.sun.type == LightType.Directional && RenderSettings.sun.name != MoonName)
            sun = RenderSettings.sun;
        if (sun == null)
            sun = lights.Find(light => light.type == LightType.Directional && light.name != MoonName);
        if (sun == null)
            throw new InvalidOperationException("The battlefield needs an existing directional sunlight before setup.");

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set up battlefield day and night");
        Material material = GetOrCreateSky(shader, day, night);
        DayNightCycle cycle = sun.GetComponent<DayNightCycle>();
        if (cycle == null) cycle = Undo.AddComponent<DayNightCycle>(sun.gameObject);
        bool firstSetup = cycle.skyboxMaterial == null;
        Undo.RecordObject(cycle, "Configure day/night cycle");
        // Disabling first restores its previous preview before reconfiguring it.
        cycle.enabled = false;
        foreach (DayNightCycle other in cycles)
        {
            if (other == cycle || !other.enabled) continue;
            Undo.RecordObject(other, "Use one day/night controller");
            other.enabled = false;
        }

        Light moon = cycle.moonLight;
        if (moon == null || moon == sun || moon.gameObject.scene != scene)
            moon = lights.Find(light => light.name == MoonName && light.type == LightType.Directional && light != sun);
        if (moon == null)
        {
            var moonObject = new GameObject(MoonName);
            Undo.RegisterCreatedObjectUndo(moonObject, "Create moonlight");
            moon = Undo.AddComponent<Light>(moonObject);
            moon.type = LightType.Directional;
            moon.color = new Color(0.58f, 0.72f, 1f);
            moon.intensity = 0.22f;
            moon.shadows = LightShadows.Soft;
            moon.shadowBias = 0.03f;
            moon.shadowNormalBias = 0.25f;
        }
        if (moon.type != LightType.Directional)
            throw new InvalidOperationException("The assigned moon must be a directional light.");

        Undo.RecordObjects(new Object[] { sun, sun.transform, moon, moon.transform }, "Configure celestial lights");
        cycle.sunLight = sun;
        cycle.moonLight = moon;
        cycle.skyboxMaterial = material;
        if (firstSetup)
        {
            cycle.timeOfDay = 9f;
            cycle.dayLengthMinutes = 12f;
            cycle.autoAdvance = true;
        }
        // The source asset also provides a valid persistent sky when preview is disabled.
        RenderSettings.skybox = material;
        RenderSettings.sun = sun;
        cycle.enabled = true;
        cycle.ApplyTimeOfDay();
        EditorUtility.SetDirty(cycle);
        EditorUtility.SetDirty(sun);
        EditorUtility.SetDirty(moon);
        if (PrefabUtility.IsPartOfPrefabInstance(cycle))
            PrefabUtility.RecordPrefabInstancePropertyModifications(cycle);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        Debug.Log("Dynamic day/night configured. Adjust DayNightCycle.timeOfDay for an Edit Mode preview; " +
                  "Play Mode advances one full day every " + cycle.dayLengthMinutes + " minutes. Scene is ready to save.");
    }

    private static Material GetOrCreateSky(Shader shader, Texture2D day, Texture2D night)
    {
        if (!AssetDatabase.IsValidFolder(SkyFolder)) AssetDatabase.CreateFolder("Assets", "BachDangAtmosphere");
        Object existing = AssetDatabase.LoadMainAssetAtPath(SkyPath);
        Material material = existing as Material;
        if (existing != null && (material == null || material.shader != shader))
            throw new InvalidOperationException("The dedicated dynamic-sky asset path already contains a different asset or shader.");
        if (material == null)
        {
            material = new Material(shader) { name = "BachDang_DynamicSky" };
            AssetDatabase.CreateAsset(material, SkyPath);
        }
        Undo.RecordObject(material, "Assign sunless and moonless sky panoramas");
        material.SetTexture("_DayTex", day);
        material.SetTexture("_NightTex", night);
        EditorUtility.SetDirty(material);
        return material;
    }
}
