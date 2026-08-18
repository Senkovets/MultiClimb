using Fusion;
using System;
using System.Collections.Generic;
using _Project.CodeBase.Movement;
using _Project.CodeBase.Weapons;
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// Стрельба. Все характеристики берутся из WeaponConfig текущего
    /// оружия (через WeaponInventory), а не из полей этого компонента.
    /// Здесь остаются только ссылки на сцену и на FX-префабы.
    /// </summary>
    public class GunController : NetworkBehaviour
    {
        [Header("Owner")]
        [SerializeField] private Player player;
        [SerializeField] private PlayerLocomotion locomotion;

        [Tooltip("Источник характеристик оружия и патронов")]
        [SerializeField] private WeaponInventory inventory;

        [Header("Muzzle")]
        [Tooltip("Точка вылета пули. Используется и для расчёта направления.")]
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

        [Header("Local Predicted Tracer")]
        [SerializeField] private bool localTracerUseSpherecast = true;
        [SerializeField] private float localTracerRadius = 0.06f;

        [Header("Recoil")]
        [SerializeField] private RecoilPatternApplier recoilPattern;

        // ---- Networked ----

        [Networked] private int NextFireTick { get; set; }
        [Networked] private NetworkButtons PreviousButtons { get; set; }

        // ---- Local ----

        private readonly List<PendingDamage> _pending = new();
        private int _nextLocalFxTick = -1;
        /// <summary>
        /// Локальная копия NextFireTick. Нужна чтобы предсказание
        /// соблюдало ту же скорострельность что и сервер.
        /// </summary>
        private int _localNextFireTick;
        private byte _lastSeenWeaponId;

        private struct PendingDamage
        {
            public float CriticalMultiplier;
            public TickTimer Timer;
            public NetworkObject Target;
            public float Damage;
            public bool IsCritical;
            public Player Owner;
            public Vector3 HitPoint;
        }

        // =========================================================
        // Доступ к характеристикам текущего оружия
        // =========================================================

        public Transform Muzzle => gunMuzzle;

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
        /// Кулдаун в тиках. НЕ кэшируется: оружие меняется в рантайме,
        /// значит и скорострельность.
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

            if (recoilPattern == null)
                recoilPattern = GetComponent<RecoilPatternApplier>();

            if (HasStateAuthority)
                NextFireTick = Runner.Tick;
        }

        // =========================================================
        // Сервер: логика выстрела
        // =========================================================

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;
            // Отложенный урон обрабатываем всегда, даже если игрок мёртв:
            // пули выпущенные до смерти должны долететь.
            ProcessPendingDamage();

            if (player == null || player.IsDead)
                return;

            if (!GetInput(out NetInput input))
                return;

            WeaponConfig weapon = Weapon;
            
            WeaponConfig dbgWeapon = Weapon;

            if (input.Buttons.Bits != 0)
                Debug.Log($"[Server] bits={input.Buttons.Bits} " +
                          $"fire={input.Buttons.IsSet((int)InputButton.Fire)}");
            
            if (weapon == null)
                return;

            bool wantFire = ReadFireIntent(input, weapon.FireMode);
            PreviousButtons = input.Buttons;

            if (!wantFire)
                return;

            if (Runner.Tick < NextFireTick)
                return;

            if (!inventory.HasAmmoForCurrent)
                return;

            if (locomotion != null && !locomotion.CanFire) return;
            
            FireHitscan_Server(input, weapon);
            inventory.ConsumeAmmo(1);

            NextFireTick = Runner.Tick + CooldownTicks;

            if (weapon.FireMode == FireMode.Bolt)
                NextFireTick += weapon.BoltReloadTicks;
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

        private void FireHitscan_Server(NetInput input, WeaponConfig weapon)
        {
            if (gunMuzzle == null)
                return;
 
            if (!TryGetFireDirection(input, out Vector3 baseDir))
                return;
 
            Vector3 origin = gunMuzzle.position;
            int shotSeed = BuildShotSeed(Runner.Tick);
 
            int pellets = Mathf.Max(1, weapon.PelletsPerShot);
 
            // Битовая маска: какие дробины попали в плоть.
            // Бит i соответствует дробине i. Максимум 16 дробин.
            ushort fleshMask = 0;
 
            for (int i = 0; i < pellets; i++)
            {
                int pelletSeed = BuildPelletSeed(shotSeed, i);
                Vector3 dir = ApplyScatter(baseDir, weapon.ScatterAngleDeg, pelletSeed);
 
                if (FireSinglePellet_Server(origin, dir, weapon, input.IsCriticalAim) && i < 16)
                    fleshMask |= (ushort)(1 << i);
            }
 
            // ОДИН RPC на весь залп
            RPC_SpawnShotConfirmed(origin, baseDir, shotSeed, fleshMask);
            RPC_EjectShell();
            RPC_MuzzleFx();
        }
        
        /// <summary>
        /// Один луч. spawnTracer=false для дробин кроме первой,
        /// иначе 8 дробин = 8 RPC и трафик улетает.
        /// </summary>
        /// <summary>
        /// Один луч. Возвращает true если попал в плоть —
        /// нужно для маски в RPC.
        /// </summary>
        private bool FireSinglePellet_Server(
            Vector3 origin,
            Vector3 dir,
            WeaponConfig weapon,
            bool isCritical)
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
        /// Seed для конкретной дробины. ОБЯЗАН совпадать на сервере
        /// и на клиенте, иначе предсказанные трейсеры разойдутся
        /// с реальными попаданиями.
        /// </summary>
        private static int BuildPelletSeed(int shotSeed, int pelletIndex)
        {
            unchecked
            {
                const int prime = (int)2654435761u;   // = -1640531535
                return shotSeed ^ ((pelletIndex + 1) * prime);
            }
        }

        /// <summary>
        /// Ищет цель с лаг-компенсацией. targetObj = null означает
        /// "попадания по живой цели не было" — пуля улетает в стену
        /// или в никуда.
        /// </summary>
        private void ResolveServerHit(
            Vector3 origin,
            Vector3 dir,
            WeaponConfig weapon,
            out Vector3 endPoint,
            out NetworkObject targetObj)
        {
            targetObj = null;
            endPoint = origin + dir * weapon.MaxDistance;

            bool didHit = Runner.LagCompensation.Raycast(
                origin,
                dir,
                weapon.MaxDistance,
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

            // В себя и в труп не попадаем — пуля летит дальше.
            // IgnoreInputAuthority защищает только хитбоксы,
            // а IncludePhysX пропускает собственную капсулу.
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
            // Сравниваем с NetworkObject игрока, а не с Object этого
            // компонента: оружие может лежать на дочернем объекте.
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

                health.ApplyDamage(finalDamage, pd.Owner,
                                   health.transform.position, pd.IsCritical);
            }
        }

        // =========================================================
        // Локальное предсказание FX (только у стрелка)
        // =========================================================

        /// <summary>
        /// Локальное предсказание FX. Работает только у стрелка —
        /// он видит трассы сразу, не дожидаясь подтверждения сервера.
        /// Остальные получают трассу через RPC_SpawnTracerConfirmed.
        /// </summary>
        public override void Render()
        {
            // Только стрелок предсказывает свои выстрелы
            if (!HasInputAuthority)
                return;
 
            if (player == null || player.IsDead)
                return;
 
            if (Runner == null || tracerPrefab == null || gunMuzzle == null)
                return;
 
            if (locomotion != null && !locomotion.CanFire) return;
            
            WeaponConfig weapon = Weapon;
            if (weapon == null)
                return;
            
            if (weapon.WeaponId != _lastSeenWeaponId)
            {
                _lastSeenWeaponId = weapon.WeaponId;
                _localNextFireTick = 0;
                _nextLocalFxTick = -1;
            }
 
            InputManager im = Runner.GetComponent<InputManager>();
            if (im == null)
                return;
 
            NetInput input = im.LastLocalInput;
 
            // Кнопка отпущена — сбрасываем цикл, чтобы Semi/Bolt
            // могли выстрелить снова при следующем нажатии
            if (!input.Buttons.IsSet((int)InputButton.Fire))
            {
                _nextLocalFxTick = -1;
                return;
            }
 
            if (inventory == null || !inventory.HasAmmoForCurrent)
                return;
 
            if (!ShouldSpawnLocalFxThisTick(weapon, out int tick))
                return;
 
            if (!TryGetFireDirection(input, out Vector3 baseDir))
                return;
 
            // Seed выстрела ОБЯЗАН совпадать с серверным на этом тике,
            // иначе предсказанные трассы разойдутся с реальными попаданиями
            int shotSeed = BuildShotSeed(tick);
 
            // Локально рисуем ВСЕ дробины: это не сеть, это дёшево.
            // Сервер по RPC отправит только одну трассу.
            int pellets = Mathf.Max(1, weapon.PelletsPerShot);
 
            for (int i = 0; i < pellets; i++)
            {
                int pelletSeed = BuildPelletSeed(shotSeed, i);
                Vector3 pelletDir = ApplyScatter(baseDir, weapon.ScatterAngleDeg, pelletSeed);
 
                SpawnLocalPredictedTracer(pelletDir, weapon);
            }
 
            // Гильза и вспышка — один раз на выстрел, а не на дробину
            if (shellEmitter != null)
                shellEmitter.Emit(1);
 
            SpawnMuzzleFx();
 
            ApplyRecoil(weapon, shotSeed);
 
            AimMarkerManager.Instance?.OnShoot();
        }

        private bool ShouldSpawnLocalFxThisTick(WeaponConfig weapon, out int tick)
        {
            tick = Runner.Tick;
 
            // Кулдаун — тот же что на сервере.
            // Без этой проверки быстрые клики рисуют выстрелы,
            // которые сервер отвергнет.
            if (tick < _localNextFireTick)
                return false;
 
            if (_nextLocalFxTick < 0)
                _nextLocalFxTick = tick;
 
            bool singleShot = weapon.FireMode == FireMode.Semi
                              || weapon.FireMode == FireMode.Bolt;
 
            if (singleShot)
            {
                // Один выстрел на одно нажатие
                if (tick != _nextLocalFxTick)
                    return false;
 
                _nextLocalFxTick = int.MaxValue;
            }
            else
            {
                if (tick < _nextLocalFxTick)
                    return false;
 
                _nextLocalFxTick = tick + CooldownTicks;
            }
 
            // Взводим локальный кулдаун ровно как сервер
            _localNextFireTick = tick + CooldownTicks;
 
            if (weapon.FireMode == FireMode.Bolt)
                _localNextFireTick += weapon.BoltReloadTicks;
 
            return true;
        }
        private void ApplyRecoil(WeaponConfig weapon, int seed)
        {
            if (recoilPattern == null)
                recoilPattern = GetComponent<RecoilPatternApplier>();

            // TODO: чтобы паттерн менялся вместе с оружием, нужен метод
            // SetPattern(RecoilPatternSO) в RecoilPatternApplier.
            // Тогда здесь: recoilPattern.SetPattern(weapon.RecoilPattern);
            if (recoilPattern != null)
            {
                recoilPattern.NotifyShot(seed);
                return;
            }

            RecoilController.NotifyShot(
                weapon.RecoilV,
                weapon.RecoilH,
                weapon.RecoilTime,
                weapon.RecoilRecoverDelay,
                weapon.RecoilRecoverSpeed);
        }

        private void SpawnLocalPredictedTracer(Vector3 dir, WeaponConfig weapon)
        {
            Vector3 start = gunMuzzle.position;
            Vector3 end = start + dir * weapon.MaxDistance;
            float distance = weapon.MaxDistance;
            bool hitFlesh = false;
            Vector3 hitNormal = -dir;

            if (TryFindNearestForeignHit(start, dir, weapon.MaxDistance, out RaycastHit best))
            {
                end = best.point;
                distance = best.distance;
                hitNormal = best.normal;

                hitFlesh = best.collider.GetComponentInParent<NetworkHealth>() != null
                        || best.collider.GetComponentInParent<Player>() != null;
            }

            float travelTime = Mathf.Max(0.02f, distance / Mathf.Max(0.001f, weapon.BulletSpeed));

            TracerFx fx = Instantiate(tracerPrefab);
            fx.Play(start, end, travelTime);
            fx.SetImpact(hitFlesh, hitNormal);

            if (shellEmitter != null)
                shellEmitter.Emit(1);

            SpawnMuzzleFx();
        }

        /// <summary>
        /// Ближайшее попадание, не принадлежащее самому стрелку.
        /// </summary>
        private bool TryFindNearestForeignHit(
            Vector3 start, Vector3 dir, float maxDistance, out RaycastHit best)
        {
            best = default;

            RaycastHit[] hits = localTracerUseSpherecast
                ? Physics.SphereCastAll(start, localTracerRadius, dir, maxDistance,
                                        hitLayers, QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(start, dir, maxDistance,
                                     hitLayers, QueryTriggerInteraction.Ignore);

            Transform ownRoot = player != null ? player.transform : transform.root;

            float bestDist = float.MaxValue;
            bool found = false;

            foreach (RaycastHit h in hits)
            {
                if (h.collider == null)
                    continue;

                if (h.collider.transform.IsChildOf(ownRoot))
                    continue;

                if (h.distance >= bestDist)
                    continue;

                bestDist = h.distance;
                best = h;
                found = true;
            }

            return found;
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

        // =========================================================
        // RPC — подтверждённые FX для всех КРОМЕ стрелка
        // =========================================================
       
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_EjectShell()
        {
            if (HasInputAuthority || Runner == null)
                return;

            if (shellEmitter != null)
                shellEmitter.Emit(1);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_MuzzleFx()
        {
            if (HasInputAuthority || Runner == null)
                return;

            SpawnMuzzleFx();
        }

        // =========================================================
        // Направление, тайминг, разброс
        // =========================================================

        /// <summary>
        /// Направление выстрела: от ствола В точку прицела.
        /// Не зависит от поворота игрока, поэтому не отстаёт от прицела
        /// и не страдает от бокового смещения ствола.
        /// </summary>
        private bool TryGetFireDirection(NetInput input, out Vector3 dir)
        {
            dir = Vector3.zero;

            if (gunMuzzle == null)
                return false;

            Vector3 raw = input.AimPoint - gunMuzzle.position;

            // Стрельба строго в горизонтальной плоскости
            raw.y = 0f;

            if (raw.sqrMagnitude < 0.0001f)
                return false;

            dir = raw.normalized;
            return true;
        }
        
        /// <summary>
        /// Подтверждённый залп для всех КРОМЕ стрелка.
        /// Наблюдатель сам считает дробины по seed — это дешевле
        /// чем слать 9 отдельных RPC.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SpawnShotConfirmed(
            Vector3 origin, Vector3 baseDir, int shotSeed, ushort fleshMask)
        {
            // Стрелок уже видел свой локальный залп
            if (HasInputAuthority)
                return;
 
            if (tracerPrefab == null || Runner == null)
                return;
 
            // Оружие берём из синхронизированного инвентаря —
            // наблюдатель знает чем стреляли
            WeaponConfig weapon = Weapon;
            if (weapon == null)
                return;
 
            int pellets = Mathf.Max(1, weapon.PelletsPerShot);
 
            for (int i = 0; i < pellets; i++)
            {
                int pelletSeed = BuildPelletSeed(shotSeed, i);
                Vector3 dir = ApplyScatter(baseDir, weapon.ScatterAngleDeg, pelletSeed);
 
                bool hitFlesh = i < 16 && (fleshMask & (1 << i)) != 0;
 
                SpawnObservedTracer(origin, dir, weapon, hitFlesh);
            }
        }
        
        /// <summary>
        /// Трасса у наблюдателя. Конец ищем локальным рейкастом —
        /// это косметика, точность до сантиметра не нужна.
        /// </summary>
        private void SpawnObservedTracer(
            Vector3 origin, Vector3 dir, WeaponConfig weapon, bool hitFlesh)
        {
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
            fx.SetImpact(hitFlesh, normal);
        }

        private int DistanceToTravelTicks(float distance, float bulletSpeed)
        {
            float travelTime = Mathf.Max(0.02f, distance / Mathf.Max(0.001f, bulletSpeed));
            return Mathf.Max(1, Mathf.RoundToInt(travelTime * Runner.TickRate));
        }

        /// <summary>
        /// Детерминированный seed: одинаковый у сервера и клиента
        /// на одном и том же тике для одного и того же игрока.
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