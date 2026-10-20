using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>One-time, scene-owned placement repair; reuses existing models and materials.</summary>
public static class BeachBoatLayoutPolish
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";
    static float Ground(Terrain terrain, Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true)
            .Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("No renderer on " + go.name);
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void Seat(GameObject go, Terrain terrain, Vector3 where)
    {
        var p = go.transform.position; p.x = where.x; p.z = where.z;
        go.transform.position = p;
        var ground = Ground(terrain, p);
        go.transform.position += Vector3.up * (ground + .18f - BoundsOf(go).min.y);
    }

    static float FootprintOverlap(Bounds a, Bounds b)
    {
        var x = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
        var z = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
        return x > 4 && z > 4 ? x * z : 0;
    }

    static int ClearVillageYards()
    {
        var settlements = GameObject.Find("BachDang_Living_Settlements");
        if (settlements == null) throw new InvalidOperationException("Living settlements missing");
        int cleared = 0;
        foreach (Transform village in settlements.transform)
        {
            if (!village.name.StartsWith("01_") && !village.name.StartsWith("02_") &&
                !village.name.StartsWith("03_")) continue;
            var houses = village.Cast<Transform>()
                .Where(t => t.name.StartsWith("Nha_Dan_")).ToArray();
            var main = houses.Select(h => new {
                house = h,
                body = h.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("pf_build"))
            }).ToArray();
            if (main.Any(x => x.body == null)) throw new InvalidOperationException("House body missing in " + village.name);
            var bodies = main.Select(x => BoundsOf(x.body.gameObject)).ToArray();
            for (int i = 0; i < main.Length; i++)
            foreach (Transform accessory in main[i].house)
            {
                if (accessory == main[i].body || !accessory.gameObject.activeSelf) continue;
                var renderers = accessory.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var bounds = BoundsOf(accessory.gameObject);
                bool intrudes = false;
                for (int j = 0; j < main.Length; j++)
                    if (j != i && FootprintOverlap(bounds, bodies[j]) > 16) { intrudes = true; break; }
                if (!intrudes) continue;
                // Keep the house and routes; hide only a surplus yard prop embedded in a neighbour.
                accessory.gameObject.SetActive(false);
                cleared++;
            }
            if (village.name.StartsWith("01_"))
            {
                var stalls = village.Cast<Transform>().Where(t => t.name == "Mai_Che_Hang_Cho")
                    .OrderBy(t => t.position.x).ToArray();
                foreach (var pair in stalls.GroupBy(t => t.position.x < -6700 ? 0 : 1))
                {
                    var upstream = pair.OrderBy(t => t.position.z).FirstOrDefault();
                    if (upstream != null) upstream.position += Vector3.back * 22;
                }
            }
        }
        return cleared;
    }

    static void RebuildPaths(Terrain terrain, Vector3 origin)
    {
        const string path = "Assets/BeachBoatStory/Camp/CompactCampPaths.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) throw new InvalidOperationException("Compact camp path mesh missing");
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        void Strip(float ax, float az, float bx, float bz, float width)
        {
            var a = origin + new Vector3(ax, 0, az);
            var b = origin + new Vector3(bx, 0, bz);
            var direction = (b - a).normalized;
            var side = new Vector3(-direction.z, 0, direction.x) * width * .5f;
            var corners = new[] { a + side, a - side, b + side, b - side };
            int first = vertices.Count;
            for (int i = 0; i < corners.Length; i++)
            {
                var p = corners[i]; p.y = Ground(terrain, p) + .23f;
                vertices.Add(p);
                uv.Add(new Vector2(i % 2, i < 2 ? 0 : Vector3.Distance(a, b) / 20));
            }
            triangles.AddRange(new[] { first, first + 2, first + 1,
                first + 1, first + 2, first + 3 });
        }
        // A bent entry avoids the left tent row; the cross lanes end before both tent rows.
        Strip(-330, 150, -275, 175, 15);
        Strip(-275, 175, -165, 175, 15);
        Strip(-165, 175, -100, 135, 13);
        Strip(-100, 135, -55, 110, 12);
        Strip(-55, 110, 0, 100, 12);
        Strip(0, 100, 0, 220, 18);
        Strip(0, -90, 0, -220, 16);
        Strip(-165, -125, 165, -125, 10);
        Strip(-165, 125, 165, 125, 10);
        Strip(0, 220, 145, 220, 10);
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
    }

    public static void ApplyAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp");
        var stage = camp?.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains("Rustic_Wooden_Stage"));
        var compact = camp?.transform.Find("Compact_Living_Camp");
        var terrain = Terrain.activeTerrain;
        if (stage == null || compact == null || terrain == null)
            throw new InvalidOperationException("Expected beachBoat camp is missing");
        var origin = stage.position;
        var forge = GameObject.Find("Lo_Ren_Quan_Do");
        if (forge == null) throw new InvalidOperationException("Forge missing");
        if (BoundsOf(forge).size.x < 150) throw new InvalidOperationException("Layout polish already applied");
        Directory.CreateDirectory(".utmp/BeachBoatLayoutPolish");
        EditorSceneManager.SaveScene(scene, ".utmp/BeachBoatLayoutPolish/before.unity", true);
        var rows = compact.Find("Barracks_Rows");
        var tents = rows.Cast<Transform>().OrderBy(t => t.name).ToArray();
        if (tents.Length != 16) throw new InvalidOperationException("Expected 16 camp tents");
        Seat(tents[14].gameObject, terrain, origin + new Vector3(-125, 0, -245));
        tents[14].rotation = Quaternion.Euler(0, 90, 0);
        Seat(tents[15].gameObject, terrain, origin + new Vector3(125, 0, -245));
        var supplies = compact.Find("Supply_And_Work");
        foreach (Transform child in supplies)
        {
            if (child.name.StartsWith("Wooden_Barracks_"))
            {
                var side = child.position.x < origin.x ? -1 : 1;
                Seat(child.gameObject, terrain, new Vector3(origin.x + side * 125, 0, child.position.z));
            }
            else if (child.name.StartsWith("Stake_Timber_") || child.name.StartsWith("Food_Stores_"))
            {
                var side = child.position.x < origin.x ? -1 : 1;
                Seat(child.gameObject, terrain, new Vector3(origin.x + side * 170, 0, child.position.z));
            }
        }
        forge.transform.localScale *= .5f;
        Seat(forge, terrain, origin + new Vector3(205, 0, 252));
        var storage = GameObject.Find("Kho_Khi_Gioi_Nho");
        if (storage != null) Seat(storage, terrain, origin + new Vector3(-275, 0, 237));
        var workers = compact.Find("Working_Soldiers");
        foreach (var entry in new[] { ("Camp_Worker_03", new Vector3(-45, 0, -110)),
            ("Camp_Worker_05", new Vector3(-45, 0, 115)) })
        {
            var worker = workers.Find(entry.Item1);
            if (worker == null) throw new InvalidOperationException("Worker missing: " + entry.Item1);
            var old = worker.position;
            Seat(worker.gameObject, terrain, origin + entry.Item2);
            var delta = worker.position - old;
            var wander = worker.GetComponent<BeachBoatStory.NPCWander>();
            if (wander != null && wander.waypoints != null)
                foreach (var waypoint in wander.waypoints) if (waypoint != null) waypoint.position += delta;
        }
        // On-duty soldiers should not lie down in the middle of the camp paths.
        foreach (var wander in workers.GetComponentsInChildren<BeachBoatStory.NPCWander>(true))
            wander.sitLayChance = 0;
        RebuildPaths(terrain, origin);
        int yardsCleared = ClearVillageYards();
        Physics.SyncTransforms(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatLayoutPolish.json", JsonConvert.SerializeObject(new {
            scene = scene.path, campTents = tents.Length, forgeSize = BoundsOf(forge).size.ToString("F1"),
            forgePosition = forge.transform.position.ToString("F1"), yardsCleared,
            campRenderers = camp.GetComponentsInChildren<Renderer>().Count(r => r.gameObject.activeInHierarchy),
            terrainTrees = terrain.terrainData.treeInstanceCount
        }, Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void FinishActorsAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp");
        var stage = camp.transform.Cast<Transform>().First(t => t.name.Contains("Rustic_Wooden_Stage"));
        var terrain = Terrain.activeTerrain;
        var center = stage.position;
        var positions = new[] {
            ("Camp_Sitting_Soldier", new Vector3(-5758, 0, 1450)),
            ("Camp_Laying_Soldier", new Vector3(-5795, 0, 1450)),
            ("Camp_Lookout_Soldier", new Vector3(-5210, 0, 1460))
        };
        foreach (var entry in positions)
        {
            var go = GameObject.Find(entry.Item1);
            if (go == null) throw new InvalidOperationException("Camp actor missing: " + entry.Item1);
            Seat(go, terrain, entry.Item2);
        }
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        EditorApplication.Exit(0);
    }

    public static void RetunePathsAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp");
        var stage = camp.transform.Cast<Transform>().First(t => t.name.Contains("Rustic_Wooden_Stage"));
        RebuildPaths(Terrain.activeTerrain, stage.position);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        EditorApplication.Exit(0);
    }
}
