using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Corrects the camp's human/building scale without changing terrain or adding heavy assets.</summary>
public static class BeachBoatCampScaleRepair
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("No visible mesh on " + go.name);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void KeepGrounded(Transform value, float previousBottom)
    {
        value.position += Vector3.up * (previousBottom - BoundsOf(value.gameObject).min.y);
    }

    static void Resize(Transform value, Vector3 newScale)
    {
        float bottom = BoundsOf(value.gameObject).min.y;
        value.localScale = newScale;
        KeepGrounded(value, bottom);
    }

    public static void ApplyAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp")?.transform.Find("Compact_Living_Camp");
        var terrain = Terrain.activeTerrain;
        var stage = GameObject.Find("TestQuest_Camp")?.transform.Cast<Transform>()
            .FirstOrDefault(t => t.name.Contains("Rustic_Wooden_Stage"));
        var rows = camp?.Find("Barracks_Rows");
        var dorms = camp?.Find("Living_Dormitories");
        var store = camp?.Find("Supply_And_Work/Central_Supply_Store");
        var fence = camp?.Find("Camp_Palisade_Reused");
        var west = dorms?.Find("Nha_Nghi_Linh_Tay");
        var east = dorms?.Find("Nha_Nghi_Linh_Dong");
        var exchangeTent = rows?.Find("Barracks_Tent_13");
        if (camp == null || terrain == null || stage == null || rows == null || dorms == null || store == null ||
            fence == null || west == null || east == null || exchangeTent == null)
            throw new InvalidOperationException("Expected beachBoat camp parts are missing.");
        if (rows.childCount != 14 || Math.Abs(rows.GetChild(0).localScale.y - 1f) > .02f ||
            Math.Abs(store.localScale.y - 2.5f) > .02f)
            throw new InvalidOperationException("Camp scale differs from audited baseline; refusing to apply twice.");

        Directory.CreateDirectory(".utmp/CampScaleRepair");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/CampScaleRepair/before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true))
            throw new IOException("Scene backup failed.");

        // The initial dorm pass consumed tents 05 and 06, both on the western row.
        // Exchange the second dorm with the eastern tent at the same z as the first dorm.
        Vector3 oldEast = east.position;
        Vector3 newEast = exchangeTent.position;
        Vector3 delta = newEast - oldEast;
        east.position = newEast;
        exchangeTent.position = oldEast;
        exchangeTent.name = "Barracks_Tent_06";
        foreach (var soldier in dorms.GetComponentsInChildren<NPCWander>(true)
                     .Where(w => w.name.StartsWith("Linh_Nghi_Ca_Dong_")))
        {
            soldier.transform.position += delta;
            if (soldier.waypoints != null && soldier.waypoints.Length > 0 && soldier.waypoints[0] != null)
                soldier.waypoints[0].parent.position += delta;
        }

        foreach (Transform tent in rows)
            Resize(tent, new Vector3(tent.localScale.x * 1.07f, tent.localScale.y * 1.6f, tent.localScale.z * 1.07f));

        // Existing wooden barracks already clear the player's head by ~28 world units.
        // Raise the two new dorm roofs and the perimeter while retaining their footprints.
        foreach (var house in new[] { west, east })
            Resize(house, new Vector3(house.localScale.x, house.localScale.y * 1.12f, house.localScale.z));
        Resize(fence, new Vector3(fence.localScale.x, fence.localScale.y * 1.23f, fence.localScale.z));

        // The small central store was shorter than the player and blocked the main path.
        // Give it a human-sized door and place it to the side near the rear palisade.
        store.localScale = Vector3.one * 5f;
        Vector3 storePlace = stage.position + new Vector3(90, 0, 315);
        storePlace.y = terrain.SampleHeight(storePlace) + terrain.transform.position.y;
        store.position = storePlace;
        store.position += Vector3.up * (storePlace.y + .1f - BoundsOf(store.gameObject).min.y);

        // The sleeping clip is only played at the mat. Keep that route directly over it.
        foreach (var soldier in dorms.GetComponentsInChildren<NPCWander>(true)
                     .Where(w => w.name.StartsWith("Linh_Nghi_Ca_")))
        {
            bool isWest = soldier.name.Contains("_Tay_");
            int index = soldier.name.EndsWith("_1") ? 0 : 1;
            int side = isWest ? -1 : 1;
            var house = isWest ? west : east;
            if (soldier.waypoints == null || soldier.waypoints.Length != 2 || soldier.waypoints[1] == null)
                throw new InvalidOperationException("Sleeping route missing for " + soldier.name);
            var mat = house.position + new Vector3(side * 7, 0, index == 0 ? -15 : 15);
            mat.y = terrain.SampleHeight(mat) + terrain.transform.position.y + .2f;
            soldier.waypoints[1].position = mat;
        }

        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        Debug.Log("Camp scale repaired: 14 taller tents, two raised dorms across both rows, taller fence and relocated full-size store.");
        EditorApplication.Exit(0);
    }

    public static void RepairRoofsAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp")?.transform.Find("Compact_Living_Camp");
        if (camp == null || camp.Find("Living_Dormitories") == null) throw new InvalidOperationException("Camp dormitories missing.");
        Directory.CreateDirectory(".utmp/CampScaleRepair");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/CampScaleRepair/before-roof-material.unity", true))
            throw new IOException("Scene backup failed.");

        const string folder = "Assets/BeachBoatStory/Camp";
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Props/Materials/wood_tiled_02_a.mat");
        if (wood == null) throw new InvalidOperationException("Shared wood texture missing.");
        string roofPath = folder + "/Camp_Shingle_Roof.mat";
        var roof = AssetDatabase.LoadAssetAtPath<Material>(roofPath);
        if (roof == null) { roof = new Material(wood) { name = "Camp_Shingle_Roof" }; AssetDatabase.CreateAsset(roof, roofPath); }
        roof.SetColor("_BaseColor", new Color(.58f, .49f, .36f));
        roof.SetFloat("_Smoothness", .08f); roof.enableInstancing = true; EditorUtility.SetDirty(roof);

        string matPath = folder + "/Camp_Sleeping_Mat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Camp_Sleeping_Mat" }; AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetColor("_BaseColor", new Color(.46f, .36f, .23f)); mat.SetFloat("_Smoothness", 0); mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);

        int changed = 0;
        foreach (var name in new[] { "Nha_Nghi_Linh_Tay", "Nha_Nghi_Linh_Dong", "Central_Supply_Store", "Lo_Ren_Quan_Do", "Kho_Khi_Gioi_Nho" })
        {
            var target = GameObject.Find(name);
            if (target == null) throw new InvalidOperationException("Roof target missing: " + name);
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith("Chiếu ngủ")) { renderer.sharedMaterial = mat; changed++; continue; }
                var materials = renderer.sharedMaterials;
                bool swapped = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name.Contains("strawroof"))
                    { materials[i] = roof; swapped = true; }
                }
                if (swapped) { renderer.sharedMaterials = materials; changed++; }
            }
        }
        if (changed < 8) throw new InvalidOperationException("Too few roofs repaired: " + changed);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        Debug.Log("Replaced bright-blue straw atlas on " + changed + " camp roof/mat renderers with shared wood texture and muted mat.");
        EditorApplication.Exit(0);
    }

    public static void AlignSleepingMatsAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var dorms = GameObject.Find("Living_Dormitories")?.transform;
        if (dorms == null) throw new InvalidOperationException("Dormitories missing.");
        Directory.CreateDirectory(".utmp/CampScaleRepair");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/CampScaleRepair/before-sleep-alignment.unity", true))
            throw new IOException("Scene backup failed.");
        foreach (var name in new[] { "Nha_Nghi_Linh_Tay", "Nha_Nghi_Linh_Dong" })
        {
            var house = dorms.Find(name);
            if (house == null) throw new InvalidOperationException("Missing house " + name);
            for (int i = 1; i <= 2; i++)
            {
                var mat = house.Find("Chiếu ngủ " + i);
                if (mat == null) throw new InvalidOperationException("Missing mat " + name + "/" + i);
                mat.localScale = new Vector3(34, mat.localScale.y, mat.localScale.z);
            }
        }
        foreach (var soldier in dorms.GetComponentsInChildren<NPCWander>(true)
                     .Where(w => w.name.StartsWith("Linh_Nghi_Ca_")))
        {
            if (soldier.waypoints == null || soldier.waypoints.Length != 2 || soldier.waypoints[1] == null)
                throw new InvalidOperationException("Sleeping route missing: " + soldier.name);
            // The source animation lies along its local X axis. Zero yaw aligns the
            // body with the long side of the mat; the old 90° pose crossed both mats.
            soldier.waypoints[1].rotation = Quaternion.Euler(0, soldier.name.Contains("_Tay_") ? 0 : 180, 0);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        EditorApplication.Exit(0);
    }

    public static void RepairLegacyRestSoldiersAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var terrain = Terrain.activeTerrain;
        if (terrain == null) throw new InvalidOperationException("Camp terrain missing.");
        Directory.CreateDirectory(".utmp/CampScaleRepair");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/CampScaleRepair/before-legacy-npc-poses.unity", true))
            throw new IOException("Scene backup failed.");
        foreach (var name in new[] { "Camp_Sitting_Soldier", "Camp_Laying_Soldier" })
        {
            var actor = GameObject.Find(name);
            var pose = actor?.GetComponent<NPCCampPose>();
            var animator = actor?.GetComponent<Animator>();
            var collider = actor?.GetComponent<BoxCollider>();
            if (pose == null || animator == null || collider == null)
                throw new InvalidOperationException("Legacy camp soldier missing: " + name);
            pose.pose = NPCCampPose.PoseType.Standing;
            animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(.05f);
            var position = actor.transform.position;
            float ground = terrain.SampleHeight(position) + terrain.transform.position.y;
            // Edit-mode skinned bounds use a different pose and under-report the
            // runtime foot height. Play-mode capture measured the 14x character's
            // root 3.1 world units above its feet in Idle.
            position.y = ground + actor.transform.lossyScale.y * .22f + .1f;
            actor.transform.position = position;
            collider.size = new Vector3(.7f, 1.85f, .65f);
            collider.center = Vector3.up * .925f;
            var dialogue = actor.GetComponent<NPCDialogue>();
            if (dialogue != null) dialogue.speakerName = "Lính gác doanh trại";
        }
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        EditorApplication.Exit(0);
    }

    public static void MoveStoreOffPathAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var store = GameObject.Find("Central_Supply_Store")?.transform;
        var stage = GameObject.Find("TestQuest_Camp")?.transform.Cast<Transform>()
            .FirstOrDefault(t => t.name.Contains("Rustic_Wooden_Stage"));
        var terrain = Terrain.activeTerrain;
        if (store == null || stage == null || terrain == null)
            throw new InvalidOperationException("Camp store or terrain missing.");
        var position = stage.position + new Vector3(90, 0, 315);
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        store.position = position;
        store.position += Vector3.up * (position.y + .1f - BoundsOf(store.gameObject).min.y);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        EditorApplication.Exit(0);
    }
}
