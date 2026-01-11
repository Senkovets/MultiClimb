using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AimMarkerPreset : MonoBehaviour
{
    [Header("Core UI")]
    [SerializeField] private Canvas canvas;                // чтобы корректно конвертить screen->local
    [SerializeField] private RectTransform canvasRoot;      // RectTransform Canvas
    [SerializeField] private RectTransform aimMarkerUI;     // истинная позиция (визуал)
    [SerializeField] private RectTransform followUI;        // инерционный слой
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Follow")]
    [SerializeField] private float followSpeed = 30f;
    [SerializeField] private float followMaxDistance = 30f;

    [Header("AimMarkerUI Smoothing")]
    [SerializeField] private bool smoothAimMarker = true;
    [SerializeField] private float aimMarkerFollowSpeed = 55f;      // 40..70
    [SerializeField] private float aimMarkerMaxSnapDistance = 140f; // px

    [Header("Punch On Shoot")]
    [SerializeField] private List<PunchReceiver> punchReceivers = new();

    [Header("Scatter Visual")]
    [SerializeField] private float scatterExpandMultiplier = 5f;
    [SerializeField] private float baseSize = 10f;

    [Header("Critical")]
    [SerializeField] private Graphic markerGraphic;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color criticalColor = Color.red;

    [Header("Crosshair Petals")]
    [SerializeField] private RectTransform up;
    [SerializeField] private RectTransform down;
    [SerializeField] private RectTransform left;
    [SerializeField] private RectTransform right;

    [SerializeField] private float baseOffset = 8f;
    [SerializeField] private float scatterOffsetMultiplier = 6f;

    private float _scatter;

    // target в локальных координатах canvasRoot
    private Vector2 _aimLocalTarget;
    private Vector2 _aimLocalVel;
    private bool _hasTarget;

    private void Reset()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvasRoot == null)
        {
            Canvas c = canvas != null ? canvas : GetComponentInParent<Canvas>();
            if (c != null)
                canvasRoot = c.GetComponent<RectTransform>();
        }

        if (aimMarkerUI == null)
            aimMarkerUI = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvasRoot == null && canvas != null)
            canvasRoot = canvas.GetComponent<RectTransform>();
    }

    private void LateUpdate()
    {
        if (aimMarkerUI == null || followUI == null)
            return;

        float dt = Time.unscaledDeltaTime;

        // 0) Плавно двигаем "истинный" маркер (AimMarkerUI) к цели
        if (_hasTarget)
        {
            if (smoothAimMarker)
            {
                Vector2 cur = aimMarkerUI.anchoredPosition;

                if ((cur - _aimLocalTarget).sqrMagnitude > aimMarkerMaxSnapDistance * aimMarkerMaxSnapDistance)
                {
                    aimMarkerUI.anchoredPosition = _aimLocalTarget;
                    _aimLocalVel = Vector2.zero;
                }
                else
                {
                    aimMarkerUI.anchoredPosition = Vector2.SmoothDamp(
                        cur,
                        _aimLocalTarget,
                        ref _aimLocalVel,
                        1f / Mathf.Max(0.01f, aimMarkerFollowSpeed),
                        Mathf.Infinity,
                        dt
                    );
                }
            }
            else
            {
                aimMarkerUI.anchoredPosition = _aimLocalTarget;
            }
        }

        // 1) Follow / inertia (как в Dukov) — тоже anchoredPosition
        Vector2 target = aimMarkerUI.anchoredPosition;
        Vector2 current = followUI.anchoredPosition;

        Vector2 next = Vector2.Lerp(current, target, dt * followSpeed);

        Vector2 delta = next - target;
        float dist = delta.magnitude;
        if (dist > followMaxDistance && dist > 0.0001f)
            next = target + (delta / dist) * followMaxDistance;

        followUI.anchoredPosition = next;

        // 2) Scatter expansion (простая версия)
        float size = baseSize + _scatter * scatterExpandMultiplier;
        followUI.sizeDelta = new Vector2(size, size);

        // 3) Лепестки (если они внутри FollowUI — anchoredPosition корректен)
        float offset = baseOffset + _scatter * scatterOffsetMultiplier;

        if (up != null) up.anchoredPosition = new Vector2(0f, offset);
        if (down != null) down.anchoredPosition = new Vector2(0f, -offset);
        if (left != null) left.anchoredPosition = new Vector2(-offset, 0f);
        if (right != null) right.anchoredPosition = new Vector2(offset, 0f);
    }

    // ===== API, которое дергает Manager =====

    // ВАЖНО: сюда приходит Screen-space (пиксели). Переводим как раньше: Overlay/Camera/World
    public void SetScreenPosition(Vector2 screenPos)
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvasRoot == null && canvas != null)
            canvasRoot = canvas.GetComponent<RectTransform>();

        // Если вдруг не нашли canvasRoot — fallback относительно центра экрана
        if (canvasRoot == null)
        {
            _aimLocalTarget = screenPos - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _hasTarget = true;
            return;
        }

        // Screen Space Overlay
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                screenPos,
                null,
                out Vector2 localPt
            );

            _aimLocalTarget = localPt;
            _hasTarget = true;
            return;
        }

        // Screen Space Camera / World Space
        Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        if (cam == null)
        {
            _aimLocalTarget = screenPos - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _hasTarget = true;
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot,
            screenPos,
            cam,
            out Vector2 localPtCam
        );

        _aimLocalTarget = localPtCam;
        _hasTarget = true;
    }

    public void SetScatter(float scatter)
    {
        _scatter = scatter;
    }

    public void SetCritical(bool critical)
    {
        if (markerGraphic != null)
            markerGraphic.color = critical ? criticalColor : normalColor;
    }

    public void OnShoot()
    {
        for (int i = 0; i < punchReceivers.Count; i++)
        {
            if (punchReceivers[i] != null)
                punchReceivers[i].Punch();
        }
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }
}
