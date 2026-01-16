using UnityEngine;

[CreateAssetMenu(menuName = "Game/Throwable Config", fileName = "ThrowableConfig")]
public sealed class ThrowableConfig : ScriptableObject
{
    [Header("Gameplay")]
    [Min(0.1f)] public float MaxThrowRange = 18f;
    [Min(0.1f)] public float EffectRadius = 4.5f;

    [Tooltip("¬ертикальна€ скорость броска (как в твоЄм Dac-коде).")]
    [Min(0.1f)] public float VerticalSpeed = 7.5f;

    [Header("Trajectory (HUD)")]
    [Min(4)] public int TrajectorySegments = 22;
    [Min(0.01f)] public float TrajectoryStep = 0.06f; // секунды между семплами

    [Tooltip("ƒл€ раннего обрыва траектории при столкновении.")]
    public LayerMask TrajectoryHitMask;

    [Min(0.01f)] public float TrajectorySphereCastRadius = 0.2f;

    [Header("Throw Timing")]
    [Tooltip("—колько тиков удерживаем 'зан€то' после броска, чтобы блокировать параллельные действи€.")]
    [Min(0)] public int LockTicksAfterThrow = 8;
}
