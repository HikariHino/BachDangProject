using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Character_Movement : MonoBehaviour
{
    [Header("Movement Speed (Scale 7x)")]
    public float walkSpeed = 30f; // Tốc độ lớn cho nhân vật to
    public float runSpeed  = 55f;
    public float rotationSpeed = 15f;
    public float acceleration = 90f;
    public float braking = 120f;

    private Rigidbody rb;
    private Animator[] animators; // Lưu TẤT CẢ animator để không sót
    private Vector3 moveDir;
    private bool isRunning;

    void Start()
    {
        // Tự động sửa lỗi nếu Inspector bị kẹt số quá nhỏ
        if (walkSpeed < 10f) 
        {
            walkSpeed = 30f;
            runSpeed = 55f;
            Debug.Log("[Movement] Đã tự động sửa tốc độ di chuyển thành 30/55 cho nhân vật Scale 7x.");
        }

        rb = GetComponent<Rigidbody>();
        // Character control needs a dynamic body with X/Z tilt locked, while Y
        // rotation and horizontal position remain free for movement.
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY |
                         RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Player runs at high world-space speeds; continuous collision detection
        // prevents tunnelling through thin hull and prop colliders between physics steps.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
        // === SỬA T-POSE TRIỆT ĐỂ ===
        // Quét tất cả con cháu, tìm mọi Animator có thể có
        animators = GetComponentsInChildren<Animator>(true);
        var ctrl = Resources.Load<RuntimeAnimatorController>("Player_Neutral");
        
        foreach (var anim in animators)
        {
            anim.enabled = true;
            anim.applyRootMotion = false;
            if (ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
            }
            anim.SetBool("isWalking", false);
            anim.SetBool("isRunning", false);
        }
    }

    void Update()
    {
        if (rb == null) return;

        if (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen)
        {
            moveDir = Vector3.zero;
            isRunning = false;
            return;
        }
        if (ScreenFader.Instance != null && ScreenFader.Instance.IsTransitioning)
        {
            moveDir = Vector3.zero;
            isRunning = false;
            return;
        }

        // Đọc input WASD
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(h) < 0.01f)
            h = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        if (Mathf.Abs(v) < 0.01f)
            v = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);

        // WASD luôn bám theo mặt phẳng ngang của camera kiểu third-person.
        Camera cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector2 input = Vector2.ClampMagnitude(new Vector2(h, v), 1f);
        moveDir = (forward * input.y + right * input.x).normalized;
        
        // Giữ Shift để chạy
        isRunning = moveDir.magnitude > 0.1f && Input.GetKey(KeyCode.LeftShift);

        // Báo animation cho TẤT CẢ animator
        bool isWalking = moveDir.magnitude > 0.1f;
        if (animators != null)
        {
            foreach (var anim in animators)
            {
                if (anim.gameObject.activeInHierarchy && anim.runtimeAnimatorController != null)
                {
                    anim.SetBool("isWalking", isWalking);
                    anim.SetBool("isRunning", isRunning);
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Contacts with props can add yaw torque. Keep the character steady when
        // there is no movement input; deliberate heading changes come from WASD.
        bool hasMoveInput = moveDir.sqrMagnitude > 0.001f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ |
                         (hasMoveInput ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeRotationY);
        rb.angularVelocity = Vector3.zero;

        float speed = isRunning ? runSpeed : walkSpeed;
        Vector3 currentPlanarVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 targetPlanarVelocity = moveDir * speed;
        float rate = moveDir.sqrMagnitude > 0.001f ? acceleration : braking;
        Vector3 planarVelocity = Vector3.MoveTowards(
            currentPlanarVelocity, targetPlanarVelocity, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(planarVelocity.x, rb.linearVelocity.y, planarVelocity.z);

        if (hasMoveInput)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
        }
    }
}
