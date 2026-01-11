using DG.Tweening;
using UnityEngine;

public class HurtVisual : MonoBehaviour
{
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private string hurtParam = "_HurtValue";
    [SerializeField] private float multiplier = 1f;
    [SerializeField] private float upTime = 0.03f;
    [SerializeField] private float downTime = 0.18f;

    private MaterialPropertyBlock mpb;
    private int hurtHash;
    private float value;
    private Tween tween;

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        hurtHash = Shader.PropertyToID(hurtParam);

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

    }

    public void PlayHurt(bool crit)
    {
        tween?.Kill();

        float peak = (crit ? 1.25f : 1f) * multiplier;

        // Быстрый подъём + плавное затухание
        value = 0f;
        Apply(0f);

        tween = DOTween.Sequence()
            .Append(DOTween.To(() => value, v => { value = v; Apply(value); }, peak, upTime).SetEase(Ease.OutQuad))
            .Append(DOTween.To(() => value, v => { value = v; Apply(value); }, 0f, downTime).SetEase(Ease.OutQuad));
    }

    private void Apply(float v)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(hurtHash, v);
            r.SetPropertyBlock(mpb);
        }
    }
}
