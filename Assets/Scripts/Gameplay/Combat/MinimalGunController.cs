using UnityEngine;

public class MinimalGunController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float mouseSensitivity = 2f;

    [Header("Gun Settings")]
    public int maxAmmo = 30;
    public float fireRate = 0.1f; // время между выстрелами
    public float reloadTime = 2f;
    public float damage = 25f;
    public float bulletSpeed = 100f;
    public float bulletDistance = 100f;
    public float recoilAmount = 0.5f;

    [Header("Visuals")]
    public Transform gunMuzzle;
    public GameObject muzzleFlashPrefab;
    public LayerMask hitLayers;

    // Internal state
    private CharacterController controller;
    private Camera playerCamera;
    private int currentAmmo;
    private bool isReloading;
    private float nextFireTime;
    private Vector3 currentRecoil;
    private float recoilRecoverySpeed = 5f;

    void Start()
    {
        controller = gameObject.AddComponent<CharacterController>();
        controller.radius = 0.5f;
        controller.height = 2f;

        // Создаём камеру если нет
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            GameObject camObj = new GameObject("PlayerCamera");
            camObj.transform.SetParent(transform);
            camObj.transform.localPosition = new Vector3(0, 1.6f, 0);
            playerCamera = camObj.AddComponent<Camera>();
        }

        currentAmmo = maxAmmo;
        //Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        HandleMovement();
        HandleMouseLook();
        HandleShooting();
        UpdateRecoil();
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool running = Input.GetKey(KeyCode.LeftShift);

        float speed = running ? runSpeed : walkSpeed;
        Vector3 move = transform.right * h + transform.forward * v;

        controller.Move(move * speed * Time.deltaTime);

        // Гравитация
        controller.Move(Vector3.down * 9.81f * Time.deltaTime);
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Поворот по Y (влево-вправо)
        transform.Rotate(Vector3.up * mouseX);

        // Поворот камеры по X (вверх-вниз) с учётом отдачи
        Vector3 currentRot = playerCamera.transform.localEulerAngles;
        currentRot.x -= mouseY;
        currentRot.x += currentRecoil.x; // Применяем отдачу

        // Clamp вертикального угла
        if (currentRot.x > 180f) currentRot.x -= 360f;
        currentRot.x = Mathf.Clamp(currentRot.x, -89f, 89f);

        playerCamera.transform.localEulerAngles = currentRot;
    }

    void HandleShooting()
    {
        if (isReloading) return;

        // Перезарядка
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo)
        {
            StartReload();
            return;
        }

        // Стрельба
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                Fire();
            }
            else
            {
                // Пустой магазин - автоперезарядка
                StartReload();
            }
        }
    }

    void Fire()
    {
        currentAmmo--;
        nextFireTime = Time.time + fireRate;

        // Отдача
        AddRecoil();

        // Muzzle flash
        if (muzzleFlashPrefab && gunMuzzle)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, gunMuzzle.position, gunMuzzle.rotation);
            Destroy(flash, 0.1f);
        }

        // Raycast выстрел
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, bulletDistance, hitLayers))
        {
            // Попадание
            Debug.Log($"Hit: {hit.collider.name} at {hit.point}");

            // Урон (если есть компонент)
            var health = hit.collider.GetComponent<MinimalHealth>();
            if (health != null)
            {
                health.TakeDamage(damage);
            }

            // Debug линия
            Debug.DrawLine(ray.origin, hit.point, Color.red, 0.5f);
        }
        else
        {
            Debug.DrawRay(ray.origin, ray.direction * bulletDistance, Color.yellow, 0.5f);
        }

        Debug.Log($"Fired! Ammo: {currentAmmo}/{maxAmmo}");
    }

    void StartReload()
    {
        isReloading = true;
        Debug.Log("Reloading...");
        Invoke(nameof(FinishReload), reloadTime);
    }

    void FinishReload()
    {
        currentAmmo = maxAmmo;
        isReloading = false;
        Debug.Log("Reload complete!");
    }

    void AddRecoil()
    {
        // Случайная отдача вверх и в стороны
        currentRecoil.x += Random.Range(recoilAmount * 0.8f, recoilAmount * 1.2f);
        currentRecoil.y += Random.Range(-recoilAmount * 0.3f, recoilAmount * 0.3f);
    }

    void UpdateRecoil()
    {
        // Плавное возвращение от отдачи
        currentRecoil = Vector3.Lerp(currentRecoil, Vector3.zero, Time.deltaTime * recoilRecoverySpeed);
    }

    void OnGUI()
    {
        // HUD
        GUI.Label(new Rect(10, 10, 200, 30), $"Ammo: {currentAmmo}/{maxAmmo}");
        if (isReloading)
        {
            GUI.Label(new Rect(10, 40, 200, 30), "RELOADING...");
        }
    }
}
