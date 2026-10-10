using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using VisualDesignCafe.Rendering.Nature;
using Object = UnityEngine.Object;

/// <summary>Read-only snapshot. Never loads scene dependencies or restarts vegetation.</summary>
public static class BachDangMapMemoryReport
{
    [MenuItem("Bach Dang/Report Current Map Memory")]
    public static void Report() => Capture("manual");

    public static string Capture(string label)
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
        var textures = Resources.FindObjectsOfTypeAll<Texture>();
        var meshes = Resources.FindObjectsOfTypeAll<Mesh>();
        var report = new
        {
            timeUtc = DateTime.UtcNow.ToString("O"), scene = scene.path, playing = Application.isPlaying,
            systemRamMB = SystemInfo.systemMemorySize, gpu = SystemInfo.graphicsDeviceName,
            gpuReportedMB = SystemInfo.graphicsMemorySize,
            // Unity counters exclude some driver/plugin/process allocations. Compare at the same stage.
            unityAllocatedMB = MB(Profiler.GetTotalAllocatedMemoryLong()),
            unityReservedMB = MB(Profiler.GetTotalReservedMemoryLong()),
            managedUsedMB = MB(Profiler.GetMonoUsedSizeLong()),
            loadedTextureMB = MB(textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t))),
            loadedMeshMB = MB(meshes.Sum(m => Profiler.GetRuntimeMemorySizeLong(m))),
            renderers = renderers.Length,
            lights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)).Count(),
            shadowLights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(true))
                .Count(l => l.isActiveAndEnabled && l.shadows != LightShadows.None),
            terrains = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Select(t => new
            {
                t.name, data = AssetDatabase.GetAssetPath(t.terrainData),
                trees = t.terrainData.treeInstanceCount, t.treeDistance, t.detailObjectDistance,
                t.drawTreesAndFoliage,
                natureTreeStreaming = TreeStreaming(t.GetComponent<NatureRenderer>())
            }).ToArray(),
            largestLoadedTextures = textures.OrderByDescending(t => Profiler.GetRuntimeMemorySizeLong(t)).Take(12)
                .Select(t => new { t.name, t.width, t.height, mb = MB(Profiler.GetRuntimeMemorySizeLong(t)), path = AssetDatabase.GetAssetPath(t) }).ToArray(),
            note = "Loaded-object snapshot, not a loading peak or a 16 GB laptop certification. Check process private bytes and system commit on the target laptop."
        };
        Directory.CreateDirectory(".utmp/MapMemory");
        string safe = new string(label.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        string path = ".utmp/MapMemory/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + safe + ".json";
        File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));
        Debug.Log("Map memory report: " + path + "; allocated " + report.unityAllocatedMB + " MB, reserved " + report.unityReservedMB + " MB.");
        return path;
    }

    static bool? TreeStreaming(NatureRenderer renderer)
    {
        if (renderer == null) return null;
        var serialized = new SerializedObject(renderer);
        var value = serialized.FindProperty("_renderTreesWithNatureRenderer");
        return value == null ? (bool?)null : value.boolValue;
    }

    static double MB(long bytes) => Math.Round(bytes / 1048576d, 2);
}
