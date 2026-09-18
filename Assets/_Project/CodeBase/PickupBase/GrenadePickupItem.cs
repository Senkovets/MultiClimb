using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    [CreateAssetMenu(menuName = "MultiClimb/Pickups/Grenade", fileName = "Pickup_Grenade_")]
    public class GrenadePickupItem : PickupItem
    {
        [Header("Grenades")]
        [SerializeField] private int amount = 2;
 
        public override bool CanGrantTo(Player player)
        {
            // TODO: проверить что не превышен лимит гранат
            return player.GetComponent<GrenadeThrower>() != null;
        }
 
        public override void GrantTo(Player player)
        {
            // TODO: player.GetComponent<GrenadeThrower>()?.AddGrenades(amount);
        }
    }
}