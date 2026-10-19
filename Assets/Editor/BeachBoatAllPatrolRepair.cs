using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using BeachBoatStoryEditor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Replaces every old patrol hull and broken white crew in beachBoat.</summary>
public static class BeachBoatAllPatrolRepair
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";
    // This is the clean red-sail hull selected by the user. Patrol copies are normalized
    // to 58 m, clearly smaller than the 92 m Nam Han warships spawned by Quest 3.
    const string BoatVisual = "Assets/junk/source/Sketchfab_2013_12_04_23_35_26.fbx";
    const string CrewModel = "Assets/Model_NPC/OldSoldier/Meshy_AI_Dũng_the_Veteran_biped/Meshy_AI_Dũng_the_Veteran_biped_Character_output.fbx";
    const string GeneratedName = "DaiVietPatrolVisual_Rebuilt";

    [MenuItem("BeachBoat/Repair All Patrol Boats And Crew")]
    public static void Apply()
    {
        var scene = BeachBoatToolSafety.Begin("AllPatrolBoatsRepair");
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoatVisual);
        var crewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrewModel);
        // Thai's rowing clip deforms this veteran avatar at runtime (the white/exploded
        // figure reported beside the patrol boats). Use the compatible seated pose here;
        // the player quest boat keeps the dedicated rowing animation.
        var rowing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/NPC_Sitting.controller");
        var guard = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/BachDangAtmosphere/BachDang_GuardIdle.controller");
        if (visualPrefab == null || crewPrefab == null || rowing == null || guard == null)
            throw new InvalidOperationException("Detailed Dai Viet boat, veteran crew, or animation controller is missing.");

        var patrols = Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x != null).Distinct().ToArray();
        if (patrols.Length < 4) throw new InvalidOperationException("Expected at least four authored patrol boats.");

        int crewCount = 0, rendererCount = 0;
        foreach (var patrol in patrols)
        {
            var boat = patrol.gameObject;
            while (boat.transform.childCount > 0)
                Undo.DestroyObjectImmediate(boat.transform.GetChild(0).gameObject);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, boat.transform);
            visual.name = GeneratedName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0, 90, 0);
            visual.transform.localScale = Vector3.one;
            StripPhysicsAndScripts(visual);
            ScaleToHorizontalLength(visual, 58f);
            var bounds = BoundsOf(visual);
            // Patrol roots are scale-14 legacy objects. Align in world space so the hull
            // follows the actual waterline instead of being multiplied by that parent scale.
            visual.transform.position += Vector3.up * ((boat.transform.position.y - 3.2f) - bounds.min.y);
            ValidateAndOptimizeRenderers(visual, boat.name);

            bounds = BoundsOf(visual);
            // Sail and mast occupy most of the model's height. The usable deck is close
            // to the hull bottom; deriving it from bounds.max put crew above the mast.
            float deckWorldY = bounds.min.y + Mathf.Clamp(bounds.size.y * .19f, 7.5f, 10f);
            Vector3 rowerWorld = bounds.center - boat.transform.right * 5.5f + boat.transform.forward * 2.5f;
            Vector3 lookoutWorld = bounds.center + boat.transform.right * 5.5f - boat.transform.forward * 3.5f;
            rowerWorld.y = lookoutWorld.y = deckWorldY;
            var rower = Crew(crewPrefab, boat.transform, "ThuyBinh_Cheo_Thuyen", rowerWorld, 180, rowing);
            var lookout = Crew(crewPrefab, boat.transform, "ThuyBinh_Quan_Sat", lookoutWorld, 165, guard);
            Undo.AddComponent<BoatCrewLookout>(lookout);

            patrol.rowers = new[] { rower.GetComponentInChildren<Animator>(true) };
            patrol.visualForwardYaw = 0f;
            patrol.cruiseSpeed = Mathf.Clamp(patrol.cruiseSpeed, 9f, 13f);
            patrol.bobAmplitude = .12f;
            EditorUtility.SetDirty(patrol);
            crewCount += 2;
            rendererCount += boat.GetComponentsInChildren<Renderer>(true).Length;
        }

        BeachBoatToolSafety.Save(scene);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/PatrolBoatRepair.json", JsonConvert.SerializeObject(new {
            success = true, patrolBoats = patrols.Length, crewCount, rendererCount,
            visual = BoatVisual, note = "All patrol boats use one shared detailed hull; broken white crew removed."
        }, Formatting.Indented));
        Debug.Log($"Rebuilt {patrols.Length} patrol boats and {crewCount} crew with the detailed Dai Viet model.");
    }

    public static void ApplyAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply(); EditorApplication.Exit(0);
    }

    static GameObject Crew(GameObject prefab, Transform parent, string name, Vector3 worldPosition, float yaw, RuntimeAnimatorController controller)
    {
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        actor.name = name; actor.transform.position = worldPosition;
        actor.transform.rotation = parent.rotation * Quaternion.Euler(0, yaw, 0); actor.transform.localScale = Vector3.one;
        foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
        var animator = actor.GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman) throw new InvalidOperationException("Veteran patrol crew is not Humanoid.");
        ScaleToHeight(actor, 12.5f);
        // Scaling changes renderer bounds around the import pivot; restore feet/deck position.
        var actorBounds = BoundsOf(actor);
        actor.transform.position += Vector3.up * (worldPosition.y - actorBounds.min.y);
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        ValidateAndOptimizeRenderers(actor, actor.name);
        return actor;
    }

    static void StripPhysicsAndScripts(GameObject root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
    }

    static void ValidateAndOptimizeRenderers(GameObject root, string owner)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            // Only 25 renderers are used by the complete patrol fleet. Keeping normal
            // lighting here prevents hulls and soldiers becoming flat white at dawn.
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.shader == null || !material.shader.isSupported)
                    throw new InvalidOperationException(owner + " contains a missing or unsupported material.");
        }
    }

    static void ScaleToHorizontalLength(GameObject root, float target)
    {
        var b = BoundsOf(root); float length = Mathf.Max(b.size.x, b.size.z);
        root.transform.localScale *= target / Mathf.Max(.01f, length);
    }

    static void ScaleToHeight(GameObject root, float target)
    {
        var b = BoundsOf(root);
        root.transform.localScale *= target / Mathf.Max(.01f, b.size.y);
    }

    static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Renderer missing on " + root.name);
        var result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
        return result;
    }
}
