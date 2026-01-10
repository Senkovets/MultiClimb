using UnityEngine;
using UnityEngine.InputSystem;

public static class RecoilController
{
    // === Virtual aim marker position (screen-space) ===
    private static Vector2 _aimPos;
    private static bool _inited;

    // === Recoil model (ported from Duckov logic) ===
    private static bool _newRecoil;
    private static float _recoilV;
    private static float _recoilH;
    private static float _recoilRecover;
    private static float _recoilTime;
    private static float _recoilRecoverDelay;

    private static Vector2 _recoilNeedToRecover; // how much offset must be returned back to base
    private static Vector2 _recoilThisShot;
    private static float _oppositeDelta;
    private static float _recoilTimer;

    // Optional: allow external clamp padding (UI safe zone)
    private static float _clampPadding = 0f;

    public static void SetClampPadding(float pixels) => _clampPadding = Mathf.Max(0f, pixels);

    public static void ResetToScreenCenter()
    {
        _aimPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        _recoilNeedToRecover = Vector2.zero;
        _recoilThisShot = Vector2.zero;
        _recoilTimer = 0f;
        _oppositeDelta = 0f;
        _newRecoil = false;
        _inited = true;
        ClampInWindow(ref _aimPos);
    }

    public static Vector2 GetAimScreenPosition()
    {
        if (!_inited)
            ResetToScreenCenter();
        return _aimPos;
    }

    /// <summary>
    /// Call every tick/frame BEFORE computing AimPoint.
    /// mouseDelta is physical delta (InputSystem Mouse.delta or your accumulator).
    /// </summary>
    public static void Tick(Vector2 mouseDelta, float sensitivity)
    {
        if (!_inited)
            ResetToScreenCenter();

        // 1) Base aim moves by mouse delta (this is "where you stopped the mouse")
        _aimPos += mouseDelta * sensitivity;

        // 2) Apply recoil offset & recovery on top of the base
        _aimPos = ProcessAimPosViaRecoil(_aimPos, mouseDelta);

        ClampInWindow(ref _aimPos);
    }

    /// <summary>
    /// Call ON EACH SHOT (local) to start a new recoil impulse.
    /// Units are "Duckov-like": vertical/horizontal are in screen-direction space, not degrees.
    /// </summary>
    public static void NotifyShot(float recoilV, float recoilH, float recoilTime, float recoverDelay, float recoverSpeed)
    {
        _recoilV = Mathf.Max(0f, recoilV);
        _recoilH = recoilH; // can be +/- for pattern
        _recoilTime = Mathf.Max(0.001f, recoilTime);
        _recoilRecoverDelay = Mathf.Max(0f, recoverDelay);
        _recoilRecover = Mathf.Max(0f, recoverSpeed);

        _newRecoil = true;
        _recoilTimer = 0f;
    }

    private static Vector2 ProcessAimPosViaRecoil(Vector2 aimPos, Vector2 mouseDelta)
    {
        // NOTE: This is a direct conceptual port of Duckov's ProcessMousePosViaRecoil()
        // Differences:
        // - no "gun check" here (you decide when to call NotifyShot)
        // - no WarpCursorPosition

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (_newRecoil)
        {
            // Direction from center to current aimPos defines "up" for recoil in top-down view.
            Vector2 dir = (aimPos - screenCenter);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.up;
            dir.Normalize();

            // Same idea as Duckov:
            // recoilThisShot = normalized * recoilV + recoilH * -Perpendicular(normalized)
            Vector2 perp = new Vector2(-dir.y, dir.x); // Perpendicular(dir)
            _recoilThisShot = dir * _recoilV + (-perp) * _recoilH;
        }

        float dt = Time.deltaTime;

        // 1) Recoil kick over recoilTime
        float kickDt = dt;
        if (_recoilTimer + kickDt >= _recoilTime)
            kickDt = _recoilTime - _recoilTimer;

        if (kickDt > 0f)
        {
            // Scale similar to Duckov: * Screen.height / 1440f
            float scale = Screen.height / 1440f;
            Vector2 step = _recoilThisShot * (kickDt / _recoilTime) * scale;

            aimPos += step;
            _recoilNeedToRecover += step;
        }

        // 2) Recovery after delay
        if (kickDt <= 0f && _recoilTimer > _recoilRecoverDelay && _recoilNeedToRecover.magnitude > 0f)
        {
            float recoverDt = dt;
            if (_recoilTimer - recoverDt < _recoilRecoverDelay)
                recoverDt = _recoilTimer - _recoilRecoverDelay;

            float scale = Screen.height / 1440f;

            Vector2 newNeed = Vector2.MoveTowards(
                _recoilNeedToRecover,
                Vector2.zero,
                recoverDt * _recoilRecover * scale
            );

            aimPos += (newNeed - _recoilNeedToRecover);
            _recoilNeedToRecover = newNeed;
        }

        // 3) If player moves mouse opposite to recoil offset — reduce recover debt (Duckov behavior)
        if (_recoilNeedToRecover.sqrMagnitude > 0.000001f)
        {
            float dot = Vector2.Dot(-_recoilNeedToRecover.normalized, mouseDelta);
            if (dot > 0f)
            {
                _oppositeDelta = 0f;
                _recoilNeedToRecover = Vector2.MoveTowards(_recoilNeedToRecover, Vector2.zero, dot);
            }
            else
            {
                _oppositeDelta += mouseDelta.magnitude;
                float resetThreshold = 15f * (Screen.height / 1440f);
                if (_oppositeDelta > resetThreshold)
                {
                    _oppositeDelta = 0f;
                    _recoilNeedToRecover = Vector2.zero;
                }
            }
        }

        _recoilTimer += dt;
        _newRecoil = false;

        return aimPos;
    }

    private static void ClampInWindow(ref Vector2 p)
    {
        float minX = _clampPadding;
        float minY = _clampPadding;
        float maxX = Screen.width - _clampPadding;
        float maxY = Screen.height - _clampPadding;

        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = Mathf.Clamp(p.y, minY, maxY);
    }
}
