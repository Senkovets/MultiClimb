using Fusion;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHealthChanged))]
    public float CurrentHealth { get; private set; }

    public float MaxHealth { get; private set; } = 100f;

    private Player owner;
    private HealthBar bar;

    public override void Spawned()
    {
        owner = GetComponent<Player>();
        bar = GetComponentInChildren<HealthBar>(true);

        if (HasStateAuthority)
            CurrentHealth = MaxHealth;

        bar?.Init(this);
    }

    public void ApplyDamage(float damage, Player attacker)
    {
        if (!HasStateAuthority)
            return;

        if (CurrentHealth <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);

        if (CurrentHealth == 0f)
            HandleDeath(attacker);
    }

    private void HandleDeath(Player attacker)
    {
        if (attacker != null && attacker != owner)
            attacker.AddKill();

        owner.Respawn();
    }

    public void ResetHealth()
    {
        if (HasStateAuthority)
            CurrentHealth = MaxHealth;
    }

    private void OnHealthChanged()
    {
        bar?.UpdateBar();
    }
}
