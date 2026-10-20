using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BeachBoatLayoutAudit
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";

    static object Item(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>(true);
        Bounds? bounds = null;
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            if (bounds == null) bounds = r.bounds;
            else { var b = bounds.Value; b.Encapsulate(r.bounds); bounds = b; }
        }
        return new {
            name = t.name, active = t.gameObject.activeInHierarchy,
            position = t.position.ToString("F1"),
            bounds = bounds?.ToString(), size = bounds?.size.ToString("F1"),
            children = t.childCount, renderers = renderers.Length,
            colliders = t.GetComponentsInChildren<Collider>(true).Length
        };
    }

    public static void AuditAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp");
        if (camp == null) throw new InvalidOperationException("TestQuest_Camp missing");
        var root = camp.transform.Find("Compact_Living_Camp");
        var stage = camp.transform.Cast<Transform>().First(t => t.name.Contains("Rustic_Wooden_Stage"));
        var sceneRoots = scene.GetRootGameObjects().Select(g => g.transform).ToArray();
        var nearStage = sceneRoots.SelectMany(t => t.GetComponentsInChildren<Transform>(true))
            .Where(t => t.parent == null || t.parent == camp.transform ||
                (t.parent != null && t.parent.parent == camp.transform))
            .Where(t => Vector2.Distance(new Vector2(t.position.x, t.position.z),
                new Vector2(stage.position.x, stage.position.z)) < 1000)
            .Select(Item).ToArray();
        var report = new {
            scene = scene.path, stage = stage.position.ToString("F1"),
            roots = sceneRoots.Select(Item).ToArray(),
            worldGroups = sceneRoots.Where(t => new[] { "BachDang_Living_Settlements", "BeachBoat_Thai_Gameplay",
                    "BeachBoat_WorldPolish_Optimized", "BeachBoat_RiverLife_Optimized", "BeachBoatMinigames" }
                    .Contains(t.name))
                .Select(t => new { root = t.name, groups = t.Cast<Transform>().Select(g => new {
                    item = Item(g), children = g.Cast<Transform>().Select(c => new {
                        item = Item(c), grandchildren = c.Cast<Transform>().Take(100).Select(Item).ToArray()
                    }).ToArray()
                }).ToArray() }).ToArray(),
            campChildren = camp.transform.Cast<Transform>().Select(Item).ToArray(),
            keyMaterials = new[] { "Lo_Ren_Quan_Do", "Kho_Khi_Gioi_Nho" }
                .Select(name => GameObject.Find(name)).Where(go => go != null)
                .Select(go => new { go.name, materials = go.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials).Where(m => m != null)
                    .Select(m => new { m.name, shader = m.shader?.name,
                        texture = m.HasProperty("_BaseMap") ? AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")) : "" })
                    .Distinct().ToArray() }).ToArray(),
            campGroups = root == null ? null : root.Cast<Transform>().Select(t => new {
                group = Item(t), items = t.Cast<Transform>().Select(Item).ToArray()
            }).ToArray(),
            nearby = nearStage
        };
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatLayoutAudit.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void CaptureAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var source = UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>()?.storyCamera;
        if (source == null) throw new InvalidOperationException("Story camera missing");
        var go = new GameObject("BeachBoat_Layout_Capture");
        var camera = go.AddComponent<Camera>();
        camera.CopyFrom(source);
        camera.enabled = false;
        camera.farClipPlane = 3500;
        var rt = new RenderTexture(1400, 900, 24);
        var image = new Texture2D(1400, 900, TextureFormat.RGB24, false);
        camera.targetTexture = rt;
        void Shot(string name, Vector3 from, Vector3 toward, bool overhead, float orthoSize)
        {
            camera.orthographic = overhead;
            if (overhead) camera.orthographicSize = orthoSize;
            else camera.fieldOfView = 60;
            go.transform.position = from;
            go.transform.LookAt(toward);
            camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0); image.Apply();
            File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        }
        var stage = GameObject.Find("TestQuest_Camp").transform.Cast<Transform>()
            .First(t => t.name.Contains("Rustic_Wooden_Stage"));
        var center = stage.position;
        Shot("BeachBoat_Layout_Camp_Overhead", center + Vector3.up * 850,
            center, true, 440);
        Shot("BeachBoat_Layout_Camp_Ground", new Vector3(center.x - 185, 143, center.z + 175),
            new Vector3(center.x, 146, center.z + 25), false, 0);
        Shot("BeachBoat_Layout_Camp_Forge", center + new Vector3(80, -17, 315),
            center + new Vector3(205, -17, 252), false, 0);
        Shot("BeachBoat_Layout_Camp_Gate", center + new Vector3(-230, -26, 150),
            center + new Vector3(-330, -28, 150), false, 0);
        var village = new Vector3(-6650, 123, -5110);
        Shot("BeachBoat_Layout_Village_Overhead", village + Vector3.up * 1900,
            village, true, 850);
        Shot("BeachBoat_Layout_Village_Ground", new Vector3(-6720, 145, -4665),
            new Vector3(-6650, 145, -5030), false, 0);
        RenderTexture.active = null; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(go);
        EditorApplication.Exit(0);
    }

    public static void PathSmokeAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var stage = GameObject.Find("TestQuest_Camp").transform.Cast<Transform>()
            .First(t => t.name.Contains("Rustic_Wooden_Stage"));
        var terrain = Terrain.activeTerrain;
        var center = stage.position;
        var segments = new[] {
            (new Vector2(-330, 150), new Vector2(-275, 175)),
            (new Vector2(-275, 175), new Vector2(-165, 175)),
            (new Vector2(-165, 175), new Vector2(-100, 135)),
            (new Vector2(-100, 135), new Vector2(-55, 110)),
            (new Vector2(-55, 110), new Vector2(0, 100)),
            (new Vector2(0, 100), new Vector2(0, 220)),
            (new Vector2(0, -90), new Vector2(0, -220)),
            (new Vector2(-165, -125), new Vector2(165, -125)),
            (new Vector2(-165, 125), new Vector2(165, 125)),
            (new Vector2(0, 220), new Vector2(145, 220))
        };
        Physics.SyncTransforms();
        var hits = new Collider[128];
        var blockers = new List<object>();
        int samples = 0;
        foreach (var segment in segments)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(segment.Item1, segment.Item2) / 8));
            for (int i = 0; i <= steps; i++)
            {
                var at = Vector2.Lerp(segment.Item1, segment.Item2, i / (float)steps);
                var p = center + new Vector3(at.x, 0, at.y);
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                int count = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 5,
                    p + Vector3.up * 22, 4, hits, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                samples++;
                for (int h = 0; h < count; h++)
                {
                    var c = hits[h];
                    if (c == null || c is TerrainCollider || c.GetComponentInParent<Animator>() != null) continue;
                    blockers.Add(new { position = p.ToString("F1"), name = c.name,
                        parent = c.transform.parent?.name, root = c.transform.root.name,
                        bounds = c.bounds.ToString() });
                }
            }
        }
        File.WriteAllText("Logs/BeachBoatLayoutPathSmoke.json", JsonConvert.SerializeObject(new {
            samples, blocked = blockers.Count, blockers = blockers.Take(80).ToArray()
        }, Formatting.Indented));
        EditorApplication.Exit(blockers.Count == 0 ? 0 : 1);
    }
}
