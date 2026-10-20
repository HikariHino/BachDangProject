using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// Cấu hình FBX import để extract animation clip chết (Death) từ idle_combat.fbx
/// và hỗ trợ 1-Click tích hợp vào Animator Controller (Sword_Anima) + Tạo Audio Chết mẫu.
/// Menu: Tools -> BachDang
/// </summary>
public static class SetupEnemyDeathAnimation
{
    private const string TargetFbxName = "idle_combat";
    private const string AnimatorControllerName = "Sword_Anima";

    [MenuItem("Tools/BachDang/1. Config FBX Import (idle_combat)")]
    public static void SetupDeathAnimation()
    {
        // 1. Tìm file FBX idle_combat
        string fbxPath = null;
        string[] guids = AssetDatabase.FindAssets($"{TargetFbxName} t:Model");
        if (guids.Length > 0)
        {
            fbxPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        }
        else
        {
            string directPath = "Assets/Animation/idle_combat.fbx";
            if (File.Exists(directPath))
            {
                fbxPath = directPath;
            }
        }

        if (string.IsNullOrEmpty(fbxPath))
        {
            EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy file FBX '{TargetFbxName}.fbx' trong project!", "OK");
            return;
        }

        var modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (modelImporter == null)
        {
            EditorUtility.DisplayDialog("Lỗi", $"Không thể load ModelImporter cho file: {fbxPath}", "OK");
            return;
        }

        Undo.RecordObject(modelImporter, "Setup Death Animation Import");

        // Bật import animation & Humanoid
        modelImporter.importAnimation = true;
        modelImporter.animationType = ModelImporterAnimationType.Human;

        // Xác định takeName thực tế (Scene hoặc mixamo.com)
        string take = "Scene";
        var defaultClips = modelImporter.defaultClipAnimations;
        if (defaultClips != null && defaultClips.Length > 0 && !string.IsNullOrEmpty(defaultClips[0].takeName))
        {
            take = defaultClips[0].takeName;
        }

        // Tạo clip animation "Death" (loopTime = false)
        var clips = new ModelImporterClipAnimation[]
        {
            new ModelImporterClipAnimation
            {
                name = "Death",
                takeName = take,
                firstFrame = 0,
                lastFrame = -1, // Lấy hết toàn bộ độ dài clip
                loopTime = false,
                loopPose = false,
                cycleOffset = 0,
                keepOriginalOrientation = true,
                keepOriginalPositionY = true,
                keepOriginalPositionXZ = true,
                heightFromFeet = false,
                mirror = false
            }
        };

        modelImporter.clipAnimations = clips;

        // Lưu và re-import asset
        AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

        // Đồng thời khôi phục lại file Idle cũ nếu trước đó bị đổi tên thành Death
        RestoreOldIdleClip();

        Debug.Log($"<color=green><b>[BachDang]</b></color> Đã cấu hình thành công FBX: {fbxPath}");
        Debug.Log($"   Clip name: 'Death' | Take source: '{take}' | Loop: false");

        EditorUtility.DisplayDialog("Thành Công (Bước 1)",
            $"Đã config FBX: {fbxPath}\n\n" +
            $"Clip 'Death' đã được tạo thành công từ 'idle_combat' (take '{take}', Loop Time = false).\n\n" +
            $"Bây giờ bạn có thể chọn:\n" +
            $"Tools/BachDang/2. 1-Click Setup Die State in Animator (Sword_Anima) để tự động nối State vào Animator!", "OK");
    }

    private static void RestoreOldIdleClip()
    {
        string[] idleGuids = AssetDatabase.FindAssets("Meshy_AI_Character_output@Idle t:Model");
        if (idleGuids.Length > 0)
        {
            string idlePath = AssetDatabase.GUIDToAssetPath(idleGuids[0]);
            var importer = AssetImporter.GetAtPath(idlePath) as ModelImporter;
            if (importer != null && importer.clipAnimations != null && importer.clipAnimations.Length > 0)
            {
                if (importer.clipAnimations[0].name == "Death")
                {
                    importer.clipAnimations[0].name = "Idle";
                    importer.clipAnimations[0].loopTime = true;
                    AssetDatabase.ImportAsset(idlePath, ImportAssetOptions.ForceUpdate);
                    Debug.Log("[BachDang] Đã khôi phục clip Idle cho Meshy_AI_Character_output@Idle.fbx (loopTime = true).");
                }
            }
        }
    }

    [MenuItem("Tools/BachDang/2. 1-Click Setup Die State in Animator (Sword_Anima)")]
    public static void SetupAnimatorController()
    {
        // 1. Tìm Sword_Anima controller
        string[] guids = AssetDatabase.FindAssets($"{AnimatorControllerName} t:AnimatorController");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy Animator Controller '{AnimatorControllerName}'!", "OK");
            return;
        }

        string controllerPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không thể load Animator Controller!", "OK");
            return;
        }

        Undo.RecordObject(controller, "Setup Die State in Animator");

        // 2. Thêm parameter "Die" kiểu Trigger nếu chưa có
        bool hasDieTrigger = false;
        foreach (var p in controller.parameters)
        {
            if (p.name == "Die" && p.type == AnimatorControllerParameterType.Trigger)
            {
                hasDieTrigger = true;
                break;
            }
        }

        if (!hasDieTrigger)
        {
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            Debug.Log("<color=green><b>[BachDang]</b></color> Đã thêm Parameter 'Die' (Trigger) vào Animator Controller.");
        }

        // 3. Tìm animation clip "Death" từ idle_combat.fbx
        AnimationClip deathClip = null;
        string[] fbxGuids = AssetDatabase.FindAssets($"{TargetFbxName} t:Model");
        if (fbxGuids.Length > 0)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var obj in subAssets)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    deathClip = clip;
                    break;
                }
            }
        }

        if (deathClip == null)
        {
            string[] clipGuids = AssetDatabase.FindAssets("Death t:AnimationClip");
            foreach (string guid in clipGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains(TargetFbxName))
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip != null)
                    {
                        deathClip = clip;
                        break;
                    }
                }
            }
        }

        // 4. Lấy StateMachine Layer 0
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // Tìm hoặc tạo State "Die"
        AnimatorState dieState = null;
        foreach (ChildAnimatorState childState in rootStateMachine.states)
        {
            if (childState.state.name == "Die" || childState.state.name == "Death")
            {
                dieState = childState.state;
                break;
            }
        }

        if (dieState == null)
        {
            dieState = rootStateMachine.AddState("Die", new Vector3(800, 200, 0));
            Debug.Log("<color=green><b>[BachDang]</b></color> Đã tạo state 'Die' trong Animator.");
        }

        if (deathClip != null)
        {
            dieState.motion = deathClip;
            Debug.Log($"<color=green><b>[BachDang]</b></color> Đã gán Animation Clip '{deathClip.name}' (từ idle_combat) vào state 'Die'.");
        }
        else
        {
            Debug.LogWarning("[BachDang] Chưa tìm thấy clip 'Death' trong idle_combat. Hãy chạy Bước 1 trước.");
        }

        // 5. Tạo Any State -> Die Transition
        bool hasTransition = false;
        foreach (var trans in rootStateMachine.anyStateTransitions)
        {
            if (trans.destinationState == dieState)
            {
                hasTransition = true;
                break;
            }
        }

        if (!hasTransition)
        {
            var transition = rootStateMachine.AddAnyStateTransition(dieState);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0, "Die");
            Debug.Log("<color=green><b>[BachDang]</b></color> Đã tạo Transition: Any State -> Die với điều kiện Trigger 'Die'.");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Thành Công (Bước 2)",
            $"Đã cấu hình Animator Controller '{controller.name}':\n\n" +
            $"1. Parameter: 'Die' (Trigger)\n" +
            $"2. State: 'Die' {(deathClip != null ? $"(đã gán clip '{deathClip.name}' từ idle_combat)" : "(chưa có clip)")}\n" +
            $"3. Any State -> Die (Condition: Die, Has Exit Time = false, Duration = 0.1s)\n\n" +
            $"Tất cả đã hoàn tất!", "OK");
    }

    [MenuItem("Tools/BachDang/3. Tạo Âm Thanh Chết Mẫu (Procedural Death Sound)")]
    public static void GenerateDeathAudio()
    {
        string dir = "Assets/Audio";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string filePath = Path.Combine(dir, "Death_Sound.wav");
        CreateHeavyThudWav(filePath);

        AssetDatabase.Refresh();

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(filePath);
        if (clip != null)
        {
            Selection.activeObject = clip;
        }

        EditorUtility.DisplayDialog("Thành Công (Bước 3)",
            $"Đã tạo file âm thanh tiếng gục ngã / rơi xác tại:\n{filePath}\n\n" +
            $"Bây giờ bạn chỉ cần gán file này vào trường 'Death Sound' trong component HitEffect của Enemy!", "OK");
    }

    /// <summary>
    /// Tạo file wav âm thanh trầm tiếng ngã/va đập (Heavy Thud Impact) để test trực tiếp
    /// </summary>
    private static void CreateHeavyThudWav(string filename)
    {
        int sampleRate = 44100;
        float duration = 0.9f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);

        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-5.0f * t);

            float freq = Mathf.Lerp(120f, 45f, t / duration);
            float sine = Mathf.Sin(2f * Mathf.PI * freq * t);

            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-12.0f * t) * 0.4f;

            samples[i] = Mathf.Clamp((sine * 0.8f + noise) * envelope, -1f, 1f);
        }

        byte[] wavBytes = EncodeWav(samples, sampleRate, 1);
        File.WriteAllBytes(filename, wavBytes);
    }

    private static byte[] EncodeWav(float[] samples, int sampleRate, int channels)
    {
        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            int subChunk2Size = samples.Length * 2;
            int chunkSize = 36 + subChunk2Size;

            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(chunkSize);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(subChunk2Size);

            for (int i = 0; i < samples.Length; i++)
            {
                short sampleInt = (short)(samples[i] * 32767f);
                writer.Write(sampleInt);
            }

            return memoryStream.ToArray();
        }
    }
}