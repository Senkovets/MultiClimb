using System.Collections.Generic;
using UnityEngine;

namespace _Project.CodeBase.Weapons
{
    [CreateAssetMenu(menuName = "MultiClimb/Weapon Registry")]
    public class WeaponRegistry : ScriptableObject
    {
        [Tooltip("Все существующие пушки. Порядок не важен, важен WeaponId.")]
        [SerializeField] private WeaponConfig[] weapons;
 
        [Tooltip("Оружие по умолчанию — то что в слоте 0 у всех")]
        [SerializeField] private WeaponConfig defaultWeapon;
 
        public WeaponConfig DefaultWeapon => defaultWeapon;
 
        private Dictionary<byte, WeaponConfig> _byId;
 
        private void BuildCacheIfNeeded()
        {
            if (_byId != null) return;
 
            _byId = new Dictionary<byte, WeaponConfig>();
 
            foreach (WeaponConfig w in weapons)
            {
                if (w == null) continue;
 
                if (_byId.ContainsKey(w.WeaponId))
                {
                    Debug.LogError($"[WeaponRegistry] Дубликат WeaponId={w.WeaponId}: " +
                                   $"{w.name} и {_byId[w.WeaponId].name}");
                    continue;
                }
 
                _byId[w.WeaponId] = w;
            }
        }
 
        public WeaponConfig Get(byte id)
        {
            if (id == 0) return null;
 
            BuildCacheIfNeeded();
            return _byId.TryGetValue(id, out WeaponConfig w) ? w : null;
        }
    }
}