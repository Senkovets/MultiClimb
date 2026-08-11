using Fusion;
using Fusion.Menu;
using Fusion.Sockets;
using MultiClimb.Menu;
using System;
using System.Collections.Generic;
using _Project.CodeBase.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : SimulationBehaviour, IBeforeUpdate, INetworkRunnerCallbacks
{
    public Player LocalPlayer;

    [Header("Headshot Check")]
    [SerializeField] private LayerMask hitboxLayers = ~0;
    [SerializeField] private float headCheckDistance = 100f;

    [Header("Aim Marker")]
    [Tooltip("Чувствительность виртуального маркера прицела. Обычно 0.6 - 1.5")]
    [SerializeField] private float aimSensitivity = 1.0f;

    [Tooltip("Отступ чтобы маркер не упирался в край экрана")]
    [SerializeField] private float aimClampPadding = 10f;

    [Header("Rotation")]
    [Tooltip("Ограничение скорости поворота персонажа, град/сек")]
    [SerializeField] private float maxYawSpeedDegPerSec = 240f;

    [Header("Aim Plane")]
    [Tooltip("Высота прицельной плоскости над позицией игрока. " +
             "ДОЛЖНА совпадать с высотой ствола, иначе пули уйдут мимо прицела.")]
    [SerializeField] private float aimPlaneHeight = 1.2f;
    
    [Tooltip("Минимальное расстояние от игрока до точки прицела. " +
             "Не даёт пулям вылетать под углом когда прицел вплотную. " +
             "Ставь 2 - 3 метра.")]
    [SerializeField] private float minAimDistance = 2.5f;

    private byte desiredWeaponSlot = 0;

    public NetInput LastLocalInput { get; private set; }

    private NetInput accumulatedInput;
    private bool resetInput;
    private AbilityMode selectedAbility;

    private float _lastYaw;
    private Vector3 _lastAimDirXZ = Vector3.forward;
    
    [Header("DEBUG — удалить когда будут пикапы")]
    [SerializeField] private bool debugWeaponKeys = true;

    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }
    
    private void ReadDebugWeaponKeys(Keyboard keyboard)
    {
        if (!debugWeaponKeys || LocalPlayer == null)
            return;
 
        var inv = LocalPlayer.GetComponent<WeaponInventory>();
        if (inv == null)
            return;
 
        if (keyboard.f1Key.wasPressedThisFrame) inv.RPC_DebugGiveWeapon(1);
        if (keyboard.f2Key.wasPressedThisFrame) inv.RPC_DebugGiveWeapon(2);
        if (keyboard.f3Key.wasPressedThisFrame) inv.RPC_DebugGiveWeapon(3);
        if (keyboard.f4Key.wasPressedThisFrame) inv.RPC_DebugGiveWeapon(4);
    }

    // =============================================================
    // Главный цикл сбора инпута
    // =============================================================

    void IBeforeUpdate.BeforeUpdate()
    {
        if (resetInput)
        {
            resetInput = false;
            accumulatedInput = default;
        }

        ReadButtonsAndMovement();
        UpdateAimMarker();

        // Точка прицела считается всегда, даже без локального игрока
        accumulatedInput.AimPoint = ComputeAimPoint();

        if (LocalPlayer == null)
        {
            LastLocalInput = default;
            return;
        }

        UpdateAimDirectionAndYaw(accumulatedInput.AimPoint);

        accumulatedInput.IsCriticalAim = IsAimingAtHead();

        LastLocalInput = accumulatedInput;
    }

    // =============================================================
    // Кнопки и движение
    // =============================================================

    private void ReadButtonsAndMovement()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        NetworkButtons buttons = default;
        Vector2 move = Vector2.zero;

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

            ReadDebugWeaponKeys(keyboard);
            ReadWeaponSwitch(keyboard);
        }

        accumulatedInput.Direction = move;
        // Кнопки НАКАПЛИВАЕМ до отправки: если игрок нажал и отпустил
        // между тиками, нажатие не должно потеряться.
        accumulatedInput.Buttons = new NetworkButtons(
            accumulatedInput.Buttons.Bits | buttons.Bits);
        accumulatedInput.DesiredWeaponSlot = desiredWeaponSlot;
    }

    private void ReadWeaponSwitch(Keyboard keyboard)
    {
        if (keyboard.digit1Key.wasPressedThisFrame)
            desiredWeaponSlot = 0;
        else if (keyboard.digit2Key.wasPressedThisFrame)
            desiredWeaponSlot = 1;
    }

    private void SetAbility(AbilityMode mode)
    {
        selectedAbility = mode;
        UIManager.Singleton?.SelectAbility(mode);
    }

    // =============================================================
    // Прицел
    // =============================================================

    private void UpdateAimMarker()
    {
        RecoilController.SetClampPadding(aimClampPadding);

        Vector2 mouseDelta = Mouse.current != null
            ? Mouse.current.delta.ReadValue()
            : Vector2.zero;

        RecoilController.Tick(mouseDelta, aimSensitivity);
    }

    /// <summary>
    /// Точка прицела — пересечение луча камеры с горизонтальной плоскостью
    /// на высоте ствола. Никакого raycast по геометрии: пули летят
    /// горизонтально, поэтому цель обязана быть на той же высоте.
    /// </summary>
    private Vector3 ComputeAimPoint()
    {
        if (LocalPlayer == null)
            return Vector3.zero;
 
        Camera cam = Camera.main;
        if (cam == null)
            return FallbackAimPoint();
 
        Ray ray = cam.ScreenPointToRay(RecoilController.GetAimScreenPosition());
 
        float planeY = LocalPlayer.transform.position.y + aimPlaneHeight;
        Plane aimPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
 
        Vector3 point = aimPlane.Raycast(ray, out float enter)
            ? ray.GetPoint(enter)
            : FallbackAimPoint();
 
        return ClampMinDistance(point, planeY);
    }
    
    /// <summary>
    /// Выталкивает точку прицела наружу если она слишком близко к игроку.
    /// Без этого направление выстрела становится непредсказуемым
    /// (вплоть до стрельбы себе в ноги).
    /// </summary>
    private Vector3 ClampMinDistance(Vector3 point, float planeY)
    {
        Vector3 playerXZ = LocalPlayer.transform.position;
        playerXZ.y = 0f;
 
        Vector3 pointXZ = point;
        pointXZ.y = 0f;
 
        Vector3 offset = pointXZ - playerXZ;
        float distSqr = offset.sqrMagnitude;
 
        if (distSqr >= minAimDistance * minAimDistance)
            return point;
 
        // Слишком близко — выталкиваем наружу.
        // Направление: текущее если есть, иначе forward игрока.
        Vector3 dir = distSqr > 0.0001f
            ? offset.normalized
            : (_lastAimDirXZ.sqrMagnitude > 0.0001f
                ? _lastAimDirXZ
                : LocalPlayer.transform.forward);
 
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        else
            dir.Normalize();
 
        Vector3 result = playerXZ + dir * minAimDistance;
        result.y = planeY;
        return result;
    }

    private Vector3 FallbackAimPoint()
    {
        Transform t = LocalPlayer.transform;
        return t.position + t.forward * 10f + Vector3.up * aimPlaneHeight;
    }

    /// <summary>
    /// Считает направление прицела в плоскости XZ и целевой yaw персонажа.
    /// </summary>
    private void UpdateAimDirectionAndYaw(Vector3 aimPoint)
    {
        Vector3 raw = aimPoint - LocalPlayer.transform.position;
        raw.y = 0f;

        Vector3 dirXZ;
        float targetYaw;

        if (raw.sqrMagnitude < 0.000001f)
        {
            // Прицел ровно над центром игрока — держим прошлое направление
            dirXZ = _lastAimDirXZ.sqrMagnitude < 0.0001f
                ? LocalPlayer.transform.forward
                : _lastAimDirXZ;

            dirXZ.y = 0f;
            dirXZ = dirXZ.sqrMagnitude < 0.0001f ? Vector3.forward : dirXZ.normalized;

            targetYaw = _lastYaw;
        }
        else
        {
            dirXZ = raw.normalized;
            targetYaw = Mathf.Atan2(dirXZ.x, dirXZ.z) * Mathf.Rad2Deg;
        }

        ApplyYawWithSpeedLimit(targetYaw);

        accumulatedInput.AimDirection = dirXZ;

        // ВАЖНО: AimDirection3D должен присваиваться — его читает Player
        // для поворота модели. Так как стрельба горизонтальная,
        // он совпадает с dirXZ.
        accumulatedInput.AimDirection3D = dirXZ;

        _lastAimDirXZ = dirXZ;

        Debug.DrawRay(LocalPlayer.transform.position, dirXZ * 3f, Color.cyan);
    }

    private void ApplyYawWithSpeedLimit(float targetYaw)
    {
        float dt = Time.deltaTime;

        if (dt <= 0f)
        {
            accumulatedInput.LookYaw = targetYaw;
            _lastYaw = targetYaw;
            return;
        }

        float maxStep = maxYawSpeedDegPerSec * dt;
        float newYaw = Mathf.MoveTowardsAngle(_lastYaw, targetYaw, maxStep);

        accumulatedInput.LookYaw = newYaw;
        _lastYaw = newYaw;
    }

    private bool IsAimingAtHead()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(RecoilController.GetAimScreenPosition());

        if (!Physics.Raycast(ray, out RaycastHit hit, headCheckDistance,
                             hitboxLayers, QueryTriggerInteraction.Collide))
            return false;

        if (!hit.collider.TryGetComponent(out PlayerHitbox hb))
            return false;

        return hb.Type == HitboxType.Head;
    }

    // =============================================================
    // Fusion callbacks
    // =============================================================

    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (accumulatedInput.Direction.sqrMagnitude > 1f)
            accumulatedInput.Direction.Normalize();

        // Нормализуем направление, но yaw НЕ пересчитываем —
        // он уже посчитан в BeforeUpdate с ограничением скорости
        Vector3 dXZ = accumulatedInput.AimDirection;
        dXZ.y = 0f;

        accumulatedInput.AimDirection = dXZ.sqrMagnitude > 0.0001f
            ? dXZ.normalized
            : Vector3.forward;

        input.Set(accumulatedInput);
        
        // Накопленные кнопки отправлены — начинаем копить заново
        resetInput = true;
    }

    async void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (shutdownReason != ShutdownReason.DisconnectedByPluginLogic)
            return;

        var connectionBehaviour = FindFirstObjectByType<MenuConnectionBehaviour>(
            FindObjectsInactive.Include);

        if (connectionBehaviour != null)
            await connectionBehaviour.DisconnectAsync(ConnectFailReason.Disconnect);

        var gameplayScreen = FindFirstObjectByType<FusionMenuUIGameplay>(
            FindObjectsInactive.Include);

        gameplayScreen?.Controller.Show<FusionMenuUIMain>();
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
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}