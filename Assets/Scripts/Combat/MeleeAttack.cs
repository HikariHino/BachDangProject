using UnityEngine;
using System.Collections;

/// <summary>
/// Charge-based melee attack with range gating and single-effect-per-swing guarantee.
/// Fixes: "attack range too far", "multiple effects per swing", "attack not keeping up"
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider))]
public class MeleeAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [Tooltip("Maximum distance to hit enemies")]
    public float attackRange = 1.5f;
    
    [Tooltip("Damage at zero charge")]
    public float minDamage = 10f;
    
    [Tooltip("Damage at full charge")]
    public float maxDamage = 30f;
    
    [Tooltip("Seconds to reach full charge")]
    public float maxChargeTime = 1.5f;
    
    [Tooltip("Cooldown between attacks")]
    public float attackCooldown = 0.5f;
    
    [Tooltip("Minimum charge to register as charged attack (0-1)")]
    [Range(0f, 1f)] public float minChargeThreshold = 0.2f;

    [Header("References")]
    [Tooltip("Transform at weapon tip (where hit detection originates)")]
    public Transform attackPoint;
    
    [Tooltip("Layer containing enemies")]
    public LayerMask enemyLayer;
    
    [Tooltip("HitEffect component for visual/sound feedback")]
    public HitEffect hitEffect;

    // Backward compatibility for Editor scripts
    [HideInInspector] public Animator animator 
    { 
        get => GetComponent<Animator>(); 
        set { /* Set by Editor script; auto-assigned in Awake */ } 
    }
    [HideInInspector] public float damage { get => maxDamage; set => maxDamage = minDamage = value; }

    [Header("Debug")]
    public bool showGizmos = true;

    // Components
    private Animator _animator;
    private HitEffect hitEffectComp;

    // State machine
    private enum AttackPhase { None, Charging, Committed, Recovering }
    private AttackPhase currentPhase = AttackPhase.None;

    // Charge tracking
    private float chargeTimer = 0f;
    private float currentChargeNormalized => Mathf.Clamp01(chargeTimer / maxChargeTime);
    
    // Gates
    private bool hasSpawnedEffectThisSwing = false;
    private bool isAttackInputHeld = false;
    
    // Cached animator hashes
    private int attackHash;
    private int chargePowerHash;
    private int attackWhiffHash;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        hitEffectComp = GetComponent<HitEffect>();
        
        // Cache animator parameter hashes for performance
        attackHash = Animator.StringToHash("Attack");
        chargePowerHash = Animator.StringToHash("ChargePower");
        attackWhiffHash = Animator.StringToHash("AttackWhiff");
        
        // Auto-find attack point if not assigned
        if (attackPoint == null)
        {
            var weapon = transform.Find("Weapon") ?? transform.Find("Sword") ?? transform.Find("Blade");
            if (weapon != null) attackPoint = weapon;
            else attackPoint = transform; // Fallback to root
        }
    }

    void Update()
    {
        HandleInput();
        UpdateChargeVisual();
    }

    void HandleInput()
    {
        // --- CHARGING: Hold Left Mouse ---
        if (Input.GetMouseButton(0))
        {
            if (currentPhase == AttackPhase.None && !isAttackInputHeld)
            {
                // Start new charge
                currentPhase = AttackPhase.Charging;
                chargeTimer = 0f;
                isAttackInputHeld = true;
            }
            else if (currentPhase == AttackPhase.Charging)
            {
                // Continue charging
                chargeTimer += Time.deltaTime;
            }
        }
        
        // --- RELEASE: Commit or Cancel ---
        if (Input.GetMouseButtonUp(0))
        {
            isAttackInputHeld = false;
            
            if (currentPhase == AttackPhase.Charging)
            {
                // Player released while charging → commit attack
                TryCommitAttack();
            }
        }
        
        // --- CANCEL: Right mouse ---
        if (Input.GetMouseButtonDown(1) && currentPhase == AttackPhase.Charging)
        {
            CancelCharge();
        }
    }

    void TryCommitAttack()
    {
        // RANGE CHECK: Only commit if enemy in range
        Vector3 center = attackPoint ? attackPoint.position : transform.position + transform.forward;
        Collider[] hits = Physics.OverlapSphere(center, attackRange, enemyLayer);
        
        // Play swing sound immediately when attack animation starts (both hit and whiff)
        if (hitEffectComp != null)
        {
            hitEffectComp.PlaySwing();
        }
        
        if (hits.Length == 0)
        {
            // No enemy → play whiff animation, no effects
            currentPhase = AttackPhase.Recovering;
            _animator.SetTrigger(attackWhiffHash);
            StartCoroutine(RecoveryRoutine());
            return;
        }
        
        // Enemy in range → commit real attack
        currentPhase = AttackPhase.Committed;
        hasSpawnedEffectThisSwing = false; // Reset gate
        
        // Pass charge power to animator for animation blending
        _animator.SetFloat(chargePowerHash, currentChargeNormalized);
        _animator.SetTrigger(attackHash);
        
        StartCoroutine(RecoveryRoutine());
    }

    void CancelCharge()
    {
        currentPhase = AttackPhase.None;
        chargeTimer = 0f;
    }

    IEnumerator RecoveryRoutine()
    {
        yield return new WaitForSeconds(attackCooldown);
        currentPhase = AttackPhase.None;
        chargeTimer = 0f;
    }

    void UpdateChargeVisual()
    {
        if (currentPhase == AttackPhase.Charging)
        {
            // Optional: Update charge UI, particle size, sword glow
            // Example: chargeParticleSystem.startSize = Mathf.Lerp(0.5f, 2f, currentChargeNormalized);
        }
    }

    /// <summary>
    /// Called by Animation Event at the exact hit frame of the Attack animation.
    /// This ensures ONE effect per swing, synchronized with visuals.
    /// </summary>
    public void OnAttackHitFrame()
    {
        // GATE 1: Only during committed attack phase
        if (currentPhase != AttackPhase.Committed) return;
        
        // GATE 2: Only once per swing
        if (hasSpawnedEffectThisSwing) return;
        hasSpawnedEffectThisSwing = true;
        
        // RE-CHECK RANGE at hit frame (enemy may have moved)
        Vector3 center = attackPoint ? attackPoint.position : transform.position + transform.forward;
        Collider[] hits = Physics.OverlapSphere(center, attackRange, enemyLayer);
        
        foreach (var hit in hits)
        {
            // Calculate damage based on charge
            float damage = Mathf.Lerp(minDamage, maxDamage, currentChargeNormalized);
            
            // Apply damage
            var health = hit.GetComponent<Health>();
            if (health == null)
            {
                health = hit.GetComponentInParent<Health>();
            }
            
            if (health != null)
            {
                health.TakeDamage(damage);
            }
            
            // Spawn hit effect at impact point
            if (hitEffectComp != null)
            {
                Vector3 hitPos = hit.ClosestPoint(center);
                hitEffectComp.PlayHit(hitPos + Vector3.up);
            }
        }
    }

    /// <summary>
    /// Called by Animation Event at the end of Attack/Whiff animation.
    /// </summary>
    public void OnAttackEnd()
    {
        currentPhase = AttackPhase.Recovering;
        hasSpawnedEffectThisSwing = false;
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        Gizmos.color = currentPhase == AttackPhase.Charging ? Color.yellow : Color.red;
        Vector3 center = attackPoint ? attackPoint.position : transform.position + transform.forward;
        Gizmos.DrawWireSphere(center, attackRange);
        Gizmos.DrawRay(center, transform.forward * attackRange);
    }

    // Public API for other scripts (UI, etc.)
    public bool IsCharging => currentPhase == AttackPhase.Charging;
    public float ChargeProgress => currentChargeNormalized;
    public bool CanAttack => currentPhase == AttackPhase.None;
}