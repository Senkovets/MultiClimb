using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    [Header("Основные параметры")]
    public Transform gunMuzzle; // Точка вылета пуль
    public GameObject muzzleFlashPrefab; // Префаб вспышки
    public LayerMask groundLayer; // Слои для попаданий
    public LayerMask hitLayers; // Слои для попаданий
    public CameraFollow playerCamera; // Камера игрока

    [Header("Параметры стрельбы")]
    public int maxAmmo = 30;
    public float fireRate = 0.1f;
    public float reloadTime = 2f;
    public float damage = 25f;
    public float bulletSpeed = 100f;
    public float bulletDistance = 100f;

    [Header("Параметры отдачи")]
    public float verticalRecoil = 1.5f; // Вертикальная отдача
    public float horizontalRecoil = 0.8f; // Горизонтальная отдача
    public float recoilRecoverySpeed = 5f; // Скорость восстановления
    public float recoilRandomness = 0.3f; // Случайность разброса

    private int currentAmmo;
    private bool isReloading;
    private float nextFireTime;
    private Vector3 currentRecoil;
    private float recoilTimer;

    private List<Vector3> debugPoints = new List<Vector3>();

    [SerializeField]
    private Projectile _projectilePrefab;

    private void Start()
    {
        currentAmmo = maxAmmo;
        playerCamera = CameraFollow.Singleton; // Или назначьте через инспектор
    }

    private void Update()
    {
        HandleShooting();
    }

    private void LateUpdate()
    {
       // UpdateRecoil();
    }

    private void HandleShooting()
    {
        if (isReloading) return;

        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                Fire();
                nextFireTime = Time.time + fireRate;
            }
            else
            {
                StartReload();
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo)
        {
            StartReload();
        }
    }

    private void Fire()
    {
        currentAmmo--;
        ApplyRecoil();
        PlayMuzzleFlash();
        ShootBullet();
    }

    private void ApplyRecoil()
    {
        // Случайная отдача с разбросом
        float vertical = verticalRecoil * (1 + Random.Range(-recoilRandomness, recoilRandomness));
        float horizontal = horizontalRecoil * (1 + Random.Range(-recoilRandomness, recoilRandomness));

        currentRecoil.x += vertical;    
        currentRecoil.y += horizontal * (Random.value * 2 - 1); // Случайное направление
        recoilTimer = 0;
    }

    private void UpdateRecoil()
    {
        if (currentRecoil != Vector3.zero)
        {
            recoilTimer += Time.deltaTime;
            float recovery = Mathf.Clamp01(recoilTimer * recoilRecoverySpeed);
            currentRecoil *= (1 - recovery);

            // Применяем отдачу к камере
            playerCamera.transform.localEulerAngles = new Vector3(
                playerCamera.transform.localEulerAngles.x + currentRecoil.x,
                playerCamera.transform.localEulerAngles.y + currentRecoil.y,
                0
            );
        }
    }

    private void ShootBullet()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray camRay = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 targetPoint;

        // Точка пересечения с объектом
        if (Physics.Raycast(camRay, out RaycastHit camHit, 2000f, groundLayer))
        {
            targetPoint = camHit.point;
        }
        else
        {
            targetPoint = camRay.origin + camRay.direction * 2000f;
        }

        Vector3 normalToCamera = (cam.transform.position - targetPoint).normalized;

        Debug.DrawRay(targetPoint, normalToCamera * 5, Color.green, 5f);


        float targetY = gunMuzzle.position.y;

        float t = (targetY - targetPoint.y) / normalToCamera.y;
        Vector3 pointAtY10 = targetPoint + normalToCamera * t;

        Vector3 fireDirection5 = (pointAtY10 - gunMuzzle.position).normalized;

        Debug.DrawRay(gunMuzzle.position, fireDirection5 * 100, Color.blue, 8f); // правильная стрельба 


        float a = gunMuzzle.position.y;
        Vector3 offsetPoint = targetPoint + normalToCamera * a;
        Vector3 finalTargetPoint = offsetPoint;

        float b = gunMuzzle.position.y;
        Vector3 offsetPoint3 = targetPoint + normalToCamera * a;
        offsetPoint3.y = gunMuzzle.position.y;
        Vector3 finalTargetPoint2 = offsetPoint;


        Debug.DrawLine(targetPoint, targetPoint + Vector3.up, Color.magenta, 5f);

        Vector3 offsetPoint2 = targetPoint;
        offsetPoint2.y = gunMuzzle.position.y;

        // Вычисляем направление выстрела от оружия к targetPoint
        Vector3 fireDirection = (targetPoint - gunMuzzle.position).normalized;
        Vector3 FinalFireDirection = (finalTargetPoint - gunMuzzle.position).normalized;
        Vector3 fireDirection3 = (offsetPoint2 - gunMuzzle.position).normalized;
        Vector3 fireDirection4 = (offsetPoint3 - gunMuzzle.position).normalized;



        Debug.DrawRay(gunMuzzle.position, fireDirection * 100, Color.green, 5f);
        Debug.DrawRay(gunMuzzle.position, FinalFireDirection * 100, Color.cyan, 5f);
        Debug.DrawRay(gunMuzzle.position, fireDirection3 * 100, Color.gray, 5f);
        Debug.DrawRay(gunMuzzle.position, fireDirection4 * 100, Color.black, 5f);


        // Плавная отдача камеры в сторону fireDirection
        CameraController.Singleton.Shake(fireDirection, 1);



        Shoot(fireDirection5);
        // Выстрел
        if (Physics.Raycast(gunMuzzle.position, fireDirection5, out RaycastHit hit, bulletDistance, hitLayers))
        {
            var health = hit.collider.GetComponent<MinimalHealth>();
            if (health != null)
            {
                Debug.DrawLine(gunMuzzle.position, hit.point, Color.red, 5f);
                health.TakeDamage(damage);
            }
            else
            {
                Debug.DrawLine(gunMuzzle.position, hit.point, Color.yellow, 5f);
            }

            debugPoints.Add(hit.point);
        }
        else
        {
            Vector3 missPoint = gunMuzzle.position + fireDirection5 * bulletDistance;
            Debug.DrawLine(gunMuzzle.position, missPoint, Color.grey, 5f);
            debugPoints.Add(missPoint);
        }
    }

    void Shoot(Vector3 fireDirection)
    {
        Projectile proj = Instantiate(
            _projectilePrefab,
            gunMuzzle.position,
            Quaternion.LookRotation(fireDirection)
        );

        proj.Init(fireDirection);
    }


    private void PlayMuzzleFlash()
    {
        if (muzzleFlashPrefab && gunMuzzle)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, gunMuzzle.position, gunMuzzle.rotation);
            Destroy(flash, 0.1f);
        }
    }

    private void StartReload()
    {
        isReloading = true;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
        isReloading = false;
    }

    public void SetGunMuzzle(Transform muzzle)
    {
        gunMuzzle = muzzle;
    }

    // UI информация
    public int GetCurrentAmmo()
    {
        return currentAmmo;
    }

    public int GetMaxAmmo()
    {
        return maxAmmo;
    }

    public bool IsReloading()
    {
        return isReloading;
    }
}
