using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    /// <summary>
    /// Базовый предмет для точек лута. Наследники решают
    /// что именно выдаётся игроку.
    /// </summary>
    public abstract class PickupItem : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Item";
 
        [Tooltip("Модель на точке лута")]
        public GameObject PickupPrefab;
 
        [Tooltip("Иконка для HUD и подсказок")]
        public Sprite Icon;
 
        /// <summary>
        /// Можно ли выдать предмет этому игроку.
        /// Аптечка не подбирается с полным HP, лёгкая броня —
        /// поверх тяжёлой. Вызывается ТОЛЬКО на сервере.
        /// </summary>
        public abstract bool CanGrantTo(Player player);
 
        /// <summary>Выдать предмет. Вызывается ТОЛЬКО на сервере.</summary>
        public abstract void GrantTo(Player player);
    }
}