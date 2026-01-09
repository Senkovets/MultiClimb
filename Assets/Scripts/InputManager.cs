// InputManager.cs
using Fusion;
using Fusion.Addons.KCC;
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

    public Vector2 AccumulatedMouseDelta => mouseDeltaAccumulator.AccumulatedValue;

    [Header("Aim / Raycast")]
    [SerializeField] private LayerMask aimMask = ~0;
    [SerializeField] private float aimMaxDistance = 500f;

    [Header("Headshot Highlight")]
    [SerializeField] private LayerMask hitboxLayers = ~0;
    [SerializeField] private float headCheckDistance = 100f;

    private NetInput accumulatedInput;
    private Vector2Accumulator mouseDeltaAccumulator = new() { SmoothingWindow = 0.025f };
    private bool resetInput;

    private AbilityMode selectedAbility;
    private bool cachedCriticalAim;

    private void Awake()
    {
        // Safety: if hitboxLayers accidentally set to 0, fallback to Default
        if (hitboxLayers == 0)
        {
            int def = LayerMask.NameToLayer("Default");
            hitboxLayers = def >= 0 ? (1 << def) : ~0;
        }

        if (aimMask == 0)
            aimMask = ~0;
    }

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
        Vector2 moveDirection = Vector2.zero;

        // Cursor unlock hotkeys (optional)
        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // Mouse buttons
        if (mouse != null)
        {
            buttons.Set((int)InputButton.Fire, mouse.leftButton.isPressed);
            buttons.Set((int)InputButton.UseAbility, mouse.leftButton.isPressed);
            buttons.Set((int)InputButton.Grapple, mouse.rightButton.isPressed);

            Vector2 mouseDelta = mouse.delta.ReadValue();
            Vector2 lookRotationDelta = new(-mouseDelta.y, mouseDelta.x);
            mouseDeltaAccumulator.Accumulate(lookRotationDelta);
        }
        else
        {
            // Legacy fallback
            buttons.Set((int)InputButton.Fire, Input.GetMouseButton(0));
            buttons.Set((int)InputButton.UseAbility, Input.GetMouseButton(0));
            buttons.Set((int)InputButton.Grapple, Input.GetMouseButton(1));
        }

        // Keyboard
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) moveDirection += Vector2.up;
            if (keyboard.sKey.isPressed) moveDirection += Vector2.down;
            if (keyboard.aKey.isPressed) moveDirection += Vector2.left;
            if (keyboard.dKey.isPressed) moveDirection += Vector2.right;

            buttons.Set((int)InputButton.Jump, keyboard.spaceKey.isPressed);
            buttons.Set((int)InputButton.Glide, keyboard.leftShiftKey.isPressed);
            buttons.Set((int)InputButton.Reload, keyboard.rKey.isPressed);

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

            // Ready
            if (keyboard.rKey.wasPressedThisFrame && LocalPlayer != null)
                LocalPlayer.RPC_SetReady();
        }

        accumulatedInput.Direction = moveDirection;

        // ===== Aim =====
        Vector3 aimDirection = ComputeAimDirection();
        aimDirection.y = 0f;

        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            // Hard fallback to player forward so it never becomes (0,0,0)
            Vector3 fb = LocalPlayer != null ? LocalPlayer.transform.forward : Vector3.forward;
            fb.y = 0f;
            aimDirection = fb.sqrMagnitude < 0.0001f ? Vector3.forward : fb.normalized;
        }
        else
        {
            aimDirection.Normalize();
        }

        accumulatedInput.AimDirection = aimDirection;
        accumulatedInput.LookYaw = GetYawFromDirection(aimDirection);

        // ===== Critical aim cached (head check) =====
        cachedCriticalAim = IsAimingAtHead();
        accumulatedInput.IsCriticalAim = cachedCriticalAim;

        accumulatedInput.Buttons = buttons;
        accumulatedInput.AbilityMode = selectedAbility;
    }

    private Vector3 ComputeAimDirection()
    {
        if (LocalPlayer == null)
            return Vector3.forward;

        Camera cam = Camera.main;
        if (cam == null)
            return LocalPlayer.transform.forward;

        Vector2 screenPos = RecoilController.GetAimScreenPosition();
        Ray ray = cam.ScreenPointToRay(screenPos);

        // 1) Try raycast into world
        if (Physics.Raycast(ray, out RaycastHit hit, aimMaxDistance, aimMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = hit.point - LocalPlayer.transform.position;
            dir.y = 0f;
            return dir;
        }

        // 2) Fallback: plane at player Y (top-down standard)
        Plane plane = new Plane(Vector3.up, new Vector3(0f, LocalPlayer.transform.position.y, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 point = ray.GetPoint(enter);
            Vector3 dir = point - LocalPlayer.transform.position;
            dir.y = 0f;
            return dir;
        }

        // 3) Final fallback
        return LocalPlayer.transform.forward;
    }

    private float GetYawFromDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return LocalPlayer != null ? LocalPlayer.transform.eulerAngles.y : 0f;

        return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    private bool IsAimingAtHead()
    {
        if (LocalPlayer == null)
            return false;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(RecoilController.GetAimScreenPosition());

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                headCheckDistance,
                hitboxLayers,
                QueryTriggerInteraction.Collide))
            return false;

        if (!hit.collider.TryGetComponent(out PlayerHitbox hb))
            return false;

        return hb.Type == HitboxType.Head;
    }

    // ====== Fusion callbacks ======

    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Normalize movement for net determinism
        if (accumulatedInput.Direction.sqrMagnitude > 1f)
            accumulatedInput.Direction.Normalize();

        // AimDirection must never be zero
        Vector3 dir = accumulatedInput.AimDirection;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
        {
            Vector3 fb = LocalPlayer != null ? LocalPlayer.transform.forward : Vector3.forward;
            fb.y = 0f;
            dir = fb.sqrMagnitude < 0.0001f ? Vector3.forward : fb.normalized;
        }
        else
        {
            dir.Normalize();
        }

        accumulatedInput.AimDirection = dir;
        accumulatedInput.LookYaw = GetYawFromDirection(dir);

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

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            // Optional: lock cursor here
            // Cursor.lockState = CursorLockMode.Locked;
            // Cursor.visible = false;
        }
    }

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
