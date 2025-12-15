using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 50f;
    public float maxDistance = 100f;
    public LayerMask hitLayers;

    private Vector3 direction;
    private Vector3 startPosition;

    public void Init(Vector3 dir)
    {
        direction = dir.normalized;
        startPosition = transform.position;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;

        // Raycast между прошлой и новой позицией
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, step, hitLayers))
        {
            OnHit(hit);
            return;
        }

        transform.position += direction * step;

        if (Vector3.Distance(startPosition, transform.position) > maxDistance)
        {
            Destroy(gameObject);
        }
    }

    void OnHit(RaycastHit hit)
    {
        // урон, эффекты
        Destroy(gameObject);
    }
}
