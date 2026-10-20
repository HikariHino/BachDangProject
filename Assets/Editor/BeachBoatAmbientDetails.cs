using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small, reusable camp work areas for beachBoat; no new textures or actors.</summary>
public static class BeachBoatAmbientDetails
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";
    const string Prefabs = "Assets/Prefabs/Props/pf_";

    struct Detail
    {
        public string name, prefab;
        public float x, z, yaw;
        public Detail(string name, string prefab, float x, float z, float yaw)
        { this.name = name; this.prefab = prefab; this.x = x; this.z = z; this.yaw = yaw; }
    }

    // Intentionally uneven groups: wood work by the forge, food stores by the north wall,
    // and a few camp chores at the far edges of the tent rows. The central lanes stay clear.
    static readonly Detail[] Details = {
        new Detail("Go_Sua_Thuyen_1", "plankpile_02", 105, 310, 78),
        new Detail("Go_Sua_Thuyen_2", "logpile_01", 143, 313, -12),
        new Detail("Xo_Xuong_Ren", "buckets_01", 178, 319, 23),
        new Detail("Thung_Luong_Thuc_1", "barrels_02", -82, 294, 15),
        new Detail("Thung_Luong_Thuc_2", "barrel_01", -119, 307, -17),
        new Detail("Xo_Nuoc_Bep", "buckets_01", -52, 316, 28),
        new Detail("Go_Du_Tru_1", "logpile_01", -217, 299, 79),
        new Detail("Van_Go_Du_Tru", "plankpile_01", -257, 290, -13),
        new Detail("Xo_Du_Tru", "bucket_01", -185, 312, 12),
        new Detail("Vu_Khi_Tap_Luyen_1", "halberd_01", 78, 279, 35),
        new Detail("Khien_Tap_Luyen", "shield_02", 112, 273, -24),
        new Detail("Vu_Khi_Tap_Luyen_2", "halberd_01", 49, 301, 11),
        new Detail("Cui_Dun_Nam_Tay", "logpile_01", -260, -252, 21),
        new Detail("Nuoc_Cho_Linh_Tay", "buckets_01", -266, -208, -10),
        new Detail("Luong_Thuc_Nam_Tay", "barrels_01", -290, -247, -17),
        new Detail("Cui_Dun_Nam_Dong", "logpile_01", 252, -259, -29),
        new Detail("Nuoc_Cho_Linh_Dong", "buckets_01", 276, -223, 15),
        new Detail("Van_Sua_Lieu", "plankpile_03", 286, -278, 62),
        new Detail("Thung_Nuoc_Tay", "barrel_01", -283, 19, 10),
        new Detail("Xo_Nuoc_Tay", "bucket_01", -294, -9, -8),
        new Detail("Thung_Nuoc_Dong", "barrel_01", 291, 12, 18),
        new Detail("Xo_Nuoc_Dong", "bucket_01", 278, -19, -6),
    };

    static Bounds BoundsOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true)
            .Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) throw new InvalidOperationException("No renderer: " + go.name);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static bool Overlaps(Bounds a, Bounds b, float gap)
    {
        return a.min.x < b.max.x + gap && a.max.x > b.min.x - gap &&
               a.min.z < b.max.z + gap && a.max.z > b.min.z - gap;
    }

    public static void ApplyAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp");
        var compact = camp?.transform.Find("Compact_Living_Camp");
        var stage = camp?.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains("Rustic_Wooden_Stage"));
        var terrain = Terrain.activeTerrain;
        if (compact == null || stage == null || terrain == null)
            throw new InvalidOperationException("Expected beachBoat camp or terrain missing");
        if (compact.Find("Camp_Daily_Work_Details") != null)
            throw new InvalidOperationException("Camp details already present; refusing duplicate placement");

        Directory.CreateDirectory(".utmp/BeachBoatAmbientDetails");
        EditorSceneManager.SaveScene(scene, ".utmp/BeachBoatAmbientDetails/before.unity", true);
        var root = new GameObject("Camp_Daily_Work_Details").transform;
        root.SetParent(compact, false);
        var stageCenter = stage.position;
        var fixedObjects = new List<Bounds>();
        foreach (var name in new[] { "Barracks_Rows", "Supply_And_Work" })
        {
            var group = compact.Find(name);
            if (group != null)
                foreach (Transform child in group)
                    if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
                        fixedObjects.Add(BoundsOf(child.gameObject));
        }
        foreach (var name in new[] { "Lo_Ren_Quan_Do", "Kho_Khi_Gioi_Nho" })
        {
            var go = GameObject.Find(name);
            if (go != null) fixedObjects.Add(BoundsOf(go));
        }
        var placed = new List<string>();
        var skipped = new List<string>();
        var addedBounds = new List<Bounds>();
        foreach (var entry in Details)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + entry.prefab + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing prefab " + entry.prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = entry.name;
            go.transform.position = stageCenter + new Vector3(entry.x, 0, entry.z);
            go.transform.rotation = Quaternion.Euler(0, entry.yaw, 0);
            go.transform.localScale *= 7f;
            var bounds = BoundsOf(go);
            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (footprint > 42f) go.transform.localScale *= 42f / footprint;
            bounds = BoundsOf(go);
            float ground = terrain.SampleHeight(go.transform.position) + terrain.transform.position.y;
            go.transform.position += Vector3.up * (ground + .15f - bounds.min.y);
            bounds = BoundsOf(go);
            if (fixedObjects.Any(b => Overlaps(bounds, b, 4)) ||
                addedBounds.Any(b => Overlaps(bounds, b, 3)))
            {
                skipped.Add(entry.name);
                UnityEngine.Object.DestroyImmediate(go);
                continue;
            }
            foreach (var body in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var collider in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            addedBounds.Add(bounds);
            placed.Add(entry.name);
        }
        if (placed.Count < 12) throw new InvalidOperationException("Too few camp details placed: " + placed.Count);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatAmbientDetails.json", JsonConvert.SerializeObject(new {
            scene = scene.path, placed = placed.Count, skipped = skipped.Count,
            names = placed, skippedNames = skipped, terrainTrees = terrain.terrainData.treeInstanceCount,
            renderers = root.GetComponentsInChildren<Renderer>(true).Length
        }, Formatting.Indented));
        EditorApplication.Exit(0);
    }
}
