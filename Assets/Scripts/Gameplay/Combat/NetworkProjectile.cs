using Fusion;
using UnityEngine;

public class NetworkProjectile : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 37.2f;
    [SerializeField] private float maxDistance = 100f;

    [Header("Damage")]
    [SerializeField] private float damage = 25f;

    [Header("Hit")]
    [SerializeField] private LayerMask hitLayers;

    private const float BULLET_RADIUS = 0.05f;

    // ===== Networked =====
    [Networked] private Vector3 Direction { get; set; }
    [Networked] private Vector3 SpawnPosition { get; set; }
    [Networked] private Player Owner { get; set; }
    [Networked] private bool IsCritical { get; set; }
    [Networked] private bool HasHit { get; set; }
    [Networked] private TickTimer DespawnTimer { get; set; }

    // ===== Visual only =====
    private Vector3 visualPosition;

    // ===== Init =====
    public void Init(Vector3 spawnPos, Vector3 dir, Player owner, bool isCritical)
    {
        if (!HasStateAuthority)
            return;

        SpawnPosition = spawnPos;
        Direction = dir.normalized;
        Owner = owner;
        IsCritical = isCritical;

        transform.position = spawnPos;
        visualPosition = spawnPos;
    }

    public override void Spawned()
    {
        transform.position = SpawnPosition;
        visualPosition = SpawnPosition;

        if (Direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(Direction);
    }

    // ===== LOGIC (StateAuthority only) =====
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        // если уже попали — ждём despawn
        if (HasHit)
        {
            if (DespawnTimer.Expired(Runner))
                Runner.Despawn(Object);

            return;
        }

        float step = speed * Runner.DeltaTime;
        Vector3 currentPos = transform.position;
        Vector3 nextPos = currentPos + Direction * step;

        // ===== НАДЁЖНАЯ ПРОВЕРКА ПОПАДАНИЯ =====
        RaycastHit[] hits = Physics.SphereCastAll(
            currentPos,
            BULLET_RADIUS,
            Direction,
            step,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        if (hits.Length > 0)
        {
            RaycastHit closestHit = hits[0];
            float minDistance = hits[0].distance;

            for (int i = 1; i < hits.Length; i++)
            {
                if (hits[i].distance < minDistance)
                {
                    minDistance = hits[i].distance;
                    closestHit = hits[i];
                }
            }

            HandleHit(closestHit);
            return;
        }

        // ===== Проверка дистанции =====
        if (Vector3.Distance(SpawnPosition, nextPos) >= maxDistance)
        {
            HasHit = true;
            DespawnTimer = TickTimer.CreateFromTicks(Runner, 1);
            return;
        }

        transform.position = nextPos;
    }

    private void HandleHit(RaycastHit hit)
    {
        HasHit = true;

        // фиксируем позицию попадания
        transform.position = hit.point;
        visualPosition = hit.point;

        NetworkHealth health = hit.collider.GetComponentInParent<NetworkHealth>();
        if (health != null)
        {
            float finalDamage = IsCritical ? damage * 2f : damage;
            health.ApplyDamage(finalDamage, Owner);
        }

        DespawnTimer = TickTimer.CreateFromTicks(Runner, 1);
    }

    // ===== VISUAL (All clients) =====
    public override void Render()
    {
        if (HasHit || Direction.sqrMagnitude < 0.0001f)
            return;

        visualPosition += Direction * speed * Time.deltaTime;

        float drift = Vector3.Distance(visualPosition, transform.position);
        if (drift > 0.5f)
        {
            visualPosition = Vector3.Lerp(
                visualPosition,
                transform.position,
                0.5f
            );
        }

        transform.position = visualPosition;
    }
}
