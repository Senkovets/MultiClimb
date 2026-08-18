using _Project.CodeBase.Weapons;
using Fusion;
using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    public class LootPickupPoint : NetworkBehaviour
    {
        [Header("Loot")]
        [Tooltip("Что может выпасть в этой точке")]
        [SerializeField] private LootTable lootTable;
 
        [Header("Respawn")]
        [Tooltip("Минимальное время до респавна, сек")]
        [SerializeField] private float respawnMin = 10f;
 
        [Tooltip("Максимальное. Равно min = фиксированное время.")]
        [SerializeField] private float respawnMax = 10f;
 
        [Tooltip("Перекатывать лут заново при каждом респавне. " +
                 "Выключено = точка всегда даёт одно и то же.")]
        [SerializeField] private bool rerollOnRespawn = true;
 
        [Header("Detection")]
        [SerializeField] private float pickupRadius = 1.2f;
 
        [Tooltip("Слой КАПСУЛЫ игрока, не хитбоксов")]
        [SerializeField] private LayerMask playerLayers;
 
        [Header("Visual")]
        [Tooltip("Сюда подставляется модель выпавшего предмета")]
        [SerializeField] private Transform visualRoot;
 
        [SerializeField] private float spinSpeed = 60f;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobSpeed = 2f;
 
        [Header("FX")]
        [SerializeField] private GameObject pickupFxPrefab;
        [SerializeField] private GameObject respawnFxPrefab;
 
        // ---- Networked ----
 
        [Networked, OnChangedRender(nameof(OnStateChanged))]
        public bool IsAvailable { get; private set; }
 
        /// <summary>Индекс записи в LootTable. 255 = ничего не выпало.</summary>
        [Networked, OnChangedRender(nameof(OnStateChanged))]
        public byte RolledIndex { get; private set; }
 
        [Networked] private TickTimer RespawnTimer { get; set; }
 
        // ---- Local ----
 
        private static readonly Collider[] OverlapBuffer = new Collider[8];
 
        private GameObject _currentModel;
        private int _builtIndex = -1;
        private bool _builtAvailable;
        private Vector3 _visualStartPos;
 
        private const byte NoLoot = 255;
 
        public override void Spawned()
        {
            if (visualRoot != null)
                _visualStartPos = visualRoot.localPosition;
 
            if (HasStateAuthority)
            {
                RollLoot();
                IsAvailable = RolledIndex != NoLoot;
                RespawnTimer = TickTimer.None;
            }
 
            RefreshVisual();
        }
 
        // =========================================================
        // Сервер
        // =========================================================
 
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;
 
            if (IsAvailable)
            {
                TryPickup();
                return;
            }
 
            if (RespawnTimer.Expired(Runner))
                Respawn();
        }
 
        private void RollLoot()
        {
            if (lootTable == null || lootTable.Count == 0)
            {
                RolledIndex = NoLoot;
                return;
            }
 
            int index = lootTable.Roll();
            RolledIndex = index >= 0 ? (byte)index : NoLoot;
        }
 
        private void TryPickup()
        {
            WeaponConfig weapon = CurrentWeapon;
            if (weapon == null)
                return;
 
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, pickupRadius, OverlapBuffer,
                playerLayers, QueryTriggerInteraction.Ignore);
 
            for (int i = 0; i < count; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null)
                    continue;
 
                Player player = col.GetComponentInParent<Player>();
                if (player == null || player.IsDead)
                    continue;
 
                WeaponInventory inventory = player.GetComponent<WeaponInventory>();
                if (inventory == null)
                    continue;
 
                inventory.GiveWeapon(weapon.WeaponId);
                Consume();
                return;
            }
        }
 
        private void Consume()
        {
            IsAvailable = false;
 
            float delay = Random.Range(respawnMin, Mathf.Max(respawnMin, respawnMax));
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, delay);
        }
 
        private void Respawn()
        {
            RespawnTimer = TickTimer.None;
 
            if (rerollOnRespawn)
                RollLoot();
 
            IsAvailable = RolledIndex != NoLoot;
        }
 
        // =========================================================
        // Визуал у всех клиентов
        // =========================================================
 
        private WeaponConfig CurrentWeapon
        {
            get
            {
                if (lootTable == null || RolledIndex == NoLoot)
                    return null;
 
                return lootTable.GetWeapon(RolledIndex);
            }
        }
 
        private void OnStateChanged()
        {
            bool wasAvailable = _builtAvailable;
 
            RefreshVisual();
 
            // FX только при смене доступности, не при смене лута
            if (wasAvailable == IsAvailable)
                return;
 
            GameObject fx = IsAvailable ? respawnFxPrefab : pickupFxPrefab;
            if (fx != null)
                Instantiate(fx, transform.position, Quaternion.identity);
        }
 
        private void RefreshVisual()
        {
            int index = IsAvailable ? RolledIndex : -1;
 
            // Ничего не изменилось — не пересоздаём модель
            if (index == _builtIndex && IsAvailable == _builtAvailable)
                return;
 
            _builtIndex = index;
            _builtAvailable = IsAvailable;
 
            ClearModel();
 
            if (!IsAvailable)
            {
                if (visualRoot != null)
                    visualRoot.gameObject.SetActive(false);
 
                return;
            }
 
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(true);
 
            BuildModel();
        }
 
        private void ClearModel()
        {
            if (_currentModel == null)
                return;
 
            Destroy(_currentModel);
            _currentModel = null;
        }
 
        private void BuildModel()
        {
            WeaponConfig weapon = CurrentWeapon;
            if (weapon == null || visualRoot == null)
                return;
 
            GameObject prefab = weapon.PickupPrefab != null
                ? weapon.PickupPrefab
                : weapon.ViewPrefab;
 
            if (prefab == null)
                return;
 
            _currentModel = Instantiate(prefab, visualRoot);
            _currentModel.transform.localPosition = Vector3.zero;
            _currentModel.transform.localRotation = Quaternion.identity;
        }
 
        public override void Render()
        {
            if (visualRoot == null || !IsAvailable)
                return;
 
            visualRoot.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
 
            if (bobHeight > 0f)
            {
                float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                visualRoot.localPosition = _visualStartPos + Vector3.up * offset;
            }
        }
 
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}