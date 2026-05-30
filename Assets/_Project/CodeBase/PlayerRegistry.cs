using System.Collections.Generic;
using _Project.CodeBase;
using Fusion;

namespace MultiClimb.Match
{
    public sealed class PlayerRegistry : NetworkBehaviour
    {
        [Networked, Capacity(12)]
        public NetworkDictionary<PlayerRef, Player> Players => default;

        public int Count => Players.Count;

        public override void Spawned()
        {
            MatchEventBus.Instance?.RaiseRegistryReady(this);
        }

        public bool TryGet(PlayerRef playerRef, out Player player) =>
            Players.TryGet(playerRef, out player);

        public bool Contains(PlayerRef playerRef) =>
            Players.ContainsKey(playerRef);

        public void Register(PlayerRef playerRef, Player player)
        {
            if (!HasStateAuthority) return;

            // "Upsert" ��� �����������:
            if (Players.ContainsKey(playerRef))
                Players.Remove(playerRef);

            Players.Add(playerRef, player);
        }

        public void Unregister(PlayerRef playerRef)
        {
            if (!HasStateAuthority) return;
            if (Players.ContainsKey(playerRef))
                Players.Remove(playerRef);
        }

        public KeyValuePair<PlayerRef, Player>[] Snapshot()
        {
            var arr = new KeyValuePair<PlayerRef, Player>[Players.Count];
            int i = 0;
            foreach (var kv in Players) arr[i++] = kv;
            return arr;
        }
    }
}
