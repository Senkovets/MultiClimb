using UnityEngine;

public class AimMarkerUI : MonoBehaviour
{
    [SerializeField] private RectTransform marker;
    [SerializeField] private Canvas canvas;

    private void Reset()
    {
        canvas = GetComponentInParent<Canvas>();
        marker = GetComponent<RectTransform>();
    }

    private void LateUpdate()
    {
        if (marker == null)
            return;

        Vector2 screenPos = RecoilController.GetAimScreenPosition();

        // Screen Space Overlay: просто ставим позицию в screen-space
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            marker.position = screenPos;
            return;
        }

        // Screen Space Camera / World Space:
        Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        if (cam == null)
        {
            marker.position = screenPos;
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            marker.position = screenPos;
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out var local))
            marker.localPosition = local;
        else
            marker.position = screenPos;
    }
}
