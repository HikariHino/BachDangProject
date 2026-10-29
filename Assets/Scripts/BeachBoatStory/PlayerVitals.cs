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
        public int CompletedTrainingLevels { get; private set; }
        public int HealingItems {get;private set;}=3;
        public string HealingFeedback {get;private set;}="";
        public float HealingFeedbackUntil {get;private set;}
        public const float HealingPerItem=35;
        float nextHealing;
        public bool UseHealingItem()
        {
            if(BeachBoatQuestSession.InputBlocked||Time.timeScale<=0||Health<=0||Time.time<nextHealing)return false;
            HealingFeedback=Health>=maxHealth-.01f?"Máu đã đầy":HealingItems<=0?"Hết thuốc • Nhận thêm tại quân y":"+35 máu";
            HealingFeedbackUntil=Time.unscaledTime+2;
            if(Health>=maxHealth-.01f||HealingItems<=0)return false;
            float amount=Mathf.Min(HealingPerItem,maxHealth-Health);Health+=amount;HealingItems--;nextHealing=Time.time+.75f;HealingFeedback=$"+{amount:0} máu";BeachBoatSoundscape.Instance?.PlayInteraction();return true;
        }
        public void RestockHealingItems(){HealingItems=Mathf.Max(HealingItems,3);}
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
            if(Input.GetKeyDown(KeyCode.H))UseHealingItem();
            if(CampRecoveryGame.Active!=null)return;
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

        public void TakeImpact(float damage, string obstacle,CombatImpactKind kind=CombatImpactKind.Other)
        {
            if (damage <= 0 || Health <= 0) return;
            if (ArmorCharges > 0)
            {
                damage *= .7f;
                ArmorCharges--;
            }
            Health = Mathf.Max(0, Health - damage);
            LastImpact = obstacle;
            LastImpactDamage = damage;
            if(kind==CombatImpactKind.Other)BeachBoatSoundscape.Instance?.PlayHit();
            else CombatHitFeedback.Show(transform.position+Vector3.up*10,kind,true);
            LastImpactUntil = Time.unscaledTime + 2.4f;
            if (Health <= 0) QuestCheckpoint.Instance?.Defeat();
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

        public bool TrainAtArmory(int level)
        {
            if(level<1||level>3||level>CompletedTrainingLevels+1)return false;
            if(level==CompletedTrainingLevels+1){CompletedTrainingLevels++;maxStamina+=20f;}
            Stamina = maxStamina;
            IsExhausted = false;
            return true;
        }

        public void TakeProvisions()
        {
            Health = Mathf.Min(maxHealth, Health + 25f);
            Stamina = Mathf.Min(maxStamina, Stamina + 35f);
            if (Stamina >= exhaustedRecoveryThreshold) IsExhausted = false;
        }

        public void TreatAtMedicalTent()
        {
            Health = maxHealth;
        }
        public float ReportRecoveryUntil {get;private set;}
        public void RecoverAfterReport()
        {
            // Only the guarded story report callbacks grant this recovery.
            // Keep armor charges and earned stamina/training upgrades.
            RestoreCheckpoint(ArmorCharges);ReportRecoveryUntil=Time.unscaledTime+4;
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
        public void RestoreCheckpoint(int armor)
        {
            Health=maxHealth;Stamina=maxStamina;ArmorCharges=Mathf.Max(0,armor);IsExhausted=false;
            lastEffortTime=float.NegativeInfinity;LastImpact=null;LastImpactDamage=0;LastImpactUntil=0;
        }
        public void RestoreSave(BachDangSaveData data)
        {
            HealingItems=data.healingItemsVersion>0?Mathf.Clamp(data.healingItems,0,5):3;nextHealing=0;HealingFeedbackUntil=0;
            ReportRecoveryUntil=0;
            maxHealth=Mathf.Max(1,data.maxHealth);maxStamina=Mathf.Max(1,data.maxStamina);
            Health=Mathf.Clamp(data.health,1,maxHealth);Stamina=Mathf.Clamp(data.stamina,0,maxStamina);
            ArmorCharges=Mathf.Max(0,data.armor);CompletedTrainingLevels=Mathf.Clamp(data.trainingLevels,0,3);
            IsExhausted=Stamina<=0;lastEffortTime=Time.time;LastImpact=null;LastImpactUntil=0;
        }
    }
}
