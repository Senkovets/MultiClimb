using Fusion;
using Gameplay.Combat;
using UnityEngine;

namespace _Project.CodeBase.Armor
{
    /// <summary>
    /// Надетая броня: сетевой id и модель на теле.
    /// Числа защиты живут в NetworkHealth — здесь только
    /// идентификатор, визуал и модификаторы.
    /// </summary>
    public class PlayerArmor : NetworkBehaviour
    {
        [SerializeField] private ArmorRegistry registry;
 
        [Tooltip("Куда крепить модель. Должен быть под ModelRoot — " +
                 "броня надета на тело и разворачивается с корпусом, " +
                 "а не смотрит на прицел.")]
        [SerializeField] private Transform armorSocket;
 
        [Networked, OnChangedRender(nameof(OnArmorIdChanged))]
        public byte ArmorId { get; private set; }
 
        /// <summary>Срабатывает при смене брони. Читает HUD.</summary>
        public event System.Action Changed;
        
        [Header("Startup")]
        [Tooltip("Броня при спавне. Пусто = спавн без брони. " +
                 "Выдаётся через Equip, поэтому модель появляется сразу.")]
        [SerializeField] private ArmorConfig startingArmor;
 
        private NetworkHealth _health;
        private Player _owner;
        private GameObject _currentModel;
        private byte _builtId = 255;   // невозможное значение
 
        /// <summary>Конфиг надетой брони. null = брони нет.</summary>
        public ArmorConfig Current => registry != null ? registry.Get(ArmorId) : null;
 
        /// <summary>
        /// Множитель скорости от брони. Читает PlayerLocomotion.
        /// Без брони — 1.
        /// </summary>
        public float SpeedMultiplier
        {
            get
            {
                ArmorConfig c = Current;
                return c != null ? c.SpeedMultiplier : 1f;
            }
        }
 
        public override void Spawned()
        {
            _health = GetComponent<NetworkHealth>();
            _owner = GetComponent<Player>();

            if (HasStateAuthority)
            {
                // Стартовая броня идёт через Equip — иначе числа появятся,
                // а ArmorId останется 0 и модели не будет
                if (startingArmor != null)
                    Equip(startingArmor.ArmorId);
                else
                    ArmorId = 0;
            }

            RefreshModel();
        }
 
        // ---- Сервер ----
 
        /// <summary>
        /// Надеть броню. Обновляет и числа в NetworkHealth,
        /// и модель у всех клиентов.
        /// </summary>
        public void Equip(byte armorId)
        {
            if (!HasStateAuthority)
                return;
 
            ArmorConfig config = registry != null ? registry.Get(armorId) : null;
            if (config == null)
            {
                Debug.LogError($"[PlayerArmor] Неизвестный ArmorId={armorId}");
                return;
            }
 
            ArmorId = armorId;
            _health?.GrantArmor(config.ArmorAmount);
        }
 
        /// <summary>
        /// Стоит ли надевать эту броню. Правило: только если даёт
        /// больше чем есть сейчас.
        ///
        /// Одна строка покрывает три случая:
        ///   тяжёлая поверх лёгкой    — да, апгрейд
        ///   лёгкая поверх тяжёлой    — нет, не тратим впустую
        ///   такая же поверх побитой  — да, это ремонт
        /// </summary>
        public bool IsUpgrade(byte armorId)
        {
            ArmorConfig config = registry != null ? registry.Get(armorId) : null;
            if (config == null || _health == null)
                return false;
 
            return config.ArmorAmount > _health.CurrentArmor;
        }
 
        /// <summary>Снять броню. Вызывается при респавне.</summary>
        public void Unequip()
        {
            if (!HasStateAuthority)
                return;
 
            ArmorId = 0;
        }
 
        // ---- Визуал у всех клиентов ----
 
        /// <summary>Скрыть или показать модель. Зовётся из Player при смерти.</summary>
        public void SetModelVisible(bool visible)
        {
            if (_currentModel != null)
                _currentModel.SetActive(visible);
        }
 
        private void OnArmorIdChanged()
        {
            RefreshModel();
            Changed?.Invoke();
        }
 
        private void RefreshModel()
        {
            if (ArmorId == _builtId)
                return;
 
            _builtId = ArmorId;
 
            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }
 
            ArmorConfig config = Current;
 
            if (config == null || config.WornPrefab == null || armorSocket == null)
                return;
 
            _currentModel = Instantiate<GameObject>(config.WornPrefab, armorSocket);
            _currentModel.transform.localPosition = Vector3.zero;
            _currentModel.transform.localRotation = Quaternion.identity;
 
            // На трупе новая модель появляться не должна: смерть может
            // произойти между сменой брони и этим вызовом
            if (_owner != null && !_owner.IsVisible)
                _currentModel.SetActive(false);
        }
    }
}