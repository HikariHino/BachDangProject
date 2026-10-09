using UnityEngine;

/// <summary>
/// Component xử lý gây sát thương khi vung kiếm / chém cận chiến.
/// Tự động dò tìm đối tượng có Health trong phạm vi phía trước nhân vật và trừ máu.
/// </summary>
public class MeleeCombat : MonoBehaviour
{
    [Header("Cấu hình sát thương chém")]
    [SerializeField] private float lightAttackDamage = 25f;
    [SerializeField] private float heavyAttackDamage = 45f;
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Thời điểm gây sát thương")]
    [SerializeField] private float hitDelay = 0.2f; // Trễ nhẹ đồng bộ theo animation vung kiếm

    private Health selfHealth;

    private void Awake()
    {
        selfHealth = GetComponent<Health>();
    }

    private void Update()
    {
        // Chuột trái: Chém thường
        if (Input.GetMouseButtonDown(0))
        {
            CancelInvoke(nameof(ExecuteLightAttack));
            Invoke(nameof(ExecuteLightAttack), hitDelay);
        }
        // Chuột phải: Chém mạnh
        else if (Input.GetMouseButtonDown(1))
        {
            CancelInvoke(nameof(ExecuteHeavyAttack));
            Invoke(nameof(ExecuteHeavyAttack), hitDelay);
        }
    }

    [ContextMenu("Test Chém Phía Trước (25 DMG)")]
    public void ExecuteLightAttack()
    {
        PerformSlash(lightAttackDamage, "Chém thường");
    }

    [ContextMenu("Test Chém Phía Trước (45 DMG)")]
    public void ExecuteHeavyAttack()
    {
        PerformSlash(heavyAttackDamage, "Chém mạnh");
    }

    public int PerformSlash(float damage, string attackType = "Chém")
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 attackCenter = origin + transform.forward * attackRange;
        Collider[] hits = Physics.OverlapSphere(attackCenter, attackRadius, hitLayers);

        int hitCount = 0;
        foreach (var col in hits)
        {
            // Bỏ qua bản thân người chém
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            Health target = col.GetComponentInParent<Health>();
            if (target == null) target = col.GetComponent<Health>();

            if (target != null && !target.IsDead && target != selfHealth)
            {
                target.TakeDamage(damage);
                hitCount++;
                Debug.Log($"<color=red>[CHIẾN ĐẤU]</color> {gameObject.name} dùng [{attackType}] trúng <b>{target.CharacterName}</b>! " +
                          $"Gây <b>{damage}</b> sát thương. Máu còn: <b>{target.CurrentHealth}/{target.MaxHealth}</b>");
            }
        }

        return hitCount;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Vector3 center = transform.position + Vector3.up * 1f + transform.forward * attackRange;
        Gizmos.DrawWireSphere(center, attackRadius);
    }
}
