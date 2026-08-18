using UnityEngine;

namespace _Project.CodeBase.Weapons
{
    [CreateAssetMenu(menuName = "MultiClimb/Weapon Config", fileName = "Weapon_")]
    public class WeaponConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный id. НЕ МЕНЯЙ после создания — он идёт по сети. " +
                 "0 зарезервирован под 'нет оружия'.")]
        public byte WeaponId = 1;
 
        public string DisplayName = "Pistol";
 
        [Header("Fire")]
        public FireMode FireMode = FireMode.Semi;
        public float FireRate = 0.15f;
        public int BoltReloadTicks = 0;
 
        [Header("Ballistics")]
        public float Damage = 25f;
        public float BulletSpeed = 60f;
        public float MaxDistance = 200f;
 
        [Tooltip("Угол конуса разброса в градусах. 0 = без разброса")]
        public float ScatterAngleDeg = 1.25f;
 
        [Header("Ammo")]
        [Tooltip("Бесконечные патроны. Для пистолета true.")]
        public bool InfiniteAmmo = false;
 
        [Tooltip("Сколько патронов даётся при подборе")]
        public int AmmoOnPickup = 30;
 
        [Header("Recoil")]
        public float RecoilV = 35f;
        public float RecoilH = 0f;
        public float RecoilTime = 0.04f;
        public float RecoilRecoverDelay = 0.10f;
        public float RecoilRecoverSpeed = 220f;
 
        [Tooltip("Паттерн отдачи. Если null — используется простой recoil выше")]
        public RecoilPatternSO RecoilPattern;
 
        [Header("Visual")]
        [Tooltip("Иконка для слота в HUD")]
        public Sprite Icon;
        [Tooltip("Модель оружия в руках. Включается при выборе этого слота.")]
        public GameObject ViewPrefab;
 
        public AimMarkerPreset AimMarker;
        
        [Header("Pellets")]
        [Tooltip("Сколько лучей за один выстрел. 1 = обычное оружие, " +
                 "8-12 = дробовик.")]
        [Min(1)]
        public int PelletsPerShot = 1;
 
        [Header("Damage Falloff")]
        [Tooltip("Урон полный до этой дистанции")]
        public float FalloffStartDistance = 20f;
 
        [Tooltip("На этой дистанции и дальше урон = DamageAtMaxRange")]
        public float FalloffEndDistance = 60f;
 
        [Tooltip("Урон на максимальной дистанции. " +
                 "Для дробовика сильно меньше Damage, для снайперки = Damage.")]
        public float DamageAtMaxRange = 25f;
 
        [Header("Critical")]
        [Tooltip("Множитель урона при попадании в голову")]
        public float CriticalMultiplier = 2f;
        
        [Header("Visual")]
        [Tooltip("Модель для точки лута. Если пусто — берётся ViewPrefab.")]
        public GameObject PickupPrefab;
        
        /// <summary>
        /// Урон с учётом дистанции. Считается ТОЛЬКО на сервере,
        /// поэтому детерминизм не требуется.
        /// </summary>
        public float GetDamageAtDistance(float distance)
        {
            if (distance <= FalloffStartDistance)
                return Damage;
 
            if (distance >= FalloffEndDistance)
                return DamageAtMaxRange;
 
            float range = FalloffEndDistance - FalloffStartDistance;
            if (range <= 0.001f)
                return DamageAtMaxRange;
 
            float t = (distance - FalloffStartDistance) / range;
            return Mathf.Lerp(Damage, DamageAtMaxRange, t);
        }
    }
}