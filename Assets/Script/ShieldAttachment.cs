using UnityEngine;

public class ShieldAttachment : MonoBehaviour
{
    [Header("Tham chiếu")]
    public GameObject shield;
    public Animator animator;

    [Header("Cài đặt vị trí khiên trên tay trái")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localRotation = Vector3.zero;
    public Vector3 localScale = Vector3.one;

    [Header("Tên bone tay trái dự phòng")]
    public string leftHandBoneName = "LeftHand";

    private Transform leftHandBone;

    void Start()
    {
        if (shield == null)
        {
            Debug.LogError("Chưa gán Shield. Kéo object khiên trong Hierarchy vào Shield Attachment.", this);
            return;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Ưu tiên ánh xạ Humanoid để không phụ thuộc tên bone của model.
        if (animator != null && animator.avatar != null &&
            animator.avatar.isValid && animator.avatar.isHuman)
        {
            leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }

        Transform searchRoot = animator != null ? animator.transform : transform;
        if (leftHandBone == null)
            leftHandBone = FindBoneRecursive(searchRoot, leftHandBoneName);

        if (leftHandBone == null)
            leftHandBone = FindBoneRecursive(searchRoot, "LeftHand");

        if (leftHandBone == null)
            leftHandBone = FindBoneRecursive(searchRoot, "mixamorig:LeftHand");

        if (leftHandBone == null)
        {
            Debug.LogError($"Không tìm thấy xương tay trái qua Animator hoặc tên '{leftHandBoneName}' / 'LeftHand' / 'mixamorig:LeftHand'. " +
                           "Kiểm tra Animator và tên bone trong Hierarchy của nhân vật!", this);
            return;
        }

        AttachShield();
    }

    void AttachShield()
    {
        if (shield == null || leftHandBone == null) return;

        shield.transform.SetParent(leftHandBone, false);
        shield.transform.localPosition = localPosition;
        shield.transform.localEulerAngles = localRotation;
        shield.transform.localScale = localScale;
    }

    // Căn Transform của khiên lúc Pause, rồi lưu độ lệch so với tay trái.
    [ContextMenu("Capture Current Shield Pose")]
    public void CaptureCurrentShieldPose()
    {
        if (!Application.isPlaying || shield == null || leftHandBone == null ||
            shield.transform.parent != leftHandBone)
        {
            Debug.LogWarning("Hãy chạy game và gắn khiên vào tay trái trước khi lấy tư thế khiên.", this);
            return;
        }

        localPosition = shield.transform.localPosition;
        localRotation = shield.transform.localEulerAngles;
        localScale = shield.transform.localScale;

        Debug.Log("Đã lấy tư thế khiên. Copy Component của Shield Attachment, thoát Play, " +
                  "rồi Paste Component Values vào cùng component và lưu scene.", this);
    }

    Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;

        foreach (Transform child in parent)
        {
            Transform found = FindBoneRecursive(child, boneName);
            if (found != null) return found;
        }

        return null;
    }

    public void DetachShield()
    {
        if (shield == null) return;
        shield.transform.SetParent(null);
    }
}
