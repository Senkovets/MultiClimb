using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public enum BarType { Health, Armor }

    [Header("Mode")]
    [SerializeField] private BarType barType = BarType.Health;

    [Header("Refs")]
    [SerializeField] private Image fill;
    [SerializeField] private Image followFill;   // optional
    [SerializeField] private Image blinkOverlay; // optional (прозрачная поверх бара)

    [Header("Follow")]
    [SerializeField] private float followDuration = 0.45f;
    [SerializeField] private float followDelay = 0.08f;
    [SerializeField] private Ease followEase = Ease.OutCubic;

    [Header("Feedback")]
    [SerializeField] private float punchScale = 0.12f;
    [SerializeField] private float punchDuration = 0.10f;
    [SerializeField] private float blinkDuration = 0.10f;
    [SerializeField] private Color blinkColor = new Color(1f, 1f, 1f, 0.6f);

    [Header("World UI")]
    [SerializeField] private Vector3 offset = new(0, 1f, 0);

    [SerializeField] private CanvasGroup canvasGroup;

    private NetworkHealth health;
    private Camera cam;
    private Transform tr;

    private Tween followTween;
    private Tween blinkTween;
    private Tween punchTween;

    public void Init(NetworkHealth h)
    {
        health = h;
        cam = Camera.main;
        tr = transform;

        if (blinkOverlay != null)
        {
            var c = blinkOverlay.color;
            c.a = 0f;
            blinkOverlay.color = c;
        }

        UpdateBar(true);
    }

    private void LateUpdate()
    {
        if (!cam || health == null) return;

        tr.position = health.transform.position + offset;
        tr.rotation = Quaternion.LookRotation(tr.position - cam.transform.position);
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    private float GetNormalized()
    {
        if (health == null) return 0f;

        float cur = barType == BarType.Health ? health.CurrentHealth : health.CurrentArmor;
        float max = barType == BarType.Health ? health.MaxHealth : health.MaxArmor;

        if (max <= 0f) return 0f;
        return Mathf.Clamp01(cur / max);
    }

    public void UpdateBar(bool instant = false)
    {
        if (health == null || fill == null) return;

        float t = GetNormalized();
        fill.fillAmount = t;

        if (followFill != null)
        {
            followTween?.Kill();

            if (instant)
            {
                followFill.fillAmount = t;
            }
            else
            {
                followTween = followFill
                    .DOFillAmount(t, followDuration)
                    .SetDelay(followDelay)
                    .SetEase(followEase);
            }
        }
    }

    // Вызывается из NetworkHealth.OnDamageEvent()
    public void PlayDamageFeedback(float damage, bool crit)
    {
        UpdateBar(false);

        // Punch (дергание)
        punchTween?.Kill();
        float p = crit ? punchScale * 1.35f : punchScale;
        float d = crit ? punchDuration * 1.15f : punchDuration;
        punchTween = transform.DOPunchScale(Vector3.one * p, d, vibrato: 10, elasticity: 0.9f);

        // Blink (вспышка)
        if (blinkOverlay != null)
        {
            blinkTween?.Kill();
            var end = blinkColor; end.a = 0f;
            var start = blinkColor; start.a = crit ? blinkColor.a * 1.2f : blinkColor.a;

            blinkOverlay.color = end;
            blinkTween = blinkOverlay.DOColor(start, blinkDuration)
                                     .From()
                                     .SetEase(Ease.OutQuad)
                                     .OnKill(() => { if (blinkOverlay) blinkOverlay.color = end; });
        }
    }
}
