using UnityEngine;

public class Character_Movement : MonoBehaviour
{
    public Rigidbody rb;
    public Animator animator;

    private bool isGrounded = false;
    private bool isRunning = false;
    private bool jumpPressed = false;

    // Chỉ lưu hướng ngang (X, Z) — KHÔNG có Y
    private float moveX;
    private float moveZ;

    void Start()
    {
        rb.freezeRotation = true;

        // Rigidbody điều khiển di chuyển;
        // không áp dụng chuyển động gốc từ animation.
        animator.applyRootMotion = false;
    }

    // Update: chỉ đọc input
    void Update()
    {
        float ipHorizontal = Input.GetAxis("Horizontal");
        float ipVertical   = Input.GetAxis("Vertical");

        // Lấy hướng camera chiếu xuống mặt phẳng ngang và normalize
        Vector3 camForward = Camera.main.transform.forward;
        camForward.y = 0f;
        camForward.Normalize(); // <-- quan trọng, tránh Y drift

        Vector3 camRight = Camera.main.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        // Tính hướng di chuyển hoàn toàn nằm ngang
        Vector3 moveDir = (camForward * ipVertical + camRight * ipHorizontal);
        if (moveDir.magnitude > 1f) moveDir.Normalize();

        // Chỉ lưu X và Z, không bao giờ lưu Y
        moveX = moveDir.x;
        moveZ = moveDir.z;

        bool isMoving = (moveX != 0f || moveZ != 0f);
        isRunning = isMoving && Input.GetKey(KeyCode.LeftShift);

        animator.SetBool("isWalking", isMoving);
        animator.SetBool("isRunning", isRunning);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            jumpPressed = true;
        }

        HandleRotation(moveDir);
    }

    // FixedUpdate: xử lý physics
    void FixedUpdate()
    {
        float moveSpeed = isRunning ? 6f : 3f;

        // Lấy velocity hiện tại, CHỈ thay X và Z, GIỮ NGUYÊN Y (gravity)
        float currentY = rb.linearVelocity.y;
        rb.linearVelocity = new Vector3(moveX * moveSpeed, currentY, moveZ * moveSpeed);

        if (jumpPressed)
        {
            rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
            animator.SetTrigger("jump");
            isGrounded = false;
            jumpPressed = false;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    void HandleRotation(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude > 0.01f)
        {
            moveDir.y = 0f;
            transform.rotation = Quaternion.LookRotation(moveDir);
        }
    }
}
