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

    public GameObject HeadColider; 

    //-----------------------------------------------

    [SerializeField] private LayerMask lagCompLayers;
    [SerializeField] private KCC kcc;
    [SerializeField] private Transform camTarget;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float lookSensitivity = 0.15f;

    [SerializeField] private KCCProcessor glideProcessor;
    [SerializeField] private AudioSource source;
    [field: SerializeField] public float AbilityRange { get; private set; } = 25f;

    public AbilityMode SelectedAbility { get; private set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private Vector2 baseLookRotation;
    private float glideDrain;

    //-----------------------------------------------------------------------------------


    public override void Spawned()
    {
        Health = GetComponent<NetworkHealth>();

        var bar = GetComponentInChildren<HealthBar>(true);
        bar.Init(Health);

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
 
        // Поворот и движение — в PlayerLocomotion
 
        SelectedAbility = input.AbilityMode;
 
        UpdateCamTarget();
 
        Vector3 lookDirection = camTarget.forward;
 
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
   
}
