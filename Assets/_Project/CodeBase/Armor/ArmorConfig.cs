using UnityEngine;

namespace _Project.CodeBase.Armor
{
    [CreateAssetMenu(menuName = "MultiClimb/Armor Config", fileName = "Armor_")]
    public class ArmorConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный id. НЕ МЕНЯЙ после создания — идёт по сети. " +
                 "0 зарезервирован под 'без брони'.")]
        public byte ArmorId = 1;
 
        public string DisplayName = "Light Armor";
 
        [Header("Protection")]
        [Tooltip("Сколько урона поглощает. Лёгкая 100, средняя 200, тяжёлая 300.")]
        [Min(1f)]
        public float ArmorAmount = 100f;
 
        [Header("Movement")]
        [Tooltip("Множитель скорости. 1 = без штрафа. " +
                 "Тяжёлая броня должна замедлять, иначе выбор очевиден.")]
        [Range(0.5f, 1f)]
        public float SpeedMultiplier = 1f;
 
        [Header("Visual")]
        [Tooltip("Модель НА ИГРОКЕ. Крепится к ArmorSocket под ModelRoot.")]
        public GameObject WornPrefab;
 
        [Tooltip("Иконка для HUD и инвентаря")]
        public Sprite Icon;
 
        [Tooltip("Цвет полоски брони в HUD. Позволяет отличать тиры на глаз.")]
        public Color BarColor = new Color(0.4f, 0.65f, 1f);
    }
}