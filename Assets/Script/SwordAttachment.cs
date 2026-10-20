using UnityEngine;

public class SwordAttachment : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Kéo kiếm hoặc Sword_Socket vào đây (nếu để trống script sẽ tự tìm kiếm con hoặc dùng GameObject này)")]
    public Transform swordTransform;

    [Tooltip("Nếu bạn đã có sẵn 1 Socket làm điểm gắn thì kéo vào đây")]
    public Transform customSocket;

    [Header("Preserve Transform")]
    [Tooltip("Tích chọn để GIỮ NGUYÊN góc xoay và vị trí bạn đã chỉnh sẵn trên Scene. Nếu bỏ tích sẽ dùng offset bên dưới.")]
    public bool keepWorldTransformWhenAttaching = true;

    [Header("Manual Offset (Chỉ dùng nếu không dùng Socket hoặc keepWorldTransform = false)")]
    public Vector3 localPositionOffset = Vector3.zero;
    public Vector3 localRotationOffset = Vector3.zero;

    // Tương thích với prefab/scene cũ
    [HideInInspector] public GameObject sword;
    [HideInInspector] public Animator animator;
    [HideInInspector] public Vector3 localPosition;
    [HideInInspector] public Vector3 localRotation;
    [HideInInspector] public Vector3 localScale = Vector3.one;
    [HideInInspector] public string rightHandBoneName;

    void Awake()
    {
        ResolveReferences();
        CleanStrayComponents();
    }

    void Start()
    {
        Attach();
    }

    void ResolveReferences()
    {
        if (swordTransform == null && sword != null)
        {
            swordTransform = sword.transform;
        }

        if (swordTransform == null)
        {
            // Nếu script gắn trên nhân vật, tìm kiếm con
            Transform foundSword = FindChildRecursive(transform, "Sword");
            if (foundSword == null)
                foundSword = FindChildRecursive(transform, "Weapon");

            if (foundSword != null)
            {
                swordTransform = foundSword;
            }
            else
            {
                swordTransform = this.transform;
            }
        }
    }

    [ContextMenu("Attach Sword Now")]
    public void Attach()
    {
        ResolveReferences();

        if (swordTransform == null)
        {
            Debug.LogError("[SwordAttachment] Không tìm thấy swordTransform!", this);
            return;
        }

        // 1. Nếu có customSocket thì gắn vào Socket
        if (customSocket != null)
        {
            AttachToTarget(customSocket);
            return;
        }

        // 2. Tìm xương tay phải thông qua Animator hoặc duyệt Hierarchy
        Transform handBone = FindRightHandBone();

        if (handBone != null)
        {
            // Kiểm tra xem bên trong handBone có sẵn Socket nào không
            Transform existingSocket = FindChildRecursive(handBone, "Socket");
            Transform targetAttachPoint = existingSocket != null ? existingSocket : handBone;

            AttachToTarget(targetAttachPoint);
            Debug.Log($"[SwordAttachment] Đã gắn kiếm thành công vào: {targetAttachPoint.name}", this);
        }
        else
        {
            Debug.LogWarning("[SwordAttachment] Không tìm thấy xương tay phải! Hãy kiểm tra lại tên bone hoặc kéo trực tiếp Socket vào Inspector.", this);
        }
    }

    void AttachToTarget(Transform target)
    {
        if (swordTransform == null || target == null)
            return;

        // Nếu kiếm ĐÃ là con của target (hoặc đang nằm trong hierarchy của target)
        // -> KIẾM ĐÃ Ở ĐÚNG VỊ TRÍ VÀ GÓC XOAY ĐÃ CHỈNH TRÊN SCENE, KHÔNG ĐƯỢC RESET!
        if (swordTransform.parent == target || swordTransform.IsChildOf(target))
        {
            CleanStrayComponents();
            return;
        }

        // Nếu kiếm chưa được gán vào target, SetParent và giữ nguyên World Transform
        swordTransform.SetParent(target, true);

        // Chỉ khi người dùng chủ động tắt keepWorldTransformWhenAttaching VÀ có offset thì mới can thiệp
        if (!keepWorldTransformWhenAttaching && (localPositionOffset != Vector3.zero || localRotationOffset != Vector3.zero))
        {
            swordTransform.localPosition = localPositionOffset;
            swordTransform.localRotation = Quaternion.Euler(localRotationOffset);
        }

        CleanStrayComponents();
    }

    public void CleanStrayComponents()
    {
        if (swordTransform == null) return;
        GameObject go = swordTransform.gameObject;

        // Tránh tình trạng kiếm bị gắn nhầm EnemyAttack khiến kiếm tự bay lơ lửng đuổi theo player
        EnemyAttack ea = go.GetComponent<EnemyAttack>();
        if (ea != null)
        {
            ea.enabled = false;
            if (Application.isPlaying) Destroy(ea); else DestroyImmediate(ea);
        }

        Health h = go.GetComponent<Health>();
        if (h != null)
        {
            if (Application.isPlaying) Destroy(h); else DestroyImmediate(h);
        }

        // Xóa thanh máu gắn nhầm trên kiếm
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = go.transform.GetChild(i);
            if (child != null && (child.name == "EnemyHealthBar" || child.name == "PlayerHealthBarCanvas"))
            {
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
        }
    }

    Transform FindRightHandBone()
    {
        // 1. Thử lấy qua Animator nếu là Humanoid
        Animator anim = animator != null ? animator : GetComponentInParent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        if (anim != null && anim.isHuman)
        {
            Transform bone = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone != null) return bone;
        }

        // 2. Tìm kiếm trong cha/con có Animator
        if (anim != null)
        {
            Transform boneInAnim = FindHandRecursive(anim.transform);
            if (boneInAnim != null) return boneInAnim;
        }

        // 3. Tìm kiếm đệ quy theo tên từ root
        Transform root = transform.root;
        Transform boneInRoot = FindHandRecursive(root);
        if (boneInRoot != null) return boneInRoot;

        // 4. Tìm kiếm từ toàn scene nếu kiếm đứng độc lập ở root
        GameObject enemyObj = GameObject.Find("Meshy_AI_Black_Armored_General_biped_Character_output");
        if (enemyObj != null)
        {
            Transform enemyHand = FindHandRecursive(enemyObj.transform);
            if (enemyHand != null) return enemyHand;
        }

        GameObject playerObj = GameObject.Find("Main_Character");
        if (playerObj != null)
        {
            Transform playerHand = FindHandRecursive(playerObj.transform);
            if (playerHand != null) return playerHand;
        }

        return null;
    }

    Transform FindHandRecursive(Transform current)
    {
        if (current == null) return null;
        string lowerName = current.name.ToLower();

        // Khớp 'righthand' hoặc 'right_hand' nhưng loại trừ các xương ngón tay
        if (lowerName.Contains("righthand") || lowerName.Contains("right_hand"))
        {
            if (!lowerName.Contains("thumb") &&
                !lowerName.Contains("index") &&
                !lowerName.Contains("middle") &&
                !lowerName.Contains("ring") &&
                !lowerName.Contains("pinky"))
            {
                return current;
            }
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindHandRecursive(current.GetChild(i));
            if (found != null) return found;
        }

        return null;
    }

    Transform FindChildRecursive(Transform parent, string keyword)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.ToLower().Contains(keyword.ToLower()))
                return child;

            Transform subChild = FindChildRecursive(child, keyword);
            if (subChild != null) return subChild;
        }
        return null;
    }
}

