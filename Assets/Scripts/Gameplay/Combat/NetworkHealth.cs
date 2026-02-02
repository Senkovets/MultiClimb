using Fusion;
using MultiClimb.Match;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHealthChanged))]
    public float CurrentHealth { get; private set; }
    public float MaxHealth { get; private set; } = 100f;

    [Networked, OnChangedRender(nameof(OnArmorChanged))]
    public float CurrentArmor { get; private set; }
    public float MaxArmor { get; private set; } = 100f;

    [SerializeField] private HealthBar healthBar;
    [SerializeField] private HealthBar armorBar;

    private Player owner;
    private HurtVisual hurtVisual;
    private DamagePopupSpawner damagePopup;

    // "Событие урона" (каждый хит увеличивает seq)
    [Networked, OnChangedRender(nameof(OnDamageEvent))] private int DamageSeq { get; set; }

    // Payload для визуала
    [Networked] private float LastDamageTotal { get; set; }      // входящий урон (как прилетело)
    [Networked] private float LastDamageToArmor { get; set; }    // сколько реально ушло в броню
    [Networked] private float LastDamageToHealth { get; set; }   // сколько реально ушло в HP
    [Networked] private byte LastWasCrit { get; set; }           // 0/1
    [Networked] private Vector3 LastHitPoint { get; set; }

    public override void Spawned()
    {
        owner = GetComponent<Player>();

        hurtVisual = GetComponentInChildren<HurtVisual>(true);
        damagePopup = GetComponentInChildren<DamagePopupSpawner>(true);

        if (HasStateAuthority)
        {
            CurrentHealth = MaxHealth;
            CurrentArmor = MaxArmor; // стартовая броня
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

        // 1) броня
        if (CurrentArmor > 0f && dmgLeft > 0f)
        {
            dmgToArmor = Mathf.Min(CurrentArmor, dmgLeft);
            CurrentArmor = Mathf.Max(0f, CurrentArmor - dmgToArmor);
            dmgLeft -= dmgToArmor;
        }

        // 2) хп
        if (dmgLeft > 0f)
        {
            dmgToHealth = Mathf.Min(CurrentHealth, dmgLeft);
            CurrentHealth = Mathf.Max(0f, CurrentHealth - dmgToHealth);
        }

        // Payload для клиентов (то, что реально произошло)
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

        owner.IsVisible = false;

        PlayerRef killerRef = attacker != null
            ? attacker.Object.InputAuthority
            : PlayerRef.None;

        MatchEventBus.Instance?.Raise(
            new PlayerDiedEvent(
                Object.InputAuthority,   // victim
                killerRef,               // killer
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

        // UI-feedback по HP (если хочешь — только когда реально по HP прилетело)
        if (LastDamageToHealth > 0f)
            healthBar?.PlayDamageFeedback(LastDamageToHealth, crit);

        // Если хочешь отдельный “щитовик” по броне — добавь метод/эффект.
         if (LastDamageToArmor > 0f)
             armorBar?.PlayDamageFeedback(LastDamageToArmor, crit);

        hurtVisual?.PlayHurt(crit);

        // Попап: чаще показывают total или реальный урон по HP — выбери стиль.
        // Я бы показывал total, чтобы игрок видел “сколько прилетело”.
        damagePopup?.Pop(LastDamageTotal, LastHitPoint, crit);
    }
}
