using _Project.CodeBase.Armor;
using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    [CreateAssetMenu(menuName = "MultiClimb/Pickups/Armor", fileName = "Pickup_Armor_")]
    public class ArmorPickupItem : PickupItem
    {
        [Header("Armor")]
        [Tooltip("Какую броню выдать")]
        [SerializeField] private ArmorConfig armor;
 
        public override bool CanGrantTo(Player player)
        {
            if (armor == null)
                return false;
 
            PlayerArmor playerArmor = player.GetComponent<PlayerArmor>();
            if (playerArmor == null)
                return false;
 
            // Решение принимает PlayerArmor — он знает и реестр,
            // и текущее состояние брони
            return playerArmor.IsUpgrade(armor.ArmorId);
        }
 
        public override void GrantTo(Player player)
        {
            if (armor == null)
                return;
 
            player.GetComponent<PlayerArmor>()?.Equip(armor.ArmorId);
        }
    }
}