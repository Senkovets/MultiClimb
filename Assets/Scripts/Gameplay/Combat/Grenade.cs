using System.Collections;
using UnityEngine;

public class Grenade : MonoBehaviour
{
    [Header("Throw Settings")]
    public float throwForce = 10f;
    public float verticalSpeed = 5f;
    public LineRenderer trajectoryRenderer;
    public int pointsCount = 30;
    public float timeStep = 0.1f;

    [Header("Explosion Settings")]
    public float explosionDelay = 3f; // задержка после падения
    public float explosionRadius = 5f;
    public int damage = 50;
    public LayerMask damageLayers;

    [Header("Effects")]
    public GameObject explosionEffect;
    public AudioClip explosionSound;
    private AudioSource audioSource;

    public LineRenderer radiusRenderer;
    public Material fillMaterial;
    public Material outlineMaterial;

    private Rigidbody rb;
    private GameObject fillObj;
    private bool hasLanded = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
    }

    // 🔹 Расчёт скорости броска
    private Vector3 CalculateVelocity(Vector3 start, Vector3 target, float verticleSpeed)
    {
        float g = Physics.gravity.magnitude;
        float t = verticleSpeed / g + Mathf.Sqrt(2f * Mathf.Abs((verticleSpeed * verticleSpeed * 0.5f / g) + start.y - target.y) / g);
        Vector3 flatDir = (target - start);
        flatDir.y = 0f;
        float distance = flatDir.magnitude;
        Vector3 dir = flatDir.normalized;
        float d = distance / t;
        return dir * d + Vector3.up * verticleSpeed;
    }

    public void ThrowTowardsCursor()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 targetPos = hit.point;
            Vector3 velocity = CalculateVelocity(transform.position, targetPos, verticalSpeed);

            rb.velocity = velocity;

            ClearVisuals();
            // ❌ взрыв не запускаем сразу, ждём падения
        }
    }

    // 🔹 Траектория
    public void DrawTrajectory(Vector3 startPos, Vector3 startVelocity)
    {
        if (trajectoryRenderer == null) return;

        trajectoryRenderer.positionCount = pointsCount;
        for (int i = 0; i < pointsCount; i++)
        {
            float t = i * timeStep;
            Vector3 pos = startPos + startVelocity * t + 0.5f * Physics.gravity * t * t;
            trajectoryRenderer.SetPosition(i, pos);
        }
    }

    // 🔹 Радиус при приземлении
    public void ShowExplosionRadiusAtLanding(Vector3 startPos, Vector3 startVelocity)
    {
        if (radiusRenderer == null) return;

        bool foundLanding = false;
        Vector3 landingPoint = startPos;

        for (float t = 0; t < 30f; t += timeStep)
        {
            Vector3 pos = startPos + startVelocity * t + 0.5f * Physics.gravity * t * t;
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, 1f))
            {
                landingPoint = hit.point;
                foundLanding = true;
                break;
            }
        }

        if (!foundLanding)
        {
            radiusRenderer.positionCount = 0;
            return;
        }

        DrawCircle(landingPoint);
    }

    private void ShowExplosionRadiusAtExplosion()
    {
        DrawCircle(transform.position);
    }

    // 🔹 Общая функция рисования круга
    private void DrawCircle(Vector3 center)
    {
        int segments = 50;

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            vertices[i] = new Vector3(Mathf.Cos(angle) * explosionRadius, 0, Mathf.Sin(angle) * explosionRadius);
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 2 > segments) ? 1 : i + 2;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        if (fillObj == null)
        {
            fillObj = new GameObject("ExplosionFill");
            fillObj.AddComponent<MeshFilter>();
            fillObj.AddComponent<MeshRenderer>();
        }

        MeshFilter mf = fillObj.GetComponent<MeshFilter>();
        MeshRenderer mr = fillObj.GetComponent<MeshRenderer>();
        mf.mesh = mesh;
        mr.material = fillMaterial;

        fillObj.transform.position = center + Vector3.up * 0.01f;
        fillObj.transform.rotation = Quaternion.identity;

        if (radiusRenderer != null)
        {
            radiusRenderer.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * explosionRadius, 0, Mathf.Sin(angle) * explosionRadius);
                radiusRenderer.SetPosition(i, pos);
            }
        }
    }

    public void ClearVisuals()
    {
        if (trajectoryRenderer != null) trajectoryRenderer.positionCount = 0;
        if (radiusRenderer != null) radiusRenderer.positionCount = 0;
        if (fillObj != null) Destroy(fillObj);
    }

    // 🔹 Срабатывает при падении гранаты — запускаем таймер взрыва
    void OnCollisionEnter(Collision collision)
    {
        if (!hasLanded)
        {
            hasLanded = true;
            StartCoroutine(Explode());
        }
    }

    IEnumerator Explode()
    {
        // ждём задержку после падения минус 2 секунды
        float soundDelay = Mathf.Max(0f, explosionDelay - 1.6f);
        yield return new WaitForSeconds(soundDelay);

        // звук проигрывается за 2 секунды до взрыва
        if (audioSource != null && explosionSound != null)
        {
            audioSource.PlayOneShot(explosionSound);
        }

        // ждём оставшееся время до взрыва
        yield return new WaitForSeconds(explosionDelay - soundDelay);

        ShowExplosionRadiusAtExplosion();

        if (explosionEffect != null)
        {
            GameObject effect = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        yield return new WaitForSeconds(0.5f);
        ClearVisuals();
        Destroy(gameObject);
    }
}