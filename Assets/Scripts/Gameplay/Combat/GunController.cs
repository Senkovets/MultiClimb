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

    [Networked] private int NextFireTick { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private int _fireCooldownTicks;

    private struct PendingDamage
    {
        public TickTimer Timer;
        public NetworkObject Target;
        public float Damage;
        public bool IsCritical;
        public Player Owner;
    }

    private readonly List<PendingDamage> _pending = new();

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            _fireCooldownTicks = Mathf.Max(1, Mathf.CeilToInt(fireRate * Runner.TickRate));
            NextFireTick = Runner.Tick;
        }
    }
    private void Update()
    {
        if (Runner == null) return;
        if (!Runner.IsRunning) return;

        // ТЕСТ: на хосте по T создаём трассер локально
        if (Runner.IsServer && Input.GetKeyDown(KeyCode.T))
        {
            if (tracerPrefab == null || gunMuzzle == null) return;

            var fx = Instantiate(tracerPrefab);
            fx.Play(gunMuzzle.position, gunMuzzle.position + transform.forward * 10f, 0.05f);

            Debug.Log("[Tracer TEST] spawned locally on host");
        }
    }

    public override void FixedUpdateNetwork()
    {
        //Debug.Log($"[Gun] tick={Runner.Tick} stateAuth={HasStateAuthority} server={Runner.IsServer}");

        if (!HasStateAuthority)
            return;

        bool gotInput = GetInput(out NetInput input);
       // Debug.Log($"[Gun] gotInput={gotInput}");

        if (!gotInput)
            return;

        bool fireHeld = input.Buttons.IsSet((int)InputButton.Fire);
        bool firePressed = input.Buttons.WasPressed(PreviousButtons, (int)InputButton.Fire);
        //Debug.Log($"[Gun] fireHeld={fireHeld} firePressed={firePressed} mode={fireMode} nextTick={NextFireTick}");


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

        FireHitscan(input);

        NextFireTick = Runner.Tick + _fireCooldownTicks;
        if (fireMode == FireMode.Bolt)
            NextFireTick += boltReloadTicks;
    }

    private void FireHitscan(NetInput input)
    {
        Debug.Log($"[Gun] FireHitscan ENTER muzzle={(gunMuzzle != null)} tracerPrefab={(tracerPrefab != null)} aimDir={input.AimDirection}");

        if (gunMuzzle == null)
            return;

        Vector3 origin = gunMuzzle.position;

        Vector3 dir = input.AimDirection;
        dir.y = 0f;

        Debug.Log($"[Gun] dir after y=0 : {dir} sqr={dir.sqrMagnitude}");

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

            // ВАЖНО: hit.Hitbox.Root имеет тип HitboxRoot, а не NetworkObject
            if (hit.Hitbox != null && hit.Hitbox.Root != null)
            {
                // Берём NetworkObject с того же объекта, где стоит HitboxRoot
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
        float travelTime = (bulletSpeed > 0.001f) ? (distance / bulletSpeed) : 0f;
        int travelTicks = Mathf.Max(1, Mathf.RoundToInt(travelTime * Runner.TickRate));

        Debug.Log($"[Gun] calling RPC_SpawnTracer start={origin} end={endPoint} ticks={travelTicks}");
        RPC_SpawnTracer(origin, endPoint, travelTicks);


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
    private void RPC_SpawnTracer(Vector3 start, Vector3 end, int travelTicks)
    {
        Debug.Log($"[Tracer RPC] received. prefab={(tracerPrefab != null)} start={start} end={end} ticks={travelTicks}");

        if (tracerPrefab == null)
            return;

        float duration = Mathf.Max(0.01f, travelTicks / (float)Runner.TickRate);

        TracerFx fx = Instantiate(tracerPrefab);
        Debug.Log($"[Tracer RPC] instantiated {fx}");

        fx.Play(start, end, duration);
    }

}
