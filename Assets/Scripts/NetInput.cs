// NetInput.cs
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
    public float LookYaw;          // абсолютный yaw (градусы)
    public Vector3 AimDirection;   // нормализованный XZ

    public AbilityMode AbilityMode;
}
