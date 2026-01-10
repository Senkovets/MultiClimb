using UnityEngine;

public enum HorizontalMode
{
    SignedCurve,     // curve даёт -1..+1
    Alternate,       // чередуем знак (+ - + -)
    AlternatePairs,  // ++ -- ++ --
    AlwaysRight,
    AlwaysLeft,
    RandomPerShot
}

[CreateAssetMenu(menuName = "Combat/Recoil Pattern", fileName = "RecoilPattern")]
public class RecoilPatternSO : ScriptableObject
{
    [Header("Pattern Length")]
    [Tooltip("Сколько выстрелов задаёт паттерн. После конца — зацикливается.")]
    public int length = 30;

    [Header("Vertical (kick)")]
    [Tooltip("Кривая вертикальной отдачи по номеру выстрела (ось X: 0..length-1, Y: 0..1).")]
    public AnimationCurve vertical01 = AnimationCurve.Linear(0, 0.6f, 29, 1.0f);

    [Header("Horizontal (shape)")]
    public HorizontalMode horizontalMode = HorizontalMode.Alternate;
    [Tooltip("Кривая горизонтальной формы (X: 0..length-1, Y: -1..+1). Используется в SignedCurve.")]
    public AnimationCurve horizontalSigned = AnimationCurve.Linear(0, -0.3f, 29, 0.3f);

    [Header("Amplitudes")]
    [Tooltip("Базовая вертикальная отдача (в тех же единицах, что recoilV в RecoilController).")]
    public float verticalBase = 35f;

    [Tooltip("Сколько добавлять вертикали поверх base (масштаб кривой).")]
    public float verticalExtra = 25f;

    [Tooltip("Амплитуда горизонтали (в тех же единицах, что recoilH).")]
    public float horizontalAmplitude = 10f;

    [Header("Timing (pass-through)")]
    public float recoilTime = 0.04f;
    public float recoverDelay = 0.10f;
    public float recoverSpeed = 220f;

    [Header("Reset / Burst")]
    [Tooltip("Если пауза между выстрелами больше этого времени — серия обнуляется.")]
    public float burstResetSeconds = 0.22f;

    public void Evaluate(int shotIndex, int randomSeed, out float recoilV, out float recoilH)
    {
        int n = Mathf.Max(1, length);
        int i = shotIndex % n;

        float x = i; // по оси X работаем в индексе (0..length-1)
        float v01 = Mathf.Clamp01(vertical01.Evaluate(x));
        recoilV = verticalBase + verticalExtra * v01;

        float hSigned = 0f;
        switch (horizontalMode)
        {
            case HorizontalMode.SignedCurve:
                hSigned = Mathf.Clamp(horizontalSigned.Evaluate(x), -1f, 1f);
                break;

            case HorizontalMode.Alternate:
                hSigned = (i % 2 == 0) ? 1f : -1f;
                break;

            case HorizontalMode.AlternatePairs:
                // ++ -- ++ --
                hSigned = ((i / 2) % 2 == 0) ? 1f : -1f;
                break;

            case HorizontalMode.AlwaysRight:
                hSigned = 1f;
                break;

            case HorizontalMode.AlwaysLeft:
                hSigned = -1f;
                break;

            case HorizontalMode.RandomPerShot:
                unchecked
                {
                    // детерминированный random [-1..1] по seed+index
                    uint s = (uint)(randomSeed * 73856093 ^ i * 19349663);
                    s ^= s >> 13; s *= 1274126177u; s ^= s >> 16;
                    float u = (s & 0x00FFFFFFu) / 16777215f; // 0..1
                    hSigned = u * 2f - 1f;
                }
                break;
        }

        recoilH = horizontalAmplitude * hSigned;
    }
}
