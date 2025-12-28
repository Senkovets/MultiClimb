using Fusion;
using UnityEngine;

public class NetworkProjectile : NetworkBehaviour
{
    [Header("Movement")]
    public float speed = 37.2f;
    public float maxDistance = 100f;

    [Header("Damage")]
    public float damage = 25f;

    [Header("Hit")]
    [SerializeField] private LayerMask hitLayers;

    // Основные networked переменные
    [Networked] private bool IsCritical { get; set; }
    [Networked] private Vector3 Direction { get; set; }
    [Networked] private Player Owner { get; set; }
    [Networked] private TickTimer DespawnTimer { get; set; }
    [Networked] private Vector3 SpawnPosition { get; set; }

    private const float BULLET_RADIUS = 0.05f;

    // Для плавной интерполяции
    private Vector3 visualPosition;

    public void Init(Vector3 spawnPos, Vector3 dir, Player owner, bool isCritical)
    {
        if (!HasStateAuthority) return;

        SpawnPosition = spawnPos;
        Direction = dir.normalized;
        Owner = owner;
        IsCritical = isCritical;

        transform.position = spawnPos;

        Debug.Log("NetworkProjectile IsCritical: " + IsCritical);
    }


    public override void Spawned()
    {
        // ✅ КРИТИЧНО: Используем SpawnPosition если она задана
        if (SpawnPosition != Vector3.zero)
        {
            transform.position = SpawnPosition;
        }

        // КРИТИЧНО: инициализируем visualPosition для ВСЕХ клиентов
        visualPosition = transform.position;

        // 🔍 DEBUG
        Debug.Log($"[Projectile] Spawned: Position={transform.position}, Direction={Direction}, HasAuthority={HasStateAuthority}");

        // На клиентах Direction уже синхронизирован из [Networked]
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

        float step = speed * Runner.DeltaTime;
        Vector3 currentPos = transform.position;
        Vector3 nextPos = currentPos + Direction * step;

        // КРИТИЧНО: Используем RaycastAll для проверки ВСЕХ попаданий
        RaycastHit[] hits = Physics.SphereCastAll(
            currentPos,
            BULLET_RADIUS,
            Direction,
            step,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        // Ищем ближайшее попадание
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

            // Попали в объект
            transform.position = closestHit.point;

            NetworkHealth health = closestHit.collider.GetComponentInParent<NetworkHealth>();

            if (health != null)
            {
                float finalDamage = damage;

                if (IsCritical)
                    finalDamage *= 2f;

                health.ApplyDamage(finalDamage, Owner);
            }

            DespawnTimer = TickTimer.CreateFromTicks(Runner, 1);
            return;
        }

        // Проверка максимальной дистанции - используем SpawnPosition
        if (Vector3.Distance(SpawnPosition, nextPos) >= maxDistance)
        {
            DespawnTimer = TickTimer.CreateFromTicks(Runner, 1);
            return;
        }

        transform.position = nextPos;
    }

    // ЭКСТРАПОЛЯЦИЯ - продолжаем движение между сетевыми апдейтами
    public override void Render()
    {
        // Проверка что Direction синхронизирован
        if (Direction.sqrMagnitude < 0.0001f)
            return;

        // Двигаем визуал вперёд на полной скорости
        visualPosition += Direction * speed * Time.deltaTime;

        // Корректируем если слишком далеко ушли от сетевой позиции
        float drift = Vector3.Distance(visualPosition, transform.position);
        if (drift > 1f) // Если отклонение больше 1 метра
        {
            // Плавная коррекция вместо резкого телепорта
            visualPosition = Vector3.Lerp(visualPosition, transform.position, 0.5f);
        }

        // ИСПРАВЛЕНИЕ: используем position напрямую, а не localPosition
        transform.position = visualPosition;
    }
}
