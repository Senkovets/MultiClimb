using UnityEngine;

public class AimMarkerManager : MonoBehaviour
{
    public static AimMarkerManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private AimMarkerPreset currentPreset;

    [Header("UI Spread (independent from weapon scatter)")]
    [Tooltip("Сколько добавлять к UI-разлёту лепестков на каждый выстрел.")]
    [SerializeField] private float uiSpreadIncreasePerShot = 1.0f;

    [Tooltip("Скорость схода UI-разлёта обратно к 0 (в единицах/сек).")]
    [SerializeField] private float uiSpreadRecoverSpeed = 6.0f;

    [Tooltip("Максимальный UI-разлёт (добавляется к базовому weapon scatter).")]
    [SerializeField] private float uiSpreadMax = 16.0f;

    private float _uiSpread;

    // небольшие кэши, чтобы не делать FindObjectOfType каждый кадр
    private GunController _cachedGun;
    private InputManager _cachedInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (currentPreset == null)
            return;

        // 0) восстановление UI-разлёта
        _uiSpread = Mathf.MoveTowards(_uiSpread, 0f, uiSpreadRecoverSpeed * Time.deltaTime);

        // 1) позиция прицела — из RecoilController
        currentPreset.SetScreenPosition(RecoilController.GetAimScreenPosition());

        // 2) gun (кэшируем)
        if (_cachedGun == null)
            _cachedGun = FindCurrentGun();

        float weaponScatter = 0f;
        if (_cachedGun != null)
            weaponScatter = _cachedGun.CurrentScatter; // константа, как ты хотел

        // Итоговый scatter для UI: базовый (weapon) + накопленный (ui)
        currentPreset.SetScatter(weaponScatter + _uiSpread);

        // 3) крит — из InputManager (кэшируем)
        if (_cachedInput == null)
            _cachedInput = FindObjectOfType<InputManager>();

        if (_cachedInput != null)
            currentPreset.SetCritical(_cachedInput.LastLocalInput.IsCriticalAim);
    }

    public void OnShoot()
    {
        // на каждый выстрел добавляем UI-разлёт
        _uiSpread = Mathf.Min(_uiSpread + uiSpreadIncreasePerShot, uiSpreadMax);

        // и визуальный punch
        currentPreset?.OnShoot();
    }

    public void SwitchPreset(AimMarkerPreset newPreset)
    {
        if (currentPreset != null)
            Destroy(currentPreset.gameObject);

        currentPreset = newPreset;

        // при смене пресета можно сбросить накопление, чтобы не переносилось между оружиями
        _uiSpread = 0f;
    }

    private GunController FindCurrentGun()
    {
        Player p = FindObjectOfType<Player>();
        if (p == null)
            return null;

        return p.GetComponentInChildren<GunController>(true);
    }
}
