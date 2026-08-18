using _Project.CodeBase;
using Fusion;
using UnityEngine;

namespace Gameplay.Combat
{
    public class NetworkHealth : NetworkBehaviour
    {
        /// <summary>
        /// Сколько попаданий помнит буфер. Если за один тик прилетит
        /// больше — самые старые потеряются. 16 хватает на дробовик
        /// с запасом; для оружия с 12+ дробинами подними до 32.
        /// </summary>
        private const int DamageLogCapacity = 16;
 
        [Header("Stats")]
        [SerializeField] private float maxHealth = 100f;
 
        [Tooltip("Стартовая броня. 0 = без брони (для манекенов).")]
        [SerializeField] private float startingArmor = 100f;
 
        [Header("Popup Feedback")]
        [Tooltip("Задержка между цифрами при множественном попадании. " +
                 "Даёт ощущение очереди вместо кляксы.")]
        [SerializeField] private float popupStagger = 0.03f;
 
        [Header("Refs")]
        [SerializeField] private HealthBar healthBar;
        [SerializeField] private HealthBar armorBar;
 
        // ---- Networked ----
 
        [Networked, OnChangedRender(nameof(OnHealthChanged))]
        public float CurrentHealth { get; private set; }
 
        [Networked, OnChangedRender(nameof(OnArmorChanged))]
        public float CurrentArmor { get; private set; }
 
        /// <summary>Кольцевой буфер попаданий.</summary>
        [Networked, Capacity(DamageLogCapacity)]
        private NetworkArray<DamageEntry> DamageLog { get; }
 
        /// <summary>
        /// Общее число попаданий за жизнь объекта. Только растёт.
        /// Клиент по разнице понимает какие записи ещё не показаны.
        /// </summary>
        [Networked, OnChangedRender(nameof(OnDamageEvent))]
        private int DamageCount { get; set; }
 
        // ---- Local ----
 
        public float MaxHealth { get; private set; }
        public float MaxArmor { get; private set; }
 
        private Player owner;
        private HurtVisual hurtVisual;
        private DamagePopupSpawner damagePopup;
 
        /// <summary>Сколько записей уже проиграно на этом клиенте.</summary>
        private int _shownDamageCount;
 
        // =========================================================
        // Lifecycle
        // =========================================================
 
        public override void Spawned()
        {
            owner = GetComponent<Player>();
 
            hurtVisual = GetComponentInChildren<HurtVisual>(true);
            damagePopup = GetComponentInChildren<DamagePopupSpawner>(true);
 
            MaxHealth = Mathf.Max(1f, maxHealth);
 
            // MaxArmor не должен быть 0 — на него делят при отрисовке полоски
            MaxArmor = Mathf.Max(1f, startingArmor);
 
            if (HasStateAuthority)
            {
                CurrentHealth = MaxHealth;
                CurrentArmor = startingArmor;
            }
 
            // Не проигрываем историю попаданий, случившихся до спавна
            _shownDamageCount = DamageCount;
 
            healthBar?.Init(this);
            armorBar?.Init(this);
 
            armorBar?.SetVisible(CurrentArmor > 0f);
        }
 
        // =========================================================
        // Сервер: приём урона
        // =========================================================
 
        public void ApplyDamage(float damage, Player attacker, Vector3 hitPoint, bool isCrit = false)
        {
            if (!HasStateAuthority)
                return;
 
            if (CurrentHealth <= 0f)
                return;
 
            float dmgLeft = Mathf.Max(0f, damage);
 
            float dmgToArmor = 0f;
            float dmgToHealth = 0f;
 
            // 1) Сначала броня
            if (CurrentArmor > 0f && dmgLeft > 0f)
            {
                dmgToArmor = Mathf.Min(CurrentArmor, dmgLeft);
                CurrentArmor = Mathf.Max(0f, CurrentArmor - dmgToArmor);
                dmgLeft -= dmgToArmor;
            }
 
            // 2) Остаток в здоровье
            if (dmgLeft > 0f)
            {
                dmgToHealth = Mathf.Min(CurrentHealth, dmgLeft);
                CurrentHealth = Mathf.Max(0f, CurrentHealth - dmgToHealth);
            }
 
            WriteDamageEntry(damage, dmgToArmor, dmgToHealth, hitPoint, isCrit);
 
            if (CurrentHealth == 0f)
                HandleDeath(attacker);
        }
 
        private void WriteDamageEntry(
            float damage, float toArmor, float toHealth, Vector3 point, bool isCrit)
        {
            DamageLog.Set(DamageCount % DamageLogCapacity, new DamageEntry
            {
                Damage = damage,
                ToArmor = toArmor,
                ToHealth = toHealth,
                Point = point,
                Flags = (byte)(isCrit ? 1 : 0)
            });
 
            DamageCount++;
        }
 
        private void HandleDeath(Player attacker)
        {
            if (attacker != null && attacker != owner)
                attacker.AddKill();
 
            // owner == null у неигровых целей (манекены, разрушаемые объекты)
            if (owner == null)
                return;
 
            owner.IsVisible = false;
 
            PlayerRef killerRef = attacker != null
                ? attacker.Object.InputAuthority
                : PlayerRef.None;
 
            MatchEventBus.Instance?.Raise(
                new PlayerDiedEvent(
                    Object.InputAuthority,
                    killerRef,
                    Runner.Tick
                )
            );
        }
 
        public void ResetHealth()
        {
            if (!HasStateAuthority)
                return;
 
            CurrentHealth = MaxHealth;
 
            // startingArmor, а не MaxArmor: MaxArmor поднят до минимума 1
            // чтобы не делить на ноль, и для безбронных целей это не 0
            CurrentArmor = startingArmor;
        }
 
        // =========================================================
        // Клиент: визуальная реакция
        // =========================================================
 
        private void OnHealthChanged()
        {
            healthBar?.UpdateBar();
        }
 
        private void OnArmorChanged()
        {
            armorBar?.UpdateBar();
            armorBar?.SetVisible(CurrentArmor > 0f);
        }
 
        /// <summary>
        /// Проигрывает ВСЕ записи, появившиеся с прошлого снапшота.
        /// За один тик их может быть много (дробовик + чужая очередь),
        /// и каждая получает свою цифру с точным значением.
        /// </summary>
        private void OnDamageEvent()
        {
            int total = DamageCount;
 
            // Если отстали больше чем на размер буфера — старое потеряно,
            // начинаем с самой ранней доступной записи
            int start = Mathf.Max(_shownDamageCount, total - DamageLogCapacity);
 
            if (start >= total)
                return;
 
            float sumToHealth = 0f;
            float sumToArmor = 0f;
            bool anyCrit = false;
 
            for (int i = start; i < total; i++)
            {
                DamageEntry entry = DamageLog.Get(i % DamageLogCapacity);
 
                float delay = (i - start) * popupStagger;
                damagePopup?.Pop(entry.Damage, entry.Point, entry.IsCrit, delay);
 
                sumToHealth += entry.ToHealth;
                sumToArmor += entry.ToArmor;
                anyCrit |= entry.IsCrit;
            }
 
            _shownDamageCount = total;
 
            // Полоски дёргаем один раз на суммарный урон,
            // иначе девять дробин дадут девять анимаций подряд
            if (sumToHealth > 0f)
                healthBar?.PlayDamageFeedback(sumToHealth, anyCrit);
 
            if (sumToArmor > 0f)
                armorBar?.PlayDamageFeedback(sumToArmor, anyCrit);
 
            hurtVisual?.PlayHurt(anyCrit);
        }
    }
}
