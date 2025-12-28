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
    [SerializeField] private float maxRecoilDistance = 360f;

    [Header("Horizontal Pattern (optional, pixels)")]
    [SerializeField] private bool usePattern;
    [SerializeField] private float[] horizontalPattern = new float[] { -2f, 2f, -3f, 3f, -2f, 2f };

    [Header("Gun Source")]
    [SerializeField] private GunController gunController;
    [SerializeField] private float fallbackFireRate = 0.1f;
    [SerializeField] private FireMode fallbackFireMode = FireMode.Auto;

    [SerializeField] float verticalRecoil = 0.2f;    // сила "назад по линии огня"
    [SerializeField] float horizontalRecoil = 0.1f;  // сила вбок

    private Vector2 fireStartCursorPosition;
    private bool cursorPositionSaved;
    private bool skipOffsetUpdateThisFrame;




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
        maxRecoilDistance = 360f;

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

        // === 1. Фиксируем позицию курсора в момент начала стрельбы ===
        if (isFiring && !wasFiring)
        {
            fireStartCursorPosition = GetRawMousePosition();
            cursorPositionSaved = true;
        }

        // === 2. Обработка стрельбы ===
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

        // === 3. Окончание стрельбы: телепорт + компенсация + плавный возврат ===
        if (!isFiring)
        {
            if (wasFiring && cursorPositionSaved)
            {
                Vector2 oldMousePos = GetRawMousePosition();

                Mouse.current.WarpCursorPosition(fireStartCursorPosition);

                Vector2 newMousePos = fireStartCursorPosition;
                Vector2 delta = oldMousePos - newMousePos;

                currentOffset += delta;
                targetOffset += delta;

                cursorPositionSaved = false;
                skipOffsetUpdateThisFrame = true;
            }


            // задаём цель плавного возврата прицела
            targetOffset = Vector2.zero;
        }

        // === 4. Плавное движение виртуального прицела ===
        if (!skipOffsetUpdateThisFrame)
        {
            currentOffset = Vector2.Lerp(
                currentOffset,
                targetOffset,
                followSpeed * Time.deltaTime
            );
        }
        else
        {
            // пропускаем ОДИН кадр, чтобы Input System стабилизировался
            skipOffsetUpdateThisFrame = false;
        }


        wasFiring = isFiring;
    }


    public static Vector2 GetAimScreenPosition()
    {
        return Singleton != null ? Singleton.AimScreenPosition : (Vector2)Input.mousePosition;
    }

    private void RegisterShot()
    {
        GunController gun = gameObject.GetComponent<GunController>();

        Vector3 aimDirection = gun.GetDirection();
        //Debug.LogError("aimDirection:  " + aimDirection);

        Vector3 forward = aimDirection.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 recoil =
    forward * verticalRecoil +
    right * horizontalRecoil;


       /* float vertical = Random.Range(Mathf.Min(verticalRange.x, verticalRange.y), Mathf.Max(verticalRange.x, verticalRange.y));
        float horizontal = GetHorizontalKick();
        float multiplier = shotsInBurst == 0 ? firstShotMultiplier : 1f;

        Vector2 kick = new Vector2(horizontal * multiplier, vertical * multiplier);*/


        //Debug.LogError("forward:  " + forward);

        Vector2 kick = new Vector2(forward.x * 25, forward.z * 25);

        //Debug.LogError("kick:  " + kick);

        targetOffset += kick;
        targetOffset = Vector2.ClampMagnitude(targetOffset, maxRecoilDistance * 1000);

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
