using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CircleFill : MonoBehaviour
{
    public float radius = 5f;
    public int segments = 50;
    public Material fillMaterial;

    void Start()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.material = fillMaterial;

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        Vector2[] uv = new Vector2[segments + 1];

        // центр
        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);

        // вершины по окружности
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices[i] = new Vector3(x, 0, z);

            // нормализованные UV (для прозрачности и корректного рендера)
            uv[i] = new Vector2((x / radius + 1f) * 0.5f, (z / radius + 1f) * 0.5f);
        }

        // треугольники
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 2 > segments) ? 1 : i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();

        mf.mesh = mesh;
    }
}
