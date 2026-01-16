using UnityEngine;

public sealed class ThrowableHudController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ThrowableController controller;

    [Header("HUD Objects")]
    [SerializeField] private GameObject fillHUD;
    [SerializeField] private GameObject ringHUD;

    [Header("Trajectory (optional)")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private bool useTrajectory = true;

    private void OnEnable()
    {
        if (controller != null)
        {
            controller.PrepareStarted += OnPrepareStarted;
            controller.PrepareCanceled += OnPrepareEnded;
            controller.Thrown += OnPrepareEnded;
        }

        SetHudVisible(false);
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.PrepareStarted -= OnPrepareStarted;
            controller.PrepareCanceled -= OnPrepareEnded;
            controller.Thrown -= OnPrepareEnded;
        }
    }

    private void LateUpdate()
    {
        if (controller == null || !controller.IsPreparing)
            return;

        Vector3 aim = controller.CurrentAimPoint;

        // позиция HUD
        if (fillHUD != null) fillHUD.transform.position = aim;
        if (ringHUD != null) ringHUD.transform.position = aim;

        // scale по радиусу эффекта
        float radius = Mathf.Max(0.01f, controller.EffectRadius);
        if (fillHUD != null) fillHUD.transform.localScale = Vector3.one * radius;
        if (ringHUD != null) ringHUD.transform.localScale = Vector3.one * radius;

        // траектория
        if (useTrajectory && trajectoryLine != null)
        {
            DrawTrajectory();
        }
    }

    private void OnPrepareStarted()
    {
        SetHudVisible(true);
    }

    private void OnPrepareEnded()
    {
        SetHudVisible(false);
    }

    private void SetHudVisible(bool visible)
    {
        if (fillHUD != null) fillHUD.gameObject.SetActive(visible);
        if (ringHUD != null) ringHUD.gameObject.SetActive(visible);

        if (trajectoryLine != null)
            trajectoryLine.enabled = visible && useTrajectory;
    }

    private void DrawTrajectory()
    {
        if (controller == null)
            return;

        // Старт = throwOrigin из контроллера (берём через transform, чтобы не плодить зависимости)
        // Практично: просто попросить в контроллере public Transform ThrowOrigin => throwOrigin;
        // Но чтобы ты мог вставить сразу — используем позицию самого контроллера как fallback:
        Vector3 start = controller.transform.position;

        // Если ты добавишь в контроллер public Transform ThrowOrigin, замени start:
        // Vector3 start = controller.ThrowOrigin.position;

        Vector3 target = controller.CurrentAimPoint;
        float verticalSpeed = controller.VerticalSpeed;

        // Параметры из config удобно получать через контроллер, но сейчас берём разумные дефолты:
        int segments = 22;
        float dt = 0.06f;

        if (trajectoryLine.positionCount != segments)
            trajectoryLine.positionCount = segments;

        Vector3 vel = ThrowableController.CalculateVelocity(start, target, verticalSpeed);
        Vector3 g = Physics.gravity;

        for (int i = 0; i < segments; i++)
        {
            float t = i * dt;
            Vector3 p = start + vel * t + 0.5f * g * (t * t);
            trajectoryLine.SetPosition(i, p);
        }
    }
}
