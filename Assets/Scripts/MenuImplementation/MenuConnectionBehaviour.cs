using Fusion;
using Fusion.Menu;
using UnityEngine;
using MultiClimb.MenuFlow;

namespace MultiClimb.Menu
{
    public class MenuConnectionBehaviour : FusionMenuConnectionBehaviour, IMenuFlowServiceProvider
    {
        [SerializeField] private FusionMenuConfig config;
        [Space]
        [Header("Provide a NetworkRunner prefab to be instantiated.\nIf no prefab is provided, a simple one will be created.")]
        [SerializeField] private NetworkRunner networkRunnerPrefab;

        private IMenuFlowService _menuFlowService;

        public IMenuFlowService MenuFlowService => _menuFlowService;

        public void SetMenuFlowService(IMenuFlowService menuFlowService)
        {
            _menuFlowService = menuFlowService;
        }
        
        private void Awake()
        {
            if (!config)
                Log.Error("Fusion menu configuration file not provided.");
            
            
            _menuFlowService ??= new MenuFlowService(new UnityMenuFlowLogger());

            OnBeforeConnect += args => _menuFlowService.OnBeforeConnect(args, "ConnectionBehaviour");
            OnBeforeDisconnect += (connection, reason) => _menuFlowService.OnBeforeDisconnect(connection, reason, "ConnectionBehaviour");
        }

        public override IFusionMenuConnection Create()
        {
            return new MenuConnection(config, networkRunnerPrefab);
        }
    }
}
