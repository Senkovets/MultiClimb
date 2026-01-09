using UnityEngine;

public class TracerFx : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private float destroyDelay = 0.02f;

    private Vector3 _start;
    private Vector3 _end;
    private float _duration;
    private float _t;

    public void Play(Vector3 start, Vector3 end, float duration)
    {
        _start = start;
        _end = end;
        _duration = Mathf.Max(0.01f, duration);
        _t = 0f;

        transform.position = _start;

        Vector3 dir = _end - _start;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        // длина визуала = дистанция (чтобы выглядело как трассер-луч)
        if (visual != null)
        {
            float dist = dir.magnitude;
            Vector3 s = visual.localScale;
            visual.localScale = new Vector3(s.x, s.y, Mathf.Max(0.05f, dist));

            // поставить куб так, чтобы он тянулся вперёд от start к end
            visual.localPosition = new Vector3(0f, 0f, dist * 0.5f);
        }
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float a = Mathf.Clamp01(_t / _duration);

        // двигаем “голову” трассера (root) вперёд
        transform.position = Vector3.LerpUnclamped(_start, _end, a);

        if (a >= 1f)
            Destroy(gameObject, destroyDelay);
    }
}
