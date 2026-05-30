using System;
using System.Collections.Generic;
using Fusion;
using MultiClimb.Match;
using UnityEngine;

namespace _Project.CodeBase
{
    public sealed class MatchEventBus : MonoBehaviour
    {
        public static MatchEventBus Instance { get; private set; }

        public event Action<PlayerDiedEvent> PlayerDied;
        public event Action<MatchStateChangedEvent> MatchStateChanged;
        public event Action<LeaderboardChangedEvent> LeaderboardChanged;

        public event Action<PlayerRegistry> RegistryReady;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Raise(PlayerDiedEvent e) => PlayerDied?.Invoke(e);
        public void Raise(MatchStateChangedEvent e) => MatchStateChanged?.Invoke(e);
        public void Raise(LeaderboardChangedEvent e) => LeaderboardChanged?.Invoke(e);

        public void RaiseRegistryReady(PlayerRegistry registry)
        {
            RegistryReady?.Invoke(registry);
        }
    }

    public readonly struct MatchStateChangedEvent
    {
        public readonly GameState State;
        public readonly PlayerRef Winner; // PlayerRef.None ���� ��� ����������

        public MatchStateChangedEvent(GameState state, PlayerRef winner)
        {
            State = state;
            Winner = winner;
        }
    }

    public readonly struct LeaderboardChangedEvent
    {
        public readonly KeyValuePair<PlayerRef, Player>[] Entries;

        public LeaderboardChangedEvent(KeyValuePair<PlayerRef, Player>[] entries)
        {
            Entries = entries;
        }
    }

    public readonly struct PlayerDiedEvent
    {
        public readonly PlayerRef Victim;
        public readonly PlayerRef Killer;
        public readonly int Tick;

        public PlayerDiedEvent(PlayerRef victim, PlayerRef killer, int tick)
        {
            Victim = victim;
            Killer = killer;
            Tick = tick;
        }
    }
}
