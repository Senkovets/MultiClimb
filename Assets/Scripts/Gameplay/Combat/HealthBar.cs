using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image fill;
    [SerializeField] private Image followFill;   // optional
    [SerializeField] private Image blinkOverlay; // optional (прозрачная поверх бара)

    [Header("Follow")]
    [SerializeField] private float followDuration = 0.12f;

    [Header("Feedback")]
    [SerializeField] private float punchScale = 0.12f;
    [SerializeField] private float punchDuration = 0.10f;
    [SerializeField] private float blinkDuration = 0.10f;
    [SerializeField] private Color blinkColor = new Color(1f, 1f, 1f, 0.6f);

    [Header("World UI")]
    [SerializeField] private Vector3 offset = new(0, 1f, 0);

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

    public void UpdateBar(bool instant = false)
    {
        if (health == null) return;

        float t = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
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
                followTween = followFill.DOFillAmount(t, followDuration).SetEase(Ease.OutQuad);
            }
        }
    }

    // Вызывается из NetworkHealth.OnDamageEvent()
    public void PlayDamageFeedback(float damage, bool crit)
    {
        // Сначала обновим бар (fill моментально, follow догоняет)
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
