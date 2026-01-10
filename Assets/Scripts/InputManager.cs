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
    [Header("Aim Marker Settings")]

    [SerializeField] private float aimSensitivity = 1.0f; // подгони (обычно 0.6..1.5)
    [SerializeField] private float aimClampPadding = 10f; // чтобы маркер не упирался в край

    [SerializeField] private float aimDeadZoneMeters = 0.35f;     // зона вокруг origin
    [SerializeField] private float maxYawSpeedDegPerSec = 900f;   // ограничение скорости

    private float _lastYaw;
    private Vector3 _lastAimDirXZ = Vector3.forward;


    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
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
        Vector2 move = Vector2.zero;

        // Mouse buttons
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

        // === IMPORTANT: update virtual aim marker BEFORE raycasts ===
        RecoilController.SetClampPadding(aimClampPadding);

        Vector2 mouseDelta = Vector2.zero;
        if (mouse != null)
            mouseDelta = mouse.delta.ReadValue();

        RecoilController.Tick(mouseDelta, aimSensitivity);

        // === AimPoint/AimDirection as you already do ===
        Vector3 aimPoint = ComputeAimPointPlaneFirst();
        accumulatedInput.AimPoint = aimPoint;

        if (LocalPlayer == null)
        {
            LastLocalInput = default;
            return;
        }

        Vector3 origin = LocalPlayer.transform.position;

        // Dukov-style: если есть gun+Muzzle — считаем yaw от ствола (компенсация правой руки)
        if (TryGetMuzzle(out Vector3 muzzlePos))
            origin = muzzlePos;

        Vector3 raw = aimPoint - origin;
        raw.y = 0f;

        float deadZoneSqr = aimDeadZoneMeters * aimDeadZoneMeters;

        Vector3 dirXZ;
        float targetYaw;

        if (raw.sqrMagnitude < deadZoneSqr)
        {
            // слишком близко к origin → не дёргаем yaw, держим прошлое направление
            dirXZ = _lastAimDirXZ.sqrMagnitude < 0.0001f ? LocalPlayer.transform.forward : _lastAimDirXZ;
            dirXZ.y = 0f;
            dirXZ = dirXZ.sqrMagnitude < 0.0001f ? Vector3.forward : dirXZ.normalized;

            targetYaw = _lastYaw;
        }
        else
        {
            dirXZ = raw.normalized;
            targetYaw = Mathf.Atan2(dirXZ.x, dirXZ.z) * Mathf.Rad2Deg;
        }

        // ограничение скорости поворота
        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            float maxStep = maxYawSpeedDegPerSec * dt;
            float newYaw = Mathf.MoveTowardsAngle(_lastYaw, targetYaw, maxStep);

            accumulatedInput.LookYaw = newYaw;
            _lastYaw = newYaw;
        }
        else
        {
            accumulatedInput.LookYaw = targetYaw;
            _lastYaw = targetYaw;
        }

        accumulatedInput.AimDirection = dirXZ;
        _lastAimDirXZ = dirXZ;


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

        // Нормализуем направление, но НЕ пересчитываем yaw — yaw уже рассчитан в BeforeUpdate()
        Vector3 dXZ = accumulatedInput.AimDirection;
        dXZ.y = 0f;

        if (dXZ.sqrMagnitude > 0.0001f)
            accumulatedInput.AimDirection = dXZ.normalized;
        else
            accumulatedInput.AimDirection = Vector3.forward;

        input.Set(accumulatedInput);
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

    private bool TryGetMuzzle(out Vector3 muzzlePos)
    {
        muzzlePos = default;

        if (LocalPlayer == null)
            return false;

        GunController gun = LocalPlayer.GetComponentInChildren<GunController>();
        if (gun == null || gun.Muzzle == null)
            return false;

        muzzlePos = gun.Muzzle.position;
        return true;
    }

    private Vector3 ComputeAimPointPlaneFirst()
    {
        if (LocalPlayer == null)
            return Vector3.zero;

        Camera cam = Camera.main;
        if (cam == null)
            return LocalPlayer.transform.position + LocalPlayer.transform.forward * 10f;

        // Луч через виртуальную позицию маркера
        Ray ray = cam.ScreenPointToRay(RecoilController.GetAimScreenPosition());

        // 1) СНАЧАЛА плоскость (как в Dukov) — стабильная точка, не зависит от коллайдеров игрока
        float planeY = LocalPlayer.transform.position.y;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

        Vector3 planePoint;
        if (plane.Raycast(ray, out float enter))
            planePoint = ray.GetPoint(enter);
        else
            planePoint = LocalPlayer.transform.position + LocalPlayer.transform.forward * 10f;

        // 2) Опциональная коррекция от Muzzle (простое "прилипание")
        // Если не хочешь пока это — просто return planePoint;
        GunController gun = LocalPlayer.GetComponentInChildren<GunController>(true);
        if (gun == null || gun.Muzzle == null)
            return planePoint;

        Vector3 muzzlePos = gun.Muzzle.position;

        Vector3 dir = planePoint - muzzlePos;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return planePoint;

        dir.Normalize();

        // Радиус и дистанцию подстрой, это безопасные стартовые значения
        const float radius = 0.20f;
        const float maxDist = 200f;

        // ВАЖНО: aimMask должен НЕ включать слой игрока/хитбоксов, иначе снова словишь дребезг.
        if (Physics.SphereCast(muzzlePos, radius, dir, out RaycastHit hit, maxDist, aimMask, QueryTriggerInteraction.Ignore))
        {
            // Игнорируем попадание в самого себя (если вдруг слой всё-таки попал)
            if (hit.collider != null && hit.collider.transform.IsChildOf(LocalPlayer.transform))
                return planePoint;

            return hit.point;
        }

        return planePoint;
    }


}
