using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Singleton
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
                Debug.LogError($"There should only ever be one instance of {nameof(CameraFollow)}!");
            }
        }
    }
    private static CameraFollow _singleton;

    [SerializeField] private Highlighter highlighter;

    private Transform target;
    private Player player;

    public Texture2D cursorTexture;

    private void Awake()
    {
        Singleton = this;

        if(cursorTexture != null)
            Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDestroy()
    {
        if (Singleton == this)
            Singleton = null;
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            // ѕозици€ камеры: выше и чуть сзади target
            Vector3 cameraPosition = target.position + new Vector3(0, 8, -6); // y=8 Ч высота, z=-6 Ч отступ назад

            // ”станавливаем позицию камеры
            transform.position = cameraPosition;

            // Ќаправл€ем камеру в точку на уровне талии target (чуть ниже головы)
            Vector3 lookAtPoint = target.position + new Vector3(0, 1, 0); // y=1 Ч смещение вниз от центра target
            transform.LookAt(lookAtPoint);

            // ќбновл€ем highlighter
            highlighter.UpdateHighlightable(transform.position, transform.forward, player);
        }
    }


    public void SetTarget(Transform newTarget, Player player)
    {
        target = newTarget;
        this.player = player;
    }
}
