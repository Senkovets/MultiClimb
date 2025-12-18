using Fusion;
using UnityEngine;

public class NetworkProjectile : NetworkBehaviour
{
    [Header("Movement")]
    public float speed = 60f;
    public float maxDistance = 100f;

    [Header("Damage")]
    public float damage = 25f;

    [Header("Hit")]
    [SerializeField] private LayerMask hitLayers;

    [Networked] private Vector3 NetPosition { get; set; }
    [Networked] private Vector3 Direction { get; set; }
    [Networked] private Vector3 StartPosition { get; set; }
    [Networked] private Player Owner { get; set; }

    private Vector3 visualPosition;

    // ¬˚Á˚‚‡ÂÚÒˇ “ŒÀ‹ Œ Ì‡ ÒÂ‚ÂÂ ÔË ÒÔ‡‚ÌÂ
    public void Init(Vector3 dir, Player owner)
    {
        if (!HasStateAuthority)
            return;

        Direction = dir.normalized;
        NetPosition = transform.position;
        StartPosition = NetPosition;
        Owner = owner;
    }

    // ¬˚Á˚‚‡ÂÚÒˇ Û ‚ÒÂı
    public override void Spawned()
    {
        visualPosition = NetPosition;
        transform.position = visualPosition;

        if (Direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(Direction, Vector3.up);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        float step = speed * Runner.DeltaTime;

        // ‘»«»◊≈— »… –≈… ¿—“ ó —≈–¬≈–Õ¿ﬂ »—“»Õ¿
        if (Physics.Raycast(NetPosition, Direction, out RaycastHit hit, step, hitLayers))
        {
            NetworkHealth health =
                hit.collider.GetComponentInParent<NetworkHealth>();

            if (health != null)
            {
                health.ApplyDamage(damage, Owner);
            }

            Runner.Despawn(Object);
            return;
        }

        NetPosition += Direction * step;

        if (Vector3.Distance(StartPosition, NetPosition) >= maxDistance)
        {
            Runner.Despawn(Object);
        }
    }

    private void Update()
    {
        // ¬»«”¿À‹ÕŒ≈ —√À¿∆»¬¿Õ»≈
        visualPosition = Vector3.Lerp(
            visualPosition,
            NetPosition,
            Time.deltaTime * 20f
        );

        transform.position = visualPosition;

        if (Direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(Direction, Vector3.up);
    }
}
