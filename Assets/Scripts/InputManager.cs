using Fusion;
using Fusion.Menu;
using Fusion.Sockets;
using MultiClimb.Menu;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : SimulationBehaviour, IBeforeUpdate, INetworkRunnerCallbacks
{
    public Player LocalPlayer;

    [Header("Aim Raycast")]
    [SerializeField] private LayerMask aimMask = ~0;
    [SerializeField] private float aimMaxDistance = 2000f;

    [Header("Headshot Check")]
    [SerializeField] private LayerMask hitboxLayers = ~0;
    [SerializeField] private float headCheckDistance = 100f;

    public NetInput LastLocalInput { get; private set; }

    private NetInput accumulatedInput;
    private bool resetInput;
    private AbilityMode selectedAbility;

    void IBeforeUpdate.BeforeUpdate()
    {
        if (resetInput)
        {
            resetInput = false;
            accumulatedInput = default;
        }

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        NetworkButtons buttons = default;
        Vector2 move = Vector2.zero;

        // Mouse
        if (mouse != null)
        {
            buttons.Set((int)InputButton.Fire, mouse.leftButton.isPressed);
            buttons.Set((int)InputButton.UseAbility, mouse.leftButton.isPressed);
            buttons.Set((int)InputButton.Grapple, mouse.rightButton.isPressed);
        }
        else
        {
            buttons.Set((int)InputButton.Fire, Input.GetMouseButton(0));
            buttons.Set((int)InputButton.UseAbility, Input.GetMouseButton(0));
            buttons.Set((int)InputButton.Grapple, Input.GetMouseButton(1));
        }

        // Keyboard
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) move += Vector2.up;
            if (keyboard.sKey.isPressed) move += Vector2.down;
            if (keyboard.aKey.isPressed) move += Vector2.left;
            if (keyboard.dKey.isPressed) move += Vector2.right;

            buttons.Set((int)InputButton.Jump, keyboard.spaceKey.isPressed);
            buttons.Set((int)InputButton.Glide, keyboard.leftShiftKey.isPressed);
            buttons.Set((int)InputButton.Reload, keyboard.rKey.isPressed);

            if (keyboard.rKey.wasPressedThisFrame && LocalPlayer != null)
                LocalPlayer.RPC_SetReady();

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                selectedAbility = AbilityMode.BreakBlock;
                UIManager.Singleton.SelectAbility(AbilityMode.BreakBlock);
            }
            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                selectedAbility = AbilityMode.Cage;
                UIManager.Singleton.SelectAbility(AbilityMode.Cage);
            }
            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                selectedAbility = AbilityMode.Shove;
                UIManager.Singleton.SelectAbility(AbilityMode.Shove);
            }
        }

        accumulatedInput.Direction = move;
        accumulatedInput.Buttons = buttons;
        accumulatedInput.AbilityMode = selectedAbility;

        // === AimPoint / AimDirectionXZ / AimDirection3D (fireDirection5) ===
        Vector3 aimPoint = ComputeAimPoint();
        accumulatedInput.AimPoint = aimPoint;

        Vector3 dirXZ = ComputeAimDirectionXZ(aimPoint);
        accumulatedInput.AimDirection = dirXZ;
        accumulatedInput.LookYaw = Mathf.Atan2(dirXZ.x, dirXZ.z) * Mathf.Rad2Deg;

        Vector3 dir3D = ComputeAimDirectionLikeOldCode(aimPoint, dirXZ);
        accumulatedInput.AimDirection3D = dir3D;

        accumulatedInput.IsCriticalAim = IsAimingAtHead();

        LastLocalInput = accumulatedInput;
    }

    private Vector3 ComputeAimPoint()
    {
        if (LocalPlayer == null)
            return Vector3.zero;

        Camera cam = Camera.main;
        if (cam == null)
            return LocalPlayer.transform.position + LocalPlayer.transform.forward * 10f;

        Ray ray = cam.ScreenPointToRay(RecoilController.GetAimScreenPosition());

        if (Physics.Raycast(ray, out RaycastHit hit, aimMaxDistance, aimMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        // fallback: плоскость на высоте игрока
        Plane plane = new Plane(Vector3.up, new Vector3(0f, LocalPlayer.transform.position.y, 0f));
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        return LocalPlayer.transform.position + LocalPlayer.transform.forward * 10f;
    }

    private Vector3 ComputeAimDirectionXZ(Vector3 aimPoint)
    {
        if (LocalPlayer == null)
            return Vector3.forward;

        Vector3 dir = aimPoint - LocalPlayer.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
        {
            Vector3 fb = LocalPlayer.transform.forward;
            fb.y = 0f;
            return fb.sqrMagnitude < 0.0001f ? Vector3.forward : fb.normalized;
        }

        return dir.normalized;
    }

    // Это прямой перенос твоей логики fireDirection5
    private Vector3 ComputeAimDirectionLikeOldCode(Vector3 targetPoint, Vector3 fallbackDirXZ)
    {
        if (LocalPlayer == null)
            return fallbackDirXZ.sqrMagnitude < 0.0001f ? Vector3.forward : fallbackDirXZ;

        Camera cam = Camera.main;
        if (cam == null)
            return fallbackDirXZ.sqrMagnitude < 0.0001f ? Vector3.forward : fallbackDirXZ;

        GunController gun = LocalPlayer.GetComponentInChildren<GunController>();
        Transform muzzle = gun != null ? gun.Muzzle : null;

        Vector3 muzzlePos = muzzle != null ? muzzle.position : LocalPlayer.transform.position;
        float muzzleY = muzzlePos.y;

        Vector3 normalToCamera = (cam.transform.position - targetPoint);
        if (normalToCamera.sqrMagnitude < 0.000001f)
            return fallbackDirXZ.sqrMagnitude < 0.0001f ? Vector3.forward : fallbackDirXZ;

        normalToCamera.Normalize();

        // Если камера почти параллельна плоскости по Y — пересечение будет мусор
        if (Mathf.Abs(normalToCamera.y) < 0.0001f)
        {
            Vector3 direct = (targetPoint - muzzlePos);
            if (direct.sqrMagnitude < 0.0001f)
                return fallbackDirXZ.sqrMagnitude < 0.0001f ? Vector3.forward : fallbackDirXZ;

            return direct.normalized;
        }

        float t = (muzzleY - targetPoint.y) / normalToCamera.y;
        Vector3 pointAtMuzzleY = targetPoint + normalToCamera * t;

        Vector3 fireDir = pointAtMuzzleY - muzzlePos;
        if (fireDir.sqrMagnitude < 0.0001f)
            return fallbackDirXZ.sqrMagnitude < 0.0001f ? Vector3.forward : fallbackDirXZ;

        return fireDir.normalized;
    }

    private bool IsAimingAtHead()
    {
        if (LocalPlayer == null)
            return false;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(RecoilController.GetAimScreenPosition());

        if (!Physics.Raycast(ray, out RaycastHit hit, headCheckDistance, hitboxLayers, QueryTriggerInteraction.Collide))
            return false;

        if (!hit.collider.TryGetComponent(out PlayerHitbox hb))
            return false;

        return hb.Type == HitboxType.Head;
    }

    // ====== Fusion callbacks ======
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (accumulatedInput.Direction.sqrMagnitude > 1f)
            accumulatedInput.Direction.Normalize();

        // страховка
        Vector3 dXZ = accumulatedInput.AimDirection; dXZ.y = 0f;
        if (dXZ.sqrMagnitude > 0.0001f) dXZ.Normalize(); else dXZ = Vector3.forward;
        accumulatedInput.AimDirection = dXZ;
        accumulatedInput.LookYaw = Mathf.Atan2(dXZ.x, dXZ.z) * Mathf.Rad2Deg;

        Vector3 d3 = accumulatedInput.AimDirection3D;
        if (d3.sqrMagnitude > 0.0001f) d3.Normalize(); else d3 = dXZ;
        accumulatedInput.AimDirection3D = d3;

        input.Set(accumulatedInput);
        resetInput = true;
    }

    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

    async void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (shutdownReason == ShutdownReason.DisconnectedByPluginLogic)
        {
            await FindFirstObjectByType<MenuConnectionBehaviour>(FindObjectsInactive.Include)
                .DisconnectAsync(ConnectFailReason.Disconnect);

            FindFirstObjectByType<FusionMenuUIGameplay>(FindObjectsInactive.Include)
                .Controller.Show<FusionMenuUIMain>();
        }
    }

    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
