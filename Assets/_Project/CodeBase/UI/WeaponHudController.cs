using _Project.CodeBase.Weapons;
using UnityEngine;

namespace _Project.CodeBase.UI
{
    public class WeaponHudController : MonoBehaviour
    {
        public static WeaponHudController Instance { get; private set; }
 
        [Header("Layout")]
        [Tooltip("Префаб одного слота с компонентом WeaponSlotView")]
        [SerializeField] private WeaponSlotView slotPrefab;
 
        [Tooltip("Контейнер с HorizontalLayoutGroup")]
        [SerializeField] private Transform slotsContainer;
 
        [Tooltip("Сколько слотов рисовать. Сейчас работают только 1 и 2.")]
        [Range(1, 8)]
        [SerializeField] private int slotCount = 8;
 
        [Header("Visibility")]
        [Tooltip("Скрывать панель когда игрока нет (меню, смерть)")]
        [SerializeField] private GameObject panelRoot;
 
        private WeaponSlotView[] _slots;
        private WeaponInventory _boundInventory;
 
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
 
            Instance = this;
            BuildSlots();
            SetPanelVisible(false);
        }
 
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
 
            UnbindInternal();
        }
 
        private void BuildSlots()
        {
            if (slotPrefab == null || slotsContainer == null)
            {
                Debug.LogError("[WeaponHud] slotPrefab или slotsContainer не назначен.");
                return;
            }
 
            _slots = new WeaponSlotView[slotCount];
 
            for (int i = 0; i < slotCount; i++)
            {
                WeaponSlotView slot = Instantiate(slotPrefab, slotsContainer);
                slot.Setup(i);
                _slots[i] = slot;
            }
        }
 
        // ---- Регистрация от локального WeaponInventory ----
 
        public void Bind(WeaponInventory inventory)
        {
            if (inventory == null)
                return;
 
            UnbindInternal();
 
            _boundInventory = inventory;
            _boundInventory.Changed += Refresh;
 
            SetPanelVisible(true);
            Refresh();
        }
 
        public void Unbind(WeaponInventory inventory)
        {
            // Игнорируем отвязку от чужого инвентаря
            if (_boundInventory != inventory)
                return;
 
            UnbindInternal();
            SetPanelVisible(false);
        }
 
        private void UnbindInternal()
        {
            if (_boundInventory == null)
                return;
 
            _boundInventory.Changed -= Refresh;
            _boundInventory = null;
        }
 
        private void SetPanelVisible(bool visible)
        {
            if (panelRoot != null)
                panelRoot.SetActive(visible);
        }
 
        // ---- Отрисовка ----
 
        private void Refresh()
        {
            if (_slots == null || _boundInventory == null)
                return;
 
            int activeSlot = _boundInventory.ActiveSlot;
 
            for (int i = 0; i < _slots.Length; i++)
            {
                WeaponConfig weapon = _boundInventory.GetWeaponInSlot(i);
 
                if (weapon == null)
                {
                    _slots[i].ShowEmpty();
                    continue;
                }
 
                int ammo = _boundInventory.GetAmmoInSlot(i);
                _slots[i].ShowWeapon(weapon, ammo, i == activeSlot);
            }
        }
    }
}