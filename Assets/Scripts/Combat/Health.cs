using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHP = 100;
    public int currentHP;

    void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        int damage = Mathf.RoundToInt(amount);
        currentHP -= damage;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        Debug.Log($"{gameObject.name} còn {currentHP}/{maxHP} HP.");

        if (currentHP <= 0)
        {
            StopAllCoroutines();
            StartCoroutine(DieRoutine());
        }
    }

    [Header("Death Settings")]
    [Tooltip("Bật để tự động ngã gục xuống sàn bằng code (dùng khi FBX chưa có animation ngã thật sự)")]
    public bool forcePhysicsDeath = true;

    IEnumerator DieRoutine()
    {
        Debug.Log($"{gameObject.name} đã bị tiêu diệt.");

        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // Tắt CharacterController nếu có
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = GetComponentInChildren<CharacterController>();
        }
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Tắt các script điều khiển/tấn công
        MonoBehaviour[] scripts = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script == this)
            {
                continue;
            }
            string typeName = script.GetType().Name;
            if (typeName == "Character_Movement" || typeName == "MeleeAttack" || typeName == "EnemyAttack" || typeName == "EnemyAI" || typeName == "UnityEngine.AI.NavMeshAgent")
            {
                script.enabled = false;
            }
        }

        // Tắt NavMeshAgent
        var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent == null) navAgent = GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        // Rigidbody: set kinematic, không cho rơi tự do
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = GetComponentInChildren<Rigidbody>();
        }
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Phát âm thanh chết (nếu có HitEffect component)
        var hitEffect = GetComponent<HitEffect>();
        if (hitEffect == null) hitEffect = GetComponentInChildren<HitEffect>();
        if (hitEffect != null)
        {
            hitEffect.PlayDeath();
        }

        // Kiểm tra xem có dùng trigger "Die" từ Animator không
        bool hasDieTrigger = false;
        if (!forcePhysicsDeath && animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger && param.name == "Die")
                {
                    hasDieTrigger = true;
                    break;
                }
            }
        }

        if (hasDieTrigger)
        {
            // Dành cho khi người dùng đã import animation ngã thật (Death FBX từ Mixamo)
            animator.enabled = true;
            animator.ResetTrigger("Die");
            animator.SetTrigger("Die");

            // Chờ animation chết chạy xong
            yield return new WaitForSeconds(2.5f);
        }
        else
        {
            // NGÃ GỤC VẬT LÝ BẰNG CODE (Không bị lún đất, ngã nằm phẳng trên mặt sàn Plane)
            if (animator != null)
            {
                animator.enabled = false;
            }

            // Đo độ cao mặt đất chính xác bằng Raycast
            float groundY = transform.position.y;
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 5f))
            {
                groundY = hit.point.y;
            }

            float duration = 0.5f;
            float elapsed = 0f;
            Quaternion startRot = transform.rotation;
            // Ngã ngửa ra phía sau (-85 độ trục X)
            Quaternion targetRot = startRot * Quaternion.Euler(-85f, 0f, 0f);

            Vector3 startPos = transform.position;
            // Lưng nằm sát mặt đất (cao hơn groundY khoảng 0.18m để không lún vào sàn)
            Vector3 targetPos = new Vector3(startPos.x, groundY + 0.18f, startPos.z) - transform.forward * 0.45f;

            while (elapsed < duration)
            {
                if (this == null || gameObject == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t); // Smoothstep

                transform.rotation = Quaternion.Slerp(startRot, targetRot, eased);
                transform.position = Vector3.Lerp(startPos, targetPos, eased);
                yield return null;
            }

            // Xác nằm yên trên mặt đất 2 giây
            yield return new WaitForSeconds(2.0f);
        }

        // Hiệu ứng biến mất: thu nhỏ dần rồi hủy (KHÔNG dìm tọa độ xuyên sàn)
        float shrinkDuration = 0.6f;
        float shrinkElapsed = 0f;
        Vector3 initialScale = transform.localScale;
        while (shrinkElapsed < shrinkDuration)
        {
            if (this == null || gameObject == null) yield break;
            shrinkElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(shrinkElapsed / shrinkDuration);
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);
            yield return null;
        }

        Debug.Log($"{gameObject.name} đã tan biến.");

        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }
}
