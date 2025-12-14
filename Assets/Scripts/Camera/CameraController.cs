using Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Singleton
    {
        get => _singleton;
        set
        {
            if (value == null)
                _singleton = null;
            else if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Destroy(value);
                Debug.LogError($"There should only ever be one instance of {nameof(CameraController)}!");
            }
        }
    }
    private static CameraController _singleton;

    [SerializeField]
    private CrosshairUI _crosshairUI;
    [SerializeField]
    private CameraShaker _cameraShaker;
    [SerializeField]
    private CameraCursorFollow _cameraCursorFollow;
    [SerializeField]
    private CinemachineVirtualCamera _VCamera;

    private Player _player;
    private Transform _followTarget;

    private void Awake()
    {
        Singleton = this;
        _crosshairUI = GetComponent<CrosshairUI>();
        _cameraShaker = GetComponent<CameraShaker>();
        _cameraCursorFollow = GetComponent<CameraCursorFollow>();
        _VCamera = GetComponent<CinemachineVirtualCamera>();

    }

    public void SetTarget(Transform newTarget, Player player)
    {
        _followTarget = newTarget;
        _player = player;

        _VCamera.Follow = _followTarget;
    }

}
