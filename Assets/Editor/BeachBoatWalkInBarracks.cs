using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Eight shared, physically open sleeping quarters replace the sealed miniature meshes.</summary>
public static class BeachBoatWalkInBarracks
{
    const string Folder = "Assets/BeachBoatStory/Camp";
    const string PrefabPath = Folder + "/WalkIn_Barracks.prefab";

    static Material Wood(string name, Color tint)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Props/Materials/wood_tiled_02_a.mat");
            if (source == null) throw new InvalidOperationException("Shared wood texture missing.");
            material = new Material(source) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Smoothness", .08f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject BuildPrefab()
    {
        var timber = Wood("Barracks_Timber", new Color(.7f, .57f, .42f));
        var beams = Wood("Barracks_Dark_Beams", new Color(.38f, .29f, .2f));
        var roof = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Camp_Shingle_Roof.mat");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Camp_Sleeping_Mat.mat");
        if (roof == null || mat == null) throw new InvalidOperationException("Camp roof or mat material missing.");
        var root = new GameObject("WalkIn_Barracks");
        var temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cube = temporary.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(temporary);
        var parts = new Dictionary<Material, List<CombineInstance>>();
        var temporaryMeshes = new List<Mesh>();
        void Box(string name, Vector3 at, Vector3 size, Material material, bool collision = false)
        {
            if (!parts.ContainsKey(material)) parts[material] = new List<CombineInstance>();
            parts[material].Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(at, Quaternion.identity, size) });
            if (!collision) return;
            var go = new GameObject(name + "_Collision"); go.transform.SetParent(root.transform, false);
            go.transform.localPosition = at; go.AddComponent<BoxCollider>().size = size;
        }
        void Gable(float z)
        {
            var mesh = new Mesh { name = "TemporaryGable" };
            mesh.vertices = new[] { new Vector3(-28, 42, z), new Vector3(28, 42, z), new Vector3(0, 59, z),
                new Vector3(-28, 42, z), new Vector3(0, 59, z), new Vector3(28, 42, z) };
            mesh.uv = new[] { Vector2.zero, Vector2.right * 2, new Vector2(1, 1),
                Vector2.zero, new Vector2(1, 1), Vector2.right * 2 };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            mesh.RecalculateNormals(); temporaryMeshes.Add(mesh);
            parts[timber].Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.identity });
        }

        Box("Floor", new Vector3(0, .1f, 0), new Vector3(56, .2f, 70), beams);
        Box("BackWall", new Vector3(0, 21, 35), new Vector3(56, 42, 2), timber, true);
        // Clear opening: 28 wide and 36 high, with no whole-house collider.
        Box("FrontLeft", new Vector3(-21, 21, -35), new Vector3(14, 42, 2), timber, true);
        Box("FrontRight", new Vector3(21, 21, -35), new Vector3(14, 42, 2), timber, true);
        Box("DoorLintel", new Vector3(0, 39, -35), new Vector3(28, 6, 2), timber, true);
        foreach (int side in new[] { -1, 1 })
        {
            Box("SideLower", new Vector3(side * 28, 10, 0), new Vector3(2, 20, 70), timber, true);
            Box("SideUpper", new Vector3(side * 28, 36.5f, 0), new Vector3(2, 11, 70), timber, true);
            foreach (int end in new[] { -1, 1 })
            {
                Box("SideWindowEdge", new Vector3(side * 28, 25.5f, end * 23.5f), new Vector3(2, 11, 23), timber, true);
                Box("CornerPost", new Vector3(side * 28, 21, end * 35), new Vector3(3.5f, 42, 3.5f), beams);
            }
            Box("DoorPost", new Vector3(side * 15.5f, 18, -35.5f), new Vector3(3, 36, 3), beams);
            Box("SideTopBeam", new Vector3(side * 28, 42, 0), new Vector3(4, 4, 74), beams);
            Box("SleepingMat", new Vector3(side * 18, .5f, 8), new Vector3(16, .6f, 40), mat);
            Box("Pillow", new Vector3(side * 18, 1.7f, 0), new Vector3(13, 2, 4), beams);
            Box("BedEdge", new Vector3(side * 26.2f, .8f, 8), new Vector3(1, 1.4f, 42), timber);
        }
        Box("FrontBeam", new Vector3(0, 42, -35), new Vector3(60, 4, 4), beams);
        Box("BackBeam", new Vector3(0, 42, 35), new Vector3(60, 4, 4), beams);
        parts[roof] = new List<CombineInstance>();
        foreach (int side in new[] { -1, 1 })
            parts[roof].Add(new CombineInstance { mesh = cube,
                transform = Matrix4x4.TRS(new Vector3(side * 16, 51, 0),
                    Quaternion.Euler(0, 0, -side * 28), new Vector3(37, 2, 78)) });
        Box("Ridge", new Vector3(0, 59.8f, 0), new Vector3(4, 3, 80), beams);
        Gable(-35); Gable(35);
        Box("BackTable", new Vector3(0, 7, 27), new Vector3(14, 1.5f, 9), timber);
        foreach (int x in new[] { -1, 1 })
            Box("TableLeg", new Vector3(x * 5, 3.5f, 27), new Vector3(2, 7, 6), beams);

        int meshIndex = 0;
        var renderers = new List<Renderer>();
        foreach (var group in parts)
        {
            string path = Folder + "/WalkInBarracks_" + meshIndex + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.name = "WalkInBarracks_" + meshIndex;
            mesh.CombineMeshes(group.Value.ToArray(), true, true); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var piece = new GameObject(group.Key.name); piece.transform.SetParent(root.transform, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderers.Add(renderer); meshIndex++;
        }
        foreach (var mesh in temporaryMeshes) UnityEngine.Object.DestroyImmediate(mesh);
        var lod = root.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(.012f, renderers.ToArray()) }); lod.RecalculateBounds();
        foreach (var data in new[] { ("Outside", new Vector3(0, 3.2f, -50)),
                     ("Door", new Vector3(0, 3.2f, -27)), ("Aisle", new Vector3(0, 3.2f, 2)),
                     ("SleepingPlace", new Vector3(-18, 3.2f, 8)) })
        {
            var anchor = new GameObject(data.Item1).transform; anchor.SetParent(root.transform, false);
            anchor.localPosition = data.Item2;
            if (data.Item1 == "SleepingPlace") anchor.localRotation = Quaternion.identity;
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root); AssetDatabase.SaveAssets();
        return prefab;
    }

    public static void ApplyAndExit()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp")?.transform.Find("Compact_Living_Camp");
        var buildings = camp?.Find("Supply_And_Work");
        var stage = GameObject.Find("TestQuest_Camp")?.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains("Rustic_Wooden_Stage"));
        var terrain = Terrain.activeTerrain;
        if (buildings == null || stage == null || terrain == null) throw new InvalidOperationException("Camp missing.");
        var oldHouses = buildings.Cast<Transform>().Where(t => t.name.StartsWith("Wooden_Barracks_")).OrderBy(t => t.name).ToArray();
        if (oldHouses.Length != 8 || oldHouses.Any(t => t.Find("SleepingPlace") != null))
            throw new InvalidOperationException("Expected eight sealed barracks; refusing duplicate setup.");
        Directory.CreateDirectory(".utmp/WalkInBarracks");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/WalkInBarracks/before.unity", true)) throw new IOException("Backup failed.");
        var prefab = BuildPrefab();
        for (int i = 0; i < oldHouses.Length; i++)
        {
            var old = oldHouses[i]; string name = old.name; var position = old.position;
            if (name == "Wooden_Barracks_-1_185") position.z += 22f;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            var house = (GameObject)PrefabUtility.InstantiatePrefab(prefab, buildings);
            house.name = name; house.transform.position = position;
            house.transform.rotation = Quaternion.Euler(0, position.x < stage.position.x ? -90 : 90, 0);
            var marker = GameObject.Find("Use_" + name)?.GetComponent<CampFacility>();
            if (marker != null)
            {
                marker.transform.position = house.transform.TransformPoint(new Vector3(0, .2f, 5));
                marker.interactDistance = 14; marker.displayName = "quân xá — chiếu nghỉ";
            }
            var actor = GameObject.Find("Camp_Worker_" + (i + 1).ToString("00"));
            var wander = actor?.GetComponent<NPCWander>();
            if (wander == null) throw new InvalidOperationException("Existing soldier missing: " + (i + 1));
            var route = new[] { "Outside", "Door", "Aisle", "SleepingPlace" }.Select(n => house.transform.Find(n)).ToArray();
            actor.transform.SetPositionAndRotation(route[0].position, house.transform.rotation);
            wander.autoGenerateWaypoints = false; wander.followWaypointsInOrder = true;
            wander.waypoints = route; wander.waypointWaitSeconds = new[] { 5f + i % 3, .1f, .1f, 18f + i % 4 };
            wander.activityLabels = new[] { "Đang đổi ca ngoài quân xá", "Đang vào quân xá", "Đang tới chiếu nghỉ", "Đang ngủ sau ca gác" };
            wander.minIdleTime = .2f; wander.maxIdleTime = 1f; wander.walkSpeed = 8f;
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        Debug.Log("Replaced 8 sealed barracks with open 56x70 sleeping rooms; reused 8 existing soldiers.");
        EditorApplication.Exit(0);
    }

    public static void RefineAndExit()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        BuildPrefab();
        var house = GameObject.Find("Wooden_Barracks_-1_185")?.transform;
        if (house == null || house.Find("SleepingPlace") == null) throw new InvalidOperationException("Open barracks missing.");
        var delta = Vector3.forward * 22f;
        house.position += delta;
        var marker = GameObject.Find("Use_" + house.name)?.transform;
        if (marker != null) marker.position += delta;
        foreach (var wander in UnityEngine.Object.FindObjectsByType<NPCWander>(FindObjectsSortMode.None))
            if (wander.waypoints != null && wander.waypoints.Length > 0 && wander.waypoints[0] != null &&
                wander.waypoints[0].IsChildOf(house)) wander.transform.position += delta;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        EditorApplication.Exit(0);
    }

    public static void FixSleepOrientationAndExit()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        var buildings = GameObject.Find("TestQuest_Camp").transform.Find("Compact_Living_Camp/Supply_And_Work");
        foreach (Transform house in buildings)
        {
            if (!house.name.StartsWith("Wooden_Barracks_")) continue;
            var place = house.Find("SleepingPlace");
            if (place == null) throw new InvalidOperationException("Sleeping place missing.");
            place.localRotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(place);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        EditorApplication.Exit(0);
    }

    public static void RebuildSharedPrefabAndExit()
    {
        BuildPrefab();
        EditorApplication.Exit(0);
    }

    public static void ResolveDormClearanceAndExit()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        var terrain = Terrain.activeTerrain;
        var stage = GameObject.Find("TestQuest_Camp").transform.Cast<Transform>().First(t => t.name.Contains("Rustic_Wooden_Stage"));
        foreach (int side in new[] { -1, 1 })
        {
            var house = GameObject.Find("Wooden_Barracks_" + side + "_80").transform;
            var delta = Vector3.right * (stage.position.x + side * 110 - house.position.x);
            house.position += delta;
            GameObject.Find("Use_" + house.name).transform.position += delta;
            foreach (var wander in UnityEngine.Object.FindObjectsByType<NPCWander>(FindObjectsSortMode.None))
                if (wander.waypoints != null && wander.waypoints.Length > 0 && wander.waypoints[0] != null &&
                    wander.waypoints[0].IsChildOf(house)) wander.transform.position += delta;

            string flank = side < 0 ? "Tay" : "Dong";
            var dorm = GameObject.Find("Nha_Nghi_Linh_" + flank).transform;
            for (int bed = 1; bed <= 2; bed++)
            {
                var mat = dorm.Find("Chiếu ngủ " + bed);
                mat.localScale = new Vector3(mat.localScale.x, mat.localScale.y, 14);
                var soldier = GameObject.Find("Linh_Nghi_Ca_" + flank + "_" + bed).GetComponent<NPCWander>();
                var oldRoute = soldier.waypoints[0].parent;
                var routeRoot = new GameObject("OpenDormRoute_" + flank + "_" + bed).transform;
                routeRoot.SetParent(dorm.parent, false);
                var offsets = new[] { new Vector3(-side * 48, 0, bed == 1 ? -12 : 12),
                    new Vector3(-side * 40, 0, 0), new Vector3(-side * 22, 0, 0),
                    new Vector3(-side * 8, 0, 0), new Vector3(side * 7, 0, bed == 1 ? -15 : 15) };
                var route = offsets.Select((offset, index) => {
                    var stop = new GameObject("Stop_" + index).transform; stop.SetParent(routeRoot, false);
                    var point = dorm.position + offset;
                    point.y = terrain.SampleHeight(point) + terrain.transform.position.y + 3.2f;
                    stop.position = point;
                    stop.rotation = Quaternion.Euler(0, side < 0 ? 90 : 270, 0);
                    return stop;
                }).ToArray();
                soldier.transform.SetPositionAndRotation(route[0].position,
                    Quaternion.LookRotation(route[1].position - route[0].position));
                soldier.followWaypointsInOrder = true; soldier.autoGenerateWaypoints = false;
                soldier.waypoints = route; soldier.waypointWaitSeconds = new[] { bed == 1 ? 4f : 10f, .1f, .1f, .1f, 20f };
                soldier.activityLabels = new[] { "Đang đổi ca gác", "Đang chờ trước cửa", "Đang vào nhà nghỉ", "Đang tới chiếu", "Đang ngủ sau ca trực" };
                soldier.walkSpeed = 6; soldier.minIdleTime = .2f; soldier.maxIdleTime = 1;
                UnityEngine.Object.DestroyImmediate(oldRoute.gameObject);
            }
            var use = GameObject.Find("Use_" + dorm.name)?.GetComponent<CampFacility>();
            if (use != null) { use.transform.position = dorm.position + Vector3.up; use.interactDistance = 14; }
        }
        Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        EditorApplication.Exit(0);
    }
}
