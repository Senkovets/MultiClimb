using System.Collections.Generic;
using UnityEngine;

namespace _Project.CodeBase.Armor
{
    /// <summary>
    /// Мост между сетевым byte-id и ассетом брони.
    /// По сети нельзя передать ссылку на ScriptableObject,
    /// поэтому передаётся id, а реестр возвращает конфиг.
    /// </summary>
    [CreateAssetMenu(menuName = "MultiClimb/Armor Registry")]
    public class ArmorRegistry : ScriptableObject
    {
        [Tooltip("Вся броня в игре. Порядок не важен, важен ArmorId.")]
        [SerializeField] private ArmorConfig[] armors;
 
        private Dictionary<byte, ArmorConfig> _byId;
 
        private void BuildCacheIfNeeded()
        {
            if (_byId != null)
                return;
 
            _byId = new Dictionary<byte, ArmorConfig>();
 
            foreach (ArmorConfig a in armors)
            {
                if (a == null)
                    continue;
 
                if (_byId.ContainsKey(a.ArmorId))
                {
                    Debug.LogError($"[ArmorRegistry] Дубликат ArmorId={a.ArmorId}: " +
                                   $"{a.name} и {_byId[a.ArmorId].name}");
                    continue;
                }
 
                _byId[a.ArmorId] = a;
            }
        }
 
        public ArmorConfig Get(byte id)
        {
            if (id == 0)
                return null;
 
            BuildCacheIfNeeded();
            return _byId.TryGetValue(id, out ArmorConfig a) ? a : null;
        }
    }
}