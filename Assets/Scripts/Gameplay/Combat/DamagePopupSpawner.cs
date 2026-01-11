using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamagePopupSpawner : MonoBehaviour
{
    [SerializeField] private TMP_Text popupPrefab;
    [SerializeField] private float lifeTime = 0.9f;
    [SerializeField] private float rise = 0.9f;
    [SerializeField] private float spread = 0.25f;

    public void Pop(float damage, Vector3 worldPoint, bool crit)
    {
        if (!popupPrefab) return;

        var t = Instantiate(popupPrefab);
        t.text = crit ? damage.ToString("0") : damage.ToString("0");
        t.transform.position = worldPoint + Vector3.up * 0.2f;

        // Случайное смещение
        Vector3 dir = new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));
        Vector3 start = t.transform.position + dir;
        Vector3 end = start + Vector3.up * rise;

        t.transform.position = start;

        // Взгляд на камеру (простая версия)
        if (Camera.main)
            t.transform.rotation = Quaternion.LookRotation(t.transform.position - Camera.main.transform.position);

        // Анимация: всплытие + fade + punch scale
        var c0 = t.color;
        var c1 = c0; c1.a = 0f;

        float punch = crit ? 0.28f : 0.18f;

        t.transform.localScale = Vector3.one;
        t.transform.DOPunchScale(Vector3.one * punch, 0.18f, 10, 0.9f);

        Sequence s = DOTween.Sequence();
        s.Join(t.transform.DOMove(end, lifeTime).SetEase(Ease.OutQuad));
        s.Join(t.DOColor(c1, lifeTime).SetEase(Ease.InQuad));
        s.OnComplete(() => Destroy(t.gameObject));
    }
}
