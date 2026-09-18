using Gameplay.Combat;
using TMPro;
using UnityEngine;

namespace _Project.CodeBase.UI
{
    public class VitalsHudController : MonoBehaviour
    {
        public static VitalsHudController Instance { get; private set; }

        [SerializeField] private GameObject panelRoot;

        [Header("Health")]
        [SerializeField] private VitalsBar healthBar;
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("Armor")]
        [Tooltip("Блок брони целиком — скрывается когда брони нет")]
        [SerializeField] private GameObject armorGroup;

        [SerializeField] private VitalsBar armorBar;
        [SerializeField] private TextMeshProUGUI armorText;

        private NetworkHealth _bound;

        // Предыдущие значения — чтобы отличить урон от лечения
        private float _lastHealth;
        private float _lastArmor;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            UnbindInternal();
        }

        // ---- Привязка ----

        public void Bind(NetworkHealth health)
        {
            if (health == null)
                return;

            UnbindInternal();

            _bound = health;
            _bound.Changed += OnVitalsChanged;

            _lastHealth = _bound.CurrentHealth;
            _lastArmor = _bound.CurrentArmor;

            SetVisible(true);
            Refresh(instant: true);
        }

        public void Unbind(NetworkHealth health)
        {
            if (_bound != health)
                return;

            UnbindInternal();
            SetVisible(false);
        }

        private void UnbindInternal()
        {
            if (_bound == null)
                return;

            _bound.Changed -= OnVitalsChanged;
            _bound = null;
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot != null)
                panelRoot.SetActive(visible);
        }

        // ---- Обновление ----

        /// <summary>
        /// Играет фидбек по направлению изменения: урон или лечение.
        /// Крит сюда не доходит — Changed его не несёт, а отдельный
        /// канал для этого пока не нужен.
        /// </summary>
        private void OnVitalsChanged()
        {
            if (_bound == null)
                return;

            float health = _bound.CurrentHealth;
            float armor = _bound.CurrentArmor;

            if (health < _lastHealth)
                healthBar?.PlayDamage(false);
            else if (health > _lastHealth)
                healthBar?.PlayGain();

            if (armor < _lastArmor)
                armorBar?.PlayDamage(false);
            else if (armor > _lastArmor)
                armorBar?.PlayGain();

            _lastHealth = health;
            _lastArmor = armor;

            Refresh(instant: false);
        }

        private void Refresh(bool instant)
        {
            if (_bound == null)
                return;

            healthBar?.SetValue(Ratio(_bound.CurrentHealth, _bound.MaxHealth), instant);

            if (healthText != null)
                healthText.text = Mathf.CeilToInt(_bound.CurrentHealth).ToString();

            bool hasArmor = _bound.CurrentArmor > 0f;

            if (armorGroup != null)
                armorGroup.SetActive(hasArmor);

            if (!hasArmor)
                return;

            armorBar?.SetValue(Ratio(_bound.CurrentArmor, _bound.MaxArmor), instant);

            if (armorText != null)
                armorText.text = Mathf.CeilToInt(_bound.CurrentArmor).ToString();
        }

        private static float Ratio(float current, float max)
        {
            return max > 0.001f ? Mathf.Clamp01(current / max) : 0f;
        }
    }
}