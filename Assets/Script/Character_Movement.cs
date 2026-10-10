using UnityEngine;

/// <summary>
/// Di chuyển nhân vật theo hướng camera (WASD), Shift để chạy.
/// - Hoàn toàn null-safe: tự tìm Rigidbody + Animator nếu chưa gán
/// - Chuyển animation đúng: idle → walk → run, không T-pose
/// - Xoay nhân vật theo hướng di chuyển mượt mà
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Character_Movement : MonoBehaviour
{
    [Header("Components (tự động tìm nếu để trống)")]
    public Rigidbody rb;
    public Animator animator;

    [Header("Movement Speed")]
    public float walkSpeed = 5f;
    public float runSpeed  = 10f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 10f;

    // --- cached hashes ---
    private static readonly int HashWalking  = Animator.StringToHash("isWalking");
    private static readonly int HashRunning  = Animator.StringToHash("isRunning");

    // --- internal ---
    private Vector3 moveDir;
    private bool    isRunning;
    private bool    animatorReady;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;  // mượt hơn
    }

    void Start()
    {
        SetupAnimator();
    }

    private void SetupAnimator()
    {
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;

            // Kiểm tra controller đã được gán chưa
            if (animator.runtimeAnimatorController == null)
            {
                var ctrl = Resources.Load<RuntimeAnimatorController>("Sword_Anima");
                if (ctrl != null)
                {
                    animator.runtimeAnimatorController = ctrl;
                    Debug.Log("[Character_Movement] Đã load Sword_Anima controller từ Resources.");
                }
                else
                {
                    Debug.LogWarning("[Character_Movement] Không tìm thấy Sword_Anima trong Resources/. Vui lòng gán thủ công trong Inspector.");
                }
            }

            animatorReady = animator.runtimeAnimatorController != null;
        }
    }

    void Update()
    {
        // Thử tìm lại nếu chưa có
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (animator == null || !animatorReady) SetupAnimator();
        if (rb == null) return;

        ReadMovementInput();
        UpdateAnimation();
    }

    private void ReadMovementInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Lấy hướng camera (bỏ trục Y)
        Camera cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        Vector3 right   = cam != null ? cam.transform.right   : transform.right;
        forward.y = 0f; forward.Normalize();
        right.y   = 0f; right.Normalize();

        moveDir = (forward * v + right * h);
        if (moveDir.magnitude > 1f) moveDir.Normalize();

        isRunning = moveDir.magnitude > 0.01f && Input.GetKey(KeyCode.LeftShift);
    }

    private void UpdateAnimation()
    {
        if (!animatorReady || animator == null) return;

        bool moving = moveDir.magnitude > 0.01f;
        animator.SetBool(HashWalking, moving);
        animator.SetBool(HashRunning, isRunning);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        float speed = isRunning ? runSpeed : walkSpeed;

        if (moveDir.magnitude > 0.01f)
        {
            // Di chuyển theo hướng
            Vector3 velocity = moveDir * speed;
            velocity.y = rb.linearVelocity.y; // giữ gravity
            rb.linearVelocity = velocity;

            // Xoay nhân vật theo hướng di chuyển
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // Dừng trượt ngang, giữ gravity
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }
}
