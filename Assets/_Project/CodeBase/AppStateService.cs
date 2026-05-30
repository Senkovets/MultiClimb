using System;
using UnityEngine;

namespace _Project.CodeBase
{
    public class AppStateService : IAppStateService
    {
        public AppGameState Current { get; private set; } = AppGameState.Initializing;
 
        public event Action<AppGameState, AppGameState> OnStateChanged;
 
        public void TransitionTo(AppGameState newState)
        {
            if (Current == newState) return;
 
            var prev = Current;
            Current = newState;
 
            Debug.Log($"[AppState] {prev} → {newState}");
            OnStateChanged?.Invoke(prev, newState);
        }
    }
}