using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Shared health and exertion for the playable soldier in beachBoat.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class PlayerVitals : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float maxStamina = 100f;
        public float staminaRecoveryPerSecond = 16f;
        public float recoveryDelay = .8f;
        public float exhaustedRecoveryThreshold = 20f;
        public float Health { get; private set; }
        public float Stamina { get; private set; }
        public float HealthFraction => maxHealth > 0 ? Health / maxHealth : 0;
        public float StaminaFraction => maxStamina > 0 ? Stamina / maxStamina : 0;
        public bool IsExhausted { get; private set; }
        public int ArmorCharges { get; private set; }
        public string LastImpact { get; private set; }
        public float LastImpactDamage { get; private set; }
        public float LastImpactUntil { get; private set; }
        float lastEffortTime = float.NegativeInfinity;

        void Awake()
        {
            Health = Mathf.Max(1, maxHealth);
            Stamina = Mathf.Max(1, maxStamina);
        }

        void Update()
        {
            if (Time.time - lastEffortTime < recoveryDelay) return;
            Stamina = Mathf.MoveTowards(Stamina, maxStamina, staminaRecoveryPerSecond * Time.deltaTime);
            if (IsExhausted && Stamina >= exhaustedRecoveryThreshold) IsExhausted = false;
        }

        public bool TryExert(float costPerSecond, float dt)
        {
            if (IsExhausted || Stamina <= 0 || dt <= 0) return false;
            Stamina = Mathf.Max(0, Stamina - Mathf.Max(0, costPerSecond) * dt);
            lastEffortTime = Time.time;
            if (Stamina <= 0) IsExhausted = true;
            return true;
        }

        public void TakeImpact(float damage, string obstacle)
        {
            if (damage <= 0) return;
            if (ArmorCharges > 0)
            {
                damage *= .7f;
                ArmorCharges--;
            }
            Health = Mathf.Max(0, Health - damage);
            LastImpact = obstacle;
            LastImpactDamage = damage;
            LastImpactUntil = Time.unscaledTime + 2.4f;
        }

        public void RestAtBarracks()
        {
            Health = Mathf.Min(maxHealth, Health + 35f);
            Stamina = maxStamina;
            IsExhausted = false;
            lastEffortTime = float.NegativeInfinity;
        }

        public void ReinforceArmor()
        {
            ArmorCharges = 3;
        }

        public void TrainAtArmory()
        {
            maxStamina = Mathf.Max(maxStamina, 120f);
            Stamina = maxStamina;
            IsExhausted = false;
        }

        public void TakeProvisions()
        {
            Health = Mathf.Min(maxHealth, Health + 25f);
            Stamina = Mathf.Min(maxStamina, Stamina + 35f);
            if (Stamina >= exhaustedRecoveryThreshold) IsExhausted = false;
        }

        public void RecoverAtDock()
        {
            Health = Mathf.Clamp(maxHealth * .6f, 1f, maxHealth);
            Stamina = Mathf.Clamp(maxStamina * .35f, 1f, maxStamina);
            IsExhausted = false;
            lastEffortTime = Time.time;
            LastImpact = "Bị thương nặng — đã quay về bến";
            LastImpactDamage = 0;
            LastImpactUntil = Time.unscaledTime + 3.5f;
        }
    }
}
