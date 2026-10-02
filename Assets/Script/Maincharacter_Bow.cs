using UnityEngine;

public class Maincharacter_Bow : MonoBehaviour
{
    public Rigidbody rb;
    public Animator animator;

    [Header("Movement Speed")]
    [SerializeField, Min(0f)] private float walkSpeed = 3f;
    [SerializeField, Min(0f)] private float runSpeed = 6f;

    [Header("Movement Animation Speed")]
    [SerializeField, Min(0.01f)] private float walkAnimationSpeed = 1f;
    [SerializeField, Min(0.01f)] private float runAnimationSpeed = 2f;

    private static readonly int MovementAnimationSpeed = Animator.StringToHash("moveAnimationSpeed");
    private bool hasMovementAnimationSpeed;

    [Header("Attack")]
    [Tooltip("Tỉ lệ tốc độ di chuyển khi tấn công: 0.2 = còn 20% tốc độ, giảm 80%.")]
    [SerializeField, Range(0f, 1f)] private float attackMoveSpeedMultiplier = 0.2f;
    [Tooltip("Đường dẫn state chém kiếm trên layer 0 của Animator.")]
    [SerializeField] private string attackStateName = "Base Layer.Attack";

    [Header("Aim")]
    [SerializeField] private string downToAimStateName = "Base Layer.DownToAim";
    [SerializeField] private string aimIdleStateName = "Base Layer.AimIdle";
    [SerializeField] private string aimToDownStateName = "Base Layer.AimToDown";
    [SerializeField] private string downIdleStateName = "Base Layer.DownIdle";

    private static readonly int AimTrigger = Animator.StringToHash("aim");
    private static readonly int LowerAimTrigger = Animator.StringToHash("lowerAim");
    private int downToAimStateHash;
    private int aimIdleStateHash;
    private int aimToDownStateHash;
    private int downIdleStateHash;
    private int pendingAimStateHash;
    private int pendingAimTrigger;
    private bool isAiming;
    private bool waitingForAim;
    private float aimStartDeadline;

    [Header("Shield Block")]
    [Tooltip("Đường dẫn state đỡ khiên trên layer 0 của Animator.")]
    [SerializeField] private string blockStateName = "Base Layer.Block";

    private static readonly int BlockParameter = Animator.StringToHash("isBlocking");
    private int blockStateHash;
    private bool hasBlockParameter;
    private bool canBlock;
    private bool isBlocking;

    private static readonly int AttackTrigger = Animator.StringToHash("attack");
    private int attackStateHash;
    private int pendingAttackTrigger;
    private bool isAttacking;
    private bool waitingForAttack;
    private float attackStartDeadline;

    private bool isRunning = false;

    // Chỉ lưu hướng ngang (X, Z) — KHÔNG có Y
    private float moveX;
    private float moveZ;

    void Start()
    {
        rb.freezeRotation = true;

        // Rigidbody điều khiển di chuyển;
        // không áp dụng chuyển động gốc từ animation.
        animator.applyRootMotion = false;
        attackStateHash = Animator.StringToHash(attackStateName);
        downToAimStateHash = Animator.StringToHash(downToAimStateName);
        aimIdleStateHash = Animator.StringToHash(aimIdleStateName);
        aimToDownStateHash = Animator.StringToHash(aimToDownStateName);
        downIdleStateHash = Animator.StringToHash(downIdleStateName);
        blockStateHash = Animator.StringToHash(blockStateName);
        hasBlockParameter = HasAnimatorParameter(BlockParameter, AnimatorControllerParameterType.Bool);
        canBlock = hasBlockParameter && animator.HasState(0, blockStateHash);
        hasMovementAnimationSpeed = HasAnimatorParameter(MovementAnimationSpeed, AnimatorControllerParameterType.Float);
        if (!hasMovementAnimationSpeed)
        {
            Debug.LogWarning("Thêm Float 'moveAnimationSpeed' vào Animator và chọn làm Speed Multiplier của state Walking, rồi chạy lại Play Mode.", this);
        }
        UpdateMovementAnimationSpeed();
    }

    // Đọc input và cập nhật trạng thái animation.
    void Update()
    {
        UpdateAttackState();
        UpdateAimState();
        UpdateBlockState();

        if (!isAttacking && !isBlocking)
        {
            // Chuột phải bật/tắt ngắm sau khi động tác nâng hoặc hạ súng hoàn tất.
            if (Input.GetMouseButtonDown(1))
            {
                TryToggleAim();
            }
            else if (!isAiming && Input.GetMouseButtonDown(0))
            {
                TryStartAttack(attackStateHash, attackStateName, AttackTrigger, "attack");
            }
        }

        if (isBlocking)
        {
            moveX = 0f;
            moveZ = 0f;
            isRunning = false;
            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
            UpdateMovementAnimationSpeed();
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

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
        isRunning = isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        animator.SetBool("isWalking", isMoving);
        animator.SetBool("isRunning", isRunning);
        UpdateMovementAnimationSpeed();

        HandleRotation(moveDir);
    }

    void UpdateMovementAnimationSpeed()
    {
        if (hasMovementAnimationSpeed)
        {
            animator.SetFloat(MovementAnimationSpeed, isRunning ? runAnimationSpeed : walkAnimationSpeed);
        }
    }

    void TryToggleAim()
    {
        // Không xếp hàng thêm yêu cầu khi đang nâng, hạ hoặc blend animation.
        if (waitingForAim || animator.IsInTransition(0) ||
            IsAnimationStateActive(downToAimStateHash) || IsAnimationStateActive(aimToDownStateHash))
        {
            return;
        }

        bool lowerAim = IsAnimationStateActive(aimIdleStateHash);
        int transitionStateHash = lowerAim ? aimToDownStateHash : downToAimStateHash;
        int idleStateHash = lowerAim ? downIdleStateHash : aimIdleStateHash;
        string transitionStateName = lowerAim ? aimToDownStateName : downToAimStateName;
        string idleStateName = lowerAim ? downIdleStateName : aimIdleStateName;
        int triggerHash = lowerAim ? LowerAimTrigger : AimTrigger;
        string triggerName = lowerAim ? "lowerAim" : "aim";

        if (!animator.HasState(0, transitionStateHash) || !animator.HasState(0, idleStateHash))
        {
            Debug.LogWarning($"Cần hai state '{transitionStateName}' và '{idleStateName}' trên layer 0.", this);
            return;
        }

        if (!HasAnimatorParameter(triggerHash, AnimatorControllerParameterType.Trigger))
        {
            Debug.LogWarning($"Hãy thêm parameter kiểu Trigger tên '{triggerName}' vào Animator.", this);
            return;
        }

        isAiming = true;
        waitingForAim = true;
        pendingAimStateHash = transitionStateHash;
        pendingAimTrigger = triggerHash;
        aimStartDeadline = Time.time + 1f;
        animator.SetTrigger(triggerHash);
    }

    void UpdateAimState()
    {
        bool aimIsActive = IsAnimationStateActive(downToAimStateHash) ||
            IsAnimationStateActive(aimIdleStateHash) || IsAnimationStateActive(aimToDownStateHash);
        if (waitingForAim && IsAnimationStateActive(pendingAimStateHash))
        {
            waitingForAim = false;
        }
        else if (waitingForAim && Time.time >= aimStartDeadline)
        {
            // Hủy yêu cầu nếu Animator không có transition hợp lệ để nâng/hạ súng.
            waitingForAim = false;
            animator.ResetTrigger(pendingAimTrigger);
            Debug.LogWarning("Animator chưa nâng/hạ súng. Kiểm tra transition với condition 'aim' hoặc 'lowerAim'.", this);
        }

        // Animator tự chuyển về AimIdle hoặc DownIdle theo Exit Time của clip.
        isAiming = waitingForAim || aimIsActive;
    }

    void TryStartAttack(int stateHash, string stateName, int triggerHash, string triggerName)
    {
        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning($"Chưa tìm thấy state '{stateName}' trên layer 0 của Animator.", this);
            return;
        }

        if (!HasAnimatorParameter(triggerHash, AnimatorControllerParameterType.Trigger))
        {
            Debug.LogWarning($"Hãy thêm parameter kiểu Trigger tên '{triggerName}' vào Animator.", this);
            return;
        }

        isAttacking = true;
        waitingForAttack = true;
        pendingAttackTrigger = triggerHash;
        // Chỉ giới hạn thời gian chờ vào state; không giới hạn độ dài animation chém.
        attackStartDeadline = Time.time + 1f;
        animator.SetTrigger(triggerHash);
    }

    void UpdateAttackState()
    {
        bool attackIsActive = IsAnimationStateActive(attackStateHash);

        if (attackIsActive)
        {
            waitingForAttack = false;
        }
        else if (waitingForAttack && Time.time >= attackStartDeadline)
        {
            // Không giữ trạng thái tấn công mãi nếu thiếu transition vào đòn đã chọn.
            waitingForAttack = false;
            animator.ResetTrigger(pendingAttackTrigger);
            Debug.LogWarning("Animator chưa vào state tấn công. Kiểm tra transition Any State -> Attack.", this);
        }

        // Giữ tốc độ di chuyển giảm cả khi đang blend vào hoặc ra khỏi Attack.
        isAttacking = waitingForAttack || attackIsActive;
    }

    void UpdateBlockState()
    {
        if (!isAiming && Input.GetKeyDown(KeyCode.Space) && !canBlock)
        {
            Debug.LogWarning($"Đỡ khiên cần parameter Bool 'isBlocking' và state '{blockStateName}' trên layer 0. " +
                             "Cấu hình Animator rồi chạy lại Play Mode.", this);
        }

        // Giữ Space để đỡ; đợi chém xong nếu đang tấn công.
        bool wantsToBlock = canBlock && Input.GetKey(KeyCode.Space) && !isAttacking && !isAiming;
        if (hasBlockParameter)
        {
            animator.SetBool(BlockParameter, wantsToBlock);
        }

        // Thả Space yêu cầu thoát Block; giữ khóa đến khi blend ra hoàn tất.
        isBlocking = wantsToBlock || (canBlock && IsAnimationStateActive(blockStateHash));
    }

    bool IsAnimationStateActive(int stateHash)
    {
        return animator.GetCurrentAnimatorStateInfo(0).fullPathHash == stateHash ||
            (animator.IsInTransition(0) &&
             animator.GetNextAnimatorStateInfo(0).fullPathHash == stateHash);
    }

    bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    void OnDisable()
    {
        if (animator != null && hasMovementAnimationSpeed)
        {
            animator.SetFloat(MovementAnimationSpeed, walkAnimationSpeed);
        }

        if (animator != null && waitingForAim)
        {
            animator.ResetTrigger(pendingAimTrigger);
        }

        waitingForAim = false;
        isAiming = false;

        if (animator != null && hasBlockParameter)
        {
            animator.SetBool(BlockParameter, false);
        }

        isBlocking = false;
    }

    // FixedUpdate: xử lý physics
    void FixedUpdate()
    {
        float moveSpeed = isRunning ? runSpeed : walkSpeed;
        if (isBlocking)
        {
            moveSpeed = 0f;
        }
        else if (isAttacking)
        {
            moveSpeed *= attackMoveSpeedMultiplier;
        }

        // Lấy velocity hiện tại, CHỈ thay X và Z, GIỮ NGUYÊN Y (gravity)
        float currentY = rb.linearVelocity.y;
        rb.linearVelocity = new Vector3(moveX * moveSpeed, currentY, moveZ * moveSpeed);
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
