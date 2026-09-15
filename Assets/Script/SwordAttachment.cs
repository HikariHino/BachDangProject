using UnityEngine;

public class SwordAttachment : MonoBehaviour
{
    [Header("Tham chiếu")]
    public GameObject sword;           // Kéo object kiếm vào đây
    public Animator animator;          // Animator của nhân vật

    [Header("Cài đặt vị trí kiếm trên tay")]
    public Vector3 localPosition = new Vector3(0f, 0f, 0f);
    public Vector3 localRotation = new Vector3(0f, 0f, 0f);
    public Vector3 localScale    = new Vector3(1f, 1f, 1f);

    // Dùng tên bone khi không lấy được xương từ Avatar Humanoid.
    [Header("Tên bone tay phải dự phòng")]
    public string rightHandBoneName = "RightHand";

    private Transform rightHandBone;

    void Start()
    {
        if (sword == null)
        {
            Debug.LogError("Chưa gán Sword. Kéo object kiếm trong Hierarchy vào Sword Attachment.", this);
            return;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Avatar ánh xạ xương tay phải, không phụ thuộc tên bone trong model.
        if (animator != null && animator.avatar != null &&
            animator.avatar.isValid && animator.avatar.isHuman)
        {
            rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        Transform searchRoot = animator != null ? animator.transform : transform;
        if (rightHandBone == null)
            rightHandBone = FindBoneRecursive(searchRoot, rightHandBoneName);

        // Hỗ trợ scene cũ vẫn lưu tên mixamorig:RightHand trong Inspector.
        if (rightHandBone == null)
            rightHandBone = FindBoneRecursive(searchRoot, "RightHand");

        if (rightHandBone == null)
        {
            Debug.LogError($"Không tìm thấy xương tay phải qua Animator hoặc tên '{rightHandBoneName}' / 'RightHand'. " +
                           "Kiểm tra Animator và tên bone trong Hierarchy của nhân vật!", this);
            return;
        }

        AttachSword();
    }

    void AttachSword()
    {
        if (sword == null || rightHandBone == null) return;

        // Gắn kiếm vào bone tay phải
        sword.transform.SetParent(rightHandBone);

        // Đặt vị trí local (tương đối với bone)
        sword.transform.localPosition = localPosition;
        sword.transform.localEulerAngles = localRotation;
        sword.transform.localScale = localScale;

        Debug.Log("Đã gắn kiếm vào tay phải thành công!");
    }

    // Sau khi căn Transform của kiếm lúc Pause, lấy độ lệch so với tay.
    // Copy component này rồi Paste Component Values sau khi thoát Play để lưu.
    [ContextMenu("Capture Current Sword Pose")]
    public void CaptureCurrentSwordPose()
    {
        if (!Application.isPlaying || sword == null || rightHandBone == null ||
            sword.transform.parent != rightHandBone)
        {
            Debug.LogWarning("Hãy chạy game và gắn kiếm vào tay trước khi lấy tư thế kiếm.", this);
            return;
        }

        localPosition = sword.transform.localPosition;
        localRotation = sword.transform.localEulerAngles;
        localScale = sword.transform.localScale;

        Debug.Log("Đã lấy tư thế kiếm. Copy Component của Sword Attachment, thoát Play, " +
                  "rồi Paste Component Values vào cùng component và lưu scene.", this);
    }

    // Hàm tìm bone theo tên trong toàn bộ hierarchy
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

    // Gọi hàm này để tháo kiếm (ví dụ khi nhân vật bị chết)
    public void DetachSword()
    {
        if (sword == null) return;
        sword.transform.SetParent(null);
    }
}
