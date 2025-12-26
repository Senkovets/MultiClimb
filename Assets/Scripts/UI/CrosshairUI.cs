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
    public float crosshairSize = 10f;
    public float size = 10f;
    public float thickness = 2f;
    public float crosshairThickness = 2f;
    public float baseGap = 5f;
    public float scatterMultiplier = 5f; // Íàñêîëüêî ðàñõîäèòñÿ ïðè scatter

    [Header("Headshot Highlight")]
    [SerializeField] private Color headshotColor = Color.cyan;
    [SerializeField] private float aimDistance = 100f;
    [SerializeField] private LayerMask hitboxLayers;

    private Camera cam;

    private Texture2D crosshairTexture;
    private MinimalGunController gunController;

    void Start()
    {
        Singleton = this;
        cam = Camera.main;

        crosshairTexture = new Texture2D(1, 1);
        crosshairTexture.SetPixel(0, 0, crosshairColor);
        crosshairTexture.Apply();

        gunController = FindObjectOfType<MinimalGunController>();
    }

    void OnGUI()
    {
        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        Vector2 aimPosition = RecoilController.GetAimScreenPosition();
        DrawCrosshair(aimPosition.x, aimPosition.y);
        Ray ray = cam.ScreenPointToRay(RecoilController.GetAimScreenPosition());
            //currentGap += gunController.GetCurrentScatter() * scatterMultiplier;
        }

        crosshairColor = IsAimingAtHead() ? headshotColor : Color.white;


        float mouseX = Input.mousePosition.x;
        float mouseY = Input.mousePosition.y;

        DrawCrosshair(mouseX, mouseY);

        /* GUI.color = crosshairColor;

         // Âåðõíÿÿ ëèíèÿ
         GUI.DrawTexture(new Rect(centerX - crosshairThickness / 2, centerY - currentGap - crosshairSize,
             crosshairThickness, crosshairSize), crosshairTexture);

         // Íèæíÿÿ ëèíèÿ
         GUI.DrawTexture(new Rect(centerX - crosshairThickness / 2, centerY + currentGap,
             crosshairThickness, crosshairSize), crosshairTexture);

         // Ëåâàÿ ëèíèÿ
         GUI.DrawTexture(new Rect(centerX - currentGap - crosshairSize, centerY - crosshairThickness / 2,
             crosshairSize, crosshairThickness), crosshairTexture);

         // Ïðàâàÿ ëèíèÿ
         GUI.DrawTexture(new Rect(centerX + currentGap, centerY - crosshairThickness / 2,
             crosshairSize, crosshairThickness), crosshairTexture);

         GUI.color = Color.white;*/
    }
    public void DrawCrosshair(float x, float y)
    {
        y = Screen.height - y;

        GUI.color = crosshairColor;

        GUI.DrawTexture(new Rect(x - thickness / 2, y - baseGap - size, thickness, size), crosshairTexture);
        GUI.DrawTexture(new Rect(x - thickness / 2, y + baseGap, thickness, size), crosshairTexture);
        GUI.DrawTexture(new Rect(x - baseGap - size, y - thickness / 2, size, thickness), crosshairTexture);
        GUI.DrawTexture(new Rect(x + baseGap, y - thickness / 2, size, thickness), crosshairTexture);

        GUI.color = Color.white;
    }

    private bool IsAimingAtHead()
    {
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

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
