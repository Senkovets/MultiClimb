using UnityEngine;

namespace _Project.CodeBase
{
    public class HudController : MonoBehaviour
    {
        [Header("Панели (назначь в Inspector)")]
        [SerializeField] private GameObject lobbyPanel;   // "Waiting for players..."
        [SerializeField] private GameObject matchPanel;   // HUD во время матча
        [SerializeField] private GameObject endPanel;     // Экран результатов
 
        private void Start()
        {
            if (Bootstrap.Bootstrap.AppState == null) return;
 
            Bootstrap.Bootstrap.AppState.OnStateChanged += Refresh;
            Refresh(AppGameState.Initializing, Bootstrap.Bootstrap.AppState.Current);
        }
 
        private void OnDestroy()
        {
            if (Bootstrap.Bootstrap.AppState != null)
                Bootstrap.Bootstrap.AppState.OnStateChanged -= Refresh;
        }
 
        private void Refresh(AppGameState prev, AppGameState current)
        {
            if (lobbyPanel) lobbyPanel.SetActive(current == AppGameState.InLobby);
            if (matchPanel) matchPanel.SetActive(current == AppGameState.InMatch);
            if (endPanel)   endPanel.SetActive(current == AppGameState.MatchEnded);
        }
    }
}