using Fusion;
using UnityEngine;

namespace _Project.CodeBase.PickupBase
{
    public abstract class PickupBase : NetworkBehaviour
    {
        [Header("Respawn")]
        [Tooltip("Через сколько секунд предмет появится снова. " +
                 "0 = не респавнится (одноразовый).")]
        [SerializeField] protected float respawnDelay = 10f;
 
        [Header("Detection")]
        [Tooltip("Радиус в котором игрок подбирает предмет")]
        [SerializeField] protected float pickupRadius = 1.2f;
 
        [Tooltip("Слой игроков. НЕ хитбоксы — капсула персонажа.")]
        [SerializeField] protected LayerMask playerLayers;
 
        [Header("Visuals")]
        [Tooltip("Что скрывать когда предмет подобран")]
        [SerializeField] protected GameObject[] visuals;
 
        [Tooltip("Объект который крутится. Оставь пустым если не нужно.")]
        [SerializeField] protected Transform spinRoot;
 
        [SerializeField] protected float spinSpeed = 60f;
        [SerializeField] protected float bobHeight = 0.15f;
        [SerializeField] protected float bobSpeed = 2f;
 
        [Header("FX")]
        [SerializeField] protected GameObject pickupFxPrefab;
        [SerializeField] protected GameObject respawnFxPrefab;
 
        [Networked, OnChangedRender(nameof(OnAvailabilityChanged))]
        public bool IsAvailable { get; private set; }
 
        [Networked] private TickTimer RespawnTimer { get; set; }
 
        // Переиспользуемый буфер — чтобы не аллоцировать каждый тик
        private static readonly Collider[] OverlapBuffer = new Collider[8];
 
        private Vector3 _spinStartLocalPos;
 
        public override void Spawned()
        {
            if (spinRoot != null)
                _spinStartLocalPos = spinRoot.localPosition;
 
            if (HasStateAuthority)
            {
                IsAvailable = true;
                RespawnTimer = TickTimer.None;
            }
 
            ApplyAvailability(IsAvailable);
        }
 
        // ---- Сервер ----
 
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;
 
            if (IsAvailable)
            {
                TryPickup();
                return;
            }
 
            if (respawnDelay > 0f && RespawnTimer.Expired(Runner))
                Respawn();
        }
 
        private void TryPickup()
        {
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
 
                if (!CanBePickedUpBy(player))
                    continue;
 
                GrantTo(player);
                Consume();
                return;
            }
        }
 
        private void Consume()
        {
            IsAvailable = false;
 
            RespawnTimer = respawnDelay > 0f
                ? TickTimer.CreateFromSeconds(Runner, respawnDelay)
                : TickTimer.None;
        }
 
        private void Respawn()
        {
            RespawnTimer = TickTimer.None;
            IsAvailable = true;
        }
 
        /// <summary>
        /// Можно ли выдать предмет этому игроку.
        /// Например: аптечка не подбирается с полным HP.
        /// </summary>
        protected virtual bool CanBePickedUpBy(Player player) => true;
 
        /// <summary>Выдать предмет. Вызывается ТОЛЬКО на сервере.</summary>
        protected abstract void GrantTo(Player player);
 
        // ---- Визуал у всех ----
 
        private void OnAvailabilityChanged()
        {
            ApplyAvailability(IsAvailable);
 
            GameObject fx = IsAvailable ? respawnFxPrefab : pickupFxPrefab;
            if (fx != null)
                Instantiate(fx, transform.position, Quaternion.identity);
        }
 
        private void ApplyAvailability(bool available)
        {
            foreach (GameObject go in visuals)
            {
                if (go != null)
                    go.SetActive(available);
            }
        }
 
        public override void Render()
        {
            if (spinRoot == null || !IsAvailable)
                return;
 
            spinRoot.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
 
            if (bobHeight > 0f)
            {
                float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                spinRoot.localPosition = _spinStartLocalPos + Vector3.up * offset;
            }
        }
 
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}