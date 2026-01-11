using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamagePopupSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private TMP_Text popupPrefab;

    [Header("Lifetime")]
    [SerializeField] private float lifeTime = 0.9f;
    [SerializeField] private float rise = 0.9f;

    [Header("Spread")]
    [SerializeField] private float spreadXZ = 0.25f;

    [Header("Vertical Offset")]
    [SerializeField] private float baseYOffset = 1f;        // базовое смещение вверх
    [SerializeField] private float randomYOffset = 0.24f;     // случайный Y
    public void Pop(float damage, Vector3 worldPoint, bool crit)
    {
        if (!popupPrefab) return;

        TMP_Text t = Instantiate(popupPrefab);

        t.text = damage.ToString("0");

        // --- стартовая позиция ---
        float yOffset =
            baseYOffset +
            Random.Range(0f, randomYOffset);

        Vector3 start = worldPoint + Vector3.up * yOffset;

        // случайное XZ смещение
        start += new Vector3(
            Random.Range(-spreadXZ, spreadXZ),
            0f,
            Random.Range(-spreadXZ, spreadXZ)
        );

        t.transform.position = start;

        // --- Billboard к камере ---
        if (Camera.main)
        {
            Transform cam = Camera.main.transform;
            Vector3 lookDir = t.transform.position - cam.position;
            t.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        }

        // --- визуал ---
        Color c0 = t.color;
        Color c1 = c0;
        c1.a = 0f;

        float punch = crit ? 0.28f : 0.18f;

        t.transform.localScale = Vector3.one;
        t.transform.DOPunchScale(Vector3.one * punch, 0.18f, 10, 0.9f);

        Vector3 end = start + Vector3.up * rise;

        Sequence s = DOTween.Sequence();
        s.Join(t.transform.DOMove(end, lifeTime).SetEase(Ease.OutQuad));
        s.Join(t.DOColor(c1, lifeTime).SetEase(Ease.InQuad));
        s.OnComplete(() => Destroy(t.gameObject));
    }
}
