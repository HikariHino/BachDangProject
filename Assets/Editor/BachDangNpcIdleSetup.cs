using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Gives unconfigured camp and fleet NPCs a stationary humanoid idle.</summary>
public static class BachDangNpcIdleSetup
{
    private const string ScenePath = "Assets/Scenes/beachBoat.unity";
    private const string ClipPath = "Assets/Animation/Meshy_AI_Character_output@Idle.fbx";
    private const string ControllerFolder = "Assets/BachDangAtmosphere";
    private const string ControllerPath = ControllerFolder + "/BachDang_GuardIdle.controller";
    private const string CampRoot = "--- CHIẾN TRƯỜNG: ĐẠI BẢN DOANH & XƯỞNG CỌC BẠCH ĐẰNG ---";
    private const string FleetRoot = "--- HẠM ĐỘI THUYỀN CHIẾN ĐẠI VIỆT (938) ---";

    [MenuItem("Bach Dang/Set Up Camp And Fleet Idle")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || !scene.isLoaded || scene.path != ScenePath)
            throw new InvalidOperationException("Open beachBoat in Edit Mode before assigning NPC idle animations.");

        var candidates = new List<Animator>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != CampRoot && root.name != FleetRoot) continue;
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController != null) continue;
                Avatar avatar = animator.avatar;
                if (avatar == null || !avatar.isValid || !avatar.isHuman) continue;
                candidates.Add(animator);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.Log("NPC idle setup: no unconfigured humanoid Animators found in the camp or allied fleet.");
            return;
        }

        AnimatorController controller = GetOrCreateController();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Assign stationary NPC idle");

        foreach (Animator animator in candidates)
        {
            Undo.RecordObject(animator, "Assign NPC idle animation");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (PrefabUtility.IsPartOfPrefabInstance(animator))
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            EditorUtility.SetDirty(animator);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"NPC idle setup: assigned {candidates.Count} stationary humanoid Animators. " +
                  "Scene is ready to inspect and save; weapon attachments were left unchanged.");
    }

    private static AnimatorController GetOrCreateController()
    {
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(ControllerPath);
        if (existing != null)
        {
            var controller = existing as AnimatorController;
            if (controller == null)
                throw new InvalidOperationException("The guard idle controller path contains a different asset type.");
            return controller;
        }

        var importer = AssetImporter.GetAtPath(ClipPath) as ModelImporter;
        if (importer == null || importer.animationType != ModelImporterAnimationType.Human)
            throw new InvalidOperationException("The existing Idle FBX must be imported as Humanoid.");

        AnimationClip idle = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ClipPath))
        {
            if (asset is AnimationClip clip && clip.name == "Idle" && !clip.legacy)
            {
                idle = clip;
                break;
            }
        }
        if (idle == null)
            throw new InvalidOperationException("Could not find the existing non-legacy Idle animation clip.");

        if (!AssetDatabase.IsValidFolder(ControllerFolder))
            AssetDatabase.CreateFolder("Assets", "BachDangAtmosphere");

        AnimatorController created = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        if (created == null)
            throw new InvalidOperationException("Could not create the guard idle controller.");

        AnimatorStateMachine machine = created.layers[0].stateMachine;
        AnimatorState state = machine.AddState("Idle");
        state.motion = idle;
        state.speed = 1f;
        machine.defaultState = state;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(created);
        return created;
    }
}
