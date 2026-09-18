using Gameplay.Combat;
using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    [CreateAssetMenu(menuName = "MultiClimb/Pickups/Armor", fileName = "Pickup_Armor_")]
    public class ArmorPickupItem : PickupItem
    {
        [Header("Armor")]
        [Tooltip("Сколько защиты даёт. Лёгкая 100, средняя 200, тяжёлая 300.")]
        [Min(1f)]
        [SerializeField] private float armorAmount = 100f;
 
        public override bool CanGrantTo(Player player)
        {
            NetworkHealth health = player.GetComponent<NetworkHealth>();
            if (health == null)
                return false;
 
            // Правило: подбираем если даёт БОЛЬШЕ чем есть сейчас.
            //
            // Это покрывает два случая одной строкой:
            //   тяжёлая поверх лёгкой   — да, апгрейд
            //   лёгкая поверх тяжёлой   — нет, не тратим впустую
            //   такая же поверх побитой — да, это ремонт
            return armorAmount > health.CurrentArmor;
        }
 
        public override void GrantTo(Player player)
        {
            NetworkHealth health = player.GetComponent<NetworkHealth>();
            health?.GrantArmor(armorAmount);
        }
    }
}