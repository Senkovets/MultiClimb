using Fusion;
using MultiClimb.Match;
using System.Collections.Generic;
using UnityEngine;

public sealed class RespawnSystem : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerRegistry registry;
    [SerializeField] private PlayerSpawner spawner;

    [Header("Settings")]
    [SerializeField] private float respawnDelaySeconds = 3f;

    // victim -> timer
    private readonly Dictionary<PlayerRef, TickTimer> _pending = new();

    public override void Spawned()
    {
        // Подписка на событие смерти
        if (MatchEventBus.Instance != null)
            MatchEventBus.Instance.PlayerDied += OnPlayerDied;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Отписка
        if (MatchEventBus.Instance != null)
            MatchEventBus.Instance.PlayerDied -= OnPlayerDied;

        _pending.Clear();
    }

    private void OnPlayerDied(PlayerDiedEvent e)
    {
        // Респавн делает только authority
        if (!HasStateAuthority) return;

        // Если уже стоит таймер — перезапишем (на всякий)
        _pending[e.Victim] = TickTimer.CreateFromSeconds(Runner, respawnDelaySeconds);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (_pending.Count == 0) return;

        // чтобы безопасно удалять из словаря во время прохода
        var toRespawn = ListPool<PlayerRef>.Get();

        foreach (var kv in _pending)
        {
            if (kv.Value.ExpiredOrNotRunning(Runner))
                toRespawn.Add(kv.Key);
        }

        for (int i = 0; i < toRespawn.Count; i++)
        {
            var victim = toRespawn[i];
            _pending.Remove(victim);
            Respawn(victim);
        }

        ListPool<PlayerRef>.Release(toRespawn);
    }

    private void Respawn(PlayerRef victim)
    {
        if (registry == null || spawner == null) return;

        if (!registry.TryGet(victim, out Player player) || player == null)
            return;

        spawner.GetRandomSpawn(out var pos, out var rot);

        player.Health.ResetHealth();  
        player.Teleport(pos, rot);
        player.IsVisible = true;
    }

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new();
        public static List<T> Get() => Pool.Count > 0 ? Pool.Pop() : new List<T>(8);
        public static void Release(List<T> list) { list.Clear(); Pool.Push(list); }
    }
}
