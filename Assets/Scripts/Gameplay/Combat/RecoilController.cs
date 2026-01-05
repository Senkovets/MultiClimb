using UnityEngine;
using UnityEngine.InputSystem;

public class RecoilController : MonoBehaviour
{
    public static RecoilController Singleton { get; private set; }

    [Header("Recoil Settings (pixels)")]
    [SerializeField] private Vector2 verticalRange = new Vector2(6f, 12f);
    [SerializeField] private Vector2 horizontalRange = new Vector2(-4f, 4f);
    [SerializeField] private float firstShotMultiplier = 1.5f;
    [SerializeField] private float followSpeed = 18f;
    [SerializeField] private float returnSpeed = 10f;
    [SerializeField] private float maxRecoilDistance = 180f;

    [Header("Horizontal Pattern (optional, pixels)")]
    [SerializeField] private bool usePattern;
    [SerializeField] private float[] horizontalPattern = new float[] { -2f, 2f, -3f, 3f, -2f, 2f };

    [Header("Gun Source")]
    [SerializeField] private GunController gunController;
    [SerializeField] private float fallbackFireRate = 0.1f;
    [SerializeField] private FireMode fallbackFireMode = FireMode.Auto;

    public Vector2 AimScreenPosition => ClampToScreen(GetRawMousePosition() + currentOffset);
    public Vector2 CurrentOffset => currentOffset;

    private Vector2 currentOffset;
    private Vector2 targetOffset;
    private float nextFireTime;
    private int shotsInBurst;
    private bool wasFiring;
    private int patternIndex;

    private void Awake()
    {
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
            return;
        }

        Singleton = this;
    }

    private void Start()
    {
        if (gunController == null)
            gunController = FindObjectOfType<GunController>();
    }

    private void Update()
    {
        bool isFiring = IsFireInputActive();
        FireMode mode = GetFireMode();
        float fireRate = GetFireRate();

        if (isFiring)
        {
            switch (mode)
            {
                case FireMode.Auto:
                    if (Time.time >= nextFireTime)
                    {
                        RegisterShot();
                        nextFireTime = Time.time + fireRate;
                    }
                    break;
                case FireMode.Semi:
                case FireMode.Bolt:
                    if (!wasFiring)
                    {
                        RegisterShot();
                        nextFireTime = Time.time + fireRate;
                    }
                    break;
            }
        }

        if (!isFiring)
        {
            targetOffset = Vector2.Lerp(targetOffset, Vector2.zero, returnSpeed * Time.deltaTime);

            if (wasFiring)
            {
                shotsInBurst = 0;
                patternIndex = 0;
            }
        }

        currentOffset = Vector2.Lerp(currentOffset, targetOffset, followSpeed * Time.deltaTime);
        wasFiring = isFiring;
    }

    public static Vector2 GetAimScreenPosition()
    {
        return Singleton != null ? Singleton.AimScreenPosition : (Vector2)Input.mousePosition;
    }

    private void RegisterShot()
    {
        float vertical = Random.Range(Mathf.Min(verticalRange.x, verticalRange.y), Mathf.Max(verticalRange.x, verticalRange.y));
        float horizontal = GetHorizontalKick();
        float multiplier = shotsInBurst == 0 ? firstShotMultiplier : 1f;

        Vector2 kick = new Vector2(horizontal * multiplier, vertical * multiplier);
        targetOffset += kick;
        targetOffset = Vector2.ClampMagnitude(targetOffset, maxRecoilDistance);

        shotsInBurst++;
    }

    private float GetHorizontalKick()
    {
        if (usePattern && horizontalPattern != null && horizontalPattern.Length > 0)
        {
            float value = horizontalPattern[patternIndex];
            patternIndex = (patternIndex + 1) % horizontalPattern.Length;
            return value;
        }

        return Random.Range(Mathf.Min(horizontalRange.x, horizontalRange.y), Mathf.Max(horizontalRange.x, horizontalRange.y));
    }

    private bool IsFireInputActive()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
            return mouse.leftButton.isPressed;

        return Input.GetMouseButton(0);
    }

    private Vector2 GetRawMousePosition()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
            return mouse.position.ReadValue();

        return Input.mousePosition;
    }

    private Vector2 ClampToScreen(Vector2 position)
    {
        position.x = Mathf.Clamp(position.x, 0f, Screen.width);
        position.y = Mathf.Clamp(position.y, 0f, Screen.height);
        return position;
    }

    private float GetFireRate()
    {
        return gunController != null ? gunController.fireRate : fallbackFireRate;
    }

    private FireMode GetFireMode()
    {
        return gunController != null ? gunController.fireMode : fallbackFireMode;
    }
}
