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
            UpdateBodyYaw(input, dt);
            UpdateMovement(input);
 
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
            bool hasInput = input.Direction.magnitude >= config.SprintMinInput;
 
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
 
            Vector3 moveDir = InputToWorld(input.Direction);
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
 
        private void UpdateMovement(NetInput input)
        {
            if (State == MoveState.Rolling)
            {
                // Импульс уже выдан, управления в перекате нет
                kcc.SetInputDirection(Vector3.zero);
                return;
            }
 
            Vector3 moveDir = InputToWorld(input.Direction);
 
            // ХИТРОСТЬ: KCC берёт скорость из KinematicSpeed процессора,
            // а магнитуда InputDirection работает множителем.
            // Ставим KinematicSpeed = SprintSpeed, а ходьбу получаем
            // уменьшением магнитуды. Так не нужно лезть в процессор.
            float multiplier = GetSpeedMultiplier(input);
 
            kcc.SetInputDirection(moveDir * multiplier);
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