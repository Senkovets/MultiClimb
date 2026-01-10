using UnityEngine;

public class TracerFx : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TrailRenderer trail;

    [Header("Destroy")]
    [SerializeField] private float destroyDelay = 0.02f;

    [Header("Rotation")]
    [SerializeField] private bool rotateAlongMove = true;
    [SerializeField] private bool rotateFromStartToEndOnly = false;

    [Header("Impact FX")]
    [SerializeField] private GameObject bulletHitFxPrefab;  // обычный
    [SerializeField] private GameObject bloodHitFxPrefab;   // кровь

    private bool _hitIsFlesh;
    private Vector3 _hitNormal;
    private bool _hasImpact;

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

        _hasImpact = false;
        _hitIsFlesh = false;
        _hitNormal = Vector3.zero;

        Vector3 dir = _end - _start;
        if (dir.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    // ВЫЗЫВАЙ после Play()
    public void SetImpact(bool hitIsFlesh, Vector3 hitNormal)
    {
        _hasImpact = true;
        _hitIsFlesh = hitIsFlesh;
        _hitNormal = hitNormal;
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float a = Mathf.Clamp01(_t / _duration);

        Vector3 pos = Vector3.LerpUnclamped(_start, _end, a);
        transform.position = pos;

        if (rotateAlongMove && !rotateFromStartToEndOnly)
        {
            Vector3 move = pos - _prevPos;
            if (move.sqrMagnitude > 0.0000005f)
                transform.rotation = Quaternion.LookRotation(move.normalized, Vector3.up);
        }

        _prevPos = pos;

        if (a >= 1f)
        {
            if (trail != null)
                trail.emitting = false;

            SpawnImpactFx();

            Destroy(gameObject, destroyDelay);
        }
    }

    private void SpawnImpactFx()
    {
        // Если нет данных — считаем “обычное попадание”
        GameObject prefab = _hitIsFlesh ? bloodHitFxPrefab : bulletHitFxPrefab;
        if (prefab == null)
            return;

        // Ориентация по нормали, если есть
        Quaternion rot = transform.rotation;
        if (_hasImpact && _hitNormal.sqrMagnitude > 0.0001f)
            rot = Quaternion.LookRotation(_hitNormal);

        Instantiate(prefab, transform.position, rot);
    }
}
