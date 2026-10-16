using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Story = BeachBoatStory;

public static class BeachBoatRowingQuestInstaller
{
    [MenuItem("Bach Dang/Install River Rowing Quest")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity" || scene.isDirty)
            throw new InvalidOperationException("Open and save beachBoat in Edit Mode first.");
        if (UnityEngine.Object.FindFirstObjectByType<Story.QuestRowboat>() != null)
            throw new InvalidOperationException("Rowing quest already installed; edit existing bindings.");
        var session = UnityEngine.Object.FindFirstObjectByType<Story.BeachBoatQuestSession>();
        var zone = session.GetComponentInChildren<Story.RiverStakeZone>(true);
        string[] names = {"CocGo_Hang1_4", "CocGo_Hang2_4", "CocGo_Hang3_4", "CocGo_Hang2_5", "CocGo_Hang1_5"};
        var stakes = names.Select(n => GameObject.Find(n)?.transform).ToArray();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/pf_build_boat_01_low.prefab");
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/NPC_Rowing_Fix.controller");
        if (zone.placements.Length != 5 || stakes.Any(s => s == null) || source == null || controller == null)
            throw new InvalidOperationException("Missing existing quest/field/Thai rowing assets.");
        System.IO.Directory.CreateDirectory(".utmp/RowingQuest");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/RowingQuest/beachBoat-before-rowing.unity", true))
            throw new InvalidOperationException("Scene backup failed.");
        var terrain = Terrain.activeTerrains.Single(t => t.gameObject.scene == scene);
        var tide = UnityEngine.Object.FindFirstObjectByType<TideSystem>();
        float water = tide.transform.position.y;
        var root = new GameObject("Thuyen_NhiemVu_DongCoc");
        Undo.RegisterCreatedObjectUndo(root, "Install river rowing quest");
        root.transform.SetParent(session.transform, false);
        root.transform.SetPositionAndRotation(new Vector3(-3570, water, 1880), Quaternion.Euler(0, 90, 0));
        var boat = root.AddComponent<Story.QuestRowboat>();
        boat.terrain = terrain; boat.rowingController = controller;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
        model.name = "Thuyen_Nhe_12k_Vertices";
        model.transform.localPosition = new Vector3(0, -12, 0);
        model.transform.localRotation = Quaternion.Euler(0, 90, 0); model.transform.localScale = Vector3.one * 9;
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        var hull = root.AddComponent<BoxCollider>(); hull.center = new Vector3(0, 1, 0); hull.size = new Vector3(28, 14, 80);
        var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        boat.seat = Child("Cho_Ngoi_Cheo", root.transform, new Vector3(0, 0, -12));
        boat.dockPoint = Child("Ben_Thuyen_MatNuoc", session.transform, Vector3.zero);
        boat.dockPoint.position = root.transform.position;
        boat.boardingPoint = Child("Len_Xuong_Thuyen_E", session.transform, Vector3.zero);
        boat.boardingPoint.SetPositionAndRotation(new Vector3(-3620, 114.55f, 1880), Quaternion.Euler(0, 90, 0));
        var material = model.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        Box("Ghe_Ngoi_Cheo", root.transform, new Vector3(0, 5.8f, -12), new Vector3(24, 1.2f, 5), material, false);
        var pier = Child("Cau_Go_Ra_Ben", session.transform, new Vector3(-3700, 111.25f, 1880));
        Box("Mat_Cau", pier, Vector3.zero, new Vector3(176, 2, 26), material, true);
        for (int n = 0; n < 4; n++)
            for (int side = -1; side <= 1; side += 2)
                Box("Tru_Cau", pier, new Vector3(-60 + n * 40, -28, side * 11), new Vector3(2, 56, 2), material, false);
        var paddle = Child("Mai_Cheo", root.transform, new Vector3(10, 8, -12));
        Box("Can", paddle, Vector3.zero, new Vector3(.65f, 29, .65f), material, false);
        Box("La", paddle, new Vector3(0, -17, 0), new Vector3(4, 8, .6f), material, false);
        boat.paddle = paddle;
        for (int i = 0; i < 5; i++)
        {
            var p = zone.placements[i];
            Undo.RecordObject(p.approach, "Move work point into river field");
            // Stand the boat alongside the real stake, leaving clearance for the hull.
            var center = stakes[i].GetComponentInChildren<Renderer>().bounds.center;
            p.approach.position = new Vector3(center.x - 55, water + 1, center.z);
            if (p.stake != null && p.stake.name.StartsWith("Coc_DaDong_")) Undo.DestroyObjectImmediate(p.stake.gameObject);
            p.stake = stakes[i];
            foreach (var t in p.stake.GetComponentsInChildren<Transform>(true))
            { Undo.RecordObject(t.gameObject, "Allow quest stake animation"); GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0); }
            p.marker.transform.localPosition = Vector3.zero;
            var ring = p.marker.GetComponent<LineRenderer>();
            for (int n = 0; n < ring.positionCount; n++)
            { float a = n * Mathf.PI * 2 / ring.positionCount; ring.SetPosition(n, new Vector3(Mathf.Cos(a) * 24, 0, Mathf.Sin(a) * 24)); }
        }
        zone.interactDistance = 24; zone.plantingDuration = 1.8f;
        EditorUtility.SetDirty(zone);
        // Original Thai asset is now properly imported as Humanoid, so the provisional copy is unnecessary.
        AssetDatabase.DeleteAsset("Assets/BeachBoatStory/Rowing");
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save rowing quest.");
        Debug.Log("Rowing quest installed: one lightweight boat, Thai animation, five existing river stakes, one simple pier.");
    }

    static Transform Child(string name, Transform parent, Vector3 position)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform; }
    static void Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collision)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
}
