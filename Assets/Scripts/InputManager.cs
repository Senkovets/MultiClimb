using Fusion;
using Fusion.Addons.KCC;
using Fusion.Menu;
using Fusion.Sockets;
using MultiClimb.Menu;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Собирает ЛОКАЛЬНЫЙ ввод (мышь/клава) и отдаёт его Fusion через OnInput.
/// Важно:
/// - Здесь разрешено использовать Camera/Mouse (это локальная логика).
/// - В сетевом геймплее (FixedUpdateNetwork на NetworkBehaviour) Camera/Mouse запрещены.
/// - Мы вычисляем AimDirection (мировой вектор прицеливания) и Fire-кнопку.
/// </summary>
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
        // Сбрасываем накопленный ввод после того, как он был отправлен в OnInput.
        if (resetInput)
        {
            resetInput = false;
            accumulatedInput = default;
        }

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        NetworkButtons buttons = default;

        // Тоггл курсора (оставил как у тебя, но без лишней логики)
        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // При желании можно вернуть lock тут.
                // Cursor.lockState = CursorLockMode.Locked;
                // Cursor.visible = false;
            }
        }

        // ====== MOUSE ======
        if (mouse != null)
        {
            // ВАЖНО: Fire должен быть edge-event (wasPressedThisFrame), а не isPressed.
            buttons.Set(InputButton.Fire, mouse.leftButton.isPressed);

            // Если UseAbility уже занят на левую кнопку — это конфликт.
            // Сейчас оставляю твою схему, но учти: Fire и UseAbility одновременно на LMB — ошибка дизайна ввода.
            // Лучше переназначить UseAbility на другую кнопку.
            buttons.Set(InputButton.UseAbility, mouse.leftButton.isPressed);
            buttons.Set(InputButton.Grapple, mouse.rightButton.isPressed);

            // Накопление дельты (если нужно для камеры/поворота)
            Vector2 mouseDelta = mouse.delta.ReadValue();
            Vector2 lookRotationDelta = new(-mouseDelta.y, mouseDelta.x);
            mouseDeltaAccumulator.Accumulate(lookRotationDelta);
        }

        // ====== KEYBOARD ======
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

            // Выбор способности
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

        // Присваиваем кнопки и выбранную способность.
        // Не OR-им со старыми кнопками — accumulatedInput и так живёт 1 тик (мы его сбрасываем после OnInput).
        accumulatedInput.Buttons = buttons;
        accumulatedInput.AbilityMode = selectedAbility;

        // Вычисляем прицеливание (мировой вектор) ЛОКАЛЬНО.
        accumulatedInput.AimDirection = GetAimDirection(LocalPlayer);
        accumulatedInput.LookYaw = GetMouseYaw(LocalPlayer); // оставил, если где-то ещё используется
    }

    /// <summary>
    /// Мировой вектор прицеливания: из позиции игрока в точку под курсором на плоскости XZ.
    /// Это то, что сервер будет использовать для направления выстрела. Сервер не трогает Camera/Mouse.
    /// </summary>
    private Vector3 GetAimDirection(Player player)
    {
        if (player == null)
            return Vector3.zero;

        Camera cam = Camera.main;
        Mouse mouse = Mouse.current;

        if (cam == null || mouse == null)
            return Vector3.zero;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        // Плоскость на уровне игрока (можно заменить на уровень оружия/плеча, если нужно)
        Plane plane = new Plane(Vector3.up, player.transform.position);

        if (!plane.Raycast(ray, out float enter))
            return Vector3.zero;

        Vector3 hit = ray.GetPoint(enter);
        Vector3 dir = hit - player.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return dir.normalized;
    }

    /// <summary>
    /// Абсолютный yaw (угол поворота по Y) в сторону курсора на плоскости XZ.
    /// </summary>
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
        // Нормализуем движение
        accumulatedInput.Direction.Normalize();

        // ВАЖНО: AimDirection уже нормализован, но на всякий случай:
        if (accumulatedInput.AimDirection.sqrMagnitude > 1.001f)
            accumulatedInput.AimDirection.Normalize();

        input.Set(accumulatedInput);

        // После отправки ввода в текущем тике — сбросим накопленное
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
            // При желании можно залочить курсор тут.
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
