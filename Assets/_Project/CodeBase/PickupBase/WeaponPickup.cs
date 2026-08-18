using _Project.CodeBase.Weapons;
using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    public class WeaponPickup : PickupBase
    {
        [Header("Weapon")]
        [Tooltip("Какое оружие выдать")]
        [SerializeField] private WeaponConfig weapon;
 
        protected override bool CanBePickedUpBy(Player player)
        {
            if (weapon == null)
                return false;
 
            return player.GetComponent<WeaponInventory>() != null;
        }
 
        protected override void GrantTo(Player player)
        {
            WeaponInventory inventory = player.GetComponent<WeaponInventory>();
            if (inventory == null)
                return;
 
            inventory.GiveWeapon(weapon.WeaponId);
        }
 
        private void OnValidate()
        {
            if (weapon == null)
                Debug.LogWarning($"[WeaponPickup] {name}: не назначено оружие.", this);
        }
    }
}