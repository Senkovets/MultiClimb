// NetInput.cs (или где у теб€ объ€влен NetInput / InputButton)
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
    public float LookYaw;       // абсолютный Yaw (градусы)
    public Vector3 AimDirection;

    public AbilityMode AbilityMode;
}
