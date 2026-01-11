using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class DebugDamageTester : NetworkBehaviour
{
    [SerializeField] private NetworkHealth health;

    [Header("Debug Damage")]
    [SerializeField] private float debugDamage = 10f;
    [SerializeField] private bool debugCrit = false;
    [SerializeField] private Vector3 localHitOffset = new Vector3(0f, 1.2f, 0f);

    public override void Spawned()
    {
        if (!health) health = GetComponent<NetworkHealth>();
    }

    [ContextMenu("DEBUG/Apply Damage")]
    private void DebugApplyDamage_Context()
    {
        DebugApplyDamage(debugDamage, debugCrit);
    }

    [ContextMenu("DEBUG/Apply 25 Damage")]
    private void DebugApply25_Context()
    {
        DebugApplyDamage(25f, false);
    }

    [ContextMenu("DEBUG/Apply Crit 25 Damage")]
    private void DebugApplyCrit25_Context()
    {
        DebugApplyDamage(25f, true);
    }

    public void DebugApplyDamage(float damage, bool crit)
    {
        if (!health) health = GetComponent<NetworkHealth>();
        if (!health) return;

        // Точка попадания для FX/цифр
        Vector3 hitPoint = health.transform.TransformPoint(localHitOffset);

        // Если у тебя StateAuthority здесь — применяем напрямую
        if (health.Object != null && health.Object.HasStateAuthority)
        {
            health.ApplyDamage(damage, attacker: null, hitPoint: hitPoint, isCrit: crit);
            return;
        }

        // Иначе — просим StateAuthority применить
        RPC_RequestDamage(damage, hitPoint, crit);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDamage(float damage, Vector3 hitPoint, bool crit)
    {
        if (!health) health = GetComponent<NetworkHealth>();
        if (!health) return;

        health.ApplyDamage(damage, attacker: null, hitPoint: hitPoint, isCrit: crit);
    }
}
