using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class PunchReceiver : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform rectTransform;

    [Header("Punch Position")]
    [SerializeField] private Vector2 punchAnchorPosition = new Vector2(0f, 8f);
    [SerializeField] private Vector2 punchAnchorRandom = new Vector2(2f, 2f);
    [SerializeField] private float punchPositionDuration = 0.12f;
    [SerializeField] private int punchPositionVibrato = 10;
    [SerializeField] private float punchPositionElasticity = 0.8f;

    [Header("Punch Scale")]
    [SerializeField] private float punchScaleUniform = 0.15f;
    [SerializeField] private float punchScaleDuration = 0.12f;
    [SerializeField] private int punchScaleVibrato = 8;
    [SerializeField] private float punchScaleElasticity = 0.7f;

    [Header("Punch Rotation")]
    [SerializeField] private float punchRotationZ = 6f;
    [SerializeField] private float punchRotationRandom = 3f;
    [SerializeField] private float punchRotationDuration = 0.12f;
    [SerializeField] private int punchRotationVibrato = 8;
    [SerializeField] private float punchRotationElasticity = 0.7f;

    [Header("Behaviour")]
    [SerializeField] private bool cachePoseWhenPunched = true;

    // cached pose
    private Vector2 _cachedAnchorPos;
    private Vector3 _cachedScale;
    private Quaternion _cachedRotation;
    private bool _poseCached;

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void CachePose()
    {
        if (_poseCached)
            return;

        _cachedAnchorPos = rectTransform.anchoredPosition;
        _cachedScale = rectTransform.localScale;
        _cachedRotation = rectTransform.localRotation;
        _poseCached = true;
    }

    private void RestorePose()
    {
        if (!_poseCached)
            return;

        rectTransform.anchoredPosition = _cachedAnchorPos;
        rectTransform.localScale = _cachedScale;
        rectTransform.localRotation = _cachedRotation;
    }

    public void Punch()
    {
        if (!enabled || rectTransform == null)
            return;

        // Убиваем ВСЕ твины на этом RectTransform
        rectTransform.DOKill(false);

        if (cachePoseWhenPunched)
            CachePose();

        // === RANDOM ===
        Vector2 posRandom = new Vector2(
            Random.Range(-punchAnchorRandom.x, punchAnchorRandom.x),
            Random.Range(-punchAnchorRandom.y, punchAnchorRandom.y)
        );

        float rotRandom = Random.Range(-punchRotationRandom, punchRotationRandom);

        // === POSITION PUNCH ===
        rectTransform
            .DOPunchAnchorPos(
                punchAnchorPosition + posRandom,
                punchPositionDuration,
                punchPositionVibrato,
                punchPositionElasticity
            )
            .OnKill(RestorePose);

        // === SCALE PUNCH ===
        rectTransform
            .DOPunchScale(
                Vector3.one * punchScaleUniform,
                punchScaleDuration,
                punchScaleVibrato,
                punchScaleElasticity
            )
            .OnKill(RestorePose);

        // === ROTATION PUNCH (Z only) ===
        rectTransform
            .DOPunchRotation(
                new Vector3(0f, 0f, punchRotationZ + rotRandom),
                punchRotationDuration,
                punchRotationVibrato,
                punchRotationElasticity
            )
            .OnKill(RestorePose);
    }
}
