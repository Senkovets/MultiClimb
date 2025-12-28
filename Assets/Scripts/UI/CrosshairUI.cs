using UnityEngine;

public class CrosshairUI : MonoBehaviour
{
    // =========================
    // Singleton
    // =========================
    public static CrosshairUI Singleton
    {
        get => _singleton;
        private set => _singleton = value;
    }
    private static CrosshairUI _singleton;

    // =========================
    // Crosshair settings
    // =========================
    [Header("Crosshair Settings")]
    [SerializeField] private float size = 10f;
    [SerializeField] private float thickness = 2f;
    [SerializeField] private float baseGap = 5f;

    [SerializeField] private Color defaultColor = Color.white;

    // =========================
    // Headshot highlight
    // =========================
    [Header("Headshot Highlight")]
    [SerializeField] private Color headshotColor = Color.cyan;
    [SerializeField] private float aimDistance = 100f;
    [SerializeField] private LayerMask hitboxLayers;

    // =========================
    // Runtime
    // =========================
    private Camera cam;
    private Texture2D pixel;
    private Color currentColor;

    // =========================
    // Unity lifecycle
    // =========================
    private void Awake()
    {
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
            return;
        }

        Singleton = this;
    }

    private void Start()
    {
        cam = Camera.main;

        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
    }

    private void OnGUI()
    {
        if (cam == null)
            return;

        // выбор цвета прицела
        currentColor = IsAimingAtHead()
            ? headshotColor
            : defaultColor;

        // позиция прицела берётся из recoil controller
        Vector2 aimScreenPos = RecoilController.GetAimScreenPosition();

        DrawCrosshair(aimScreenPos);
    }

    // =========================
    // Drawing
    // =========================
    private void DrawCrosshair(Vector2 screenPos)
    {
        float x = screenPos.x;
        float y = Screen.height - screenPos.y;

        GUI.color = currentColor;

        // верх
        GUI.DrawTexture(
            new Rect(x - thickness / 2f, y - baseGap - size, thickness, size),
            pixel
        );

        // низ
        GUI.DrawTexture(
            new Rect(x - thickness / 2f, y + baseGap, thickness, size),
            pixel
        );

        // лево
        GUI.DrawTexture(
            new Rect(x - baseGap - size, y - thickness / 2f, size, thickness),
            pixel
        );

        // право
        GUI.DrawTexture(
            new Rect(x + baseGap, y - thickness / 2f, size, thickness),
            pixel
        );

        GUI.color = Color.white;
    }

    // =========================
    // Headshot detection
    // =========================
    private bool IsAimingAtHead()
    {
        Vector2 aimScreenPos = RecoilController.GetAimScreenPosition();
        Ray ray = cam.ScreenPointToRay(aimScreenPos);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                aimDistance,
                hitboxLayers,
                QueryTriggerInteraction.Collide))
            return false;

        if (!hit.collider.TryGetComponent(out PlayerHitbox hitbox))
            return false;

        return hitbox.Type == HitboxType.Head;
    }
}
