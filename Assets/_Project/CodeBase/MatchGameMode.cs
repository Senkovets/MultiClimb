using Fusion;
using MultiClimb.Match;
using System.Collections.Generic;
using _Project.CodeBase;
using UnityEngine;

namespace MultiClimb.Match
{
    public enum GameState
    {
        Waiting,
        Playing,
    }

    public class MatchGameMode : NetworkBehaviour
    {
        public PlayerSpawner PlayerSpawner;
        public PlayerRegistry PlayerRegistry;
        public ReadySystem ReadySystem;
        public LeaderboardSystem LeaderboardSystem;

        [Header("Win Condition")]
        [SerializeField] private int killsToWin = 10;

        [Header("Round Restart")]
        [SerializeField] private float roundRestartDelay = 3f;

        [Networked] private Player Winner { get; set; }
        [Networked, OnChangedRender(nameof(GameStateChanged))] private GameState State { get; set; }

        // ������ ������ ��������
        [Networked] private TickTimer NextRoundTimer { get; set; }

        public override void Spawned()
        {
            Winner = null;
            State = GameState.Waiting;
            NextRoundTimer = TickTimer.None;

            // UI ������ �� ������� �������� � ������ ����� event bus
            GameStateChanged();

            Runner.SetIsSimulated(Object, true);

            UIManager.Singleton.Init();
        }

        // ?? ������ �� ����� (����� ���������� ������)
        // private void OnTriggerEnter(Collider other) { ... }

        public override void FixedUpdateNetwork()
        {
            if (PlayerRegistry.Players.Count < 1)
                return;

            if (Runner.IsServer)
            {
                // 1) Waiting -> Playing (������ ����� ����� ready)
                if (State == GameState.Waiting)
                {
                    // ���� ����� ������ ����-������ � ��� ���
                    if (NextRoundTimer.IsRunning && !NextRoundTimer.ExpiredOrNotRunning(Runner))
                        return;

                    // ����-����� ����� ������
                    if (NextRoundTimer.ExpiredOrNotRunning(Runner) && Winner != null)
                    {
                        StartNewRound();
                        return;
                    }

                    // ������ �����/������ ����� ����� Ready
                    if (Winner == null && ReadySystem.AreAllReady())
                    {
                        StartNewRound();
                        return;
                    }
                }

                // 2) Playing: �������� win condition �� ������
                if (State == GameState.Playing && Winner == null)
                {
                    foreach (KeyValuePair<PlayerRef, Player> kv in PlayerRegistry.Players)
                    {
                        var p = kv.Value;
                        if (p != null && p.Kills >= killsToWin)
                        {
                            Winner = p;

                            //�������������� ���������� 
                            LeaderboardSystem.Tick();

                            State = GameState.Waiting;

                            // ������� ����������, ����� �� ���� �����-����������
                            ReadySystem.UnreadyAll();

                            // ��������� ����-������� ������
                            NextRoundTimer = TickTimer.CreateFromSeconds(Runner, roundRestartDelay);
                            break;
                        }
                    }
                }
            }

            // ��������� ��������� �� ����� ���� (�� �� �����������)
            if (State == GameState.Playing || (State == GameState.Waiting && Winner != null))
                LeaderboardSystem.Tick();

        }

        private void StartNewRound()
        {
            Winner = null;
            State = GameState.Playing;
            NextRoundTimer = TickTimer.None;

            PreparePlayers();

            // ����� ������/����� �� ����� �����
            foreach (var kv in PlayerRegistry.Players)
            {
                var p = kv.Value;
                if (p == null) continue;

                // ������� 1 (���� ���� ������� � ������� ��������):
                // p.Kills = 0;
                // p.Score = 0;

                // ������� 2 (����������): ������ ����� � Player
                p.ResetRoundStats();
            }
        }

        private void GameStateChanged()
        {
            var winnerRef = Winner != null ? Winner.Object.InputAuthority : PlayerRef.None;
            MatchEventBus.Instance?.Raise(new MatchStateChangedEvent(State, winnerRef));
        }

        private void PreparePlayers()
        {
            foreach (KeyValuePair<PlayerRef, Player> player in PlayerRegistry.Players)
            {
                PlayerSpawner.GetRandomSpawn(out Vector3 position, out Quaternion rotation);
                player.Value.Teleport(position, rotation);
                player.Value.IsCaged = false;
                player.Value.ResetCooldowns();
                player.Value.IsReady = false; // �� ������
            }
        }
    }
}