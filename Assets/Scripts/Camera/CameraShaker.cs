using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraShaker : MonoBehaviour
{

    private float horizontalBias = 0f;
    private void Awake()
    {
        CameraShaker._instance = this;
    }

    public static void Shake(Vector3 velocity, CameraShaker.CameraShakeTypes shakeType)
    {
        if (CameraShaker._instance == null)
        {
            return;
        }
        switch (shakeType)
        {
            case CameraShaker.CameraShakeTypes.recoil:
                CameraShaker._instance.recoilSource.GenerateImpulseWithVelocity(velocity);
                return;
            case CameraShaker.CameraShakeTypes.explosion:
                CameraShaker._instance.explosionSource.GenerateImpulseWithVelocity(velocity);
                return;
            case CameraShaker.CameraShakeTypes.meleeAttackHit:
                CameraShaker._instance.meleeAttackSource.GenerateImpulseWithVelocity(velocity);
                return;
            default:
                return;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard.mKey.isPressed)
            TestShake();
    }

    [ContextMenu("TestShake")]
    public void TestShake()
    {
        Debug.Log("TestShake");
        Vector3 vector3 = new Vector3(0.1f, 0.1f, 0.1f);
        CameraShakeTypes cameraShakeTypes = CameraShakeTypes.recoil;
        Shake(vector3, cameraShakeTypes);
    }

    public void TestShake(Vector3 fireDirection)
    {
        // Нормализуем направление выстрела
        fireDirection.Normalize();

        // Горизонтальная нормаль относительно направления выстрела
        Vector3 horizontalNormal = Vector3.Cross(fireDirection, Vector3.up).normalized;

        // Задаём случайное горизонтальное смещение (влево/вправо)
        float horizontalRecoil = Random.Range(-0.1f, 0.1f);

        // Задаём вертикальное смещение
        float verticalRecoil = Random.Range(0.05f, 0.15f);

        // Итоговое смещение камеры
        Vector3 recoilVector = horizontalNormal * horizontalRecoil + Vector3.up * verticalRecoil;

        // Вызываем Shake с этим вектором
        Shake(recoilVector, CameraShakeTypes.recoil);
    }

    public void TestShake(Vector3 fireDirection, int power)
    {
        fireDirection.Normalize();

        Vector3 right = Vector3.Cross(fireDirection, Vector3.up).normalized;

        horizontalBias = Mathf.Lerp(
            horizontalBias,
            Random.Range(-1f, 1f),
            0.2f
        );

        float horizontal = horizontalBias * 0.02f * power;
        float vertical = 0.03f * power;

        Vector3 recoil = right * horizontal + Vector3.up * vertical;
        Shake(recoil, CameraShakeTypes.recoil);
    }


    private static CameraShaker _instance;
    public CinemachineImpulseSource recoilSource;
    public CinemachineImpulseSource meleeAttackSource;
    public CinemachineImpulseSource explosionSource;
    public enum CameraShakeTypes
    {
        recoil,
        explosion,
        meleeAttackHit
    }
}
