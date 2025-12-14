using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraCursorFollow : MonoBehaviour
{
    public CinemachineVirtualCamera vcam;

    [Header("Distance")]
    public float maxOffsetDistance = 4f;
    public float smooth = 8f;

    private CinemachineTransposer transposer;
    private Vector3 baseOffset;

    void Awake()
    {
        transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        baseOffset = transposer.m_FollowOffset;
    }

    void LateUpdate()
    {
        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector2 delta = (mouse - center) / center;
        delta = Vector2.ClampMagnitude(delta, 1f);

        Vector3 targetOffset = baseOffset;
        targetOffset.x += delta.x * maxOffsetDistance;
        targetOffset.z += delta.y * maxOffsetDistance;

        transposer.m_FollowOffset = Vector3.Lerp(
            transposer.m_FollowOffset,
            targetOffset,
            Time.deltaTime * smooth
        );
    }
}
