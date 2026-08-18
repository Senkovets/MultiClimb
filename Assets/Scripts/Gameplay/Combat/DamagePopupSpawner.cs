using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Gameplay.Combat
{
    public class DamagePopupSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private TMP_Text popupPrefab;

        [Header("Lifetime")]
        [SerializeField] private float lifeTime = 0.9f;
        [SerializeField] private float rise = 0.9f;

        [Header("Spread")]
        [Tooltip("Разлёт цифр по горизонтали. Для дробовика ставь " +
                 "0.5 и больше, иначе девять цифр наложатся друг на друга.")]
        [SerializeField] private float spreadXZ = 0.25f;

        [Header("Vertical Offset")]
        [Tooltip("Базовый подъём цифры над точкой попадания")]
        [SerializeField] private float baseYOffset = 1f;

        [Tooltip("Случайная добавка к высоте, чтобы цифры не выстроились в ряд")]
        [SerializeField] private float randomYOffset = 0.24f;
        
        [Header("Critical")]
        [SerializeField] private Color critColor = new Color(1f, 0.85f, 0.2f);

        [Tooltip("Во сколько раз крит крупнее обычного урона")]
        [SerializeField] private float critScale = 1.35f;

        /// <summary>
        /// Показывает цифру урона.
        /// </summary>
        /// <param name="delay">
        /// Задержка перед появлением. Нужна при множественных попаданиях
        /// (дробовик): цифры выходят очередью, а не одной кляксой.
        /// </param>
        public void Pop(float damage, Vector3 worldPoint, bool crit, float delay = 0f)
        {
            if (!popupPrefab)
                return;

            TMP_Text t = Instantiate(popupPrefab);
            t.text = damage.ToString("0");

            // Цвет и размер задаём ДО чтения t.color —
            // ниже он берётся как целевой для fade-in
            if (crit)
                t.color = critColor;

            float scale = crit ? critScale : 1f;

            Vector3 start = BuildStartPosition(worldPoint);
            t.transform.position = start;

            FaceCamera(t.transform);

            Color visible = t.color;
            Color faded = visible;
            faded.a = 0f;

            t.transform.localScale = Vector3.one * scale;

            // Прячем на время задержки, иначе все цифры мигнут сразу
            if (delay > 0f)
                t.color = faded;

            Vector3 end = start + Vector3.up * rise;
            float punch = (crit ? 0.28f : 0.18f) * scale;

            Sequence s = DOTween.Sequence();

            if (delay > 0f)
            {
                s.AppendInterval(delay);
                s.Append(t.DOColor(visible, 0.04f));
            }

            // Punch внутри последовательности, иначе он сыграет
            // сразу, ещё до окончания задержки
            s.Append(t.transform.DOPunchScale(Vector3.one * punch, 0.18f, 10, 0.9f));

            s.Join(t.transform.DOMove(end, lifeTime).SetEase(Ease.OutQuad));
            s.Join(t.DOColor(faded, lifeTime).SetEase(Ease.InQuad));

            s.OnComplete(() => Destroy(t.gameObject));
        }

        private Vector3 BuildStartPosition(Vector3 worldPoint)
        {
            float yOffset = baseYOffset + Random.Range(0f, randomYOffset);

            Vector3 start = worldPoint + Vector3.up * yOffset;

            start += new Vector3(
                Random.Range(-spreadXZ, spreadXZ),
                0f,
                Random.Range(-spreadXZ, spreadXZ));

            return start;
        }

        private static void FaceCamera(Transform t)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            Vector3 lookDir = t.position - cam.transform.position;

            if (lookDir.sqrMagnitude < 0.0001f)
                return;

            t.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        }
    }
}