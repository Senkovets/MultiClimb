using Fusion.Menu;
using UnityEngine;

namespace MultiClimb.Menu
{
    public class MenuUIController : FusionMenuUIController<FusionMenuConnectArgs>
    {
        private bool _startedFromBootstrap;

        protected override void Start()
        {
            // Intentionally no auto-start here.
            // Menu startup is controlled only by GameBootstrap.Start().
        }

        public void StartFromBootstrap()
        {
            if (_startedFromBootstrap)
                return;

            if (_screens != null && _screens.Length > 0)
            {
                _screens[0].Show();
                _activeScreen = _screens[0];
                _startedFromBootstrap = true;
            }
            else
            {
                Debug.LogError("[MenuUIController] No screens configured for bootstrap start.");
            }
        }
    }
}
