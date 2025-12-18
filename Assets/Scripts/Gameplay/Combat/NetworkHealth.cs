using Fusion;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [Networked] public float CurrentHealth { get; private set; }

    [SerializeField] private float maxHealth = 100f;

    private Player owner;

    public override void Spawned()
    {
        owner = GetComponent<Player>();

        if (HasStateAuthority)
            CurrentHealth = maxHealth;
    }

    public void ApplyDamage(float damage, Player attacker)
    {
        Debug.Log("ApplyDamage");
        if (!HasStateAuthority)
            return;

        if (CurrentHealth <= 0f)
            return;

        CurrentHealth -= damage;

        if (CurrentHealth <= 0f)
        {
            CurrentHealth = 0f;

            if (attacker != null && attacker != owner)
                attacker.AddKill();

            owner.Respawn();
        }
    }

    public void ResetHealth()
    {
        if (HasStateAuthority)
            CurrentHealth = maxHealth;
    }
}
