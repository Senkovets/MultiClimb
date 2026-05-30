// ============================================================
// Assets/_Project/Game/GameSceneInitializer.cs  — НОВЫЙ ФАЙЛ
// ============================================================
//
// SETUP В EDITOR:
//   GameObject [GameInit] в Game.unity
//   Компонент: GameSceneInitializer (этот файл)
//   Никаких ссылок в Inspector не нужно — всё через ServiceLocator
//
// ============================================================

/*
using UnityEngine;

namespace _Project.CodeBase
{
    public class GameSceneInitializer : MonoBehaviour
    {
        private void Start()
        {
            if (GameBootstrap.GameState == null)
            {
                // Защита на случай запуска Game сцены напрямую из Editor без Bootstrap
                Debug.LogWarning("[GameSceneInit] GameState не найден. " +
                                 "Запусти сцену через Bootstrap или Bootstrap.unity.");
                return;
            }

            MatchEventBus.OnMatchStarted += OnMatchStarted;
            MatchEventBus.OnMatchEnded   += OnMatchEnded;

            // Fusion загрузил сцену — мы в лобби
            GameBootstrap.GameState.TransitionTo(GameState.InLobby);
        }

        private void OnDestroy()
        {
            MatchEventBus.OnMatchStarted -= OnMatchStarted;
            MatchEventBus.OnMatchEnded   -= OnMatchEnded;

            // Сцена выгружается — возврат в меню
            // (на случай если Fusion выгрузил сцену без явного DisconnectAsync)
            GameBootstrap.GameState?.TransitionTo(GameState.MainMenu);
        }

        private void OnMatchStarted() =>
            GameBootstrap.GameState?.TransitionTo(GameState.InMatch);

        private void OnMatchEnded() =>
            GameBootstrap.GameState?.TransitionTo(GameState.MatchEnded);
    }
}
*/


// ============================================================
// Правки в СУЩЕСТВУЮЩИЙ Assets/_Project/CodeBase/MatchEventBus.cs
// Добавить два события — остальное не трогать
// ============================================================

// Найди свой MatchEventBus и добавь в него:

/*
    // --- Добавить эти две строки ---
    public static event Action OnMatchStarted;
    public static event Action OnMatchEnded;

    // --- Добавить эти два метода-триггера ---
    public static void RaiseMatchStarted() => OnMatchStarted?.Invoke();
    public static void RaiseMatchEnded()   => OnMatchEnded?.Invoke();
*/


// ============================================================
// Правки в СУЩЕСТВУЮЩИЙ Assets/_Project/CodeBase/MatchGameMode.cs
// Минимально — только добавить вызовы событий
// ============================================================

// В методе который запускает раунд (у тебя он называется StartRound или аналог):
/*
    private void StartRound()
    {
        // ... твой существующий код ...
        MatchEventBus.RaiseMatchStarted();
    }
*/

// В методе который заканчивает раунд / проверяет победу:
/*
    private void CheckWinCondition()
    {
        // ... твой существующий код проверки kills >= killsToWin ...
        if (somePlayerWon)
        {
            // ... существующий код ...
            MatchEventBus.RaiseMatchEnded();
        }
    }
*/


// ============================================================
// Правки в СУЩЕСТВУЮЩИЙ MenuConnectionBehaviour.cs
// Добавить один публичный метод
// ============================================================

// Найди свой MenuConnectionBehaviour (наследник FusionMenuConnectionBehaviour)
// и добавь:

/*
    public FusionMenuConnection GetOrCreateConnection()
    {
        // _connection — protected поле базового класса FusionMenuConnectionBehaviour
        return _connection ??= Create();
    }
*/


// ============================================================
// ПОРЯДОК СОЗДАНИЯ ФАЙЛОВ (копируй именно в таком порядке)
// ============================================================
//
//  1. Assets/_Project/Services/GameState.cs
//  2. Assets/_Project/Services/IGameStateService.cs
//  3. Assets/_Project/Services/GameStateService.cs
//  4. Assets/_Project/Services/IMenuFlowService.cs
//  5. Assets/_Project/Services/MenuFlowService.cs
//  6. Assets/_Project/Services/ServiceLocator.cs
//  7. Assets/_Project/Bootstrap/GameBootstrap.cs
//  8. Assets/_Project/Bootstrap/AppFlowController.cs
//  9. Assets/_Project/Bootstrap/FatalErrorScreen.cs
// 10. Assets/_Project/Menu/MenuSceneInitializer.cs
// 11. Assets/_Project/Menu/MenuUIRoot.cs
// 12. Assets/_Project/Game/GameSceneInitializer.cs   ← этот файл
//
// Затем правки в существующие:
// 13. MatchEventBus.cs    — добавить 2 события + 2 метода
// 14. MatchGameMode.cs    — добавить 2 вызова событий
// 15. MenuConnectionBehaviour.cs — добавить GetOrCreateConnection()
//
// Затем Editor:
// 16. Создать Bootstrap.unity, добавить в Build Settings index 0
// 17. [Bootstrap] GameObject → GameBootstrap + AppFlowController + FatalErrorScreen
// 18. Main.unity → [MenuInit] с MenuSceneInitializer, отключить корневой Canvas
// 19. Game.unity → [GameInit] с GameSceneInitializer
//
// ============================================================