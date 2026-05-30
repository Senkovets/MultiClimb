using System;

namespace _Project.CodeBase
{
    public interface IAppStateService
    {
        AppGameState Current { get; }
        event Action<AppGameState, AppGameState> OnStateChanged;
        void TransitionTo(AppGameState newState);
    }
}


