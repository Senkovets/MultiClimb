using MultiClimb.Match;
using UnityEngine;

namespace _Project.CodeBase
{
    public class GameSceneInitializer : MonoBehaviour
    {
        private void Start()
        {
            if (Bootstrap.Bootstrap.AppState == null)
            {
                Debug.LogWarning("[GameSceneInit] AppState не найден. " +
                                 "Запускай игру через Bootstrap, а не напрямую с Game сцены.");
                return;
            }
 
            // Подписываемся на матчевые события через существующий MatchEventBus
            if (MatchEventBus.Instance != null)
                MatchEventBus.Instance.MatchStateChanged += OnMatchStateChanged;
            else
                Debug.LogWarning("[GameSceneInit] MatchEventBus.Instance не найден.");
 
            // Fusion загрузил сцену — сообщаем что мы в лобби
            Bootstrap.Bootstrap.AppState.TransitionTo(AppGameState.InLobby);
        }
 
        private void OnDestroy()
        {
            if (MatchEventBus.Instance != null)
                MatchEventBus.Instance.MatchStateChanged -= OnMatchStateChanged;
 
            // Сцена выгружается — возвращаемся в меню
            Bootstrap.Bootstrap.AppState?.TransitionTo(AppGameState.MainMenu);
        }
 
        private void OnMatchStateChanged(MatchStateChangedEvent e)
        {
            // Переводим матчевый MatchState в наш AppGameState
            // MatchState.Playing  → InMatch
            // MatchState.Waiting + есть победитель → MatchEnded
            switch (e.State)
            {
                case MatchState.Playing:
                    Bootstrap.Bootstrap.AppState?.TransitionTo(AppGameState.InMatch);
                    break;
 
                case MatchState.Waiting when e.Winner != Fusion.PlayerRef.None:
                    Bootstrap.Bootstrap.AppState?.TransitionTo(AppGameState.MatchEnded);
                    break;
            }
        }
    }
}