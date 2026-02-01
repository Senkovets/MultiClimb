using Fusion;
using MultiClimb.Match;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHealthChanged))]
    public float CurrentHealth { get; private set; }

    public float MaxHealth { get; private set; } = 100f;

    // "Событие урона" (каждый хит увеличивает seq)
    [Networked, OnChangedRender(nameof(OnDamageEvent))]
    private int DamageSeq { get; set; }

    // Payload для визуала (реплицируется вместе с DamageSeq)
    [Networked] private float LastDamage { get; set; }
    [Networked] private byte LastWasCrit { get; set; } // 0/1
    [Networked] private Vector3 LastHitPoint { get; set; }

    private Player owner;
    private HealthBar bar;
    private HurtVisual hurtVisual;
    private DamagePopupSpawner damagePopup;

    public override void Spawned()
    {
        owner = GetComponent<Player>();
        bar = GetComponentInChildren<HealthBar>(true);
        hurtVisual = GetComponentInChildren<HurtVisual>(true);
        damagePopup = GetComponentInChildren<DamagePopupSpawner>(true);

        if (HasStateAuthority)
            CurrentHealth = MaxHealth;

        bar?.Init(this);
    }

    public void ApplyDamage(float damage, Player attacker, Vector3 hitPoint, bool isCrit = false)
    {
        if (!HasStateAuthority)
            return;

        if (CurrentHealth <= 0f)
            return;

        float newHealth = Mathf.Max(0f, CurrentHealth - damage);

        // Payload для клиентов
        LastDamage = damage;
        LastWasCrit = (byte)(isCrit ? 1 : 0);
        LastHitPoint = hitPoint;

        // ВАЖНО: сначала обновляем здоровье, потом "событие"
        CurrentHealth = newHealth;
        DamageSeq++;

        if (CurrentHealth == 0f)
            HandleDeath(attacker);
    }

    private void HandleDeath(Player attacker)
    {
        if (attacker != null && attacker != owner)
            attacker.AddKill();

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
        if (HasStateAuthority)
            CurrentHealth = MaxHealth;
    }

    private void OnHealthChanged()
    {
        bar?.UpdateBar(); // просто fill
    }

    private void OnDamageEvent()
    {
        // Это вызовется на всех клиентах (Render side) на каждый хит
        bool crit = LastWasCrit != 0;

        bar?.PlayDamageFeedback(LastDamage, crit);
        hurtVisual?.PlayHurt(crit);
        damagePopup?.Pop(LastDamage, LastHitPoint, crit);
    }
}
