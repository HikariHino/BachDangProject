using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time relocation: preserve human-scale buildings on the seven-times-larger island.</summary>
public static class BeachBoatCampIsland
{
    static readonly Vector3 MainlandOffset = new Vector3(-5936, 105.15f, -3160);
    static Terrain terrain;
    static float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
    static Vector3 Island(Vector3 p) => new Vector3(p.x * 7, 0, p.z * 7);

    static void Seat(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        float floor = Ground(t.position);
        t.position += Vector3.up * (floor - .15f - renderers.Min(r => r.bounds.min.y));
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var edge = b - a;
        return (p - (a + edge * Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(.001f, edge.sqrMagnitude)))).magnitude;
    }

    static bool Inside(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = poly[j]; var b = poly[i];
            if (SegmentDistance(p, a, b) < 25) return true;
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    [MenuItem("Bach Dang/Move TestQuest Camp To Island")]
    public static void Run() => Debug.Log(Relocate());

    public static int OptimizeFenceCollisions()
    {
        if (Application.isPlaying) throw new Exception("Edit Mode required.");
        terrain = Terrain.activeTerrain;
        var host = GameObject.Find("TestQuest_Camp");
        if (host == null || host.transform.Find("Island_Fence_Infill") == null) throw new Exception("Move camp first.");
        var fence = host.transform.Cast<Transform>().Single(t => t.name.Contains("HANG RAO"));
        var panels = fence.Cast<Transform>().Where(t => t.name.StartsWith("HangRao"))
            .GroupBy(t => int.Parse(t.name.Split('_')[2])).OrderBy(g => g.Key)
            .SelectMany(g => {
                var numbered = g.Where(t => !t.name.Contains("(")).OrderBy(t => int.Parse(t.name.Split('_')[3])).ToArray();
                var direction = numbered.Last().position - numbered.First().position;
                return g.OrderBy(t => Vector3.Dot(t.position, direction));
            }).ToArray();
        var old = host.transform.Find("Island_Fence_Collisions");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        foreach (var panel in panels.Concat(new[] { host.transform.Find("Island_Fence_Infill") }))
            foreach (var collider in panel.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(collider);
        var root = new GameObject("Island_Fence_Collisions"); root.transform.SetParent(host.transform, false);
        Undo.RegisterCreatedObjectUndo(root, "Simplify fence collisions");
        var gate = GameObject.Find("CampToVillage").transform.position;
        int count = 0;
        Action<Vector3, Vector3> add = (a, b) => {
            var direction = b - a; direction.y = 0; if (direction.magnitude < .5f) return;
            float low = Mathf.Min(Ground(a), Ground(b)) - 2, high = Mathf.Max(Ground(a), Ground(b)) + 26;
            var go = new GameObject("FenceBarrier_" + count++); go.transform.SetParent(root.transform, false);
            var p = (a + b) * .5f; p.y = (low + high) * .5f;
            go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(direction));
            go.AddComponent<BoxCollider>().size = new Vector3(5, high - low, direction.magnitude + 1);
        };
        for (int i = 0; i < panels.Length; i++)
        {
            var a = panels[i].position; var b = panels[(i + 1) % panels.Length].position;
            var d = new Vector2(b.x - a.x, b.z - a.z); var f = new Vector2(a.x - gate.x, a.z - gate.z);
            float aa = d.sqrMagnitude, bb = 2 * Vector2.Dot(f, d), cc = f.sqrMagnitude - 40 * 40;
            float disc = bb * bb - 4 * aa * cc;
            if (disc <= 0 || aa < .001f) { add(a, b); continue; }
            float enter = (-bb - Mathf.Sqrt(disc)) / (2 * aa), leave = (-bb + Mathf.Sqrt(disc)) / (2 * aa);
            if (leave <= 0 || enter >= 1) { add(a, b); continue; }
            if (enter > 0) add(a, Vector3.Lerp(a, b, enter));
            if (leave < 1) add(Vector3.Lerp(a, b, leave), b);
        }
        EditorSceneManager.MarkSceneDirty(host.scene); return count;
    }

    public static string Relocate()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity") throw new Exception("Open beachBoat in Edit Mode.");
        var host = GameObject.Find("TestQuest_Camp");
        if (host == null || host.transform.Find("Island_Fence_Infill") != null) throw new Exception("Missing camp or already on island.");
        terrain = Terrain.activeTerrain;
        var roots = host.transform.Cast<Transform>().ToArray();
        var sourcePositions = roots.ToDictionary(t => t, t => t.position - MainlandOffset);
        var structures = roots.Where(t => t.name.Contains("Nomad") || t.name.Contains("Rustic_Wooden_Stage")).ToArray();
        var stage = structures.Single(t => t.name.Contains("Rustic_Wooden_Stage"));
        var originalStage = stage.position;
        var fence = roots.Single(t => t.name.Contains("HANG RAO"));
        var nodes = fence.Cast<Transform>().ToArray();
        var oldPositions = nodes.ToDictionary(t => t, t => t.position - MainlandOffset);
        var panels = nodes.Where(t => t.name.StartsWith("HangRao"))
            .GroupBy(t => int.Parse(t.name.Split('_')[2])).OrderBy(g => g.Key)
            .SelectMany(g => {
                var numbered = g.Where(t => !t.name.Contains("(")).OrderBy(t => int.Parse(t.name.Split('_')[3])).ToArray();
                var direction = oldPositions[numbered.Last()] - oldPositions[numbered.First()];
                return g.OrderBy(t => Vector3.Dot(oldPositions[t], direction));
            }).ToArray();
        var polygon = panels.Select(t => { var p = Island(oldPositions[t]); return new Vector2(p.x, p.z); }).ToArray();
        var session = GameObject.Find("BeachBoat_Thai_Gameplay").GetComponent<BeachBoatStory.BeachBoatQuestSession>();
        var npc = session.GetComponentInChildren<BeachBoatStory.NPCQuestGiver>();
        var npcStageOffset = npc.transform.position - originalStage;
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Move camp to TestQuest island");
        Undo.RegisterFullObjectHierarchyUndo(host, "Move island camp");
        foreach (var t in roots.Where(t => t != fence))
        {
            var p = sourcePositions[t]; var destination = Island(p);
            // Small flags/torches remain beside their shelter instead of spreading seven times apart.
            if (!structures.Contains(t))
            {
                var anchor = structures.OrderBy(a => (sourcePositions[a] - p).sqrMagnitude).First();
                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(sourcePositions[anchor].x, sourcePositions[anchor].z)) < 60)
                    destination = Island(sourcePositions[anchor]) + (p - sourcePositions[anchor]);
            }
            t.position = new Vector3(destination.x, t.position.y, destination.z); Seat(t);
        }
        foreach (var t in nodes)
        {
            var p = Island(oldPositions[t]); t.position = new Vector3(p.x, t.position.y, p.z);
            t.rotation = Quaternion.Euler(0, t.eulerAngles.y, 0); Seat(t);
        }
        var infill = new GameObject("Island_Fence_Infill"); infill.transform.SetParent(host.transform, false);
        Undo.RegisterCreatedObjectUndo(infill, "Fill island perimeter");
        // All infill shares the existing 3,124-vertex panel mesh and instanced material.
        // The opening at the source gate is intentionally kept clear.
        var gateSource = new Vector3(-911.8f, 0, 65);
        int inserted = 0;
        for (int i = 0; i < panels.Length; i++)
        {
            var a = panels[i]; var b = panels[(i + 1) % panels.Length];
            var start = a.position; var end = b.position;
            int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z)) / 14.5f);
            for (int k = 1; k < steps; k++)
            {
                var p = Vector3.Lerp(start, end, (float)k / steps);
                var gate = Island(gateSource);
                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(gate.x, gate.z)) < 32) continue;
                // Avoid overlapping the source watchtowers at corners.
                if (nodes.Where(t => t.name.StartsWith("ThapCanh")).Any(t => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(t.position.x, t.position.z)) < 12)) continue;
                var copy = UnityEngine.Object.Instantiate(a.gameObject, infill.transform);
                copy.name = "SharedFence_" + inserted++; copy.transform.position = p;
                copy.transform.rotation = a.rotation; Seat(copy.transform);
                foreach (var group in copy.GetComponentsInChildren<LODGroup>())
                { var lods = group.GetLODs(); foreach (var lod in lods) foreach (var r in lod.renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            }
        }
        Undo.RecordObject(npc.transform, "Move island commander");
        npc.transform.position = stage.position + npcStageOffset;
        Physics.SyncTransforms();
        var hits = Physics.RaycastAll(npc.transform.position + Vector3.up * 100, Vector3.down, 200)
            .Where(h => h.collider.transform.IsChildOf(stage)).OrderByDescending(h => h.point.y).ToArray();
        if (hits.Length == 0) throw new Exception("Commander must stand over the stage.");
        npc.transform.position += Vector3.up * (hits[0].point.y + .1f - npc.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y));
        var arrival = npc.transform.position + Vector3.forward * 95; arrival.y = Ground(arrival) + 1.15f;
        foreach (var t in new[] { session.playerSpawn, session.player.transform })
        { Undo.RecordObject(t, "Move island spawn"); t.SetPositionAndRotation(arrival, Quaternion.Euler(0, 180, 0)); }
        var gatePosition = Island(gateSource); gatePosition.y = Ground(gatePosition) + .15f;
        foreach (var name in new[] { "CampToVillage", "CampToRiver" })
        { var t = session.transform.Find(name); Undo.RecordObject(t, "Move island exit"); t.position = gatePosition; }
        OptimizeFenceCollisions();
        // Keep the root used by map tools/HUD located at the new camp.
        var campRoot = host.transform.parent; Undo.RecordObject(campRoot, "Move camp map anchor");
        host.transform.SetParent(null, true); campRoot.position = stage.position; host.transform.SetParent(campRoot, true);
        var data = terrain.terrainData; var before = data.treeInstances;
        var kept = before.Where(t => { var p = Vector3.Scale(t.position, data.size) + terrain.transform.position; return !Inside(new Vector2(p.x, p.z), polygon); }).ToArray();
        Undo.RegisterCompleteObjectUndo(data, "Clear island camp trees"); data.SetTreeInstances(kept, false); EditorUtility.SetDirty(data); terrain.Flush();
        Undo.CollapseUndoOperations(undo);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        var report = new { tents = structures.Count(t => t.name.Contains("Nomad")), insertedFencePanels = inserted, removedTrees = before.Length - kept.Length,
            remainingTrees = kept.Length, arrival = arrival.ToString(), npc = npc.transform.position.ToString(), exit = gatePosition.ToString(),
            renderers = host.GetComponentsInChildren<Renderer>().Length, colliders = host.GetComponentsInChildren<Collider>().Length,
            sharedMeshMiB = host.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).Distinct().Sum(m => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m)) / 1048576f };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
        System.IO.File.WriteAllText(".utmp/CampIsland/result.json", json); return json;
    }
}
