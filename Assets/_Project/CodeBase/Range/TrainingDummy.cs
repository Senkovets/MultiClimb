using Fusion;
using Gameplay.Combat;
using UnityEngine;

namespace _Project.CodeBase.Range
{
    /// <summary>
    /// Мишень для настройки визуала стрельбы.
    /// Умирает, прячется, через N секунд возвращается.
    /// </summary>
    [RequireComponent(typeof(NetworkHealth))]
    public class TrainingDummy : NetworkBehaviour
    {
        [Header("Respawn")]
        [Tooltip("Через сколько секунд манекен вернётся")]
        [SerializeField] private float respawnDelay = 3f;
 
        [Header("Visuals")]
        [Tooltip("Что прятать при смерти. Обычно модель + полоска HP.")]
        [SerializeField] private GameObject[] visualsToHide;
 
        [Tooltip("Коллайдеры которые отключаются пока манекен мёртв")]
        [SerializeField] private Collider[] collidersToDisable;
 
        [Header("FX")]
        [SerializeField] private GameObject deathFxPrefab;
        [SerializeField] private GameObject respawnFxPrefab;
 
        /// <summary>Мёртв ли манекен. Синхронизируется всем.</summary>
        [Networked, OnChangedRender(nameof(OnDownStateChanged))]
        public bool IsDown { get; private set; }
 
        [Networked] private TickTimer RespawnTimer { get; set; }
 
        private NetworkHealth _health;
 
        public override void Spawned()
        {
            _health = GetComponent<NetworkHealth>();
 
            if (HasStateAuthority)
            {
                IsDown = false;
                RespawnTimer = TickTimer.None;
            }
 
            ApplyDownState(IsDown);
        }
 
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;
 
            if (!IsDown)
            {
                // Ждём смерти. NetworkHealth сам обнулит HP.
                if (_health.CurrentHealth <= 0f)
                    Die();
 
                return;
            }
 
            if (RespawnTimer.Expired(Runner))
                Respawn();
        }
 
        private void Die()
        {
            IsDown = true;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        }
 
        private void Respawn()
        {
            RespawnTimer = TickTimer.None;
 
            _health.ResetHealth();
            IsDown = false;
        }
 
        // ---- Визуал у всех клиентов ----
 
        private void OnDownStateChanged()
        {
            ApplyDownState(IsDown);
 
            GameObject fx = IsDown ? deathFxPrefab : respawnFxPrefab;
            if (fx != null)
                Instantiate(fx, transform.position, Quaternion.identity);
        }
 
        private void ApplyDownState(bool isDown)
        {
            foreach (GameObject go in visualsToHide)
            {
                if (go != null)
                    go.SetActive(!isDown);
            }
 
            foreach (Collider col in collidersToDisable)
            {
                if (col != null)
                    col.enabled = !isDown;
            }
        }
    }
}