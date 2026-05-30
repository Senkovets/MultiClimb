using System.Collections;
using Fusion.Menu;
using MultiClimb.Menu;
using MultiClimb.MenuFlow;
using UnityEngine;

namespace _Project.CodeBase.Bootstrap
{
    public sealed class Bootstrap : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private MenuUIController menuUIController;
        [SerializeField] private MenuConnectionBehaviour menuConnectionBehaviour;
        [SerializeField] private FusionMenuConfig fusionMenuConfig;

        [Header("Behavior")]
        [SerializeField] private bool startMenuOnStart = true;
        [SerializeField] private bool waitOneFrameBeforeStart = true;
        [SerializeField] private bool failFast = true;

        private bool _preflightOk;

        private void Awake()
        {
            _preflightOk = PreflightChecks();
            if (!_preflightOk && failFast)
            {
                enabled = false;
                return;
            }

            if (menuConnectionBehaviour != null)
            {
                var logger = new UnityMenuFlowLogger();
                var flowService = new MenuFlowService(logger);
                menuConnectionBehaviour.SetMenuFlowService(flowService);
            }
        }

        private void Start()
        {
            if (!_preflightOk || !startMenuOnStart || menuUIController == null)
                return;

            if (waitOneFrameBeforeStart)
                StartCoroutine(StartMenuNextFrame());
            else
                menuUIController.StartFromBootstrap();
        }
        
        private IEnumerator StartMenuNextFrame()
        {
            yield return null;
            menuUIController.StartFromBootstrap();
        }

        private bool PreflightChecks()
        {
            bool ok = true;

            if (menuUIController == null)
            {
                Debug.LogError("[GameBootstrap] MenuUIController is not assigned.");
                ok = false;
            }

            if (menuConnectionBehaviour == null)
            {
                Debug.LogError("[GameBootstrap] MenuConnectionBehaviour is not assigned.");
                ok = false;
            }

            if (fusionMenuConfig == null)
            {
                Debug.LogError("[GameBootstrap] FusionMenuConfig is not assigned.");
                ok = false;
            }

            return ok;
        }
    }
}