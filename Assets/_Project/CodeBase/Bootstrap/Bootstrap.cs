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
 
        // Единственное место где живёт app state — всё остальное читает отсюда
        public static IAppStateService AppState { get; private set; }
 
        private bool _preflightOk;
 
        private void Awake()
        {
            // Создаём state service ДО любой другой инициализации
            AppState = new AppStateService();
 
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
 
            // Инициализируем AppFlowController после того как state готов
            GetComponent<AppFlowController>()?.Initialize(AppState);
        }
 
        private void Start()
        {
            if (!_preflightOk || !startMenuOnStart || menuUIController == null)
                return;
 
            if (waitOneFrameBeforeStart)
                StartCoroutine(StartMenuNextFrame());
            else
                FinishBoot();
        }
 
        private IEnumerator StartMenuNextFrame()
        {
            yield return null;
            FinishBoot();
        }
 
        private void FinishBoot()
        {
            menuUIController.StartFromBootstrap();
 
            // Только после того как UI включён — сообщаем что мы в меню
            AppState.TransitionTo(AppGameState.MainMenu);
        }
 
        private bool PreflightChecks()
        {
            bool ok = true;
            if (menuUIController == null)
            {
                Debug.LogError("[Bootstrap] MenuUIController is not assigned.");
                ok = false;
            }
            if (menuConnectionBehaviour == null)
            {
                Debug.LogError("[Bootstrap] MenuConnectionBehaviour is not assigned.");
                ok = false;
            }
            if (fusionMenuConfig == null)
            {
                Debug.LogError("[Bootstrap] FusionMenuConfig is not assigned.");
                ok = false;
            }
            return ok;
        }
    }
}