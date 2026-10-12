using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Enemy melee attack with chase behavior, range-based state machine.
/// - detectionRange: Start chasing player
/// - attackRange: Stop and attack
/// - stopRange: Stop moving (automatically clamped < attackRange to avoid jitter)
/// </summary>
public class EnemyAttack : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Target to chase. If empty, automatically finds player by Tag, Component, or Name.")]
    public Transform playerTarget;

    [Header("Detection & Attack Settings")]
    [Tooltip("Khoảng cách phát hiện nhân vật để bắt đầu đuổi theo")]
    public float detectionRange = 10f;

    [Tooltip("Khoảng cách địch dừng lại để tấn công")]
    public float attackRange = 1.8f;
    
    [Tooltip("Khoảng cách dừng di chuyển (sẽ tự động điều chỉnh < attackRange)")]
    public float stopRange = 1.2f;
    
    [Tooltip("Sát thương mỗi đòn đánh")]
    public float damage = 15f;
    
    [Tooltip("Thời gian hồi chiêu giữa các đòn đánh")]
    public float attackCooldown = 1.5f;
    
    [Tooltip("Thời gian vung kiếm trước khi gây dame (dự phòng khi animation thiếu event)")]
    public float attackWindupTime = 0.5f;

    [Tooltip("Thời gian tối đa của 1 đòn đánh (tránh bị kẹt đứng đơ nếu animation thiếu event kết thúc)")]
    public float attackDuration = 1.2f;

    [Header("Movement Settings")]
    [Tooltip("Tốc độ đi bộ khi đuổi theo")]
    public float walkSpeed = 2.5f;
    
    [Tooltip("Tốc độ chạy khi đuổi theo từ xa")]
    public float runSpeed = 4f;
    
    [Tooltip("Sử dụng tốc độ chạy khi người chơi ở xa")]
    public bool useRunWhenFar = true;
    
    [Tooltip("Khoảng cách để chuyển sang chạy")]
    public float runDistanceThreshold = 5f;
    
    [Tooltip("Tốc độ quay mặt về phía người chơi")]
    public float rotationSpeed = 10f;

    [Header("References")]
    [Tooltip("Vị trí mũi vũ khí (để tính toán va chạm)")]
    public Transform attackPoint;
    
    [Tooltip("Layer của player")]
    public LayerMask playerLayer;
    
    [Tooltip("HitEffect component để tạo hiệu ứng âm thanh/chém")]
    public HitEffect hitEffect;

    // Backward compatibility for Editor scripts
    [HideInInspector] public Animator animator 
    { 
        get => _animator != null ? _animator : GetComponentInChildren<Animator>(); 
        set { /* Auto-assigned in Awake */ } 
    }

    [Header("Debug")]
    public bool showGizmos = true;
    public bool drawPath = false;

    // Components
    private Animator _animator;
    private NavMeshAgent _agent;
    private Rigidbody _rb;
    private Health playerHealth;
    private Transform playerTransform; // Giữ nguyên tên để EnemyAIDebug reflection đọc được

    // State machine (Giữ nguyên tên để EnemyAIDebug reflection đọc được)
    private enum EnemyState { Idle, Chasing, Attacking, Recovering, Dead }
    private EnemyState currentState = EnemyState.Idle;
    private float cooldownTimer = 0f;
    private float attackTimer = 0f;
    private bool hasDealtDamageThisSwing = false;
    private float searchPlayerTimer = 0f;

    // Animator hashes & availability
    private int attackHash;
    private int attackLowerHash;
    private int moveSpeedHash;
    private int isMovingHash;
    private int isWalkingHash;
    private int isRunningHash;
    private bool hasAttackParam;
    private bool hasAttackLowerParam;
    private bool hasMoveSpeedParam;
    private bool hasIsMovingParam;
    private bool hasIsWalkingParam;
    private bool hasIsRunningParam;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();

        _agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        
        // Cảnh báo & tắt component trùng lặp (thường xảy ra khi setup tay + chạy tool SetupEnemyAI)
        var allAttacks = GetComponents<EnemyAttack>();
        if (allAttacks.Length > 1)
        {
            Debug.LogWarning($"[EnemyAttack] {name} có {allAttacks.Length} component EnemyAttack! Chỉ giữ lại cái đầu tiên, disable các cái còn lại.", this);
            for (int i = 1; i < allAttacks.Length; i++)
                allAttacks[i].enabled = false;
        }

        SetupAnimatorParameters();
        
        if (hitEffect == null)
            hitEffect = GetComponent<HitEffect>();
        
        if (attackPoint == null)
            attackPoint = transform;

        // Tự động đảm bảo stopRange < attackRange
        ValidateRanges();

        // Cấu hình NavMeshAgent nếu có
        SetupNavMeshAgent();

        FindPlayerTarget();
    }

    void Start()
    {
        if (playerTransform == null)
        {
            FindPlayerTarget();
        }
    }

    void OnValidate()
    {
        ValidateRanges();
    }

    private void ValidateRanges()
    {
        if (attackRange <= 0.2f) attackRange = 1.5f;
        if (stopRange >= attackRange)
        {
            stopRange = Mathf.Max(0.5f, attackRange * 0.75f);
        }
        if (detectionRange < attackRange)
        {
            detectionRange = attackRange * 2.5f;
        }
    }

    private void SetupAnimatorParameters()
    {
        if (_animator == null) return;

        attackHash = Animator.StringToHash("Attack");
        attackLowerHash = Animator.StringToHash("attack");
        moveSpeedHash = Animator.StringToHash("MoveSpeed");
        isMovingHash = Animator.StringToHash("IsMoving");
        isWalkingHash = Animator.StringToHash("isWalking");
        isRunningHash = Animator.StringToHash("isRunning");

        foreach (var param in _animator.parameters)
        {
            if (param.nameHash == attackHash) hasAttackParam = true;
            if (param.nameHash == attackLowerHash) hasAttackLowerParam = true;
            if (param.nameHash == moveSpeedHash) hasMoveSpeedParam = true;
            if (param.nameHash == isMovingHash) hasIsMovingParam = true;
            if (param.nameHash == isWalkingHash) hasIsWalkingParam = true;
            if (param.nameHash == isRunningHash) hasIsRunningParam = true;
        }
    }

    private void SetupNavMeshAgent()
    {
        if (_agent == null) return;

        _agent.speed = walkSpeed;
        _agent.stoppingDistance = stopRange;
        _agent.autoBraking = true;

        // Sync agent radius với CapsuleCollider để pathfinding khớp vật lý
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            _agent.radius = capsule.radius;
            _agent.height = capsule.height;
        }

        // Nếu agent chưa nằm trên NavMesh, kiểm tra sample NavMesh
        if (_agent.isOnNavMesh == false)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
            else
            {
                // Nếu scene chưa bake NavMesh, tắt tạm agent để fallback Rigidbody/Transform hoạt động trơn tru
                _agent.enabled = false;
            }
        }

        if (_rb != null && _agent != null && _agent.enabled)
        {
            _rb.freezeRotation = true;
        }
    }

    /// <summary>
    /// Tìm người chơi thông minh qua nhiều cách để đảm bảo luôn nhận diện được
    /// </summary>
    public void FindPlayerTarget()
    {
        if (playerTarget != null)
        {
            playerTransform = playerTarget;
            playerHealth = playerTarget.GetComponent<Health>();
            if (playerHealth == null) playerHealth = playerTarget.GetComponentInChildren<Health>();
            return;
        }

        // 1. Tìm theo tag "Player"
        GameObject playerObj = null;
        try
        {
            playerObj = GameObject.FindGameObjectWithTag("Player");
        }
        catch { /* Tag Player có thể chưa được khai báo */ }

        // 2. Tìm theo component Character_Movement (bỏ qua nếu gắn trên chính enemy)
        if (playerObj == null)
        {
            var charMovements = Object.FindObjectsByType<Character_Movement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var cm in charMovements)
            {
                if (cm.gameObject != this.gameObject)
                {
                    playerObj = cm.gameObject;
                    break;
                }
            }
        }

        // 3. Tìm theo Maincharacter_Bow (bỏ qua nếu gắn trên chính enemy)
        if (playerObj == null)
        {
            var bowChars = Object.FindObjectsByType<Maincharacter_Bow>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var bc in bowChars)
            {
                if (bc.gameObject != this.gameObject)
                {
                    playerObj = bc.gameObject;
                    break;
                }
            }
        }

        // 4. Tìm theo tên
        if (playerObj == null)
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (go != this.gameObject && (
                    go.name.Contains("Main_Character") || 
                    go.name.Contains("SSword_MainCharacter") || 
                    go.name.Contains("Bow_MainCharacter") ||
                    go.name.Equals("Player", System.StringComparison.OrdinalIgnoreCase)))
                {
                    playerObj = go;
                    break;
                }
            }
        }

        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
            playerTransform = playerObj.transform;
            playerHealth = playerObj.GetComponent<Health>();
            if (playerHealth == null) playerHealth = playerObj.GetComponentInChildren<Health>();
        }
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;

        // Nếu chưa tìm thấy player, thử tìm lại định kỳ mỗi 1 giây
        if (playerTransform == null)
        {
            searchPlayerTimer -= Time.deltaTime;
            if (searchPlayerTimer <= 0f)
            {
                searchPlayerTimer = 1f;
                FindPlayerTarget();
            }

            if (playerTransform == null)
            {
                SetState(EnemyState.Idle);
                UpdateAnimator();
                return;
            }
        }

        // Nếu player đã chết
        if (playerHealth != null && playerHealth.currentHP <= 0)
        {
            SetState(EnemyState.Idle);
            UpdateAnimator();
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        cooldownTimer -= Time.deltaTime;

        switch (currentState)
        {
            case EnemyState.Idle:
                // Người chơi bước vào phạm vi phát hiện -> bắt đầu đuổi theo
                if (distToPlayer <= detectionRange)
                {
                    SetState(EnemyState.Chasing);
                }
                break;

            case EnemyState.Chasing:
                // Người chơi chạy quá xa -> dừng đuổi
                if (distToPlayer > detectionRange * 1.25f)
                {
                    SetState(EnemyState.Idle);
                }
                // Người chơi trong tầm chém và hết cooldown -> chém
                else if (distToPlayer <= attackRange && cooldownTimer <= 0f)
                {
                    SetState(EnemyState.Attacking);
                }
                // Nếu đang hồi chiêu nhưng vẫn trong tầm dừng -> dừng lại và nhìn về player
                else if (distToPlayer <= stopRange)
                {
                    StopMoving();
                    FacePlayer();
                }
                // Đuổi theo người chơi
                else
                {
                    ChasePlayer(distToPlayer);
                }
                break;

            case EnemyState.Attacking:
                FacePlayer();
                attackTimer += Time.deltaTime;

                // Tự động gây sát thương nếu animation không có Event OnEnemyAttackHitFrame
                if (!hasDealtDamageThisSwing && attackTimer >= attackWindupTime)
                {
                    OnEnemyAttackHitFrame();
                }

                // Tự động kết thúc đòn đánh nếu animation không có Event OnEnemyAttackEnd (tránh kẹt đứng đơ vĩnh viễn)
                if (attackTimer >= attackDuration)
                {
                    OnEnemyAttackEnd();
                }
                break;

            case EnemyState.Recovering:
                // Nhìn về phía player trong lúc hồi chiêu
                FacePlayer();
                if (cooldownTimer <= 0f)
                {
                    if (distToPlayer <= detectionRange)
                        SetState(EnemyState.Chasing);
                    else
                        SetState(EnemyState.Idle);
                }
                break;
        }

        UpdateAnimator();
    }

    void ChasePlayer(float distToPlayer)
    {
        float targetSpeed = (useRunWhenFar && distToPlayer > runDistanceThreshold) ? runSpeed : walkSpeed;

        // Ưu tiên NavMeshAgent nếu đang hoạt động trên NavMesh
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.speed = Mathf.Lerp(_agent.speed, targetSpeed, Time.deltaTime * 5f);
            
            // QUAN TRỌNG: Luôn cập nhật stoppingDistance để tránh đè collider player (scene có thể lưu giá trị cũ = 0)
            // Thêm biên 0.1f để capsule collider không chạm nhau → tránh bị "hất" lên
            float desiredStopDist = Mathf.Max(stopRange, _agent.radius + 0.1f);
            if (Mathf.Abs(_agent.stoppingDistance - desiredStopDist) > 0.01f)
                _agent.stoppingDistance = desiredStopDist;
            
            _agent.SetDestination(playerTransform.position);
            FacePlayer();
            return;
        }

        // Fallback 1: Di chuyển qua Rigidbody
        Vector3 dir = (playerTransform.position - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            dir.Normalize();
            FacePlayer();

            if (_rb != null && !_rb.isKinematic)
            {
                Vector3 currentVel = _rb.linearVelocity;
                Vector3 targetVel = dir * targetSpeed;
                targetVel.y = currentVel.y; // Giữ nguyên trọng lực
                _rb.linearVelocity = Vector3.Lerp(currentVel, targetVel, Time.deltaTime * 8f);
            }
            else
            {
                // Fallback 2: Di chuyển trực tiếp qua Transform
                transform.position += dir * targetSpeed * Time.deltaTime;
            }
        }
    }

    void StopMoving()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        if (_rb != null && !_rb.isKinematic)
        {
            _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        }
    }

    void SetState(EnemyState newState)
    {
        if (currentState == newState) return;

        // Thoát state cũ
        if (currentState == EnemyState.Chasing)
        {
            StopMoving();
        }

        currentState = newState;

        // Vào state mới
        switch (newState)
        {
            case EnemyState.Idle:
                StopMoving();
                break;

            case EnemyState.Chasing:
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                }
                break;

            case EnemyState.Attacking:
                StartAttack();
                break;

            case EnemyState.Recovering:
                cooldownTimer = attackCooldown;
                StopMoving();
                break;

            case EnemyState.Dead:
                StopMoving();
                if (_agent != null) _agent.enabled = false;
                if (_rb != null) _rb.isKinematic = true;
                break;
        }
    }

    void StartAttack()
    {
        currentState = EnemyState.Attacking;
        hasDealtDamageThisSwing = false;
        attackTimer = 0f;
        cooldownTimer = attackCooldown;

        StopMoving();

        if (_animator != null)
        {
            // LƯU Ý: Sword_Anima.controller chỉ có transition AnyState -> Attack với trigger
            // 'attack' (viết thường). Trigger 'Attack' (viết hoa) tuy có parameter nhưng
            // KHÔNG transition nào lắng nghe -> bắn nó sẽ không tạo ra động tác vung kiếm.
            // Bắn cả hai để tương thích với mọi controller (cái không dùng sẽ bị bỏ qua).
            if (hasAttackLowerParam)
                _animator.SetTrigger(attackLowerHash);
            if (hasAttackParam)
                _animator.SetTrigger(attackHash);
        }

        // Âm thanh vung kiếm
        if (hitEffect != null)
        {
            hitEffect.PlaySwing();
        }
    }

    /// <summary>
    /// Gây sát thương tại thời điểm vung trúng (Animation Event hoặc auto-timer)
    /// </summary>
    public void OnEnemyAttackHitFrame()
    {
        if (currentState != EnemyState.Attacking) return;
        if (hasDealtDamageThisSwing) return;

        hasDealtDamageThisSwing = true;

        if (playerTransform != null)
        {
            Vector3 center = attackPoint ? attackPoint.position : (transform.position + transform.forward);
            float dist = Vector3.Distance(center, playerTransform.position);
            // Kiểm tra trong tầm tấn công (+ sai số nhỏ)
            if (dist <= attackRange + 0.5f)
            {
                DealDamage();
            }
        }
    }

    void DealDamage()
    {
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
        }

        // Tạo hiệu ứng chém trúng
        if (hitEffect != null && playerTransform != null)
        {
            Vector3 hitPos = playerTransform.position + Vector3.up * 1f;
            hitEffect.PlayHit(hitPos);
        }
    }

    /// <summary>
    /// Kết thúc đòn đánh (Animation Event hoặc auto-timer)
    /// </summary>
    public void OnEnemyAttackEnd()
    {
        if (currentState != EnemyState.Attacking) return;
        SetState(EnemyState.Recovering);
        hasDealtDamageThisSwing = false;
    }

    void FacePlayer()
    {
        if (playerTransform == null) return;
        Vector3 dir = (playerTransform.position - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    void UpdateAnimator()
    {
        if (_animator == null) return;

        bool isMoving = currentState == EnemyState.Chasing;
        float speed = 0f;
        if (isMoving)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                speed = _agent.velocity.magnitude;
            else if (_rb != null && !_rb.isKinematic)
                speed = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
            else
                speed = walkSpeed;
        }

        bool isRunning = isMoving && (speed > walkSpeed * 1.1f || (playerTransform != null && useRunWhenFar && Vector3.Distance(transform.position, playerTransform.position) > runDistanceThreshold));
        bool isWalking = isMoving && !isRunning;

        if (hasIsMovingParam) _animator.SetBool(isMovingHash, isMoving);
        if (hasMoveSpeedParam) _animator.SetFloat(moveSpeedHash, speed / Mathf.Max(0.1f, walkSpeed));
        if (hasIsWalkingParam) _animator.SetBool(isWalkingHash, isWalking || isMoving);
        if (hasIsRunningParam) _animator.SetBool(isRunningHash, isRunning);
    }

    public void TakeDamage(float dmg)
    {
        if (currentState == EnemyState.Dead) return;
        // Có thể bổ sung stun/knockback tại đây nếu cần
    }

    public void Die()
    {
        SetState(EnemyState.Dead);
        if (_animator != null)
        {
            _animator.SetTrigger("Die");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        // Vòng tròn phát hiện (Vàng)
        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Vòng tròn dừng lại (Cam)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, stopRange);

        // Vòng tròn tấn công (Đỏ)
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Vector3 center = attackPoint ? attackPoint.position : transform.position + transform.forward;
        Gizmos.DrawWireSphere(center, attackRange);

        // Vòng chuyển sang chạy (Xanh lá)
        if (useRunWhenFar)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, runDistanceThreshold);
        }

        // Vẽ đường NavMesh path
        if (drawPath && _agent != null && _agent.enabled && _agent.hasPath)
        {
            Gizmos.color = Color.cyan;
            var corners = _agent.path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                Gizmos.DrawLine(corners[i - 1], corners[i]);
            }
        }
    }
}