using UnityEngine;

public class Character_Movement : MonoBehaviour
{
    public Rigidbody rb;
    public Animator animator;

    [Header("Movement Speed")]
    [SerializeField, Min(0f)] private float walkSpeed = 3f;
    [SerializeField, Min(0f)] private float runSpeed = 6f;

    [Header("Attack")]
    [Tooltip("Tỉ lệ tốc độ di chuyển khi tấn công: 0.2 = còn 20% tốc độ, giảm 80%.")]
    [SerializeField, Range(0f, 1f)] private float attackMoveSpeedMultiplier = 0.2f;
    [Tooltip("Đường dẫn state chém kiếm trên layer 0 của Animator.")]
    [SerializeField] private string attackStateName = "Base Layer.Attack";
    [Tooltip("Đường dẫn state đòn chuột phải trên layer 0 của Animator.")]
    [SerializeField] private string attack2StateName = "Base Layer.Attack2";

    [Header("Shield Block")]
    [Tooltip("Đường dẫn state đỡ khiên trên layer 0 của Animator.")]
    [SerializeField] private string blockStateName = "Base Layer.Block";

    [Header("Combat Damage")]
    public int attackDamage = 20;
    public float attackRange = 1.8f;
    public LayerMask enemyLayer;
    private bool hasAppliedDamage;
    private HitEffect hitEffect;

    private static readonly int BlockParameter = Animator.StringToHash("isBlocking");
    private int blockStateHash;
    private bool hasBlockParameter;
    private bool canBlock;
    private bool isBlocking;

    private static readonly int AttackTrigger = Animator.StringToHash("attack");
    private static readonly int Attack2Trigger = Animator.StringToHash("attack2");
    private int attackStateHash;
    private int attack2StateHash;
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
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Rigidbody điều khiển di chuyển;
        // không áp dụng chuyển động gốc từ animation.
        if (animator != null) animator.applyRootMotion = false;
        attackStateHash = Animator.StringToHash(attackStateName);
        attack2StateHash = Animator.StringToHash(attack2StateName);
        blockStateHash = Animator.StringToHash(blockStateName);
        hasBlockParameter = HasAnimatorParameter(BlockParameter, AnimatorControllerParameterType.Bool);
        canBlock = hasBlockParameter && animator.HasState(0, blockStateHash);
        hitEffect = GetComponent<HitEffect>();
        hasAppliedDamage = false;
    }

    // Đọc input và cập nhật trạng thái animation.
    void Update()
    {
        UpdateAttackState();
        UpdateBlockState();

        if (!isAttacking && !isBlocking)
        {
            // Nếu nhấn đồng thời hai nút, ưu tiên đòn chuột trái.
            if (Input.GetMouseButtonDown(0))
            {
                TryStartAttack(attackStateHash, attackStateName, AttackTrigger, "attack");
            }
            else if (Input.GetMouseButtonDown(1))
            {
                TryStartAttack(attack2StateHash, attack2StateName, Attack2Trigger, "attack2");
            }
        }

        if (isBlocking)
        {
            moveX = 0f;
            moveZ = 0f;
            isRunning = false;
            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
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
        isRunning = isMoving && Input.GetKey(KeyCode.LeftShift);

        animator.SetBool("isWalking", isMoving);
        animator.SetBool("isRunning", isRunning);

        HandleRotation(moveDir);
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
        hasAppliedDamage = false;
        pendingAttackTrigger = triggerHash;
        // Chỉ giới hạn thời gian chờ vào state; không giới hạn độ dài animation chém.
        attackStartDeadline = Time.time + 1f;
        animator.SetTrigger(triggerHash);
    }

    void UpdateAttackState()
    {
        bool attackIsActive = IsAnimationStateActive(attackStateHash) || IsAnimationStateActive(attack2StateHash);

        if (attackIsActive)
        {
            waitingForAttack = false;
        }
        else if (waitingForAttack && Time.time >= attackStartDeadline)
        {
            // Không giữ trạng thái tấn công mãi nếu thiếu transition vào đòn đã chọn.
            waitingForAttack = false;
            animator.ResetTrigger(pendingAttackTrigger);
            Debug.LogWarning("Animator chưa vào state tấn công. Kiểm tra transition Any State -> Attack hoặc Attack2.", this);
        }

        // Khi đang ở state chém và chưa áp damage thì áp damage một lần.
        if (attackIsActive && !hasAppliedDamage)
        {
            hasAppliedDamage = true;
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1f, attackRange, enemyLayer);
            foreach (Collider hit in hits)
            {
                var h = hit.GetComponentInParent<Health>();
                if (h != null)
                {
                    h.TakeDamage(attackDamage);
                    hitEffect?.PlayHit(hit.transform.position + Vector3.up);
                    Debug.Log($"Chém trúng {h.name}, trừ {attackDamage} HP");
                }
            }
        }

        // Giữ tốc độ di chuyển giảm cả khi đang blend vào hoặc ra khỏi Attack/Attack2.
        isAttacking = waitingForAttack || attackIsActive;
    }

    void UpdateBlockState()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !canBlock)
        {
            Debug.LogWarning($"Đỡ khiên cần parameter Bool 'isBlocking' và state '{blockStateName}' trên layer 0. " +
                             "Cấu hình Animator rồi chạy lại Play Mode.", this);
        }

        // Giữ Space để đỡ; đợi chém xong nếu đang tấn công.
        bool wantsToBlock = canBlock && Input.GetKey(KeyCode.Space) && !isAttacking;
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
