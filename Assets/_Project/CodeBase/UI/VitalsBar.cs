using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.CodeBase.UI
{
    /// <summary>
    /// Экранная полоска. Ничего не знает о NetworkHealth —
    /// принимает нормализованное значение снаружи.
    ///
    /// Отдельно от мировой HealthBar намеренно: у них разное
    /// будущее (сегменты брони и анимация лечения здесь,
    /// затухание по дистанции и ники там).
    /// </summary>
    public class VitalsBar : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Image с Type = Filled")]
        [SerializeField] private Image fill;

        [Tooltip("Отстающая полоска — показывает откуда упало")]
        [SerializeField] private Image followFill;

        [Tooltip("Оверлей вспышки при уроне")]
        [SerializeField] private Image blinkOverlay;

        [Tooltip("Что дёргать при punch. Пусто = сам объект.")]
        [SerializeField] private Transform punchTarget;

        [Header("Follow")]
        [SerializeField] private float followDuration = 0.45f;
        [SerializeField] private float followDelay = 0.08f;
        [SerializeField] private Ease followEase = Ease.OutCubic;

        [Header("Feedback")]
        [SerializeField] private float punchScale = 0.12f;
        [SerializeField] private float punchDuration = 0.10f;
        [SerializeField] private float blinkDuration = 0.10f;
        [SerializeField] private Color blinkColor = new Color(1f, 1f, 1f, 0.6f);

        [Header("Heal")]
        [Tooltip("Цвет вспышки при лечении")]
        [SerializeField] private Color healBlinkColor = new Color(0.4f, 1f, 0.5f, 0.5f);

        private Tween _followTween;
        private Tween _blinkTween;
        private Tween _punchTween;

        private float _lastValue = -1f;

        private Transform PunchTarget => punchTarget != null ? punchTarget : transform;

        private void Awake()
        {
            ResetBlink();
        }

        private void OnDestroy()
        {
            _followTween?.Kill();
            _blinkTween?.Kill();
            _punchTween?.Kill();
        }

        /// <summary>
        /// Установить значение 0..1.
        /// instant = без анимации отстающей полоски.
        /// </summary>
        public void SetValue(float normalized, bool instant = false)
        {
            normalized = Mathf.Clamp01(normalized);

            // Определяем направление изменения до записи _lastValue —
            // нужно чтобы follow-полоска работала правильно при лечении
            bool increased = normalized > _lastValue && _lastValue >= 0f;
            _lastValue = normalized;

            if (fill != null)
                fill.fillAmount = normalized;

            if (followFill == null)
                return;

            _followTween?.Kill();

            if (instant)
            {
                followFill.fillAmount = normalized;
                return;
            }

            // При росте отстающая полоска должна прыгнуть сразу,
            // иначе она окажется НИЖЕ основной и будет выглядеть
            // как полоска урона при лечении
            if (increased)
            {
                followFill.fillAmount = normalized;
                return;
            }

            _followTween = followFill
                .DOFillAmount(normalized, followDuration)
                .SetDelay(followDelay)
                .SetEase(followEase);
        }

        /// <summary>Вспышка и дёрганье при получении урона.</summary>
        public void PlayDamage(bool crit)
        {
            PlayPunch(crit ? 1.35f : 1f, crit ? 1.15f : 1f);
            PlayBlink(blinkColor, crit ? 1.2f : 1f);
        }

        /// <summary>Вспышка при лечении или надевании брони.</summary>
        public void PlayGain()
        {
            PlayPunch(0.8f, 1f);
            PlayBlink(healBlinkColor, 1f);
        }

        private void PlayPunch(float scaleMul, float durationMul)
        {
            _punchTween?.Kill();

            Transform t = PunchTarget;
            t.localScale = Vector3.one;

            _punchTween = t.DOPunchScale(
                Vector3.one * (punchScale * scaleMul),
                punchDuration * durationMul,
                vibrato: 10,
                elasticity: 0.9f);
        }

        private void PlayBlink(Color color, float alphaMul)
        {
            if (blinkOverlay == null)
                return;

            _blinkTween?.Kill();

            Color hidden = color;
            hidden.a = 0f;

            Color shown = color;
            shown.a = color.a * alphaMul;

            blinkOverlay.color = hidden;

            _blinkTween = blinkOverlay
                .DOColor(shown, blinkDuration)
                .From()
                .SetEase(Ease.OutQuad)
                .OnKill(ResetBlink);
        }

        private void ResetBlink()
        {
            if (blinkOverlay == null)
                return;

            Color c = blinkOverlay.color;
            c.a = 0f;
            blinkOverlay.color = c;
        }
    }
}