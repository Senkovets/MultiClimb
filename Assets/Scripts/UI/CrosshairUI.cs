using UnityEngine;

public class CrosshairUI : MonoBehaviour
{
    public static CrosshairUI Singleton
    {
        get => _singleton;
        set
        {
            if (value == null)
                _singleton = null;
            else if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Destroy(value);
                Debug.LogError($"There should only ever be one instance of {nameof(CrosshairUI)}!");
            }
        }
    }
    private static CrosshairUI _singleton;

    [Header("Crosshair Settings")]
    public Color crosshairColor = Color.white;
    public float size = 10f;
    public float thickness = 2f;
    public float baseGap = 5f;
    public float scatterMultiplier = 5f; // насколько расходится при scatter (пока не используется)

    [Header("Headshot Highlight")]
    [SerializeField] private Color headshotColor = Color.cyan;
    [SerializeField] private float aimDistance = 100f;
    [SerializeField] private LayerMask hitboxLayers;

    private Camera cam;

    private Texture2D crosshairTexture;
    private MinimalGunController gunController;

    private void Start()
    {
        Singleton = this;
        cam = Camera.main;

        crosshairTexture = new Texture2D(1, 1);
        crosshairTexture.SetPixel(0, 0, Color.white); // текстура белая, цвет задаём через GUI.color
        crosshairTexture.Apply();

        gunController = FindObjectOfType<MinimalGunController>();
    }

    private void OnGUI()
    {
        // 1) Обновляем цвет прицела (хедшот / обычный)
        crosshairColor = IsAimingAtHead() ? headshotColor : Color.white;

        // 2) Рисуем прицел в позиции мыши
        float mouseX = Input.mousePosition.x;
        float mouseY = Input.mousePosition.y;
        DrawCrosshair(mouseX, mouseY);

        // Если хочешь рисовать именно в кастомной точке аима:
        // Vector2 aimPos = RecoilController.GetAimScreenPosition();
        // DrawCrosshair(aimPos.x, aimPos.y);
    }

    public void DrawCrosshair(float x, float y)
    {
        // GUI координаты: (0,0) вверху слева, а Input.mousePosition (0,0) снизу слева
        y = Screen.height - y;

        GUI.color = crosshairColor;

        // Верх
        GUI.DrawTexture(new Rect(x - thickness / 2f, y - baseGap - size, thickness, size), crosshairTexture);
        // Низ
        GUI.DrawTexture(new Rect(x - thickness / 2f, y + baseGap, thickness, size), crosshairTexture);
        // Лево
        GUI.DrawTexture(new Rect(x - baseGap - size, y - thickness / 2f, size, thickness), crosshairTexture);
        // Право
        GUI.DrawTexture(new Rect(x + baseGap, y - thickness / 2f, size, thickness), crosshairTexture);

        GUI.color = Color.white;
    }

    private bool IsAimingAtHead()
    {
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, aimDistance, hitboxLayers, QueryTriggerInteraction.Collide))
            return false;

        if (!hit.collider.TryGetComponent(out PlayerHitbox hitbox))
            return false;

        return hitbox.Type == HitboxType.Head;
    }
}
