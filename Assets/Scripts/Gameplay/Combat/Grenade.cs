using UnityEngine;
using System.Collections;

public class Grenade : MonoBehaviour
{
    [Header("Throw Settings")]
    public float throwForce = 10f;
    public LineRenderer lineRenderer;
    public int pointsCount = 30;
    public float timeStep = 0.1f;

    [Header("Explosion Settings")]
    public float explosionDelay = 3f;
    public float explosionRadius = 5f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Метод броска гранаты в сторону курсора
    public void ThrowTowardsCursor()
    {
        // Луч от камеры через курсор
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Проверяем пересечение с землёй/коллайдером
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 targetPos = hit.point; // точка на поверхности
            Vector3 direction = (targetPos - transform.position).normalized;

            rb.AddForce(direction * throwForce, ForceMode.Impulse);

            DrawTrajectory(transform.position, direction * throwForce);

            StartCoroutine(Explode());
        }
    }


    // Отрисовка траектории
    void DrawTrajectory(Vector3 startPos, Vector3 startVelocity)
    {
        if (lineRenderer == null) return;

        lineRenderer.positionCount = pointsCount;
        for (int i = 0; i < pointsCount; i++)
        {
            float t = i * timeStep;
            Vector3 pos = startPos + startVelocity * t + 0.5f * Physics.gravity * t * t;
            lineRenderer.SetPosition(i, pos);
        }
    }

    // Визуализация радиуса взрыва в редакторе
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    // Механика взрыва
    IEnumerator Explode()
    {
        yield return new WaitForSeconds(explosionDelay);

        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider col in colliders)
        {
            if (col.GetComponent<Player>() != null)
            {
                Debug.Log("Игрок подорвался!");
            }
        }

        Destroy(gameObject);
    }
}
