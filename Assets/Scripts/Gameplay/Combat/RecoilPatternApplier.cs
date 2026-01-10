using UnityEngine;

public class RecoilPatternApplier : MonoBehaviour
{
    [SerializeField] private RecoilPatternSO pattern;

    private int _shotIndex;
    private float _lastShotTime;

    public void NotifyShot(int deterministicSeed)
    {
        if (pattern == null)
            return;

        float now = Time.time;

        // сброс серии, если была пауза
        if (now - _lastShotTime > pattern.burstResetSeconds)
            _shotIndex = 0;

        _lastShotTime = now;

        pattern.Evaluate(_shotIndex, deterministicSeed, out float v, out float h);

        RecoilController.NotifyShot(
            recoilV: v,
            recoilH: h,
            recoilTime: pattern.recoilTime,
            recoverDelay: pattern.recoverDelay,
            recoverSpeed: pattern.recoverSpeed
        );

        _shotIndex++;
    }
}

