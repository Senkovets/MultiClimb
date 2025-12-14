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
    public float scatterMultiplier = 5f; // Насколько расходится при scatter

    private Texture2D crosshairTexture;
    private MinimalGunController gunController;

    void Start()
    {
        Singleton = this;

        crosshairTexture = new Texture2D(1, 1);
        crosshairTexture.SetPixel(0, 0, crosshairColor);
        crosshairTexture.Apply();

        gunController = FindObjectOfType<MinimalGunController>();
    }

    void OnGUI()
    {
        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        // Динамическое расширение прицела при scatter
        float currentGap = baseGap;
        if (gunController != null)
        {
            //currentGap += gunController.GetCurrentScatter() * scatterMultiplier;
        }

        float mouseX = Input.mousePosition.x;
        float mouseY = Input.mousePosition.y;

        DrawCrosshair(mouseX, mouseY);

        /* GUI.color = crosshairColor;

         // Верхняя линия
         GUI.DrawTexture(new Rect(centerX - crosshairThickness / 2, centerY - currentGap - crosshairSize,
             crosshairThickness, crosshairSize), crosshairTexture);

         // Нижняя линия
         GUI.DrawTexture(new Rect(centerX - crosshairThickness / 2, centerY + currentGap,
             crosshairThickness, crosshairSize), crosshairTexture);

         // Левая линия
         GUI.DrawTexture(new Rect(centerX - currentGap - crosshairSize, centerY - crosshairThickness / 2,
             crosshairSize, crosshairThickness), crosshairTexture);

         // Правая линия
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

}
