using Fusion;
using UnityEngine;

public class GunController : NetworkBehaviour
{
    [Header("Fire")]
    public float fireRate = 0.1f;
    public FireMode fireMode = FireMode.Auto;
    public int boltReloadTicks = 60;
    [SerializeField] private NetworkProjectile projectilePrefab;
    public Transform gunMuzzle;

    [Networked] private int NextFireTick { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    public LayerMask groundLayer;

    private int fireCooldownTicks;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            fireCooldownTicks = Mathf.CeilToInt(fireRate / Runner.DeltaTime);
            NextFireTick = Runner.Tick;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetInput input))
            return;

        if (!HasStateAuthority)
            return;

        bool wantFire = false;
        switch (fireMode)
        {
            case FireMode.Auto:
                wantFire = input.Buttons.IsSet(InputButton.Fire);
                break;
            case FireMode.Semi:
            case FireMode.Bolt:
                wantFire = input.Buttons.WasPressed(PreviousButtons, (int)InputButton.Fire);
                break;
        }

        PreviousButtons = input.Buttons;

        if (!wantFire)
            return;

        if (Runner.Tick < NextFireTick)
            return;

        TryFire(input);

        NextFireTick = Runner.Tick + fireCooldownTicks;

        if (fireMode == FireMode.Bolt)
        {
            NextFireTick += boltReloadTicks;
        }
    }

    private void TryFire(NetInput input)
    {
        Vector3 dir = input.AimDirection.normalized;

        if (dir.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning($"[GunController] Invalid fire direction! AimDirection={input.AimDirection}");
            return;
        }

        // ✅ КРИТИЧНО: Вычисляем позицию ДО спавна
        Vector3 spawnPos = gunMuzzle.position;

        // 🔍 DEBUG: Логируем направление стрельбы
        Debug.Log($"[GunController] Firing from {spawnPos} in direction {dir} | IsServer={HasStateAuthority}");

        Runner.Spawn(
            projectilePrefab,
            spawnPos, // ← Передаём явно
            Quaternion.LookRotation(dir),
            Object.InputAuthority,
            (runner, obj) =>
            {
                // ✅ ПЕРЕДАЁМ ПОЗИЦИЮ В INIT
                obj.GetComponent<NetworkProjectile>().Init(spawnPos, dir, GetComponent<Player>());
            }
        );
    }

    public Vector3 GetDirection()
    {
        var cam = Camera.main;
        if (cam == null || gunMuzzle == null)
        {
            Debug.LogWarning("[GunController] Camera or gunMuzzle is null!");
            return Vector3.forward;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        Vector3 aimPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
            aimPoint = hit.point;
        else
            aimPoint = ray.origin + ray.direction * 2000f;

        Vector3 cameraToAim = aimPoint - cam.transform.position;

        if (Mathf.Abs(cameraToAim.y) < 0.0001f)
            return gunMuzzle.forward;

        float t = (gunMuzzle.position.y - cam.transform.position.y) / cameraToAim.y;
        Vector3 projectedPoint = cam.transform.position + cameraToAim * t;

        Vector3 fireDirection = (projectedPoint - gunMuzzle.position).normalized;

        return fireDirection;
    }
}