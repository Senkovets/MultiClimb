using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AimMarkerPreset : MonoBehaviour
{
    [Header("Core UI")]
    [SerializeField] private RectTransform aimMarkerUI;     // истинная позиция
    [SerializeField] private RectTransform followUI;        // инерционный слой
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Follow")]
    [SerializeField] private float followSpeed = 30f;
    [SerializeField] private float followMaxDistance = 30f;

    [Header("Punch On Shoot")]
   // [SerializeField] private List<PunchReceiver> punchReceivers = new();

    [Header("Scatter Visual")]
    [SerializeField] private float scatterExpandMultiplier = 5f;
    [SerializeField] private float baseSize = 10f;

    [Header("Critical")]
    [SerializeField] private Graphic markerGraphic;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color criticalColor = Color.red;

    private float _scatter;
    private bool _isCritical;

    private void LateUpdate()
    {
        if (aimMarkerUI == null || followUI == null)
            return;

        // === Follow / inertia (как в Dukov) ===
        Vector3 target = aimMarkerUI.position;
        Vector3 current = followUI.position;

        Vector3 next = Vector3.Lerp(current, target, Time.deltaTime * followSpeed);

        if ((next - target).magnitude > followMaxDistance)
            next = target + (next - target).normalized * followMaxDistance;

        followUI.position = next;

        // === Scatter expansion (простая версия) ===
        float size = baseSize + _scatter * scatterExpandMultiplier;
        followUI.sizeDelta = new Vector2(size, size);
    }

    // ===== API, которое дергает Manager =====

    public void SetScreenPosition(Vector2 screenPos)
    {
        if (aimMarkerUI != null)
            aimMarkerUI.position = screenPos;
    }

    public void SetScatter(float scatter)
    {
        _scatter = scatter;
    }

    public void SetCritical(bool critical)
    {
        _isCritical = critical;
        if (markerGraphic != null)
            markerGraphic.color = critical ? criticalColor : normalColor;
    }

    public void OnShoot()
    {
     /*   for (int i = 0; i < punchReceivers.Count; i++)
        {
            if (punchReceivers[i] != null)
                punchReceivers[i].Punch();
        }*/
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }
}
