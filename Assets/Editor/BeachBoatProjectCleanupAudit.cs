using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only dependency audit. Never deletes assets or treats Resources as unused.</summary>
public static class BeachBoatProjectCleanupAudit
{
    [Serializable] public class Entry { public string path; public long bytes; }
    [Serializable] public class Report
    {
        public string utc;
        public int roots, dependencies;
        public Entry[] unusedDemoAssets;
        public string[] protectedDemoAssets;
    }
    static readonly string[] DemoScopes = {
        "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/DemoScene/",
        "Assets/Visual Design Cafe/Nature Renderer Demo/Realistic/Realistic Demo/",
        "Assets/OptiWater/Demo/OptiWater Demo/"
    };
    static readonly string[] DemoScenes = {
        "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/DemoScene/DemoScene.unity",
        "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/DemoScene/DemoScene02.unity",
        "Assets/Visual Design Cafe/Nature Renderer Demo/Realistic/Realistic Demo.unity",
        "Assets/OptiWater/Demo/OptiWater Demo.unity",
        "Assets/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/VFXPlayerScene.unity"
    };
    static bool IsDemo(string path) => DemoScenes.Contains(path) || DemoScopes.Any(p => path.StartsWith(p, StringComparison.Ordinal));

    [MenuItem("Tools/Bach Dang/Audit unused demo assets")]
    public static void Audit()
    {
        var files = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
            && !AssetDatabase.IsValidFolder(p) && File.Exists(p)).ToArray();
        // Everything outside the explicitly scoped example scenes is retained, including
        // all authoring inputs, reusable models, Resources, editor tools and recovery scenes.
        var roots = files.Where(p => !IsDemo(p)).ToList();
        // Protect literal paths/directories used by code even when not serialized in a scene.
        foreach (var source in files.Where(p => p.EndsWith(".cs") || p.EndsWith(".py")))
        {
            var body = File.ReadAllText(source);
            foreach (var candidate in files.Where(IsDemo))
                if (body.Contains(candidate) && source != "Assets/Editor/BeachBoatProjectCleanupAudit.cs") roots.Add(candidate);
        }
        var dependencySet = new HashSet<string>(AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true));
        var candidates = files.Where(IsDemo).OrderBy(p => p).ToArray();
        var report = new Report {
            utc = DateTime.UtcNow.ToString("O"), roots = roots.Distinct().Count(), dependencies = dependencySet.Count,
            unusedDemoAssets = candidates.Where(p => !dependencySet.Contains(p)).Select(p => new Entry { path = p, bytes = new FileInfo(p).Length }).ToArray(),
            protectedDemoAssets = candidates.Where(dependencySet.Contains).ToArray()
        };
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ProjectCleanupDependencyAudit.json", JsonUtility.ToJson(report, true));
        Debug.Log($"Cleanup audit: {report.unusedDemoAssets.Length} unused demo assets; {report.protectedDemoAssets.Length} retained demo dependencies.");
    }
    public static void RunBatch()
    {
        try { Audit(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
