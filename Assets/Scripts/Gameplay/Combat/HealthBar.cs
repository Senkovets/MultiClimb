using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private Vector3 offset = new(0, 1f, 0);

    private NetworkHealth health;
    private Camera cam;

    public void Init(NetworkHealth h)
    {
        health = h;
        cam = Camera.main;
        UpdateBar();
    }

    private void LateUpdate()
    {
        if (!cam) return;

        transform.position = health.transform.position + offset;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }

    public void UpdateBar()
    {
        fill.fillAmount = health.CurrentHealth / health.MaxHealth;
    }
}
