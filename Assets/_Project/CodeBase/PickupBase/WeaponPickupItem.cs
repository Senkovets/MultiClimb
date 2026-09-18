using _Project.CodeBase.Weapons;
using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    [CreateAssetMenu(menuName = "MultiClimb/Pickups/Weapon", fileName = "Pickup_Weapon_")]
    public class WeaponPickupItem : PickupItem
    {
        [Header("Weapon")]
        [SerializeField] private WeaponConfig weapon;
 
        public override bool CanGrantTo(Player player)
        {
            if (weapon == null)
                return false;
 
            return player.GetComponent<WeaponInventory>() != null;
        }
 
        public override void GrantTo(Player player)
        {
            WeaponInventory inventory = player.GetComponent<WeaponInventory>();
            if (inventory == null)
                return;
 
            inventory.GiveWeapon(weapon.WeaponId);
        }
    }
}