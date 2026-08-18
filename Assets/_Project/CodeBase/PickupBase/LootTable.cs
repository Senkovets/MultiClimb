using _Project.CodeBase.Weapons;
using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    [CreateAssetMenu(menuName = "MultiClimb/Loot Table", fileName = "LootTable_")]
    public class LootTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public WeaponConfig Weapon;
 
            [Tooltip("Вес, а не процент. Веса 3 и 1 дают шансы 75% и 25%.")]
            [Min(0f)]
            public float Weight;
        }
 
        [SerializeField] private Entry[] entries;
 
        public int Count => entries != null ? entries.Length : 0;
 
        public WeaponConfig GetWeapon(int index)
        {
            if (entries == null || index < 0 || index >= entries.Length)
                return null;
 
            return entries[index].Weapon;
        }
 
        /// <summary>
        /// Взвешенный случайный выбор. Возвращает индекс записи,
        /// -1 если таблица пуста или все веса нулевые.
        /// Вызывается ТОЛЬКО на сервере.
        /// </summary>
        public int Roll()
        {
            if (entries == null || entries.Length == 0)
                return -1;
 
            float total = 0f;
 
            foreach (Entry e in entries)
            {
                if (e.Weapon != null && e.Weight > 0f)
                    total += e.Weight;
            }
 
            if (total <= 0f)
                return -1;
 
            float roll = Random.Range(0f, total);
            float acc = 0f;
 
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Weapon == null || entries[i].Weight <= 0f)
                    continue;
 
                acc += entries[i].Weight;
 
                if (roll <= acc)
                    return i;
            }
 
            // Страховка от погрешности float
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                if (entries[i].Weapon != null && entries[i].Weight > 0f)
                    return i;
            }
 
            return -1;
        }
 
        /// <summary>Шанс записи в процентах. Для инспектора.</summary>
        public float GetChancePercent(int index)
        {
            if (entries == null || index < 0 || index >= entries.Length)
                return 0f;
 
            float total = 0f;
            foreach (Entry e in entries)
            {
                if (e.Weapon != null && e.Weight > 0f)
                    total += e.Weight;
            }
 
            if (total <= 0f)
                return 0f;
 
            return entries[index].Weight / total * 100f;
        }
    }
}