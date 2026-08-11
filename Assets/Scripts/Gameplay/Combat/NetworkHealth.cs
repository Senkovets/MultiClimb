using _Project.CodeBase;
using Fusion;
using UnityEngine;

namespace Gameplay.Combat
{
    public class NetworkHealth : NetworkBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float maxHealth = 100f;
 
        [Tooltip("Стартовая броня. 0 = без брони (для манекенов).")]
        [SerializeField] private float startingArmor = 100f;
    
        [Networked, OnChangedRender(nameof(OnHealthChanged))]
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }

        [Networked, OnChangedRender(nameof(OnArmorChanged))]
        public float CurrentArmor { get; private set; }
        public float MaxArmor { get; private set; }

        [SerializeField] private HealthBar healthBar;
        [SerializeField] private HealthBar armorBar;

        private Player owner;
        private HurtVisual hurtVisual;
        private DamagePopupSpawner damagePopup;

        // "������� �����" (������ ��� ����������� seq)
        [Networked, OnChangedRender(nameof(OnDamageEvent))] private int DamageSeq { get; set; }

        // Payload ��� �������
        [Networked] private float LastDamageTotal { get; set; }      // �������� ���� (��� ���������)
        [Networked] private float LastDamageToArmor { get; set; }    // ������� ������� ���� � �����
        [Networked] private float LastDamageToHealth { get; set; }   // ������� ������� ���� � HP
        [Networked] private byte LastWasCrit { get; set; }           // 0/1
        [Networked] private Vector3 LastHitPoint { get; set; }

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
 
            healthBar?.Init(this);
            armorBar?.Init(this);
 
            armorBar?.SetVisible(CurrentArmor > 0f);
        }

        public void ApplyDamage(float damage, Player attacker, Vector3 hitPoint, bool isCrit = false)
        {
            if (!HasStateAuthority)
                return;

            if (CurrentHealth <= 0f)
                return;

            float dmgLeft = Mathf.Max(0f, damage);

            float dmgToArmor = 0f;
            float dmgToHealth = 0f;

            // 1) �����
            if (CurrentArmor > 0f && dmgLeft > 0f)
            {
                dmgToArmor = Mathf.Min(CurrentArmor, dmgLeft);
                CurrentArmor = Mathf.Max(0f, CurrentArmor - dmgToArmor);
                dmgLeft -= dmgToArmor;
            }

            // 2) ��
            if (dmgLeft > 0f)
            {
                dmgToHealth = Mathf.Min(CurrentHealth, dmgLeft);
                CurrentHealth = Mathf.Max(0f, CurrentHealth - dmgToHealth);
            }

            // Payload ��� �������� (��, ��� ������� ���������)
            LastDamageTotal = damage;
            LastDamageToArmor = dmgToArmor;
            LastDamageToHealth = dmgToHealth;
            LastWasCrit = (byte)(isCrit ? 1 : 0);
            LastHitPoint = hitPoint;

            DamageSeq++;

            if (CurrentHealth == 0f)
                HandleDeath(attacker);
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
            CurrentArmor = MaxArmor;
        }

        private void OnHealthChanged()
        {
            healthBar?.UpdateBar();
        }

        private void OnArmorChanged()
        {
            armorBar?.UpdateBar();
            armorBar?.SetVisible(CurrentArmor > 0f);
        }

        private void OnDamageEvent()
        {
            bool crit = LastWasCrit != 0;

            // UI-feedback �� HP (���� ������ � ������ ����� ������� �� HP ���������)
            if (LastDamageToHealth > 0f)
                healthBar?.PlayDamageFeedback(LastDamageToHealth, crit);

            // ���� ������ ��������� �������� �� ����� � ������ �����/������.
            if (LastDamageToArmor > 0f)
                armorBar?.PlayDamageFeedback(LastDamageToArmor, crit);

            hurtVisual?.PlayHurt(crit);

            // �����: ���� ���������� total ��� �������� ���� �� HP � ������ �����.
            // � �� ��������� total, ����� ����� ����� �������� ���������.
            damagePopup?.Pop(LastDamageTotal, LastHitPoint, crit);
        }
    }
}
