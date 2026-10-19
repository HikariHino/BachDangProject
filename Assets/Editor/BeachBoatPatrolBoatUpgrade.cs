using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using BeachBoatStoryEditor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class BeachBoatPatrolBoatUpgrade
{
    const string BoatName = "Thuyen_Tuan_Tieu_Ben_Doanh_Trai";
    const string BoatPrefab = "Assets/Prefabs/Buildings/pf_build_boat_02.prefab";
    const string CrewModel = "Assets/Model_NPC/OldSoldier/Meshy_AI_Dũng_the_Veteran_biped/Meshy_AI_Dũng_the_Veteran_biped_Character_output.fbx";

    [MenuItem("BeachBoat/Legacy/Upgrade One Old Patrol Boat")]
    public static void Apply()
    {
        var scene = BeachBoatToolSafety.Begin("PatrolBoatUpgrade");
        var boat = GameObject.Find(BoatName);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoatPrefab);
        var crewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrewModel);
        var tide = Object.FindAnyObjectByType<TideSystem>();
        if (boat == null || prefab == null || crewPrefab == null || tide == null)
            throw new InvalidOperationException("Patrol boat, detailed boat prefab, veteran crew or tide is missing.");

        foreach (var component in boat.GetComponents<AmbientRiverBoat>()) Undo.DestroyObjectImmediate(component);
        Bounds oldBounds = BoundsOf(boat);
        while (boat.transform.childCount > 0) Undo.DestroyObjectImmediate(boat.transform.GetChild(0).gameObject);

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, boat.transform);
        visual.name = "ThuyenTuanTieu_DaiViet_ChiTiet";
        visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * .72f;
        var newBounds = BoundsOf(visual); visual.transform.position += Vector3.up * (oldBounds.min.y - newBounds.min.y);
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        { renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; }

        var rowing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/NPC_Rowing_Fix.controller");
        var guard = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/BachDangAtmosphere/BachDang_GuardIdle.controller");
        var rower = Crew(crewPrefab, boat.transform, "ThuyBinh_CuuBinh_Cheo", new Vector3(-.72f, 2.05f, .95f), 12, rowing);
        var lookout = Crew(crewPrefab, boat.transform, "ThuyBinh_CuuBinh_QuanSat", new Vector3(.68f, 2.05f, -1.35f), -18, guard);
        Undo.AddComponent<BoatCrewLookout>(lookout);
        Paddle(boat.transform, "Mai_Cheo_Mạn_Trai", new Vector3(-2.05f, 2.2f, .75f), new Vector3(68, 0, -12));
        Paddle(boat.transform, "Mai_Cheo_Mạn_Phai", new Vector3(2.05f, 2.2f, .75f), new Vector3(-68, 0, 12));

        var routeParent = GameObject.Find("BeachBoat_RiverLife_Optimized")?.transform;
        if (routeParent == null) { routeParent = new GameObject("BeachBoat_RiverLife_Optimized").transform; Undo.RegisterCreatedObjectUndo(routeParent.gameObject, "Create river life root"); }
        var oldRoute = routeParent.Find("Tuyen_Thuyen_Tuan_Tieu_Moi"); if (oldRoute != null) Undo.DestroyObjectImmediate(oldRoute.gameObject);
        var routeRoot = new GameObject("Tuyen_Thuyen_Tuan_Tieu_Moi").transform; routeRoot.SetParent(routeParent, false);
        Vector3 p = boat.transform.position; Vector3 f = boat.transform.forward; f.y = 0; f.Normalize(); Vector3 r = Vector3.Cross(Vector3.up, f);
        Vector3[] offsets = { f * 18, f * 92 + r * 35, f * 165, f * 82 - r * 42 };
        var route = new Transform[offsets.Length];
        for (int i = 0; i < route.Length; i++)
        {
            var point = new GameObject("Diem_Tuan_Tra_" + (i + 1)).transform; point.SetParent(routeRoot, false);
            point.position = p + offsets[i]; point.position = new Vector3(point.position.x, tide.lowTideY + (p.y - tide.transform.position.y), point.position.z); route[i] = point;
        }
        var floating = boat.GetComponent<TidalBoatFloat>(); if (floating != null) { Undo.RecordObject(floating, "Use patrol movement"); floating.enabled = false; }
        var patrol = Undo.AddComponent<AmbientRiverBoat>(boat); patrol.tide = tide; patrol.route = route;
        patrol.rowers = new[] { rower.GetComponentInChildren<Animator>(true) }; patrol.cruiseSpeed = 10; patrol.turnSpeed = 22; patrol.pauseAtPoint = 2;
        patrol.visualForwardYaw = HullAlongX(boat.transform) ? 90 : 0;
        EditorUtility.SetDirty(patrol);

        BeachBoatToolSafety.Save(scene);
        BeachBoatBattleSetup.SetupBattle();
        File.WriteAllText("Logs/PatrolBoatUpgrade.json", JsonConvert.SerializeObject(new {
            success = true, boat = BoatName, visual = BoatPrefab, crew = CrewModel,
            crewCount = boat.GetComponentsInChildren<Animator>(true).Length,
            renderers = boat.GetComponentsInChildren<Renderer>(true).Length,
            bounds = BoundsOf(boat).size.ToString("F1"), routePoints = route.Length,
            note = "One detailed shared-mesh patrol boat, two humanoid veteran crew and two 80-vertex paddles."
        }, Formatting.Indented));
    }

    static GameObject Crew(GameObject prefab, Transform parent, string name, Vector3 localPosition, float yaw, RuntimeAnimatorController controller)
    {
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); actor.name = name;
        actor.transform.localPosition = localPosition; actor.transform.localRotation = Quaternion.Euler(0, yaw, 0); actor.transform.localScale = Vector3.one;
        foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        var animator = actor.GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman) throw new InvalidOperationException("Veteran crew is not Humanoid.");
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        return actor;
    }

    static void Paddle(Transform parent, string name, Vector3 position, Vector3 rotation)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BeachBoatStory/Rowboat/RiverPaddle.asset");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachBoatStory/Rowboat/Song_Wood.mat");
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(rotation); go.transform.localScale = Vector3.one * .12f;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true); if (renderers.Length == 0) throw new InvalidOperationException("Renderer missing on " + root.name);
        var result = renderers[0].bounds; for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds); return result;
    }
    static bool HullAlongX(Transform root)
    {
        var visual = root.Find("ThuyenTuanTieu_DaiViet_ChiTiet"); if (visual == null) return false;
        var bounds = BoundsOf(visual.gameObject); Vector3 a = root.InverseTransformVector(Vector3.right * bounds.size.x);
        Vector3 b = root.InverseTransformVector(Vector3.forward * bounds.size.z);
        return Mathf.Abs(a.x) + Mathf.Abs(b.x) > Mathf.Abs(a.z) + Mathf.Abs(b.z);
    }
}
