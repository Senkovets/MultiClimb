using Fusion;
using Gameplay.Combat;
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

    // Networked
    [Networked] private bool IsCritical { get; set; }
    [Networked] private Vector3 Direction { get; set; }
    [Networked] private Player Owner { get; set; }
    [Networked] private Vector3 SpawnPosition { get; set; }
    [Networked] private bool HasHit { get; set; }
    [Networked] private TickTimer DespawnTimer { get; set; }

    // Ключевое: авторитетная позиция с сервера
    [Networked] private Vector3 NetPos { get; set; }

    // Visual-only
    private Vector3 visualPosition;

    public void Init(Vector3 spawnPos, Vector3 dir, Player owner, bool isCritical)
    {
        if (!HasStateAuthority) return;

        SpawnPosition = spawnPos;
        Direction = dir.normalized;
        Owner = owner;
        IsCritical = isCritical;

        HasHit = false;
        DespawnTimer = TickTimer.None;

        NetPos = spawnPos;
        transform.position = spawnPos;
        visualPosition = spawnPos;
    }

    public override void Spawned()
    {
        // На всех: стартуем от NetPos/SpawnPosition
        Vector3 start = (NetPos != default) ? NetPos : SpawnPosition;

        transform.position = start;
        visualPosition = start;

        if (Direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(Direction);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (DespawnTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        if (HasHit)
            return;

        float step = speed * Runner.DeltaTime;
        Vector3 currentPos = NetPos; // используем сетевую позицию как источник истины
        Vector3 nextPos = currentPos + Direction * step;

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
            float closestDistance = hits[0].distance;

            for (int i = 1; i < hits.Length; i++)
            {
                if (hits[i].distance < closestDistance)
                {
                    closestHit = hits[i];
                    closestDistance = hits[i].distance;
                }
            }

            HasHit = true;

            // фиксируем позицию попадания
            NetPos = closestHit.point;
            transform.position = NetPos;

            // урон
            NetworkHealth health = null;
            if (!closestHit.collider.TryGetComponent(out health))
                closestHit.collider.GetComponentInParent<NetworkHealth>()?.TryGetComponent(out health);

            if (health != null)
            {
                float finalDamage = IsCritical ? damage * 2f : damage;
              //  health.ApplyDamage(finalDamage, Owner);
            }

            // 1 тик на синхрон, затем despawn
            DespawnTimer = TickTimer.CreateFromTicks(Runner, 1);
            return;
        }

        if (Vector3.Distance(SpawnPosition, nextPos) >= maxDistance)
        {
            HasHit = true;
            NetPos = nextPos;
            transform.position = NetPos;
            DespawnTimer = TickTimer.CreateFromTicks(Runner, 1);
            return;
        }

        NetPos = nextPos;
        transform.position = NetPos;
    }

    public override void Render()
    {
        if (HasStateAuthority)
            return;

        // если уже попали — сразу фиксируемся в точке хита без "киселя"
        if (HasHit)
        {
            visualPosition = NetPos;
            transform.position = visualPosition;
            return;
        }

        if (Direction.sqrMagnitude < 0.0001f)
            return;

        float dt = Time.deltaTime;

        // 1) постоянная визуальная скорость
        visualPosition += Direction * speed * dt;

        // 2) линейная коррекция к серверной позиции (без экспоненциального замедления)
        // чем больше correctionSpeed — тем быстрее подтяжка, но без "киселя"
        const float correctionSpeed = 80f; // подстрой под свой тикрейт/скорость
        Vector3 corrected = Vector3.MoveTowards(visualPosition, NetPos, correctionSpeed * dt);

        // 3) защита от слишком большого рассинхрона (телепорт при большом дрейфе)
        const float snapDistance = 1.5f;
        if ((corrected - NetPos).sqrMagnitude > snapDistance * snapDistance)
            corrected = NetPos;

        visualPosition = corrected;
        transform.position = visualPosition;

        transform.rotation = Quaternion.LookRotation(Direction);
    }

}
