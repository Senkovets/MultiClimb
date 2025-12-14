using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraShaker : MonoBehaviour
{
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
