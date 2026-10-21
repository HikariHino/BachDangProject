using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Gives the existing camp buildings human-scale entrances and useful door interactions.</summary>
public static class BeachBoatCampFacilitiesSetup
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("No mesh on " + go.name);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void ResizeKeepingFloor(Transform building, Vector3 multiplier)
    {
        float floor = BoundsOf(building.gameObject).min.y;
        building.localScale = Vector3.Scale(building.localScale, multiplier);
        building.position += Vector3.up * (floor - BoundsOf(building.gameObject).min.y);
    }

    static void AddFacility(Transform root, Transform building, Vector3 campCenter,
        Terrain terrain, CampFacility.FacilityKind kind, string displayName)
    {
        var bounds = BoundsOf(building.gameObject);
        bool west = building.position.x < campCenter.x;
        float doorX = west ? bounds.max.x + 7f : bounds.min.x - 7f;
        var point = new Vector3(doorX, 0, building.position.z);
        point.y = terrain.SampleHeight(point) + terrain.transform.position.y + .2f;
        var marker = new GameObject("Use_" + building.name);
        marker.transform.SetParent(root, false);
        marker.transform.position = point;
        var facility = marker.AddComponent<CampFacility>();
        facility.kind = kind;
        facility.displayName = displayName;
        facility.interactDistance = 24f;
        facility.cooldownSeconds = kind == CampFacility.FacilityKind.Armory ? 0f : 45f;
    }

    [MenuItem("BeachBoat/Polish/Make Camp Buildings Useful")]
    public static void ApplyAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp")?.transform.Find("Compact_Living_Camp");
        var stage = GameObject.Find("TestQuest_Camp")?.transform.Cast<Transform>()
            .FirstOrDefault(t => t.name.Contains("Rustic_Wooden_Stage"));
        var terrain = Terrain.activeTerrain;
        if (camp == null || stage == null || terrain == null || camp.Find("Useful_Buildings") != null)
            throw new InvalidOperationException("Camp missing or useful buildings already installed.");
        var buildings = camp.Find("Supply_And_Work");
        var dorms = camp.Find("Living_Dormitories");
        if (buildings == null || dorms == null) throw new InvalidOperationException("Camp houses missing.");
        var barracks = buildings.Cast<Transform>().Where(t => t.name.StartsWith("Wooden_Barracks_"))
            .OrderBy(t => t.name).ToArray();
        var sleepingHouses = dorms.Cast<Transform>().Where(t => t.name.StartsWith("Nha_Nghi_Linh_"))
            .OrderBy(t => t.name).ToArray();
        var forge = GameObject.Find("Lo_Ren_Quan_Do")?.transform;
        var armory = GameObject.Find("Kho_Khi_Gioi_Nho")?.transform;
        var store = buildings.Find("Central_Supply_Store");
        if (barracks.Length != 8 || forge == null || armory == null || store == null || sleepingHouses.Length != 2)
            throw new InvalidOperationException("Unexpected camp building count.");
        Directory.CreateDirectory(".utmp/CampFacilities");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/CampFacilities/before.unity", true))
            throw new IOException("Scene backup failed.");

        foreach (var house in barracks) ResizeKeepingFloor(house, new Vector3(1.05f, 1.17f, 1.05f));
        // This rear house's outer post otherwise clips the west cross-path.
        buildings.Find("Wooden_Barracks_-1_185").position += Vector3.right * 15f;
        ResizeKeepingFloor(forge, new Vector3(1f, 1.7f, 1f));
        ResizeKeepingFloor(armory, new Vector3(1f, 2f, 1f));
        ResizeKeepingFloor(store, new Vector3(1f, 1.4f, 1f));

        var facilities = new GameObject("Useful_Buildings").transform;
        facilities.SetParent(camp, false);
        foreach (var house in barracks)
            AddFacility(facilities, house, stage.position, terrain, CampFacility.FacilityKind.Barracks, "quân xá");
        foreach (var house in sleepingHouses)
            AddFacility(facilities, house, stage.position, terrain, CampFacility.FacilityKind.Barracks, "nhà nghỉ quân sĩ");
        AddFacility(facilities, forge, stage.position, terrain, CampFacility.FacilityKind.Forge, "lò rèn");
        AddFacility(facilities, armory, stage.position, terrain, CampFacility.FacilityKind.Armory, "kho khí giới");
        AddFacility(facilities, store, stage.position, terrain, CampFacility.FacilityKind.Provisions, "kho lương");

        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        Debug.Log("Installed 13 camp facility entrances: 10 barracks, forge, armory and provisions store.");
        EditorApplication.Exit(0);
    }

    public static void ClearRearBarracksPathAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var house = GameObject.Find("Wooden_Barracks_-1_185")?.transform;
        var marker = GameObject.Find("Use_Wooden_Barracks_-1_185")?.transform;
        if (house == null || marker == null) throw new InvalidOperationException("Rear barracks or doorway missing.");
        house.position += Vector3.right * 27f;
        marker.position += Vector3.right * 27f;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        EditorApplication.Exit(0);
    }
}
