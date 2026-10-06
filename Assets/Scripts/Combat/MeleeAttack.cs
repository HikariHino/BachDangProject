using UnityEngine;

public class MeleeAttack : MonoBehaviour
{
    public Animator animator;
    public float attackRange = 1.8f;
    public int damage = 20;
    public LayerMask enemyLayer;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Attack();
        }
    }

    public void Attack()
    {
        animator.SetTrigger("Attack");

        Vector3 attackCenter = transform.position + transform.forward * 1f;
        Collider[] hits = Physics.OverlapSphere(attackCenter, attackRange, enemyLayer);

        foreach (Collider hit in hits)
        {
            // Tìm Health trên chính collider đó hoặc ở object cha.
            Health health = hit.GetComponent<Health>();
            if (health == null)
            {
                health = hit.GetComponentInParent<Health>();
            }

            if (health != null)
            {
                health.TakeDamage(damage);
                Debug.Log($"Đã trúng mục tiêu: {health.gameObject.name}");
                HitEffect hitEffect = GetComponent<HitEffect>();
                hitEffect?.PlayHit(hit.transform.position + Vector3.up);
            }
        }
    }
}
