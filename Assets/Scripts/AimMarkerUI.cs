using UnityEngine;
using UnityEngine.UI;

public class AimMarkerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform marker;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image markerImage;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color criticalColor = Color.red;

    private InputManager _inputManager;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (marker == null)
            marker = GetComponent<RectTransform>();

        if (markerImage == null)
            markerImage = GetComponent<Image>();

        _inputManager = FindFirstObjectByType<InputManager>();
    }

    private void LateUpdate()
    {
        UpdatePosition();
        UpdateColor();
    }

    private void UpdatePosition()
    {
        Vector2 screenPos = RecoilController.GetAimScreenPosition();

        // Screen Space Overlay — самый простой и надёжный вариант
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            marker.position = screenPos;
            return;
        }

        // Screen Space Camera / World Space
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

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                cam,
                out Vector2 localPos))
        {
            marker.localPosition = localPos;
        }
    }

    private void UpdateColor()
    {
        if (markerImage == null || _inputManager == null)
        {
            //Debug.LogError("markerImage:" + markerImage);
            //Debug.LogError("_inputManager:" + _inputManager);
            _inputManager = FindFirstObjectByType<InputManager>();  //появляется только при старте сцены game
            return;
        }
            

       // Debug.LogError("UpdateColor");
        // Источник истины — последний локальный инпут
        bool isCritical = _inputManager.LastLocalInput.IsCriticalAim;

        markerImage.color = isCritical ? criticalColor : normalColor;
    }
}
