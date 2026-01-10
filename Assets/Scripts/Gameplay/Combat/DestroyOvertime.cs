using UnityEngine;

public class DestroyOvertime : MonoBehaviour
{
    public float life = 1f;

    private void Awake()
    {
        if (life <= 0f)
            Destroy(gameObject);
    }

    private void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f)
            Destroy(gameObject);
    }

    private void OnValidate()
    {
        ProcessParticleSystem();
    }

    private void ProcessParticleSystem()
    {
        float maxLifetime = 0f;

        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps)
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
            maxLifetime = Mathf.Max(maxLifetime, main.startLifetime.constant);
        }

        foreach (var p in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = p.main;
            main.stopAction = ParticleSystemStopAction.None;
            maxLifetime = Mathf.Max(maxLifetime, main.startLifetime.constant);
        }

        life = maxLifetime + 0.2f;
    }
}
