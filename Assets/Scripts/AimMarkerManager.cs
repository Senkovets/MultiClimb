using UnityEngine;

public class AimMarkerManager : MonoBehaviour
{
    public static AimMarkerManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private AimMarkerPreset currentPreset;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (currentPreset == null)
            return;

        // 1) позиция прицела — из твоего RecoilController
        Vector2 screenPos = RecoilController.GetAimScreenPosition();
        currentPreset.SetScreenPosition(screenPos);

        // 2) scatter — из текущего оружия
        GunController gun = FindCurrentGun();
       // if (gun != null)
           // currentPreset.SetScatter(gun.CurrentScatter);

        // 3) крит — из InputManager
     /*   InputManager im = FindObjectOfType<InputManager>();
        if (im != null)
            currentPreset.SetCritical(im.LastLocalInput.IsCriticalAim);*/
    }

    public void OnShoot()
    {
        if (currentPreset != null)
            currentPreset.OnShoot();
    }

    public void SwitchPreset(AimMarkerPreset newPreset)
    {
        if (currentPreset != null)
            Destroy(currentPreset.gameObject);

        currentPreset = newPreset;
    }

    private GunController FindCurrentGun()
    {
        Player p = FindObjectOfType<Player>();
        if (p == null)
            return null;

        return p.GetComponentInChildren<GunController>();
    }
}
