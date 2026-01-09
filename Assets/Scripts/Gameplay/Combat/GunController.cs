// GunController.cs
using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class GunController : NetworkBehaviour
{
    [Header("Fire")]
    public float fireRate = 0.1f;
    public FireMode fireMode = FireMode.Auto;
    public int boltReloadTicks = 60;

    [SerializeField] private Transform gunMuzzle;

    [Header("Hitscan + Travel Time")]
    [SerializeField] private float bulletSpeed = 60f;
    [SerializeField] private float maxDistance = 200f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private LayerMask hitLayers;

    [Header("Visual (NOT network)")]
    [SerializeField] private TracerFx tracerPrefab;

    [Header("Local Predicted Tracer")]
    [SerializeField] private bool localTracerUseSpherecast = true;
    [SerializeField] private float localTracerRadius = 0.06f; // чуть больше хитбокса, чтобы не "промахиваться" визуально

    [Networked] private int NextFireTick { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private int _cooldownTicks;

    // ===== pending damage на сервере =====
    private struct PendingDamage
    {
        public TickTimer Timer;
        public NetworkObject Target;
        public float Damage;
        public bool IsCritical;
        public Player Owner;
    }

    private readonly List<PendingDamage> _pending = new();

    // ===== локальный визуал стрелка (Plan A) =====
    private int _nextLocalFxTick = -1;

    public override void Spawned()
    {
        _cooldownTicks = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.01f, fireRate) * Runner.TickRate));

        if (HasStateAuthority)
            NextFireTick = Runner.Tick;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!GetInput(out NetInput input))
            return;

        ProcessPendingDamage();

        bool wantFire = false;
        switch (fireMode)
        {
            case FireMode.Auto:
                wantFire = input.Buttons.IsSet((int)InputButton.Fire);
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

        FireHitscan_Server(input);

        NextFireTick = Runner.Tick + _cooldownTicks;
        if (fireMode == FireMode.Bolt)
            NextFireTick += boltReloadTicks;
    }

    public override void Render()
    {
        // Plan A: стрелок видит ТОЛЬКО локальный FX (мгновенно), серверный FX ему не показываем
        if (!HasInputAuthority)
            return;

        if (Runner == null || tracerPrefab == null || gunMuzzle == null)
            return;

        var im = Runner.GetComponent<InputManager>();
        if (im == null)
            return;

        NetInput input = im.LastLocalInput;

        bool fireHeld = input.Buttons.IsSet((int)InputButton.Fire);

        if (!fireHeld)
        {
            _nextLocalFxTick = -1;
            return;
        }

        int tick = Runner.Tick;

        // локальный тик-гейт совпадает с серверным по cooldownTicks
        if (_nextLocalFxTick < 0)
            _nextLocalFxTick = tick;

        if (fireMode == FireMode.Semi || fireMode == FireMode.Bolt)
        {
            if (tick != _nextLocalFxTick)
                return;

            _nextLocalFxTick = int.MaxValue; // до отпускания
        }
        else
        {
            if (tick < _nextLocalFxTick)
                return;

            _nextLocalFxTick = tick + _cooldownTicks;
            if (fireMode == FireMode.Bolt)
                _nextLocalFxTick += boltReloadTicks;
        }

        Vector3 dir = input.AimDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        dir.Normalize();

        SpawnLocalPredictedTracer(dir);
    }

    private void SpawnLocalPredictedTracer(Vector3 dir)
    {
        Vector3 start = gunMuzzle.position;

        Vector3 end = start + dir * maxDistance;
        float distance = maxDistance;

        // Локальная PhysX-проверка: если попали — обрываем трейсер на hit.point
        if (localTracerUseSpherecast)
        {
            if (Physics.SphereCast(start, localTracerRadius, dir, out RaycastHit hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                distance = hit.distance;
            }
        }
        else
        {
            if (Physics.Raycast(start, dir, out RaycastHit hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                distance = hit.distance;
            }
        }

        float travelTime = Mathf.Max(0.02f, distance / Mathf.Max(0.001f, bulletSpeed));

        TracerFx fx = Instantiate(tracerPrefab);
        fx.Play(start, end, travelTime);
    }

    private void FireHitscan_Server(NetInput input)
    {
        if (gunMuzzle == null)
            return;

        Vector3 origin = gunMuzzle.position;

        Vector3 dir = input.AimDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        dir.Normalize();

        bool isCritical = input.IsCriticalAim;
        Player owner = GetComponent<Player>();

        bool didHit = Runner.LagCompensation.Raycast(
            origin,
            dir,
            maxDistance,
            Object.InputAuthority,
            out LagCompensatedHit hit,
            hitLayers,
            HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority
        );

        Vector3 endPoint;
        NetworkObject targetObj = null;

        if (didHit)
        {
            endPoint = hit.Point;

            if (hit.Hitbox != null && hit.Hitbox.Root != null)
            {
                targetObj = hit.Hitbox.Root.GetComponent<NetworkObject>();
                if (targetObj == null)
                    targetObj = hit.Hitbox.Root.GetComponentInParent<NetworkObject>();
            }
            else if (hit.GameObject != null)
            {
                targetObj = hit.GameObject.GetComponentInParent<NetworkObject>();
            }
        }
        else
        {
            endPoint = origin + dir * maxDistance;
        }

        float distance = Vector3.Distance(origin, endPoint);
        float travelTime = Mathf.Max(0.02f, distance / Mathf.Max(0.001f, bulletSpeed));
        int travelTicks = Mathf.Max(1, Mathf.RoundToInt(travelTime * Runner.TickRate));

        RPC_SpawnTracerConfirmed(origin, endPoint, travelTicks);

        if (didHit && targetObj != null && targetObj.IsValid)
        {
            _pending.Add(new PendingDamage
            {
                Timer = TickTimer.CreateFromTicks(Runner, travelTicks),
                Target = targetObj,
                Damage = damage,
                IsCritical = isCritical,
                Owner = owner
            });
        }
    }

    private void ProcessPendingDamage()
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var pd = _pending[i];

            if (!pd.Timer.Expired(Runner))
                continue;

            _pending.RemoveAt(i);

            if (pd.Target == null || !pd.Target.IsValid)
                continue;

            NetworkHealth health = null;
            if (!pd.Target.TryGetComponent(out health))
                health = pd.Target.GetComponentInParent<NetworkHealth>();

            if (health == null)
                continue;

            float finalDamage = pd.IsCritical ? pd.Damage * 2f : pd.Damage;
            health.ApplyDamage(finalDamage, pd.Owner);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnTracerConfirmed(Vector3 start, Vector3 end, int travelTicks)
    {
        // Plan A: стрелку подтверждённый FX НЕ показываем (иначе двойной трассер)
        if (HasInputAuthority)
            return;

        if (tracerPrefab == null || Runner == null)
            return;

        float duration = Mathf.Max(0.02f, travelTicks / (float)Runner.TickRate);

        TracerFx fx = Instantiate(tracerPrefab);
        fx.Play(start, end, duration);
    }
}
