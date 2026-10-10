using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Character_Movement : MonoBehaviour
{
    [Header("Movement Speed (Scale 7x)")]
    public float walkSpeed = 30f; // Tốc độ lớn cho nhân vật to
    public float runSpeed  = 55f;
    public float rotationSpeed = 15f;

    private Rigidbody rb;
    private Animator[] animators; // Lưu TẤT CẢ animator để không sót
    private Vector3 moveDir;
    private bool isRunning;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        
        // === SỬA T-POSE TRIỆT ĐỂ ===
        // Quét tất cả con cháu, tìm mọi Animator có thể có
        animators = GetComponentsInChildren<Animator>(true);
        var ctrl = Resources.Load<RuntimeAnimatorController>("Sword_Anima");
        
        foreach (var anim in animators)
        {
            anim.enabled = true;
            anim.applyRootMotion = false;
            // Nếu animator chưa có controller, ép gán Sword_Anima
            if (anim.runtimeAnimatorController == null && ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
                Debug.Log("[Movement] Đã gán Controller cho: " + anim.gameObject.name);
            }
        }
    }

    void Update()
    {
        if (rb == null) return;

        // Đọc input WASD
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Hướng đi dựa trên hướng camera
        Camera cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        Vector3 right   = cam != null ? cam.transform.right   : transform.right;
        forward.y = 0f; forward.Normalize();
        right.y   = 0f; right.Normalize();

        moveDir = (forward * v + right * h).normalized;
        
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

        float speed = isRunning ? runSpeed : walkSpeed;

        if (moveDir.magnitude > 0.1f)
        {
            // Set vận tốc di chuyển vật lý
            Vector3 vel = moveDir * speed;
            vel.y = rb.linearVelocity.y; // Giữ trọng lực rơi tự do
            rb.linearVelocity = vel;

            // Xoay nhân vật mượt mà
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
        }
        else
        {
            // Trượt phanh
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }
}
