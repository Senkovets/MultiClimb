using _Project.CodeBase.UI;
using Fusion;
using UnityEngine;

namespace _Project.CodeBase.Weapons
{
    public class WeaponInventory : NetworkBehaviour
    {
        [SerializeField] private WeaponRegistry registry;
 
        [Header("View")]
        [Tooltip("Куда инстанцировать модель оружия")]
        [SerializeField] private Transform weaponViewRoot;
        
        /// <summary>Срабатывает при любом изменении инвентаря</summary>
        public event System.Action Changed;
 
        // ---- Сетевое состояние ----
 
        /// <summary>0 = пистолет, 1 = подобранное</summary>
        [Networked, OnChangedRender(nameof(OnInventoryChanged))]
        public byte ActiveSlot { get; private set; }
 
        /// <summary>WeaponId в слоте 1. 0 = слот пустой</summary>
        [Networked, OnChangedRender(nameof(OnInventoryChanged))]
        public byte SecondaryWeaponId { get; private set; }
 
        /// <summary>Патроны в слоте 1</summary>
        [Networked, OnChangedRender(nameof(OnInventoryChanged))] 
        public int SecondaryAmmo { get; private set; }
 
        // ---- Локальное ----
 
        /// <summary>Конфиг оружия в конкретном слоте. null = слот пуст.</summary>
        public WeaponConfig GetWeaponInSlot(int slot)
        {
            if (slot == 0)
                return registry.DefaultWeapon;
 
            if (slot == 1)
                return registry.Get(SecondaryWeaponId);
 
            return null;   // слоты 2-7 пока не используются
        }
 
        /// <summary>Патроны в слоте. -1 = бесконечные.</summary>
        public int GetAmmoInSlot(int slot)
        {
            WeaponConfig w = GetWeaponInSlot(slot);
            if (w == null)
                return 0;
 
            if (w.InfiniteAmmo)
                return -1;
 
            return slot == 1 ? SecondaryAmmo : 0;
        }
        
        
        private GameObject _currentView;
        private byte _lastViewedWeaponId = 255; // невозможное значение
 
        /// <summary>Конфиг оружия которое сейчас в руках</summary>
        public WeaponConfig CurrentWeapon
        {
            get
            {
                if (ActiveSlot == 0)
                    return registry.DefaultWeapon;
 
                return registry.Get(SecondaryWeaponId);
            }
        }
        
        /// <summary>
        /// ВРЕМЕННО для тестов. Удалить когда появятся пикапы.
        /// RPC нужен потому что GiveWeapon требует StateAuthority,
        /// а нажатие клавиши происходит у InputAuthority.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_DebugGiveWeapon(byte weaponId)
        {
            GiveWeapon(weaponId);
        }
 
        public bool HasAmmoForCurrent
        {
            get
            {
                WeaponConfig w = CurrentWeapon;
                if (w == null) return false;
                if (w.InfiniteAmmo) return true;
 
                return ActiveSlot == 0 || SecondaryAmmo > 0;
            }
        }
 
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                ActiveSlot = 0;
                SecondaryWeaponId = 0;
                SecondaryAmmo = 0;
            }
 
            RefreshView();
 
            // HUD показывает только СВОЁ оружие
            if (HasInputAuthority)
                WeaponHudController.Instance?.Bind(this);
        }
        
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (HasInputAuthority)
                WeaponHudController.Instance?.Unbind(this);
        }
 
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;
 
            if (!GetInput(out NetInput input))
                return;
 
            HandleSlotRequest(input.DesiredWeaponSlot);
            AutoFallbackIfEmpty();
        }
 
        // ---- Сервер: смена слота ----
 
        private void HandleSlotRequest(byte desired)
        {
            if (desired == ActiveSlot)
                return;
 
            // Слот 0 всегда доступен
            if (desired == 0)
            {
                ActiveSlot = 0;
                return;
            }
 
            // Слот 1 только если там есть оружие с патронами
            if (desired == 1 && SecondaryWeaponId != 0 && SecondaryAmmo > 0)
                ActiveSlot = 1;
        }
 
        private void AutoFallbackIfEmpty()
        {
            if (ActiveSlot != 1)
                return;
 
            if (SecondaryAmmo > 0 && SecondaryWeaponId != 0)
                return;
 
            // Патроны кончились — возвращаемся к пистолету
            ActiveSlot = 0;
            SecondaryWeaponId = 0;
            SecondaryAmmo = 0;
        }
 
        // ---- Сервер: API для пикапов и стрельбы ----
 
        /// <summary>Вызывается пикапом при подборе оружия</summary>
        public void GiveWeapon(byte weaponId)
        {
            if (!HasStateAuthority)
                return;
 
            WeaponConfig config = registry.Get(weaponId);
            if (config == null)
            {
                Debug.LogError($"[WeaponInventory] Неизвестный WeaponId={weaponId}");
                return;
            }
 
            SecondaryWeaponId = weaponId;
            SecondaryAmmo = config.AmmoOnPickup;
 
            // Сразу в руки — как ты и хотел
            ActiveSlot = 1;
        }
 
        /// <summary>Вызывается GunController после выстрела</summary>
        public void ConsumeAmmo(int amount = 1)
        {
            if (!HasStateAuthority)
                return;
 
            WeaponConfig w = CurrentWeapon;
            if (w == null || w.InfiniteAmmo)
                return;
 
            if (ActiveSlot == 1)
                SecondaryAmmo = Mathf.Max(0, SecondaryAmmo - amount);
        }
 
        // ---- Визуал (у всех клиентов) ----
 
        private void OnInventoryChanged()
        {
            RefreshView();
            Changed?.Invoke();
        }
 
        private void RefreshView()
        {
            WeaponConfig w = CurrentWeapon;
            byte id = w != null ? w.WeaponId : (byte)0;
 
            if (id == _lastViewedWeaponId)
                return;
 
            _lastViewedWeaponId = id;
 
            if (_currentView != null)
            {
                Destroy(_currentView);
                _currentView = null;
            }
 
            if (w == null || w.ViewPrefab == null || weaponViewRoot == null)
                return;
 
            _currentView = Instantiate(w.ViewPrefab, weaponViewRoot);
            _currentView.transform.localPosition = Vector3.zero;
            _currentView.transform.localRotation = Quaternion.identity;
 
            // Прицел меняем только у локального игрока
            if (HasInputAuthority && w.AimMarker != null)
                AimMarkerManager.Instance?.SwitchPreset(w.AimMarker);
        }
    }
}