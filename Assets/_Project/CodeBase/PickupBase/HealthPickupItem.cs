using Gameplay.Combat;
using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    [CreateAssetMenu(menuName = "MultiClimb/Pickups/Health", fileName = "Pickup_Health_")]
    public class HealthPickupItem : PickupItem
    {
        [Header("Heal")]
        [Tooltip("Сколько HP восстанавливает")]
        [Min(1f)]
        [SerializeField] private float healAmount = 50f;
 
        public override bool CanGrantTo(Player player)
        {
            NetworkHealth health = player.GetComponent<NetworkHealth>();
            if (health == null)
                return false;
 
            // С полным HP не подбирается — иначе аптечка исчезнет зря
            return health.CurrentHealth < health.MaxHealth;
        }
 
        public override void GrantTo(Player player)
        {
            NetworkHealth health = player.GetComponent<NetworkHealth>();
            health?.Heal(healAmount);
        }
    }
}