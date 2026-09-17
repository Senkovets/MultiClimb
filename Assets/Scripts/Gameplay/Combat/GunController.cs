using Fusion;
using System;
using System.Collections.Generic;
using _Project.CodeBase.Movement;
using _Project.CodeBase.Weapons;
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// Стрельба. Характеристики берутся из WeaponConfig текущего оружия
    /// (через WeaponInventory), здесь только ссылки на сцену и FX.
    ///
    /// ВАЖНО ПРО АРХИТЕКТУРУ: решение о выстреле принимается ОДИН раз,
    /// в FixedUpdateNetwork. Fusion прогоняет его и на сервере, и на
    /// клиенте с input authority — с одним инпутом на одном тике.
    /// Сервер делает урон, клиент рисует FX.
    ///
    /// Предсказания в Render() больше нет: два независимых кода-пути
    /// не могли совпадать (разная частота, разные origin), и на оружии
    /// с длинным кулдауном это давало лишние или пропущенные выстрелы.
    /// </summary>
    public class GunController : NetworkBehaviour
    {
        [Header("Owner")]
        [SerializeField] private Player player;
        [SerializeField] private PlayerLocomotion locomotion;

        [Tooltip("Источник характеристик оружия и патронов")]
        [SerializeField] private WeaponInventory inventory;
      
        [Tooltip("Визуальная точка вылета: вспышка, гильзы, начало трассы. " +
                 "Может быть под ModelRoot, на расчёт не влияет.")]
        [SerializeField] private Transform gunMuzzle;

        [Header("Collision")]
        [Tooltip("По каким слоям пуля регистрирует попадание")]
        [SerializeField] private LayerMask hitLayers;

        [Header("Visual (NOT networked)")]
        [SerializeField] private TracerFx tracerPrefab;
        [SerializeField] private ParticleSystem shellEmitter;
        [SerializeField] private GameObject muzzleFxPrefab;

        [Tooltip("Transform для muzzle flash. Обычно тот же что gunMuzzle.")]
        [SerializeField] private Transform muzzleFxAnchor;

        [Tooltip("Эффект попадания в плоть. Спавнится только по " +
                 "подтверждению сервера — клиент не решает попал ли он.")]
        [SerializeField] private GameObject fleshImpactPrefab;

        [Header("Recoil")]
        [SerializeField] private RecoilPatternApplier recoilPattern;

        [Header("Weapon Switch")]
        [Tooltip("Задержка перед первым выстрелом после смены оружия, сек")]
        [SerializeField] private float switchDelay = 0.25f;

        // ---- Networked ----

        [Networked] private int NextFireTick { get; set; }
        [Networked] private NetworkButtons PreviousButtons { get; set; }
        [Networked] private byte LastWeaponId { get; set; }

        // ---- Local ----

        private readonly List<PendingDamage> _pending = new();

        private struct PendingDamage
        {
            public TickTimer Timer;
            public NetworkObject Target;
            public float Damage;
            public bool IsCritical;
            public float CriticalMultiplier;
            public Vector3 HitPoint;
            public Player Owner;
        }

        // =========================================================
        // Доступ к характеристикам оружия
        // =========================================================

        private WeaponConfig Weapon => inventory != null ? inventory.CurrentWeapon : null;

        /// <summary>Читает AimMarkerManager для отрисовки разлёта прицела</summary>
        public float CurrentScatter
        {
            get
            {
                WeaponConfig w = Weapon;
                return w != null ? w.ScatterAngleDeg : 0f;
            }
        }

        /// <summary>
        /// Кулдаун в тиках. НЕ кэшируется: оружие меняется в рантайме.
        /// </summary>
        private int CooldownTicks
        {
            get
            {
                WeaponConfig w = Weapon;
                float rate = w != null ? Mathf.Max(0.01f, w.FireRate) : 0.1f;
                return Mathf.Max(1, Mathf.CeilToInt(rate * Runner.TickRate));
            }
        }

        // =========================================================
        // Lifecycle
        // =========================================================

        public override void Spawned()
        {
            if (player == null)
                player = GetComponentInParent<Player>();

            if (inventory == null)
                inventory = GetComponentInParent<WeaponInventory>();

            if (locomotion == null)
                locomotion = GetComponentInParent<PlayerLocomotion>();

            if (recoilPattern == null)
                recoilPattern = GetComponent<RecoilPatternApplier>();

            if (HasStateAuthority)
                NextFireTick = Runner.Tick;
        }

        // =========================================================
        // Единый код-путь выстрела
        // =========================================================

        public override void FixedUpdateNetwork()
        {
            // Урон обрабатывает только сервер. Делаем это всегда,
            // даже если игрок мёртв: пули выпущенные до смерти
            // должны долететь.
            if (HasStateAuthority)
                ProcessPendingDamage();

            // Дальше идут И сервер, И клиент с input authority.
            // Клиент выполняет тот же код в режиме предсказания —
            // поэтому его FX совпадают с серверным выстрелом.
            if (!HasStateAuthority && !HasInputAuthority)
                return;

            if (player == null || player.IsDead)
                return;

            if (!GetInput(out NetInput input))
                return;

            WeaponConfig weapon = Weapon;
            if (weapon == null)
                return;

            if (HandleWeaponSwitch(weapon, input))
                return;

            bool wantFire = ReadFireIntent(input, weapon.FireMode);
            PreviousButtons = input.Buttons;

            if (!wantFire)
                return;

            if (Runner.Tick < NextFireTick)
                return;

            if (inventory == null || !inventory.HasAmmoForCurrent)
                return;

            if (locomotion != null && !locomotion.CanFire)
                return;

            // --- Выстрел состоялся. Роли расходятся. ---

            int shotSeed = BuildShotSeed(Runner.Tick);

            if (HasStateAuthority)
            {
                FireHitscan_Server(input, weapon, shotSeed);
                inventory.ConsumeAmmo(1);
            }

            // FX только у стрелка и только на прямом проходе.
            // Runner.IsForward отсекает ре-симуляцию: без него один
            // выстрел даст несколько трасс на плохом пинге.
            if (HasInputAuthority && Runner.IsForward)
                PlayLocalShotFx(input, weapon, shotSeed);

            NextFireTick = Runner.Tick + CooldownTicks;

            if (weapon.FireMode == FireMode.Bolt)
                NextFireTick += weapon.BoltReloadTicks;
        }

        /// <summary>
        /// Обрабатывает смену оружия. true = этот тик пропускаем.
        /// PreviousButtons = input.Buttons гасит зажатую кнопку,
        /// чтобы новое оружие не выстрелило само.
        /// </summary>
        private bool HandleWeaponSwitch(WeaponConfig weapon, NetInput input)
        {
            if (weapon.WeaponId == LastWeaponId)
                return false;

            LastWeaponId = weapon.WeaponId;

            int delayTicks = Mathf.CeilToInt(switchDelay * Runner.TickRate);
            NextFireTick = Mathf.Max(NextFireTick, Runner.Tick + delayTicks);

            PreviousButtons = input.Buttons;
            return true;
        }

        private bool ReadFireIntent(NetInput input, FireMode mode)
        {
            switch (mode)
            {
                case FireMode.Auto:
                    return input.Buttons.IsSet((int)InputButton.Fire);

                case FireMode.Semi:
                case FireMode.Bolt:
                    return input.Buttons.WasPressed(PreviousButtons, (int)InputButton.Fire);

                default:
                    return false;
            }
        }

        // =========================================================
        // Сервер: расчёт попаданий
        // =========================================================

        private void FireHitscan_Server(NetInput input, WeaponConfig weapon, int shotSeed)
        {
            if (!TryGetShot(input, weapon, out Vector3 origin, out Vector3 baseDir))
                return;
 
            int pellets = Mathf.Max(1, weapon.PelletsPerShot);
            ushort fleshMask = 0;
 
            for (int i = 0; i < pellets; i++)
            {
                int pelletSeed = BuildPelletSeed(shotSeed, i);
                Vector3 dir = ApplyScatter(baseDir, weapon.ScatterAngleDeg, pelletSeed);
 
                if (FireSinglePellet_Server(origin, dir, weapon, input.IsCriticalAim) && i < 16)
                    fleshMask |= (ushort)(1 << i);
            }
 
            RPC_SpawnShotConfirmed(origin, baseDir, shotSeed, fleshMask);
        }

        /// <summary>
        /// Один луч. Возвращает true если попал в плоть — для маски в RPC.
        /// </summary>
        private bool FireSinglePellet_Server(
            Vector3 origin, Vector3 dir, WeaponConfig weapon, bool isCritical)
        {
            ResolveServerHit(origin, dir, weapon,
                             out Vector3 endPoint,
                             out NetworkObject targetObj);

            if (targetObj == null || !targetObj.IsValid)
                return false;

            if (!targetObj.TryGetComponent<NetworkHealth>(out _))
                return false;

            float dist = Vector3.Distance(origin, endPoint);
            int travelTicks = DistanceToTravelTicks(dist, weapon.BulletSpeed);

            _pending.Add(new PendingDamage
            {
                Timer = TickTimer.CreateFromTicks(Runner, travelTicks),
                Target = targetObj,
                Damage = weapon.GetDamageAtDistance(dist),
                IsCritical = isCritical,
                CriticalMultiplier = weapon.CriticalMultiplier,
                HitPoint = endPoint,
                Owner = player
            });

            return true;
        }

        /// <summary>
        /// Ищет цель с лаг-компенсацией. targetObj = null означает
        /// что попадания по живой цели не было.
        /// </summary>
        private void ResolveServerHit(
            Vector3 origin, Vector3 dir, WeaponConfig weapon,
            out Vector3 endPoint, out NetworkObject targetObj)
        {
            targetObj = null;
            endPoint = origin + dir * weapon.MaxDistance;

            bool didHit = Runner.LagCompensation.Raycast(
                origin, dir, weapon.MaxDistance,
                Object.InputAuthority,
                out LagCompensatedHit hit,
                hitLayers,
                HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority);

            if (!didHit)
                return;

            endPoint = hit.Point;
            targetObj = ResolveNetworkObject(hit);

            if (targetObj == null)
                return;

            // В себя и в труп не попадаем. IgnoreInputAuthority защищает
            // только хитбоксы, а IncludePhysX пропускает свою капсулу.
            if (IsSelf(targetObj) || IsDeadPlayer(targetObj))
            {
                targetObj = null;
                endPoint = origin + dir * weapon.MaxDistance;
            }
        }

        private static NetworkObject ResolveNetworkObject(LagCompensatedHit hit)
        {
            if (hit.Hitbox != null && hit.Hitbox.Root != null)
            {
                NetworkObject fromHitbox = hit.Hitbox.Root.GetComponent<NetworkObject>();

                return fromHitbox != null
                    ? fromHitbox
                    : hit.Hitbox.Root.GetComponentInParent<NetworkObject>();
            }

            if (hit.GameObject != null)
                return hit.GameObject.GetComponentInParent<NetworkObject>();

            return null;
        }

        private bool IsSelf(NetworkObject obj)
        {
            NetworkObject mine = player != null ? player.Object : Object;
            return mine != null && obj == mine;
        }

        private static bool IsDeadPlayer(NetworkObject obj)
        {
            return obj.TryGetComponent(out Player p) && p.IsDead;
        }

        private void ProcessPendingDamage()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingDamage pd = _pending[i];

                if (!pd.Timer.Expired(Runner))
                    continue;

                _pending.RemoveAt(i);

                if (pd.Target == null || !pd.Target.IsValid)
                    continue;

                if (!pd.Target.TryGetComponent(out NetworkHealth health))
                    health = pd.Target.GetComponentInParent<NetworkHealth>();

                if (health == null)
                    continue;

                float finalDamage = pd.IsCritical
                    ? pd.Damage * pd.CriticalMultiplier
                    : pd.Damage;

                // Передаём реальную точку попадания, а не центр цели —
                // иначе попапы урона кучкуются в одном месте
                health.ApplyDamage(finalDamage, pd.Owner, pd.HitPoint, pd.IsCritical);
            }
        }

        // =========================================================
        // FX у стрелка
        // =========================================================

        /// <summary>
        /// Вызывается из FixedUpdateNetwork на том же тике и с тем же
        /// seed что и серверный выстрел — поэтому трассы совпадают
        /// с реальными попаданиями.
        /// </summary>
        private void PlayLocalShotFx(NetInput input, WeaponConfig weapon, int shotSeed)
        {
            if (tracerPrefab == null)
                return;
 
            if (!TryGetShot(input, weapon, out Vector3 origin, out Vector3 baseDir))
                return;
 
            int pellets = Mathf.Max(1, weapon.PelletsPerShot);
 
            for (int i = 0; i < pellets; i++)
            {
                int pelletSeed = BuildPelletSeed(shotSeed, i);
                Vector3 pelletDir = ApplyScatter(baseDir, weapon.ScatterAngleDeg, pelletSeed);
 
                SpawnTracer(origin, pelletDir, weapon);
            }
 
            if (shellEmitter != null)
                shellEmitter.Emit(1);
 
            SpawnMuzzleFx();
            ApplyRecoil(weapon, shotSeed);
 
            AimMarkerManager.Instance?.OnShoot();
        }
        
        /// <summary>
        /// Трасса. origin вычисленный, не из transform — поэтому
        /// одинаковый у стрелка, наблюдателя и сервера.
        /// </summary>
        private void SpawnTracer(Vector3 origin, Vector3 dir, WeaponConfig weapon)
        {
            Vector3 end = origin + dir * weapon.MaxDistance;
            float distance = weapon.MaxDistance;
            Vector3 normal = -dir;
            bool isFlesh = false;
 
            if (Physics.Raycast(origin, dir, out RaycastHit hit,
                    weapon.MaxDistance, hitLayers,
                    QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                distance = hit.distance;
                normal = hit.normal;
                isFlesh = hit.collider.GetComponentInParent<NetworkHealth>() != null;
            }
 
            float travelTime = Mathf.Max(0.02f, distance / Mathf.Max(0.001f, weapon.BulletSpeed));
 
            TracerFx fx = Instantiate(tracerPrefab);
            fx.Play(origin, end, travelTime);
            fx.SetImpact(isFlesh, normal);
        }
        
        private void SpawnMuzzleFx()
        {
            if (muzzleFxPrefab == null)
                return;

            Transform anchor = muzzleFxAnchor != null ? muzzleFxAnchor : gunMuzzle;
            if (anchor == null)
                return;

            Instantiate(muzzleFxPrefab, anchor.position, anchor.rotation, anchor);
        }

        private void ApplyRecoil(WeaponConfig weapon, int seed)
        {
            if (recoilPattern == null)
                recoilPattern = GetComponent<RecoilPatternApplier>();

            // TODO: чтобы паттерн менялся вместе с оружием, нужен метод
            // SetPattern(RecoilPatternSO) в RecoilPatternApplier.
            if (recoilPattern != null)
            {
                recoilPattern.NotifyShot(seed);
                return;
            }

            RecoilController.NotifyShot(
                weapon.RecoilV, weapon.RecoilH, weapon.RecoilTime,
                weapon.RecoilRecoverDelay, weapon.RecoilRecoverSpeed);
        }

        // =========================================================
        // RPC — подтверждение выстрела
        // =========================================================

        /// <summary>
        /// Подтверждённый залп. Наблюдатели рисуют полные трассы,
        /// стрелок — только всплески крови (трассы он уже нарисовал).
        ///
        /// Дробины восстанавливаются по seed — один RPC вместо девяти.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SpawnShotConfirmed(
            Vector3 origin, Vector3 baseDir, int shotSeed, ushort fleshMask)
        {
            if (Runner == null)
                return;

            // Оружие берём из синхронизированного инвентаря
            WeaponConfig weapon = Weapon;
            if (weapon == null)
                return;

            int pellets = Mathf.Max(1, weapon.PelletsPerShot);
            bool isShooter = HasInputAuthority;

            for (int i = 0; i < pellets; i++)
            {
                int pelletSeed = BuildPelletSeed(shotSeed, i);
                Vector3 dir = ApplyScatter(baseDir, weapon.ScatterAngleDeg, pelletSeed);

                bool hitFlesh = i < 16 && (fleshMask & (1 << i)) != 0;

                if (isShooter)
                {
                    // Стрелку — только подтверждённая кровь.
                    // Так она никогда не появится там где урона не было.
                    if (hitFlesh)
                        SpawnFleshImpact(origin, dir, weapon);
                }
                else
                {
                    SpawnObservedTracer(origin, dir, weapon, hitFlesh);
                }
            }

            // Гильза и вспышка — здесь же, а не отдельными RPC.
            // Стрелок их уже сделал локально в PlayLocalShotFx.
            if (isShooter)
                return;

            if (shellEmitter != null)
                shellEmitter.Emit(1);

            SpawnMuzzleFx();
        }

        /// <summary>
        /// Всплеск крови у стрелка. Точку ищем локальным рейкастом —
        /// сам факт попадания уже подтверждён сервером.
        /// </summary>
        private void SpawnFleshImpact(Vector3 origin, Vector3 dir, WeaponConfig weapon)
        {
            if (fleshImpactPrefab == null)
                return;

            if (!Physics.Raycast(origin, dir, out RaycastHit hit,
                                 weapon.MaxDistance, hitLayers,
                                 QueryTriggerInteraction.Ignore))
                return;

            Instantiate(fleshImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        }

        /// <summary>
        /// Трасса у наблюдателя. Конец ищем локальным рейкастом —
        /// это косметика, точность до сантиметра не нужна.
        /// </summary>
        private void SpawnObservedTracer(
            Vector3 origin, Vector3 dir, WeaponConfig weapon, bool hitFlesh)
        {
            if (tracerPrefab == null)
                return;
 
            Vector3 end = origin + dir * weapon.MaxDistance;
            float distance = weapon.MaxDistance;
            Vector3 normal = -dir;
 
            if (Physics.Raycast(origin, dir, out RaycastHit hit,
                    weapon.MaxDistance, hitLayers,
                    QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                distance = hit.distance;
                normal = hit.normal;
            }
 
            float travelTime = Mathf.Max(0.02f, distance / Mathf.Max(0.001f, weapon.BulletSpeed));
 
            TracerFx fx = Instantiate(tracerPrefab);
            fx.Play(origin, end, travelTime);
            fx.SetImpact(hitFlesh, normal);   // от сервера
        }

        

        // =========================================================
        // Направление, тайминг, разброс
        // =========================================================

        /// <summary>
        /// Направление и точка выстрела. Считаются по формуле из
        /// позиции игрока, BodyYaw и константы из конфига — никаких
        /// transform под ModelRoot, поэтому сервер и клиент совпадают.
        /// </summary>
        private bool TryGetShot(
            NetInput input, WeaponConfig weapon,
            out Vector3 origin, out Vector3 dir)
        {
            origin = Vector3.zero;
            dir = Vector3.zero;
 
            if (player == null)
                return false;
 
            float bodyYaw = locomotion != null
                ? locomotion.BodyYaw
                : player.transform.eulerAngles.y;
 
            return AimGeometry.TryGetFireDirection(
                player.transform.position,
                bodyYaw,
                weapon.MuzzleLocalOffset,
                input.AimPoint,
                out dir,
                out origin);
        }

        private int DistanceToTravelTicks(float distance, float bulletSpeed)
        {
            float travelTime = Mathf.Max(0.02f, distance / Mathf.Max(0.001f, bulletSpeed));
            return Mathf.Max(1, Mathf.RoundToInt(travelTime * Runner.TickRate));
        }

        /// <summary>
        /// Детерминированный seed: одинаковый у сервера и клиента
        /// на одном тике для одного игрока.
        /// </summary>
        private int BuildShotSeed(int tick)
        {
            unchecked
            {
                int id = Object != null ? (int)Object.Id.Raw : 0;
                int auth = Object != null ? Object.InputAuthority.PlayerId : 0;

                return (tick * 73856093) ^ (id * 19349663) ^ (auth * 83492791);
            }
        }

        /// <summary>
        /// Seed конкретной дробины. ОБЯЗАН совпадать на сервере
        /// и клиенте, иначе трассы разойдутся с попаданиями.
        /// </summary>
        private static int BuildPelletSeed(int shotSeed, int pelletIndex)
        {
            unchecked
            {
                const int prime = (int)2654435761u;   // = -1640531535
                return shotSeed ^ ((pelletIndex + 1) * prime);
            }
        }

        private static Vector3 ApplyScatter(Vector3 forward, float angleDeg, int seed)
        {
            if (angleDeg <= 0.0001f)
                return forward;

            // System.Random не трогает состояние UnityEngine.Random
            var rng = new System.Random(seed);

            // Равномерно по площади круга: r = sqrt(u)
            double u1 = rng.NextDouble();
            double u2 = rng.NextDouble();

            double r = Math.Sqrt(u1);
            double theta = 2.0 * Math.PI * u2;

            float angleRad = angleDeg * Mathf.Deg2Rad;
            float x = (float)(r * Math.Cos(theta)) * angleRad;
            float y = (float)(r * Math.Sin(theta)) * angleRad;

            Vector3 f = forward.normalized;

            Vector3 right = Vector3.Cross(f, Vector3.up);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(f, Vector3.forward);
            right.Normalize();

            Vector3 up = Vector3.Cross(right, f).normalized;

            Vector3 dir = f + right * x + up * y;
            return dir.sqrMagnitude < 0.000001f ? f : dir.normalized;
        }
    }
}