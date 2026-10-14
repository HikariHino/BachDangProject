using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Story = BeachBoatStory;

/// <summary>Completes the nine Thai soldiers already imported by 04b839b. No source scene is needed.</summary>
public static class BeachBoatCampActors
{
    const string Folder = "Assets/BeachBoatStory/Camp";
    static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

    static AnimatorController Controller()
    {
        string path = Folder + "/ThaiCampPoses.controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing != null) return existing;
        var sitting = UnityEngine.Object.Instantiate(Clip("Assets/Animation/SittingIdle.fbx"));
        sitting.name = "ThaiCampSittingLoop";
        var settings = AnimationUtility.GetAnimationClipSettings(sitting);
        settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(sitting, settings);
        AssetDatabase.CreateAsset(sitting, Folder + "/ThaiCampSittingLoop.anim");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Pose", AnimatorControllerParameterType.Int);
        foreach (string parameter in new[] { "isWalking", "isRunning", "isSitting", "isLaying" })
            controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle"); idle.motion = Clip("Assets/Animation/Meshy_AI_Character_output@Idle.fbx");
        machine.defaultState = idle;
        machine.AddState("Sitting").motion = sitting;
        machine.AddState("Laying").motion = Clip("Assets/Animation/LayingDown.fbx");
        return controller;
    }

    [MenuItem("Bach Dang/Complete Thai Camp Actors")]
    public static void Complete()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Open beachBoat in Edit Mode.");
        var root = GameObject.Find("Camp_Soldiers");
        if (root == null || root.GetComponentsInChildren<Animator>(true).Length != 9)
            throw new InvalidOperationException("Expected the nine soldiers imported by 04b839b.");
        var controller = Controller();
        string[] names = { "Camp_Guard_Back_1", "Camp_Guard_Back_2", "Camp_Guard_Back_3",
            "Camp_Guard_Front_Left", "Camp_Guard_Front_Right", "Camp_Guard_Mid_Right",
            "Camp_Lookout_Soldier", "Camp_Sitting_Soldier", "Camp_Laying_Soldier" };
        // Four guards on the stage, two in its forecourt, a lookout and two resting soldiers.
        var positions = new[] { new Vector2(-5520,1115), new Vector2(-5495,1115), new Vector2(-5495,1130),
            new Vector2(-5518,1135), new Vector2(-5470,1185), new Vector2(-5570,1190),
            new Vector2(-6382,485), new Vector2(-5570,1245), new Vector2(-5600,1245) };
        var stage = GameObject.Find("TestQuest_Camp").transform.Cast<Transform>()
            .Single(t => t.name.Contains("Rustic_Wooden_Stage"));
        var stageColliders = stage.GetComponentsInChildren<Collider>();
        Physics.SyncTransforms();
        var floors = positions.Select((p, i) =>
        {
            if (i >= 4) return Terrain.activeTerrain.SampleHeight(new Vector3(p.x, 0, p.y)) + Terrain.activeTerrain.transform.position.y;
            foreach (var surface in stageColliders)
                if (surface.Raycast(new Ray(new Vector3(p.x, 155, p.y), Vector3.down), out var hit, 25) &&
                    hit.point.y > 136 && hit.point.y < 142 && hit.normal.y > .5f)
                    return hit.point.y;
            throw new InvalidOperationException("No stage floor beneath " + names[i]);
        }).ToArray();
        Undo.RegisterFullObjectHierarchyUndo(root, "Complete Thai soldiers");
        for (int i = 0; i < names.Length; i++)
        {
            var actor = root.transform.Find(names[i]).gameObject;
            var animator = actor.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var pose = actor.GetComponent<NPCCampPose>();
            string state = i == 7 ? "Sitting" : i == 8 ? "Laying" : "Idle";
            if (pose != null) pose.previewInEditMode = false;
            actor.transform.SetPositionAndRotation(new Vector3(positions[i].x, 0, positions[i].y),
                Quaternion.Euler(0, i < 4 ? 0 : i == 6 ? 180 : 90, 0));
            animator.Rebind(); animator.Play(state, 0, i == 8 ? 1 : 0); animator.Update(.05f);
            var renderers = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            Bounds bounds = default; bool first = true;
            foreach (var renderer in renderers)
            {
                // FBX mesh child carries a 100x import scale. Bake with scale once;
                // applying TransformPoint afterwards would multiply that scale again.
                var mesh = new Mesh(); renderer.BakeMesh(mesh, true);
                var toWorld = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices)
                {
                    var world = toWorld.MultiplyPoint3x4(v);
                    if (first) { bounds = new Bounds(world, Vector3.zero); first = false; } else bounds.Encapsulate(world);
                }
                UnityEngine.Object.DestroyImmediate(mesh);
                renderer.updateWhenOffscreen = false; renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            float floor = floors[i];
            actor.transform.position += Vector3.up * (floor + .1f - bounds.min.y);
            foreach (var old in actor.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(old);
            var collider = actor.AddComponent<BoxCollider>();
            collider.size = i == 8 ? new Vector3(.75f,.6f,1.8f) : i == 7 ? new Vector3(.8f,1f,.9f) : new Vector3(.7f,1.85f,.65f);
            collider.center = Vector3.up * collider.size.y * .5f;
            var lod = actor.GetComponent<LODGroup>();
            if (lod == null) lod = actor.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.035f, renderers.Cast<Renderer>().ToArray()) });
            lod.RecalculateBounds(); lod.size = 2.2f; lod.localReferencePoint = Vector3.up;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var dialogue = actor.GetComponent<Story.NPCDialogue>();
            if (dialogue == null) dialogue = actor.AddComponent<Story.NPCDialogue>();
            dialogue.speakerName = i == 7 ? "Người lính ngồi nghỉ" : i == 8 ? "Người lính nằm nghỉ" : "Lính gác doanh trại";
            dialogue.interactDistance = 20;
            dialogue.dialogueText = i == 7
                ? "Quân Nam Hán đang dòm ngó bờ cõi. Chúng tôi sẽ giữ vững doanh trại để Chủ tướng yên tâm lo kế sách."
                : i == 8
                ? "Dân làng ven sông đã sẵn lòng giúp sức. Mong quân ta sớm chuẩn bị đủ cọc để chặn thuyền giặc."
                : "Doanh trại đã sẵn sàng. Quân sĩ đang chờ lệnh của Chủ tướng.";
        }
        Physics.SyncTransforms(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
}
