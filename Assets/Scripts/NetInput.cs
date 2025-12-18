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
    public float LookYaw;        // ? ÀÁÑÎËÞÒÍÛÉ ןמגמנמע
    public Vector3 AimDirection;
    public AbilityMode AbilityMode;
}