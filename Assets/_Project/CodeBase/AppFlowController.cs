using UnityEngine;

namespace _Project.CodeBase
{
    [RequireComponent(typeof(_Project.CodeBase.Bootstrap.Bootstrap))]
    public class AppFlowController : MonoBehaviour
    {
        private IAppStateService _appState;
 
        public void Initialize(IAppStateService appState)
        {
            _appState = appState;
            _appState.OnStateChanged += OnStateChanged;
        }
 
        private void OnDestroy()
        {
            if (_appState != null)
                _appState.OnStateChanged -= OnStateChanged;
        }
 
        private void OnStateChanged(AppGameState prev, AppGameState next)
        {
            switch (next)
            {
                // Fusion сам загружает Game сцену через NetworkSceneManager — не вмешиваемся
                case AppGameState.InLobby:
                    break;
 
                // Вернулись из матча — если Main сцена не активна, загрузить её
                case AppGameState.MainMenu when prev != AppGameState.Initializing:
                    EnsureMainSceneLoaded();
                    break;
            }
        }
 
        private void EnsureMainSceneLoaded()
        {
            // Bootstrap живёт на Main сцене — если мы здесь, сцена уже загружена.
            // Этот метод важен когда перейдём на отдельную Bootstrap сцену.
        }
    }
}