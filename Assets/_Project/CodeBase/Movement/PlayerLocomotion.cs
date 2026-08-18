using Fusion;
using Fusion.Addons.KCC;
using UnityEngine;

namespace _Project.CodeBase.Movement
{
    public enum MoveState : byte
    {
        Walking,
        Sprinting,
        Rolling
    }
 
    public class PlayerLocomotion : NetworkBehaviour
    {
        [SerializeField] private LocomotionConfig config;
        [SerializeField] private KCC kcc;
        [SerializeField] private Player player;
 
        [Tooltip("Отдельный transform под визуал и оружие. " +
                 "Вращается независимо от KCC — иначе спринт " +
                 "утащит за собой камеру.")]
        [SerializeField] private Transform modelRoot;
 
        // ---- Networked ----
 
        [Networked] public MoveState State { get; private set; }
 
        /// <summary>Куда смотрит прицел. Ведёт KCC и камеру.</summary>
        [Networked] public float AimYaw { get; private set; }
 
        /// <summary>Куда развёрнут корпус. Ведёт модель.</summary>
        [Networked] public float BodyYaw { get; private set; }
 
        [Networked] private TickTimer RollTimer { get; set; }
        [Networked] private TickTimer RollCooldownTimer { get; set; }
        [Networked] private TickTimer ReturnTimer { get; set; }
        [Networked] private NetworkButtons PreviousButtons { get; set; }
 
        // ---- Local ----
 
        private bool _yawInitialized;
        
        /// <summary>
        /// Сглаженное направление движения. Именно оно идёт в KCC,
        /// а не сырой ввод — отсюда инерция.
        /// </summary>
        [Networked] private Vector3 SmoothedMoveDir { get; set; }
 
        public bool IsSprinting => State == MoveState.Sprinting;
        public bool IsRolling => State == MoveState.Rolling;
 
        /// <summary>Читает GunController перед выстрелом</summary>
        public bool CanFire
        {
            get
            {
                if (config == null)
                    return true;
 
                if (IsRolling)
                    return false;
 
                return !(config.BlockFireWhileSprinting && IsSprinting);
            }
        }
 
        public override void Spawned()
        {
            if (kcc == null)
                kcc = GetComponent<KCC>();
 
            if (player == null)
                player = GetComponent<Player>();
 
            if (modelRoot == null)
            {
                Debug.LogError("[Locomotion] modelRoot не назначен. " +
                               "Спринт будет вращать камеру.", this);
                modelRoot = transform;
            }
        }
 
        // =========================================================
        // Сервер + предсказание
        // =========================================================
 
        public override void FixedUpdateNetwork()
        {
            if (config == null)
                return;
 
            if (player != null && player.IsDead)
            {
                kcc.SetInputDirection(Vector3.zero);
                return;
            }
 
            if (!GetInput(out NetInput input))
                return;
 
            float dt = Runner.DeltaTime;
 
            UpdateState(input);
            UpdateAimYaw(input, dt);
            UpdateMovement(input, dt);
            UpdateBodyYaw(input, dt);
            
 
            PreviousButtons = input.Buttons;
        }
 
        // ---- Состояние ----
 
        private void UpdateState(NetInput input)
        {
            if (State == MoveState.Rolling)
            {
                if (RollTimer.Expired(Runner))
                    State = MoveState.Walking;
 
                return;
            }
 
            if (TryStartRoll(input))
                return;
 
            bool wantsSprint = input.Buttons.IsSet((int)InputButton.Sprint);
            bool hasInput = input.Direction.magnitude >= config.SprintMinInput && SmoothedMoveDir.magnitude > 0.1f;
 
            bool wasSprinting = State == MoveState.Sprinting;
            bool nowSprinting = wantsSprint && hasInput;
 
            // Отпустил спринт — включаем плавный доворот к прицелу
            if (wasSprinting && !nowSprinting)
                ReturnTimer = TickTimer.CreateFromSeconds(Runner, config.ReturnDuration);
 
            State = nowSprinting ? MoveState.Sprinting : MoveState.Walking;
        }
 
        private bool TryStartRoll(NetInput input)
        {
            if (!input.Buttons.WasPressed(PreviousButtons, (int)InputButton.Roll))
                return false;
 
            if (!RollCooldownTimer.ExpiredOrNotRunning(Runner))
                return false;
 
            if (!kcc.FixedData.IsGrounded)
                return false;
 
            Vector3 dir = InputToWorld(input.Direction);
 
            // Без ввода катимся туда, куда смотрит корпус
            if (dir.sqrMagnitude < 0.01f)
                dir = YawToDirection(BodyYaw);
 
            dir.Normalize();
 
            // Тот же механизм что у Shove — импульс с затуханием
            kcc.AddExternalImpulse(dir * config.RollImpulse);
 
            BodyYaw = DirectionToYaw(dir);
 
            RollTimer = TickTimer.CreateFromSeconds(Runner, config.RollDuration);
            RollCooldownTimer = TickTimer.CreateFromSeconds(Runner, config.RollCooldown);
 
            State = MoveState.Rolling;
            return true;
        }
 
        // ---- Прицел: ведёт KCC и камеру ----
 
        private void UpdateAimYaw(NetInput input, float dt)
        {
            Vector3 aim = input.AimDirection3D;
            aim.y = 0f;
 
            if (aim.sqrMagnitude > 0.0001f)
            {
                float target = DirectionToYaw(aim);
 
                if (!_yawInitialized)
                {
                    AimYaw = target;
                    BodyYaw = target;
                    _yawInitialized = true;
                }
 
                // Прицел следует за курсором мгновенно —
                // сглаживание только у корпуса
                AimYaw = target;
            }
 
            kcc.SetLookRotation(0f, AimYaw);
        }
 
        // ---- Корпус: ведёт модель ----
 
        private void UpdateBodyYaw(NetInput input, float dt)
        {
            // В перекате корпус зафиксирован
            if (State == MoveState.Rolling)
            {
                ApplyBodyRotation();
                return;
            }
 
            Vector3 moveDir = SmoothedMoveDir;
            bool isAiming = input.Buttons.IsSet((int)InputButton.Aim);
 
            float targetYaw;
            float turnSpeed;
 
            if (State == MoveState.Sprinting && moveDir.sqrMagnitude > 0.01f)
            {
                targetYaw = DirectionToYaw(moveDir);
                turnSpeed = config.SprintTurnSpeed;
            }
            else
            {
                targetYaw = AimYaw;
 
                if (isAiming)
                    turnSpeed = config.AimTurnSpeed;
                else if (!ReturnTimer.ExpiredOrNotRunning(Runner))
                    turnSpeed = config.ReturnTurnSpeed;
                else
                    turnSpeed = config.WalkTurnSpeed;
            }
 
            BodyYaw = Mathf.MoveTowardsAngle(BodyYaw, targetYaw, turnSpeed * dt);
            ApplyBodyRotation();
        }
 
        private void ApplyBodyRotation()
        {
            if (modelRoot == null || modelRoot == transform)
                return;
 
            // Мировой поворот: modelRoot не должен наследовать
            // поворот корня, который KCC крутит по прицелу
            modelRoot.rotation = Quaternion.Euler(0f, BodyYaw, 0f);
        }
 
        // ---- Движение ----
 
        private void UpdateMovement(NetInput input, float dt)
        {
            if (State == MoveState.Rolling)
            {
                SmoothedMoveDir = Vector3.zero;
                kcc.SetInputDirection(Vector3.zero);
                return;
            }

            Vector3 rawDir = InputToWorld(input.Direction);
            float multiplier = GetSpeedMultiplier(input);

            Vector3 target = rawDir * multiplier;
            target *= GetTurnPenalty(rawDir);

            bool accelerating = target.sqrMagnitude > SmoothedMoveDir.sqrMagnitude;

            float time = accelerating
                ? config.AccelerationTime
                : config.DecelerationTime;

            if (State == MoveState.Sprinting)
                time /= Mathf.Max(0.01f, config.SprintTurnInertia);

            float t = time > 0.001f
                ? 1f - Mathf.Exp(-dt / time)
                : 1f;

            SmoothedMoveDir = Vector3.Lerp(SmoothedMoveDir, target, t);

            // Обнуляем ТОЛЬКО когда игрок отпустил клавиши.
            // Порог по одной лишь магнитуде убивал разгон: при прицеливании
            // первый шаг меньше порога, движение не могло начаться.
            if (target.sqrMagnitude < 0.0001f && SmoothedMoveDir.sqrMagnitude < 0.0004f)
                SmoothedMoveDir = Vector3.zero;
            // ↑↑↑ КОНЕЦ ИЗМЕНЕНИЯ ↑↑↑

            kcc.SetInputDirection(SmoothedMoveDir);
        }

        /// <summary>
        /// Чем сильнее игрок меняет направление на бегу, тем больше
        /// теряет скорость. Разворот назад бьёт сильнее всего.
        /// </summary>
        private float GetTurnPenalty(Vector3 rawDir)
        {
            if (State != MoveState.Sprinting)
                return 1f;

            // Оба вектора должны быть ненулевыми, иначе normalized даст NaN
            if (rawDir.sqrMagnitude < 0.01f || SmoothedMoveDir.sqrMagnitude < 0.01f)
                return 1f;

            // 1 = бежим прямо, 0 = поворот на 90, -1 = разворот назад
            float alignment = Vector3.Dot(
                SmoothedMoveDir.normalized, rawDir.normalized);

            // alignment -1..1 → t 0..1
            float t = (alignment + 1f) * 0.5f;

            return Mathf.Lerp(config.MinTurnSpeedPenalty, 1f, t);
        }
 
        private float GetSpeedMultiplier(NetInput input)
        {
            if (State == MoveState.Sprinting)
                return 1f;
 
            float m = config.WalkSpeed / Mathf.Max(0.01f, config.SprintSpeed);
 
            if (input.Buttons.IsSet((int)InputButton.Aim))
                m *= config.AimSpeedMultiplier;
 
            return m;
        }
 
        // ---- Плавность визуала между тиками ----
 
        public override void Render()
        {
            // Без этого корпус будет дёргаться на низком тикрейте
            ApplyBodyRotation();
        }
 
        // =========================================================
        // Утилиты
        // =========================================================
 
        private static Vector3 InputToWorld(Vector2 input)
        {
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }
 
        private static float DirectionToYaw(Vector3 dir)
        {
            dir.y = 0f;
 
            if (dir.sqrMagnitude < 0.0001f)
                return 0f;
 
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
 
        private static Vector3 YawToDirection(float yaw)
        {
            float rad = yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }
    }
}