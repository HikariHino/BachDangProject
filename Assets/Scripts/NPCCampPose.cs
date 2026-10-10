using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quản lý tư thế của NPC trong doanh trại (Đứng gác, Ngồi bẹp, Nằm nghỉ).
/// Hỗ trợ XEM TRƯỚC TỨC THÌ trong Scene View (Edit Mode) mà không cần bấm Play!
/// </summary>
[ExecuteAlways]
public class NPCCampPose : MonoBehaviour
{
    public enum PoseType
    {
        Standing = 0,
        Sitting = 1,
        Laying = 2
    }

    [Header("Tùy chọn tư thế")]
    [Tooltip("Chọn tư thế bạn muốn cho NPC này trong doanh trại")]
    public PoseType pose = PoseType.Standing;

    [Header("Xem trước trong Scene (Edit Mode)")]
    [Tooltip("Bật để xem trước tư thế ngay trong cửa sổ Scene trước khi bấm Play")]
    public bool previewInEditMode = true;

    [Header("Animation Clips (Tự động tải nếu để trống)")]
    [SerializeField] private AnimationClip standingClip;
    [SerializeField] private AnimationClip sittingClip;
    [SerializeField] private AnimationClip layingClip;

    private Animator anim;

    void Awake()
    {
        CacheComponents();
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            ApplyPoseInPlayMode();
        }
    }

    private void CacheComponents()
    {
        if (anim == null) anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

#if UNITY_EDITOR
        AutoLoadClipsIfNull();
#endif
    }

#if UNITY_EDITOR
    private void AutoLoadClipsIfNull()
    {
        if (sittingClip == null)
            sittingClip = FindClipInAsset("Assets/Animation/SittingIdle.fbx");

        if (layingClip == null)
            layingClip = FindClipInAsset("Assets/Animation/LayingDown.fbx");

        if (standingClip == null)
            standingClip = FindClipInAsset("Assets/Animation/Meshy_AI_Character_output@Idle.fbx");
    }

    private AnimationClip FindClipInAsset(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null) return null;

        foreach (var obj in assets)
        {
            if (obj is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }

    private void OnValidate()
    {
        CacheComponents();

        if (Application.isPlaying)
        {
            ApplyPoseInPlayMode();
        }
        else if (previewInEditMode)
        {
            EditorApplication.delayCall -= DelayPreview;
            EditorApplication.delayCall += DelayPreview;
        }
    }

    private void DelayPreview()
    {
        if (this == null || !previewInEditMode || Application.isPlaying) return;
        PreviewPoseInEditMode();
    }

    [ContextMenu("Xem trước tư thế ngay")]
    public void PreviewPoseInEditMode()
    {
        CacheComponents();
        if (anim == null) return;

        AnimationClip targetClip = pose switch
        {
            PoseType.Sitting => sittingClip,
            PoseType.Laying => layingClip,
            _ => standingClip
        };

        if (targetClip == null) return;

        // Dùng PlayableGraph để sample trực tiếp tư thế qua Avatar trong Edit Mode
        PlayableGraph graph = PlayableGraph.Create("PosePreview");
        try
        {
            var clipPlayable = AnimationClipPlayable.Create(graph, targetClip);
            var output = AnimationPlayableOutput.Create(graph, "Animation", anim);
            output.SetSourcePlayable(clipPlayable);
            graph.Evaluate(0.05f); // Sample frame đầu tiên
        }
        finally
        {
            graph.Destroy();
        }

        // Đánh dấu Scene đã thay đổi để Unity cập nhật hiển thị
        EditorUtility.SetDirty(gameObject);
    }
#endif

    /// <summary>
    /// Áp dụng tư thế khi đang chạy game (Play Mode)
    /// </summary>
    public void ApplyPoseInPlayMode()
    {
        CacheComponents();
        if (anim == null) return;

        anim.SetInteger("Pose", (int)pose);
        anim.SetBool("isSitting", pose == PoseType.Sitting);
        anim.SetBool("isLaying", pose == PoseType.Laying);
        anim.SetBool("isWalking", false);
        anim.SetBool("isRunning", false);

        string targetState = pose switch
        {
            PoseType.Sitting => "Sitting",
            PoseType.Laying => "Laying",
            _ => "Standing"
        };

        if (anim.HasState(0, Animator.StringToHash(targetState)))
        {
            anim.Play(targetState, 0, 0f);
        }
        else if (pose == PoseType.Standing)
        {
            if (anim.HasState(0, Animator.StringToHash("Idle")))
                anim.Play("Idle", 0, 0f);
            else if (anim.HasState(0, Animator.StringToHash("sword_idle")))
                anim.Play("sword_idle", 0, 0f);
        }
    }
}
