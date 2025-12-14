using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    [Header("Основные параметры")]
    public Transform gunMuzzle; // Точка вылета пуль
    public GameObject muzzleFlashPrefab; // Префаб вспышки
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
        if (cam == null)
            return;

        // Луч из камеры через курсор
        Ray camRay = cam.ScreenPointToRay(Input.mousePosition);

        Vector3 targetPoint;

        // Если камера попала в объект
        if (Physics.Raycast(camRay, out RaycastHit camHit, 2000f, hitLayers))
        {
            targetPoint = camHit.point;
        }
        else
        {
            targetPoint = camRay.origin + camRay.direction * 2000f;
        }

        // Выравнивание точки по высоте gunMuzzle
        targetPoint.y = gunMuzzle.position.y;

        // Итоговое направление
        Vector3 fireDirection = (targetPoint - gunMuzzle.position).normalized;

        // Выстрел
        if (Physics.Raycast(gunMuzzle.position, fireDirection, out RaycastHit hit, bulletDistance, hitLayers))
        {
            var health = hit.collider.GetComponent<MinimalHealth>();
            if (health != null)
                health.TakeDamage(damage);

            Debug.DrawLine(gunMuzzle.position, hit.point, Color.red, 0.5f);

            // Запоминаем точку удара для гизма
            debugPoints.Add(hit.point);
        }
        else
        {
            Vector3 missPoint = gunMuzzle.position + fireDirection * bulletDistance;
            Debug.DrawLine(gunMuzzle.position, missPoint, Color.yellow, 0.5f);

            // Запоминаем точку промаха
            debugPoints.Add(missPoint);
        }
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
