using UnityEngine;

public class TracerFx : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TrailRenderer trail;   // перетащи сюда TrailRenderer с дочернего Visual

    [Header("Destroy")]
    [SerializeField] private float destroyDelay = 0.02f;

    [Header("Rotation")]
    [SerializeField] private bool rotateAlongMove = true; // чтобы гарантированно смотрел по движению
    [SerializeField] private bool rotateFromStartToEndOnly = false; // если хочешь как раньше — один раз

    private Vector3 _start;
    private Vector3 _end;
    private float _duration;
    private float _t;

    private Vector3 _prevPos;

    public void Play(Vector3 start, Vector3 end, float duration)
    {
        _start = start;
        _end = end;
        _duration = Mathf.Max(0.01f, duration);
        _t = 0f;

        transform.position = _start;
        _prevPos = _start;

        // Поворот как у тебя раньше (по start->end)
        Vector3 dir = _end - _start;
        if (dir.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // Trail: сброс, чтобы не тянул из прошлого (особенно если будет пул)
        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float a = Mathf.Clamp01(_t / _duration);

        Vector3 pos = Vector3.LerpUnclamped(_start, _end, a);
        transform.position = pos;

        // Поворот по движению (исправляет “не туда смотрит” при любых кейсах)
        if (rotateAlongMove && !rotateFromStartToEndOnly)
        {
            Vector3 move = pos - _prevPos;
            if (move.sqrMagnitude > 0.0000005f)
                transform.rotation = Quaternion.LookRotation(move.normalized, Vector3.up);
        }

        if (trail != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 80f) * 0.1f;
            trail.widthMultiplier = pulse;
        }


        _prevPos = pos;

        if (a >= 1f)
        {
            if (trail != null)
                trail.emitting = false;

            Destroy(gameObject, destroyDelay);
        }
    }
}
