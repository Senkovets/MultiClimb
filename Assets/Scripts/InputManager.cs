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

    private NetInput accumulatedInput;
    private Vector2Accumulator mouseDeltaAccumulator = new() { SmoothingWindow = 0.025f };
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

        // Курсор
        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // MOUSE
        if (mouse != null)
        {
            buttons.Set(InputButton.Fire, mouse.leftButton.isPressed);
            buttons.Set(InputButton.UseAbility, mouse.leftButton.isPressed);
            buttons.Set(InputButton.Grapple, mouse.rightButton.isPressed);

            Vector2 mouseDelta = mouse.delta.ReadValue();
            Vector2 lookRotationDelta = new(-mouseDelta.y, mouseDelta.x);
            mouseDeltaAccumulator.Accumulate(lookRotationDelta);
        }

        // KEYBOARD
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame && LocalPlayer != null)
                LocalPlayer.RPC_SetReady();

            Vector2 moveDirection = Vector2.zero;

            if (keyboard.wKey.isPressed) moveDirection += Vector2.up;
            if (keyboard.sKey.isPressed) moveDirection += Vector2.down;
            if (keyboard.aKey.isPressed) moveDirection += Vector2.left;
            if (keyboard.dKey.isPressed) moveDirection += Vector2.right;

            accumulatedInput.Direction += moveDirection;

            buttons.Set(InputButton.Jump, keyboard.spaceKey.isPressed);
            buttons.Set(InputButton.Glide, keyboard.leftShiftKey.isPressed);

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

        accumulatedInput.Buttons = buttons;
        accumulatedInput.AbilityMode = selectedAbility;

        // ✅ ИСПОЛЬЗУЕМ МЕТОД ИЗ GunController ДЛЯ КОНСИСТЕНТНОСТИ
        accumulatedInput.AimDirection = GetAimDirectionFromGun();
        accumulatedInput.LookYaw = GetMouseYaw(LocalPlayer);
    }

    /// <summary>
    /// ✅ НОВЫЙ МЕТОД: использует GunController.GetDirection() для консистентности
    /// </summary>
    private Vector3 GetAimDirectionFromGun()
    {
        if (LocalPlayer == null)
            return Vector3.forward;

        GunController gun = LocalPlayer.GetComponent<GunController>();
        if (gun != null)
        {
            return gun.GetDirection();
        }

        // Fallback на простое направление
        return GetAimDirection(LocalPlayer);
    }

    /// <summary>
    /// Оригинальный метод (теперь fallback)
    /// </summary>
    private Vector3 GetAimDirection(Player player)
    {
        if (player == null)
            return Vector3.forward;

        Camera cam = Camera.main;
        Mouse mouse = Mouse.current;

        if (cam == null || mouse == null)
            return Vector3.forward;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        Plane plane = new Plane(Vector3.up, player.transform.position);

        if (!plane.Raycast(ray, out float enter))
            return player.transform.forward;

        Vector3 hit = ray.GetPoint(enter);
        Vector3 dir = hit - player.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return player.transform.forward;

        return dir.normalized;
    }

    private float GetMouseYaw(Player player)
    {
        if (player == null)
            return 0f;

        Camera cam = Camera.main;
        Mouse mouse = Mouse.current;

        if (cam == null || mouse == null)
            return 0f;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        Plane plane = new Plane(Vector3.up, player.transform.position);
        if (!plane.Raycast(ray, out float enter))
            return player.transform.eulerAngles.y;

        Vector3 hit = ray.GetPoint(enter);
        Vector3 dir = hit - player.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return player.transform.eulerAngles.y;

        return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    // ====== Fusion callbacks ======

    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        accumulatedInput.Direction.Normalize();

        if (accumulatedInput.AimDirection.sqrMagnitude > 1.001f)
            accumulatedInput.AimDirection.Normalize();

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
            // Можно залочить курсор
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