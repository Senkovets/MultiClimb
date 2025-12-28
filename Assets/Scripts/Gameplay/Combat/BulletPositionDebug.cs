using UnityEngine;

public class BulletPositionDebug : MonoBehaviour
{
    [SerializeField]
    private Vector3 targetPosition = new Vector3(
        -8.68258572f,
        -2.08616257e-07f,
        -11.0376339f
    );

    [SerializeField]
    private float checkRadius = 1f; // радиус допуска

    private void Update()
    {
        if (Vector3.Distance(transform.position, targetPosition) <= checkRadius)
        {
            Debug.LogError(
                $"Bullet reached target area. Position: {transform.position}"
            );
        }
    }
}
