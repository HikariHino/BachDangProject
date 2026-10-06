using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    public float attackRange = 1.8f;
    public int damage = 15;
    public float attackCooldown = 2f;
    public LayerMask playerLayer;
    public Animator animator; // optional, có thể null

    private float nextAttackTime;

    void Update()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        Vector3 attackCenter = transform.position + transform.forward * 1f;
        Collider[] hits = Physics.OverlapSphere(attackCenter, attackRange, playerLayer);

        foreach (Collider hit in hits)
        {
            Health health = hit.GetComponent<Health>();
            if (health == null)
            {
                health = hit.GetComponentInParent<Health>();
            }

            if (health != null)
            {
                if (animator != null)
                {
                    animator.SetTrigger("Attack");
                }
                health.TakeDamage(damage);
                Debug.Log($"{gameObject.name} chém {health.gameObject.name}: -{damage} HP.");
                GetComponent<HitEffect>()?.PlayHit(hit.transform.position + Vector3.up);
                nextAttackTime = Time.time + attackCooldown;
                break;
            }
        }
    }
}
