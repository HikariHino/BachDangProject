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

    public void TakeDamage(int amount)
    {
        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        Debug.Log($"{gameObject.name} còn {currentHP}/{maxHP} HP.");

        if (currentHP <= 0)
        {
            StopAllCoroutines();
            StartCoroutine(DieRoutine());
        }
    }

    IEnumerator DieRoutine()
    {
        Debug.Log($"{gameObject.name} đã bị tiêu diệt.");

        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (animator != null)
        {
            animator.enabled = false;
        }

        // Tắt CharacterController nếu có (không dùng ?.enabled trên MonoBehaviour cho class này).
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = GetComponentInChildren<CharacterController>();
        }
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Tắt các script điều khiển/tấn công nếu có: KHÔNG tắt collider để thân không xuyên plane.
        MonoBehaviour[] scripts = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script == this)
            {
                continue;
            }
            string typeName = script.GetType().Name;
            if (typeName == "Character_Movement" || typeName == "MeleeAttack" || typeName == "EnemyAttack")
            {
                script.enabled = false;
            }
        }

        // Rigidbody: set kinematic, không cho rơi tự do.
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

        // Nếu có trigger "Die" trong Animator, dùng animation chết có sẵn thay vì xoay bằng code.
        bool useDieTrigger = false;
        if (animator != null)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger && param.name == "Die")
                {
                    useDieTrigger = true;
                    break;
                }
            }
        }

        if (useDieTrigger)
        {
            animator.enabled = true;
            animator.SetTrigger("Die");
            // Chờ animation chết chạy xong rồi chìm/destroy
            yield return new WaitForSeconds(2f);
        }
        else
        {
            // Xoay bằng code (fallback): dùng ease-in-out để mượt hơn
            float duration = 0.8f;
            float elapsed = 0f;
            Quaternion startRotation = transform.localRotation;
            Quaternion targetRotation = startRotation * Quaternion.Euler(0f, 0f, 90f);
            while (elapsed < duration)
            {
                if (this == null || gameObject == null)
                {
                    yield break;
                }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // EaseInOutQuad
                float eased = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                yield return null;
            }

            // Chờ 1.5s rồi chìm xuống.
            float waitTime = 1.5f;
            float waited = 0f;
            while (waited < waitTime)
            {
                if (this == null || gameObject == null)
                {
                    yield break;
                }
                waited += Time.deltaTime;
                yield return null;
            }
        }

        float sinkDuration = 1f;
        float sinkElapsed = 0f;
        while (sinkElapsed < sinkDuration)
        {
            if (this == null || gameObject == null)
            {
                yield break;
            }
            sinkElapsed += Time.deltaTime;
            transform.position -= Vector3.up * 0.5f * Time.deltaTime;
            yield return null;
        }

        Debug.Log($"{gameObject.name} gục ngã và biến mất.");

        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }
}
