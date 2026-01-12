using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CircleDrawer : MonoBehaviour
{
    public float radius = 5f;       // радиус окружности
    public int segments = 50;       // количество сегментов (чем больше, тем круглее)
    private LineRenderer lineRenderer;

    void Start()
    {   
        lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Custom/LineAlwaysOnTop"));
        lineRenderer.material.color = new Color(1f, 1f, 0f, 1f); // любой цвет


        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true; // замкнёт линию
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;

        lineRenderer.alignment = LineAlignment.TransformZ;

        transform.rotation = Quaternion.Euler(90, 0, 0);

        DrawCircle(Vector3.zero); // окружность в точке (0,0,0)
    }

    void DrawCircle(Vector3 center)
    {
        lineRenderer.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            Vector3 pos = new Vector3(x, 0, z) + center;
            lineRenderer.SetPosition(i, pos);
        }
    }
}
