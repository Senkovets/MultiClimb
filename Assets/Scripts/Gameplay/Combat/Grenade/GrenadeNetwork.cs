using Fusion;
using UnityEngine;


public class GrenadeNetwork : NetworkBehaviour, IThrowableProjectile
{
    [Header("Explosion")]
    [SerializeField] private float explodeDelay = 2.5f;
    [SerializeField] private LayerMask hitLayers;

    [Header("Debug")]
    [SerializeField] private bool drawDebugSphere = true;

    private Rigidbody _rb;
    private float _effectRadius;
    private TickTimer _explodeTimer;

    private bool _launched;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true; // до Launch граната "спит"
    }

    public void Launch(Vector3 startPos, Vector3 velocity, float effectRadius)
    {
        if (_launched)
            return;

        _launched = true;
        _effectRadius = effectRadius;

        transform.position = startPos;

        _rb.isKinematic = false;
        _rb.velocity = velocity;

        // таймер взрыва (сетевой)
        if (Object.HasStateAuthority)
        {
            _explodeTimer = TickTimer.CreateFromSeconds(Runner, explodeDelay);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!_launched || !Object.HasStateAuthority)
            return;

        if (_explodeTimer.Expired(Runner))
        {
            Explode();
        }
    }

    private void Explode()
    {
        Vector3 pos = transform.position;

        // --- Заглушка урона ---
        Collider[] hits = Physics.OverlapSphere(
            pos,
            _effectRadius,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            // тут потом:
            // - урон
            // - отбрасывание
            // - эффекты
            Debug.Log($"[Grenade] Hit: {hit.name}");
        }

        if (drawDebugSphere)
        {
            DebugDrawSphere(pos, _effectRadius, Color.red, 1.0f);
        }

        // уничтожаем сетевой объект
        Runner.Despawn(Object);
    }

    // Только для визуального дебага
    private static void DebugDrawSphere(Vector3 center, float radius, Color color, float duration)
    {
        const int segments = 24;
        float step = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float a0 = step * i * Mathf.Deg2Rad;
            float a1 = step * (i + 1) * Mathf.Deg2Rad;

            Vector3 p0 = center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;

            Debug.DrawLine(p0, p1, color, duration);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!_launched)
            return;

        Gizmos.color = new Color(1, 0, 0, 0.25f);
        Gizmos.DrawSphere(transform.position, _effectRadius);
    }
#endif
}
