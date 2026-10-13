using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Explicit one-time editor migration. Source scene and imported FBX assets are read-only.
public static class BeachBoatCampTransfer
{
    const string AssetsPath = "Assets/BeachBoatStory/Camp";
    static readonly Vector3 Offset = new Vector3(-5936f, 105.15f, -3160f);
    static readonly Dictionary<string, Mesh[]> meshes = new Dictionary<string, Mesh[]>();
    static readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();

    public static void ImportMeshes()
    {
        Directory.CreateDirectory(AssetsPath); AssetDatabase.Refresh();
        foreach (var key in new[] { "tent", "stage", "post" })
        for (int lod = 0; lod < 3; lod++)
        {
            string path = AssetsPath + "/" + key + "_LOD" + lod + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) continue;
            using (var reader = new BinaryReader(File.OpenRead(".utmp/CampTransfer/" + key + "-lod" + lod + ".bin")))
            {
                int count = reader.ReadInt32(), indexCount = reader.ReadInt32();
                var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count]; var indices = new int[indexCount];
                for (int i = 0; i < count; i++) vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                for (int i = 0; i < count; i++) normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                for (int i = 0; i < count; i++) uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                for (int i = 0; i < indexCount; i++) indices[i] = reader.ReadInt32();
                var mesh = new Mesh { name = key + "_Laptop_LOD" + lod, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = indices;
                mesh.RecalculateBounds(); mesh.RecalculateTangents(); MeshUtility.Optimize(mesh);
                AssetDatabase.CreateAsset(mesh, path);
            }
        }
        AssetDatabase.SaveAssets();
    }

    static Material InstanceMaterial(Material source)
    {
        if (source == null) return null;
        if (materials.TryGetValue(source, out var value)) return value;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        string path = AssetsPath + "/CampMaterial_" + guid + ".mat";
        value = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (value == null)
        {
            value = new Material(source) { name = source.name + "_CampInstanced", enableInstancing = true };
            AssetDatabase.CreateAsset(value, path);
        }
        materials.Add(source, value); return value;
    }

    static GameObject Copy(GameObject source, Transform parent)
    {
        var clone = UnityEngine.Object.Instantiate(source); clone.name = source.name;
        SceneManager.MoveGameObjectToScene(clone, parent.gameObject.scene);
        if (PrefabUtility.IsPartOfPrefabInstance(clone))
            PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        clone.transform.SetParent(parent, true); clone.transform.position += Offset;
        foreach (var c in clone.GetComponentsInChildren<MonoBehaviour>(true)) if (c != null) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in clone.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in clone.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in clone.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in clone.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var t in clone.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            // Shared instancing avoids static batching duplicating the same mesh per tent/fence.
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }
        foreach (var r in clone.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = r.sharedMaterials.Select(InstanceMaterial).ToArray();
        return clone;
    }

    static void OptimizedVisual(GameObject go, string key)
    {
        var filter = go.GetComponentInChildren<MeshFilter>(); var renderer = filter.GetComponent<MeshRenderer>();
        filter.sharedMesh = meshes[key][0];
        var lodRenderers = new Renderer[3]; lodRenderers[0] = renderer;
        for (int i = 1; i < 3; i++)
        {
            var child = new GameObject("Laptop_LOD" + i); child.transform.SetParent(filter.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = meshes[key][i];
            var r = child.AddComponent<MeshRenderer>(); r.sharedMaterials = renderer.sharedMaterials;
            r.shadowCastingMode = i == 2 ? ShadowCastingMode.Off : renderer.shadowCastingMode; lodRenderers[i] = r;
        }
        var group = filter.gameObject.AddComponent<LODGroup>(); group.animateCrossFading = false;
        group.SetLODs(new[] { new LOD(.20f, new[] { lodRenderers[0] }), new LOD(.055f, new[] { lodRenderers[1] }), new LOD(.008f, new[] { lodRenderers[2] }) });
        group.RecalculateBounds();
        if (key != "post")
        {
            var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = meshes[key][1];
        }
    }

    static void SimpleColliders(GameObject root)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            var b = filter.sharedMesh.bounds;
            var box = filter.gameObject.AddComponent<BoxCollider>(); box.center = b.center; box.size = b.size;
            var renderer = filter.GetComponent<Renderer>(); if (renderer == null) continue;
            var group = filter.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(.012f, new[] { renderer }) }); group.RecalculateBounds();
        }
    }

    static bool InsideOrNear(Vector2 p, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[j]; var b = polygon[i]; var edge = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(.001f, edge.sqrMagnitude));
            if ((p - (a + edge * t)).sqrMagnitude < 30f * 30f) return true;
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    public static string ReplaceCamp()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity") throw new Exception("Open beachBoat in Edit Mode.");
        var old = GameObject.Find("05_Trai_Du_Bi");
        if (old == null || old.transform.Find("TestQuest_Camp") != null) throw new Exception("Missing old camp or replacement already exists.");
        meshes.Clear(); materials.Clear();
        foreach (var key in new[] { "tent", "stage", "post" })
        {
            meshes[key] = Enumerable.Range(0, 3).Select(i => AssetDatabase.LoadAssetAtPath<Mesh>(AssetsPath + "/" + key + "_LOD" + i + ".asset")).ToArray();
            if (meshes[key].Any(m => m == null)) throw new Exception("Import optimized meshes first.");
        }
        var terrain = Terrain.activeTerrain;
        var source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/TestQuest.unity");
        var host = new GameObject("TestQuest_Camp");
        SceneManager.MoveGameObjectToScene(host, scene);
        try
        {
            var roots = source.GetRootGameObjects();
            var fence = roots.Single(g => g.name.Contains("HANG RAO DOANH TRAI"));
            var polygon = fence.transform.Cast<Transform>().Where(t => t.name.StartsWith("HangRao"))
                .Select(t => new Vector2(t.position.x + Offset.x, t.position.z + Offset.z)).ToArray();
            var newFence = Copy(fence, host.transform); SimpleColliders(newFence);
            foreach (var item in roots.Where(g => g.name.Contains("Nomad") || g.name.Contains("Rustic_Wooden_Stage") || g.name.Contains("Bound_Relic") || g.name == "VillageArrivalPoint"))
            {
                string key = item.name.Contains("Nomad") ? "tent" : item.name.Contains("Stage") ? "stage" : "post";
                OptimizedVisual(Copy(item, host.transform), key);
            }
            foreach (var item in roots.Where(g => g.name.StartsWith("prop_torch") && g.transform.position.x < -350))
                Copy(item, host.transform);
            foreach (var item in roots.Where(g => g.name.StartsWith("build_boat") && g.transform.position.x < -350))
                SimpleColliders(Copy(item, host.transform));

            // Commit the replacement only after all source objects and mesh bindings succeeded.
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Replace beachBoat camp from TestQuest");
            Undo.RegisterCreatedObjectUndo(host, "Transfer camp");
            foreach (var child in old.transform.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
            host.transform.SetParent(old.transform, true);
            var session = GameObject.Find("BeachBoat_Thai_Gameplay").GetComponent<BeachBoatStory.BeachBoatQuestSession>();
            var proxies = session.transform.Find("ExistingBuildingCollisions");
            foreach (var proxy in proxies.Cast<Transform>().Where(t => t.name.StartsWith("Lan_Quan") || t.name.StartsWith("Kho_Tiep_Te")).ToArray()) Undo.DestroyObjectImmediate(proxy.gameObject);
            Vector3 arrival = new Vector3(-991.6f, 0, 265.86f) + Offset;
            arrival.y = terrain.SampleHeight(arrival) + terrain.transform.position.y + 1.15f;
            foreach (var t in new[] { session.playerSpawn, session.player.transform })
            { Undo.RecordObject(t, "Move camp arrival"); t.SetPositionAndRotation(arrival, Quaternion.Euler(0, 115, 0)); }
            var ngo = session.GetComponentInChildren<BeachBoatStory.NPCQuestGiver>();
            Undo.RecordObject(ngo.transform, "Move Ngo Quyen to command platform");
            var sourceNgo = roots.Single(g => g.name == "NPC_NgoQuyen (1)");
            ngo.transform.SetPositionAndRotation(sourceNgo.transform.position + Offset, sourceNgo.transform.rotation);
            // Source pivot sits below the feet; align the copied character with the new stage collider.
            Physics.SyncTransforms();
            var stageFloor = Physics.RaycastAll(ngo.transform.position + Vector3.up * 100, Vector3.down, 150)
                .Where(h => h.collider.name.Contains("Rustic_Wooden_Stage")).OrderByDescending(h => h.point.y).FirstOrDefault();
            var npcRenderers = ngo.GetComponentsInChildren<Renderer>();
            if (stageFloor.collider != null && npcRenderers.Length > 0)
                ngo.transform.position += Vector3.up * (stageFloor.point.y + .1f - npcRenderers.Min(r => r.bounds.min.y));
            Undo.RecordObject(ngo, "Command platform interaction range"); ngo.interactDistance = 55;
            var gatePos = new Vector3(-911.8f, 0, 65f) + Offset;
            gatePos.y = terrain.SampleHeight(gatePos) + terrain.transform.position.y + .15f;
            foreach (var name in new[] { "CampToVillage", "CampToRiver" })
            { var t = session.transform.Find(name); Undo.RecordObject(t, "Rebind camp exit"); t.position = gatePos; }
            var data = terrain.terrainData; var trees = data.treeInstances;
            var kept = trees.Where(t => { var p = Vector3.Scale(t.position, data.size) + terrain.transform.position; return !InsideOrNear(new Vector2(p.x, p.z), polygon); }).ToArray();
            Undo.RegisterCompleteObjectUndo(data, "Clear trees inside transferred camp");
            data.SetTreeInstances(kept, false); EditorUtility.SetDirty(data); terrain.Flush();
            Undo.CollapseUndoOperations(undo);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var report = new { tents = host.GetComponentsInChildren<LODGroup>().Count(g => g.name.Contains("Nomad")), renderers = host.GetComponentsInChildren<Renderer>().Length,
                colliders = host.GetComponentsInChildren<Collider>().Length, removedTrees = trees.Length - kept.Length, remainingTrees = kept.Length,
                optimizedMeshMiB = meshes.Values.SelectMany(m => m).Distinct().Sum(m => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m)) / 1048576f };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(".utmp/CampTransfer/transfer-result.json", json); return json;
        }
        catch { if (host != null && host.transform.parent == null) UnityEngine.Object.DestroyImmediate(host); throw; }
        finally { EditorSceneManager.ClosePreviewScene(source); }
    }
}
