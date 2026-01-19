using Fusion;
using TMPro;
using UnityEngine;

public class Nameplate : NetworkBehaviour
{
    [SerializeField] private Player player;              // твой Player (где Name)
    [SerializeField] private TextMeshProUGUI nameText;   // TMP над головой
    [SerializeField] private Transform billboardRoot;    // что вращаем к камере

    private Camera _cam;
    private string _last;

    public override void Spawned()
    {
        _cam = Camera.main;
        ApplyName(force: true);
    }

    public override void Render()
    {
        // 1) текст — только если изменился
        ApplyName(force: false);

        // 2) билборд к камере
        if (_cam != null && billboardRoot != null)
        {
            Vector3 dir = billboardRoot.position - _cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                billboardRoot.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void ApplyName(bool force)
    {
        if (player == null || nameText == null) return;

        string n = player.Name ?? string.Empty;
        if (force || _last != n)
        {
            _last = n;
            nameText.text = n;
        }
    }
}
