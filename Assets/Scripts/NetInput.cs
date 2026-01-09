using Fusion;
using UnityEngine;

public enum InputButton
{
    Jump,
    UseAbility,
    Grapple,
    Glide,
    Fire,
    Reload
}

public struct NetInput : INetworkInput
{
    public NetworkButtons Buttons;
    public Vector2 Direction;

    public bool IsCriticalAim;
    public float LookYaw;

    // XZ для поворота/йоу (можно оставлять)
    public Vector3 AimDirection;

    // Точка под прицелом (обычно по ground/aim mask)
    public Vector3 AimPoint;

    // 3D-направление выстрела, рассчитанное как в твоём старом коде (fireDirection5)
    public Vector3 AimDirection3D;

    public AbilityMode AbilityMode;
}
