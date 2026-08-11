using Fusion;
using Fusion.Addons.KCC;
using Gameplay.Combat;
using UnityEngine;

public enum AbilityMode : byte
{
    BreakBlock,
    Cage,
    Shove
}

public class Player : NetworkBehaviour
{
    [Networked] public string Name { get; private set; }

    public NetworkHealth Health;
    public bool IsReady;
    public bool IsDead => Health != null && Health.CurrentHealth <= 0;
    [Networked, OnChangedRender(nameof(OnDeathStateChanged))] public NetworkBool IsVisible { get; set; } = true;

    [SerializeField] private Renderer[] modelParts;
    [SerializeField] private Canvas[] сanvas;
    [Networked] public int Kills { get; private set; }
    [Networked] public int Score { get; private set; }

    private InputManager inputManager;
    private Vector3 _moveDirection;

    const float deadZoneSqr = 0.04f; // ~0.2м

    public GameObject HeadColider; 

    //-----------------------------------------------

    [SerializeField] private LayerMask lagCompLayers;
    [SerializeField] private KCC kcc;
    [SerializeField] private Transform camTarget;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float lookSensitivity = 0.15f;

    [SerializeField] private KCCProcessor glideProcessor;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip shoveSound;
    [SerializeField] private Cage cagePrefab;
    [SerializeField] private Vector3 jumpImpulse = new(0f, 10f, 0f);
    [SerializeField] private float doubleJumpMultiplier = 0.75f;
    [SerializeField] private float breakBlockCD = 1.25f;
    [SerializeField] private float cageCD = 10f;
    [SerializeField] private float shoveCD = 2f;
    [SerializeField] private float grappleCD = 2f;
    [SerializeField] private float glideCD = 20f;
    [SerializeField] private float doubleJumpCD = 5f;
    [SerializeField] private float shoveStrength = 20f;
    [SerializeField] private float grappleStrength = 12f;
    [SerializeField] private float maxGlideTime = 2f;
    [field: SerializeField] public float AbilityRange { get; private set; } = 25f;

    public float BreakCDFactor => (BreakBlockCD.RemainingTime(Runner) ?? 0f) / breakBlockCD;
    public float CageCDFactor => (CageCD.RemainingTime(Runner) ?? 0f) / cageCD;
    public float ShoveCDFactor => (ShoveCD.RemainingTime(Runner) ?? 0f) / shoveCD;
    public float GrappleCDFactor => (GrappleCD.RemainingTime(Runner) ?? 0f) / grappleCD;
    public float GlideCDFactor => (GlideCD.RemainingTime(Runner) ?? 0f) / glideCD;
    public float DoubleJumpCDFactor => (DoubleJumpCD.RemainingTime(Runner) ?? 0f) / doubleJumpCD;
    
    private bool CanGlide => !kcc.Data.IsGrounded && GlideCharge > 0f && !IsCaged;
    public AbilityMode SelectedAbility { get; private set; }

 
    [Networked] public float GlideCharge { get; private set; }
    [Networked] public bool IsGliding { get; private set; }
    [Networked] public bool IsCaged { get; set; }
    [Networked] private TickTimer BreakBlockCD { get; set; }
    [Networked] private TickTimer CageCD { get; set; }
    [Networked] private TickTimer ShoveCD { get; set; }
    [Networked] private TickTimer GrappleCD { get; set; }
    [Networked] private TickTimer GlideCD { get; set; }
    [Networked] private TickTimer DoubleJumpCD { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    [Networked, OnChangedRender(nameof(Jumped))] private int JumpSync { get; set; }
    [Networked, OnChangedRender(nameof(Shoved))] private int ShoveSync { get; set; }

    private Vector2 baseLookRotation;
    private float glideDrain;

    private float _yawVelocity = 0f;

    private float _currentYaw;
    private bool _yawInitialized;

    //-----------------------------------------------------------------------------------


    public override void Spawned()
    {
        Health = GetComponent<NetworkHealth>();

        var bar = GetComponentInChildren<HealthBar>(true);
        bar.Init(Health);

        glideDrain = 1f / (maxGlideTime * Runner.TickRate);
        GlideCharge = 1f;

        if (HasInputAuthority)
        {
            inputManager = Runner.GetComponent<InputManager>();
            inputManager.LocalPlayer = this;
            Name = PlayerPrefs.GetString("Photon.Menu.Username");
            RPC_PlayerName(Name);
            CameraController.Singleton.SetTarget(camTarget, this);
            UIManager.Singleton.LocalPlayer = this;
        }
    }
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            CameraController.Singleton.SetTarget(null, this);
            UIManager.Singleton.LocalPlayer = null;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (IsDead)
        {
            kcc.SetInputDirection(Vector3.zero);
            return;
        }

        if (!GetInput(out NetInput input))
            return;

        // --------------------
        // LOOK / YAW STABILIZER
        // --------------------
        Vector3 aim = input.AimDirection3D;
        aim.y = 0f;

        // Если aim невалидный — не трогаем yaw (но продолжаем остальную логику)
        if (aim.sqrMagnitude > 0.0001f)
        {
            float targetYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;

            // Инициализируем yaw один раз (чтобы не было принудительного 0°)
            if (!_yawInitialized)
            {
                _currentYaw = targetYaw;
                _yawInitialized = true;
            }

            // Dead-zone: цель слишком близко → держим текущий yaw
            if (aim.sqrMagnitude >= deadZoneSqr)
            {
                // Ограничение скорости поворота
                float maxYawSpeed = 900f; // deg/sec
                float maxStep = maxYawSpeed * Runner.DeltaTime;

                _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, targetYaw, maxStep);
            }

            kcc.SetLookRotation(0f, _currentYaw);
        }

        // --------------------
        // REST OF YOUR LOGIC
        // --------------------
        SelectedAbility = input.AbilityMode;
        // CheckGlide(input);
        // CheckJump(input);

        UpdateCamTarget();

        Vector3 lookDirection = camTarget.forward;

        SetInputDirection(input);
        CheckAbilities(input, lookDirection);

        PreviousButtons = input.Buttons;
        baseLookRotation = kcc.GetLookRotation();
    }

    public override void Render()
    {
        UpdateCamTarget();
    }

    public void AddKill()
    {
        if (HasStateAuthority)
            Kills++;
    }
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        kcc.SetPosition(position);
        kcc.SetLookRotation(rotation);
    }

    public void ResetRoundStats()
    {
        if (!HasStateAuthority) return;

        Kills = 0;
        Score = 0;
        IsReady = false;
    }

    private void OnDeathStateChanged()
    {
        // Определяем целевой слой для физики Unity
        int targetLayer = IsVisible ? LayerMask.NameToLayer("Player") : LayerMask.NameToLayer("Ignore Raycast");

        // 1. Стандартная логика для Unity (визуал и обычные лучи)
        SetLayerRecursively(transform, targetLayer);

        

        if (modelParts != null)
        {
            foreach (var part in modelParts)
                if (part != null) part.enabled = IsVisible;
        }

        if (сanvas != null)
        {
            foreach (var can in сanvas)
                if (can != null) can.enabled = IsVisible;
        }

        // 2. Сетевые хитбоксы (Lag Compensation)
        var hbRoot = GetComponent<HitboxRoot>();
        if (hbRoot != null) hbRoot.enabled = IsVisible;

        // 3. Управление KCC (Физика перемещения)
        if (kcc != null)
        {
            if (IsVisible)
            {
                HeadColider.SetActive(true);
                kcc.SetColliderLayer(LayerMask.NameToLayer("Player"));
                kcc.SetCollisionLayerMask(LayerMask.GetMask("Default", "Player", "Ground"));
            }
            else
            {
                HeadColider.SetActive(false);
                kcc.SetColliderLayer(LayerMask.NameToLayer("Ignore Raycast"));
                kcc.SetCollisionLayerMask(LayerMask.GetMask("Ground"));
            }
        }
    }

    private void SetLayerRecursively(Transform parent, int layer)
    {
        parent.gameObject.layer = layer;
        foreach (Transform child in parent)
        {
            SetLayerRecursively(child, layer);
        }
    }

    private void SetInputDirection(NetInput input)
    {
        _moveDirection = new Vector3(input.Direction.x, 0f, input.Direction.y);

        if (IsGliding)
        {
            GlideCharge = Mathf.Max(0f, GlideCharge - glideDrain);
            kcc.SetInputDirection(kcc.Data.TransformDirection);
        }
        else
        {
            kcc.SetInputDirection(_moveDirection);
        }
    }

    private void UpdateCamTarget()
    {
        camTarget.localRotation = Quaternion.Euler(
            kcc.GetLookRotation().x,
            kcc.GetLookRotation().y,
            0f
        );
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    public void RPC_SetReady()
    {
        IsReady = true;
        if (HasInputAuthority)
            UIManager.Singleton.DidSetReady();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_PlayerName(string name)
    {
        Name = name;
    }


















    //-----------------------------------------------------------------------------------
    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, transform.position.y);

        float distance;
        if (groundPlane.Raycast(ray, out distance))
        {
            return ray.GetPoint(distance);
        }

        return ray.GetPoint(100f);
    }

    private void CheckGlide(NetInput input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Glide) && GlideCD.ExpiredOrNotRunning(Runner) && CanGlide)
            ToggleGlide(true);
        else if (input.Buttons.WasReleased(PreviousButtons, InputButton.Glide) && IsGliding)
            ToggleGlide(false);
    }

    private void CheckJump(NetInput input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Jump))
        {
            if (kcc.FixedData.IsGrounded)
            {
                kcc.Jump(jumpImpulse);
                JumpSync++;
            }
            else if (DoubleJumpCD.ExpiredOrNotRunning(Runner))
            {
                kcc.Jump(jumpImpulse * doubleJumpMultiplier);
                DoubleJumpCD = TickTimer.CreateFromSeconds(Runner, doubleJumpCD);
                ToggleGlide(false);
                JumpSync++;
            }
        }
    }



    private void CheckAbilities(NetInput input, Vector3 lookDirection)
    {
        if (!HasStateAuthority || !input.Buttons.WasPressed(PreviousButtons, InputButton.UseAbility))
            return;

        switch (input.AbilityMode)
        {
            case AbilityMode.BreakBlock:
                TryBreakBlock(lookDirection);
                break;
            case AbilityMode.Cage:
                TryCage(lookDirection);
                break;
            case AbilityMode.Shove:
                TryShove(lookDirection);
                break;
            default:
                break;
        }
    }

    public void ResetCooldowns()
    {
        BreakBlockCD = TickTimer.None;
        CageCD = TickTimer.None;
        ShoveCD = TickTimer.None;
        GrappleCD = TickTimer.None;
        GlideCD = TickTimer.None;
        DoubleJumpCD = TickTimer.None;
    }

    private void TryBreakBlock(Vector3 lookDirection)
    {
        if (BreakBlockCD.ExpiredOrNotRunning(Runner) && Physics.Raycast(camTarget.position, lookDirection, out RaycastHit hitInfo, AbilityRange))
        {
            if (hitInfo.collider.TryGetComponent(out Block block))
            {
                BreakBlockCD = TickTimer.CreateFromSeconds(Runner, breakBlockCD);
                block.Disable();
            }
        }
    }

    private void TryCage(Vector3 lookDirection)
    {
        if (CageCD.ExpiredOrNotRunning(Runner) && Runner.LagCompensation.Raycast(camTarget.position, lookDirection, AbilityRange, Object.InputAuthority, out LagCompensatedHit hit, lagCompLayers, HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority))
        {
            if (hit.Hitbox != null && hit.Hitbox.TryGetComponent(out Player other))
            {
                CageCD = TickTimer.CreateFromSeconds(Runner, cageCD);
                other.Cage();
            }
        }
    }

    private void TryShove(Vector3 lookDirection)
    {
        if (ShoveCD.ExpiredOrNotRunning(Runner) && Runner.LagCompensation.Raycast(camTarget.position, lookDirection, AbilityRange, Object.InputAuthority, out LagCompensatedHit hit, lagCompLayers, HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority))
        {
            if (hit.Hitbox != null && hit.Hitbox.TryGetComponent(out Player other))
            {
                ShoveCD = TickTimer.CreateFromSeconds(Runner, shoveCD);
                other.Shove(lookDirection, shoveStrength);
            }
        }
    }

    private void TryGrapple(Vector3 lookDirection)
    {
        if (GrappleCD.ExpiredOrNotRunning(Runner) && Physics.Raycast(camTarget.position, lookDirection, out RaycastHit hitInfo, AbilityRange))
        {
            if (hitInfo.collider.TryGetComponent(out Block _))
            {
                GrappleCD = TickTimer.CreateFromSeconds(Runner, grappleCD);
                Vector3 grappleVector = Vector3.Normalize(hitInfo.point - transform.position);
                if (grappleVector.y > 0f)
                    grappleVector = Vector3.Normalize(grappleVector + Vector3.up);

                kcc.Jump(grappleVector * grappleStrength);
                ToggleGlide(false);
            }
        }
    }

    private void Cage()
    {
        Runner.Spawn(cagePrefab, transform.position, Quaternion.identity, Object.InputAuthority).Init(this);
        IsCaged = true;
        ToggleGlide(false);
    }

    private void Shove(Vector3 direction, float strength)
    {
        kcc.AddExternalImpulse(direction * strength);
        ShoveSync++;
    }

    private void ToggleGlide(bool isGliding)
    {
        if (IsGliding == isGliding)
            return;

        if (isGliding)
        {
            kcc.AddModifier(glideProcessor);
            Vector3 velocity = kcc.Data.DynamicVelocity;
            velocity.y *= 0.25f;
            kcc.SetDynamicVelocity(velocity);
        }
        else
        {
            kcc.RemoveModifier(glideProcessor);
            GlideCharge = 1f;
            GlideCD = TickTimer.CreateFromSeconds(Runner, glideCD);
        }

        IsGliding = isGliding;
    }

    private void Jumped()
    {
        source.Play();
    }

    private void Shoved()
    {
        source.PlayOneShot(shoveSound);
    }

   

}
