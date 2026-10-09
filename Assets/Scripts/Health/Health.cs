using System;
using UnityEngine;

/// <summary>
/// Component quản lý lượng máu cho nhân vật.
/// Hỗ trợ sự kiện khi nhận sát thương, hồi máu, tử vong.
/// </summary>
public class Health : MonoBehaviour
{
    [Header("Cấu hình Máu")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Hiển thị")]
    [SerializeField] private string characterName = "";

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float HealthPercent => maxHealth > 0 ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsDead => currentHealth <= 0f;
    public string CharacterName => string.IsNullOrEmpty(characterName) ? gameObject.name : characterName;

    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action<float> OnDamaged;              // (amount)
    public event Action<float> OnHealed;               // (amount)
    public event Action OnDied;

    private void Awake()
    {
        if (string.IsNullOrEmpty(characterName))
        {
            characterName = GenerateFriendlyName(gameObject.name);
        }
        if (currentHealth <= 0f)
        {
            currentHealth = maxHealth;
        }
    }

    private void Start()
    {
        // Kích hoạt sự kiện ban đầu để cập nhật UI
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public static string GenerateFriendlyName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Nhân vật";
        if (raw.StartsWith("NPC_")) raw = raw.Substring(4);
        if (raw.Contains("NgoQuyen") || raw.Contains("Ngo_Quyen")) return "Ngô Quyền";
        if (raw.Contains("OldPeasent") || raw.Contains("DanLang_0")) return "Lão Nông";
        if (raw.Contains("Female_Pea") || raw.Contains("DanLang_1") || raw.Contains("FemalePea")) return "Nữ Dân Làng";
        if (raw.Contains("Blue_Scrub") || raw.Contains("DanLang_2") || raw.Contains("MalePea")) return "Dân Làng";
        if (raw.Contains("Disciple") || raw.Contains("Young_Disciple")) return "Võ Sinh";
        if (raw.Contains("Bow_MainCharacter")) return "Xạ Thủ";
        if (raw.Contains("SSword_MainCharacter")) return "Kiếm Sĩ";
        if (raw.Contains("Main_Character") || raw.Contains("MainCharacter")) return "Tướng Quân";
        if (raw.Contains("Test_NPC")) return "Chiến Binh";
        return raw;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke(amount);

        if (currentHealth <= 0f)
        {
            OnDied?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealed?.Invoke(amount);
    }

    public void SetHealth(float current, float max)
    {
        maxHealth = Mathf.Max(1f, max);
        currentHealth = Mathf.Clamp(current, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetCharacterName(string newName)
    {
        characterName = newName;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // Các hàm test tiện lợi trong Editor Inspector
    [ContextMenu("Test - Trừ 20 Máu")]
    public void TestDamage20() => TakeDamage(20f);

    [ContextMenu("Test - Hồi 20 Máu")]
    public void TestHeal20() => Heal(20f);

    [ContextMenu("Test - Trừ 50% Máu")]
    public void TestDamage50Percent() => TakeDamage(maxHealth * 0.5f);

    [ContextMenu("Test - Hạ Gục")]
    public void TestKill() => TakeDamage(currentHealth);

    [ContextMenu("Test - Hồi Đầy Máu")]
    public void TestReviveFull()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
