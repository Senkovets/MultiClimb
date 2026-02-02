using Fusion;
using MultiClimb.Match;
using System.Collections.Generic;
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

        // Таймер “между раундами”
        [Networked] private TickTimer NextRoundTimer { get; set; }

        public override void Spawned()
        {
            Winner = null;
            State = GameState.Waiting;
            NextRoundTimer = TickTimer.None;

            // UI теперь НЕ дергаем напрямую — только через event bus
            GameStateChanged();

            Runner.SetIsSimulated(Object, true);

            UIManager.Singleton.Init();
        }

        // ?? Больше не нужно (киллы определяют победу)
        // private void OnTriggerEnter(Collider other) { ... }

        public override void FixedUpdateNetwork()
        {
            if (PlayerRegistry.Players.Count < 1)
                return;

            if (Runner.IsServer)
            {
                // 1) Waiting -> Playing (первый старт через ready)
                if (State == GameState.Waiting)
                {
                    // Если стоит таймер авто-старта — ждём его
                    if (NextRoundTimer.IsRunning && !NextRoundTimer.ExpiredOrNotRunning(Runner))
                        return;

                    // Авто-старт после победы
                    if (NextRoundTimer.ExpiredOrNotRunning(Runner) && Winner != null)
                    {
                        StartNewRound();
                        return;
                    }

                    // Первый старт/ручной старт через Ready
                    if (Winner == null && ReadySystem.AreAllReady())
                    {
                        StartNewRound();
                        return;
                    }
                }

                // 2) Playing: проверка win condition по киллам
                if (State == GameState.Playing && Winner == null)
                {
                    foreach (KeyValuePair<PlayerRef, Player> kv in PlayerRegistry.Players)
                    {
                        var p = kv.Value;
                        if (p != null && p.Kills >= killsToWin)
                        {
                            Winner = p;

                            //принудительное обновление 
                            LeaderboardSystem.Tick();

                            State = GameState.Waiting;

                            // сбросим готовность, чтобы не было “авто-готовности”
                            ReadySystem.UnreadyAll();

                            // запускаем авто-рестарт раунда
                            NextRoundTimer = TickTimer.CreateFromSeconds(Runner, roundRestartDelay);
                            break;
                        }
                    }
                }
            }

            // Лидерборд обновляем во время игры (не на ресимуляции)
            if (State == GameState.Playing || (State == GameState.Waiting && Winner != null))
                LeaderboardSystem.Tick();

        }

        private void StartNewRound()
        {
            Winner = null;
            State = GameState.Playing;
            NextRoundTimer = TickTimer.None;

            PreparePlayers();

            // Сброс киллов/очков на новый раунд
            foreach (var kv in PlayerRegistry.Players)
            {
                var p = kv.Value;
                if (p == null) continue;

                // Вариант 1 (если поля сетевые и сеттеры доступны):
                // p.Kills = 0;
                // p.Score = 0;

                // Вариант 2 (рекомендую): сделай метод в Player
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
                player.Value.IsReady = false; // на всякий
            }
        }
    }
}