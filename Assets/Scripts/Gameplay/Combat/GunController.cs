using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

public class GunController : NetworkBehaviour
{
    [Header("Fire")]
    public float fireRate = 0.1f;
    public FireMode fireMode = FireMode.Auto;
    public int boltReloadTicks = 60;

    [SerializeField] private Transform gunMuzzle;
    public Transform Muzzle => gunMuzzle;

    [Header("Hitscan + Travel Time")]
    [SerializeField] private float bulletSpeed = 60f;
    [SerializeField] private float maxDistance = 200f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private LayerMask hitLayers;

    [Header("Scatter (bullet spread)")]
    [Tooltip("Угол конуса разброса в градусах. 0 = без разброса.")]
    [SerializeField] private float scatterAngleDeg = 1.25f;
    public float CurrentScatter => scatterAngleDeg;


    [Header("Visual (NOT network)")]
    [SerializeField] private TracerFx tracerPrefab;

    [Header("Local Predicted Tracer")]
    [SerializeField] private bool localTracerUseSpherecast = true;
    [SerializeField] private float localTracerRadius = 0.06f;

    [Header("Recoil (local aim marker)")]
    [SerializeField] private float recoilV = 35f;
    [SerializeField] private float recoilH = 0f;
    [SerializeField] private float recoilTime = 0.04f;
    [SerializeField] private float recoilRecoverDelay = 0.10f;
    [SerializeField] private float recoilRecoverSpeed = 220f;

    [Networked] private int NextFireTick { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    [SerializeField] private RecoilPatternApplier recoilPattern;

    private int _cooldownTicks;

    [SerializeField] private ParticleSystem shellEmitter;


    [SerializeField] private GameObject muzzleFxPrefab;
    [SerializeField] private Transform muzzle;

    private struct PendingDamage
    {
        public TickTimer Timer;
        public NetworkObject Target;
        public float Damage;
        public bool IsCritical;
        public Player Owner;
    }

    private readonly List<PendingDamage> _pending = new();
    private int _nextLocalFxTick = -1;

    public override void Spawned()
    {
        _cooldownTicks = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.01f, fireRate) * Runner.TickRate));
        if (HasStateAuthority)
            NextFireTick = Runner.Tick;

        if (recoilPattern == null)
            recoilPattern = GetComponent<RecoilPatternApplier>();

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
        // Plan A: стрелок видит ТОЛЬКО локальный FX
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

            // Важно: если ты хочешь, чтобы серия паттерна сбрасывалась сразу при отпускании,
            // добавь в RecoilPatternApplier метод ResetBurst() и вызывай его здесь.
            // Пока можно оставить — у тебя уже есть burstResetSeconds по времени.
            return;
        }

        int tick = Runner.Tick;

        if (_nextLocalFxTick < 0)
            _nextLocalFxTick = tick;

        // Режимы огня: удержание/одиночные
        if (fireMode == FireMode.Semi || fireMode == FireMode.Bolt)
        {
            // Один выстрел на тик "разрешения"
            if (tick != _nextLocalFxTick)
                return;

            _nextLocalFxTick = int.MaxValue;
        }
        else
        {
            if (tick < _nextLocalFxTick)
                return;

            _nextLocalFxTick = tick + _cooldownTicks;

            // bolt тут по факту не попадёт (ты уже в else от Auto),
            // но оставляю, чтобы не ломать твою структуру.
            if (fireMode == FireMode.Bolt)
                _nextLocalFxTick += boltReloadTicks;
        }

        // Dukov-style: направление всегда от ствола
        Vector3 dir = gunMuzzle.forward;
        dir.y = 0f; // top-down: держим в плоскости XZ (как у тебя поворот)
        if (dir.sqrMagnitude < 0.0001f)
            return;

        dir.Normalize();

        // === Seed один и тот же для scatter и recoil-pattern ===
        int seed = BuildShotSeed(tick);

        // Scatter должен совпадать с сервером на этом тике
        Vector3 scatteredDir = ApplyScatter(dir, scatterAngleDeg, seed);

        SpawnLocalPredictedTracer(scatteredDir);

        // === Recoil marker: паттерн или fallback ===
        if (recoilPattern == null)
            recoilPattern = GetComponent<RecoilPatternApplier>();

        if (recoilPattern != null)
        {
            recoilPattern.NotifyShot(seed);
        }
        else
        {
            // fallback (как было)
            RecoilController.NotifyShot(recoilV, recoilH, recoilTime, recoilRecoverDelay, recoilRecoverSpeed);
        }

        AimMarkerManager.Instance?.OnShoot();
    }


    private void SpawnLocalPredictedTracer(Vector3 dir)
    {
        bool hitFlesh = false;
        Vector3 hitNormal = -dir; // fallback

        Vector3 start = gunMuzzle.position;

        Vector3 end = start + dir * maxDistance;
        float distance = maxDistance;

        if (localTracerUseSpherecast)
        {
            if (Physics.SphereCast(start, localTracerRadius, dir, out RaycastHit hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                distance = hit.distance;
                hitNormal = hit.normal;

                // Критерий “это игрок”: PlayerHitbox или NetworkHealth/Player в родителях
                hitFlesh =
                    hit.collider.GetComponentInParent<NetworkHealth>() != null ||
                    hit.collider.GetComponentInParent<Player>() != null;
            }
        }
        else
        {
            if (Physics.Raycast(start, dir, out RaycastHit hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                distance = hit.distance;
                hitNormal = hit.normal;

                hitFlesh =
                    hit.collider.GetComponentInParent<NetworkHealth>() != null ||
                    hit.collider.GetComponentInParent<Player>() != null;
            }
        }

        float travelTime = Mathf.Max(0.02f, distance / Mathf.Max(0.001f, bulletSpeed));

        TracerFx fx = Instantiate(tracerPrefab);
        fx.Play(start, end, travelTime);
        fx.SetImpact(hitFlesh, hitNormal);

        shellEmitter.Emit(1);
        if (muzzleFxPrefab)
            Instantiate(muzzleFxPrefab, muzzle.position, muzzle.rotation, muzzle);
    }

   


    private void FireHitscan_Server(NetInput input)
    {
        if (gunMuzzle == null)
            return;

        Vector3 origin = gunMuzzle.position;

        Vector3 dir = gunMuzzle.forward;
        dir.y = 0f; // top-down
        if (dir.sqrMagnitude < 0.0001f)
            return;

        dir.Normalize();


        // Scatter на сервере (тот же seed на этом тике)
        int seed = BuildShotSeed(Runner.Tick);
        dir = ApplyScatter(dir, scatterAngleDeg, seed);

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

        float dist = Vector3.Distance(origin, endPoint);
        float travelTime = Mathf.Max(0.02f, dist / Mathf.Max(0.001f, bulletSpeed));
        int travelTicks = Mathf.Max(1, Mathf.RoundToInt(travelTime * Runner.TickRate));

        bool hitFlesh = (didHit && targetObj != null); // для твоей структуры этого достаточно
        Vector3 hitNormal = -dir; // если нет нормали из lag hit

        NetworkHealth health;
        hitFlesh = targetObj != null && targetObj.TryGetComponent(out health);

        // подтверждённый FX видят все КРОМЕ стрелка (Plan A)
        RPC_SpawnTracerConfirmed(origin, endPoint, travelTicks, hitFlesh, hitNormal);
        RPC_EjectShell();
        RPC_MuzzleFx();
       


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
            health.ApplyDamage(finalDamage, pd.Owner); // вместо Player.TakeDamage
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnTracerConfirmed(Vector3 start, Vector3 end, int travelTicks)
    {
        // Это НОРМАЛЬНО: RpcTargets.All зовёт и стрелка тоже, поэтому фильтруем так.
        // (Если бы у тебя была цель “all except input authority”, можно было бы иначе.)
        if (HasInputAuthority)
            return;

        if (tracerPrefab == null || Runner == null)
            return;

        float duration = Mathf.Max(0.02f, travelTicks / (float)Runner.TickRate);

        TracerFx fx = Instantiate(tracerPrefab);
        fx.Play(start, end, duration); // duration обязателен
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_EjectShell()
    {
        if (HasInputAuthority)
            return;

        if (Runner == null)
            return;

        shellEmitter.Emit(1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_MuzzleFx()
    {
        if (HasInputAuthority)
            return;

        if (Runner == null)
            return;

        if (muzzleFxPrefab)
            Instantiate(muzzleFxPrefab, muzzle.position, muzzle.rotation, muzzle);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnTracerConfirmed(Vector3 start, Vector3 end, int travelTicks, bool hitFlesh, Vector3 hitNormal)
    {
        if (HasInputAuthority)
            return;

        if (tracerPrefab == null || Runner == null)
            return;

        float duration = Mathf.Max(0.02f, travelTicks / (float)Runner.TickRate);

        TracerFx fx = Instantiate(tracerPrefab);
        fx.Play(start, end, duration);
        fx.SetImpact(hitFlesh, hitNormal);
    }


    // ===== Scatter helpers =====

    private int BuildShotSeed(int tick)
    {
        unchecked
        {
            int id = Object != null ? (int)Object.Id.Raw : 0;
            int auth = Object != null ? Object.InputAuthority.PlayerId : 0;

            return (tick * 73856093) ^ (id * 19349663) ^ (auth * 83492791);
        }
    }


    private static Vector3 ApplyScatter(Vector3 forward, float angleDeg, int seed)
    {
        if (angleDeg <= 0.0001f)
            return forward;

        // System.Random не трогает UnityEngine.Random state
        var rng = new System.Random(seed);

        // равномерно по площади круга: r = sqrt(u)
        double u1 = rng.NextDouble();
        double u2 = rng.NextDouble();

        double r = Math.Sqrt(u1);
        double theta = 2.0 * Math.PI * u2;

        // отклонение в радианах
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float x = (float)(r * Math.Cos(theta)) * angleRad;
        float y = (float)(r * Math.Sin(theta)) * angleRad;

        // строим ортонормальный базис вокруг forward
        Vector3 f = forward.normalized;
        Vector3 right = Vector3.Cross(f, Vector3.up);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(f, Vector3.forward);
        right.Normalize();
        Vector3 up = Vector3.Cross(right, f).normalized;

        // малые углы: dir = f + right*x + up*y
        Vector3 dir = (f + right * x + up * y);
        return dir.sqrMagnitude < 0.000001f ? f : dir.normalized;
    }
}
