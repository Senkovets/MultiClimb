using Fusion;
using UnityEngine;

public enum ThrowableState : byte
{
    Idle = 0,
    Preparing = 1
}

public interface IThrowableProjectile
{
    void Launch(Vector3 startPos, Vector3 velocity, float effectRadius);
}

public sealed class ThrowableController : NetworkBehaviour
{
    [Header("Config")]
    [SerializeField] private ThrowableConfig config;

    [Header("Throw Setup")]
    [Tooltip("Точка старта броска (сокет руки/оружия).")]
    [SerializeField] private Transform throwOrigin;

    [Tooltip("Префаб сетевого снаряда (NetworkObject). На нём должен быть компонент, реализующий IThrowableProjectile.")]
    [SerializeField] private NetworkPrefabRef projectilePrefab;

    [Header("Input")]
    [Tooltip("Какая кнопка включает режим прицеливания способности.")]
    [SerializeField] private InputButton prepareButton = InputButton.Reload;

    [Header("Debug")]
    [SerializeField] private bool logState;

    // --- Network state ---
    [Networked] public ThrowableState State { get; private set; }
    [Networked] public Vector3 CurrentAimPoint { get; private set; }

    public Vector3 AimPointForLine { get; private set; }
    [Networked] private int LockedUntilTick { get; set; }

    public bool IsPreparing => State == ThrowableState.Preparing;

    // Events (локальные, не сетевые)
    public event System.Action PrepareStarted;
    public event System.Action PrepareCanceled;
    public event System.Action Thrown;

    public float EffectRadius => config != null ? config.EffectRadius : 0f;
    public float MaxThrowRange => config != null ? config.MaxThrowRange : 0f;
    public float VerticalSpeed => config != null ? config.VerticalSpeed : 0f;

    public override void FixedUpdateNetwork()
    {
        if (config == null || throwOrigin == null)
            return;

        if (!GetInput(out NetInput input))
            return;

        // Простейший lock, чтобы потом легко расширить под систему способностей
        if (Runner.Tick < LockedUntilTick)
        {
            // если был prepare — сбрасываем
            if (State != ThrowableState.Idle)
                SetState(ThrowableState.Idle, canceled: true);
            return;
        }

        bool hold = input.Buttons.IsSet(prepareButton);

        AimPointForLine = input.AimDirection;

        if (State == ThrowableState.Idle)
        {
            if (hold)
            {
                // вход в prepare
                SetState(ThrowableState.Preparing, canceled: false);
            }
        }
        else // Preparing
        {
            if (!hold)
            {
                // отпускание = commit
                TryThrow(input);
                return;
            }

            // обновляем aim point каждый тик (на всех — для консистентного HUD)
            CurrentAimPoint = ClampAimPoint(input.AimPoint);
        }
    }

    private void TryThrow(in NetInput input)
    {
        // перед броском финально обновим aimpoint
        Vector3 aim = ClampAimPoint(input.AimPoint);
        CurrentAimPoint = aim;

        // Только StateAuthority спавнит сетевой объект
        if (Object.HasStateAuthority)
        {
            Vector3 start = throwOrigin.position;
            Vector3 vel = CalculateVelocity(start, aim, config.VerticalSpeed);

            Runner.Spawn(projectilePrefab, start, Quaternion.identity, Object.InputAuthority,
                (runner, obj) =>
                {
                    // Компонент на префабе должен уметь Launch(...)
                    var launchable = obj.GetComponent<IThrowableProjectile>();
                    if (launchable != null)
                        launchable.Launch(start, vel, config.EffectRadius);
                    else
                    {
                        // fallback: попробуем Rigidbody, если просто физика
                        var rb = obj.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.position = start;
                            rb.velocity = vel;
                        }
                    }
                });
        }

        // lock на несколько тиков, чтобы нельзя было тут же стрелять/спамить (расширишь до ActionLock)
        if (config.LockTicksAfterThrow > 0)
            LockedUntilTick = Runner.Tick + config.LockTicksAfterThrow;

        SetState(ThrowableState.Idle, canceled: false);
        Thrown?.Invoke();
    }

    private void SetState(ThrowableState newState, bool canceled)
    {
        if (State == newState)
            return;

        State = newState;

        if (logState && Object.HasInputAuthority)
            Debug.Log($"[Throwable] State -> {State}");

        if (newState == ThrowableState.Preparing)
        {
            // подстрахуемся — выставим aimpoint сразу
            CurrentAimPoint = ClampAimPoint(CurrentAimPoint);
            PrepareStarted?.Invoke();
        }
        else
        {
            if (canceled)
                PrepareCanceled?.Invoke();
        }
    }

    private Vector3 ClampAimPoint(Vector3 rawAimPoint)
    {
        Vector3 start = throwOrigin.position;

        Vector3 delta = rawAimPoint - start;
        delta.y = 0f;

        float max = Mathf.Max(0.1f, config.MaxThrowRange);
        float sqr = delta.sqrMagnitude;

        if (sqr > max * max)
        {
            delta = delta.normalized * max;
            rawAimPoint = new Vector3(start.x + delta.x, rawAimPoint.y, start.z + delta.z);
        }

        return rawAimPoint;
    }

    // Баллистика как в твоём Dac-подходе: задаём вертикальную скорость, горизонталь подбираем по времени
    public static Vector3 CalculateVelocity(Vector3 start, Vector3 target, float verticalSpeed)
    {
        float g = Mathf.Abs(Physics.gravity.y);

        Vector3 delta = target - start;
        Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);

        float y = delta.y;
        float v = Mathf.Max(0.01f, verticalSpeed);

        // tUp = v/g, y(t)= v*t - 0.5*g*t^2
        float tUp = v / g;

        // Решаем для tDown из: y = v*tUp - 0.5*g*tUp^2 - 0.5*g*tDown^2  (т.к. скорость в вершине 0)
        // y = (v^2)/(2g) - 0.5*g*tDown^2  => tDown = sqrt( max(0, (v^2)/(g^2) - 2y/g ) )?
        // Более надёжно: используем общую формулу времени падения из вершины:
        // heightAtApex = v^2/(2g); targetOffsetFromApex = heightAtApex - y
        float heightApex = (v * v) / (2f * g);
        float dropFromApex = Mathf.Max(0f, heightApex - y);
        float tDown = Mathf.Sqrt(2f * dropFromApex / g);

        float tTotal = tUp + tDown;
        tTotal = Mathf.Max(0.01f, tTotal);

        Vector3 velXZ = deltaXZ / tTotal;
        Vector3 velY = Vector3.up * v;

        return velXZ + velY;
    }
}
