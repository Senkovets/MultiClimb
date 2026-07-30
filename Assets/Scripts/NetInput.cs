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

    public Vector3 AimDirection;

    public Vector3 AimPoint;

    public Vector3 AimDirection3D;

    public AbilityMode AbilityMode;
    
    public byte DesiredWeaponSlot;
}
