using UnityEngine;

public class HeadCollider : MonoBehaviour
{
    public bool isHead = true;

    // Можно добавить визуал для отладки
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}