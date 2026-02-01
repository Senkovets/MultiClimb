using Fusion;
using UnityEngine;

namespace MultiClimb.Match
{
    public sealed class ReadySystem : NetworkBehaviour
    {
        [SerializeField] private PlayerRegistry registry;

        public bool AreAllReady()
        {
            if (registry == null) return false;
            if (registry.Players.Count < 1) return false;

            foreach (var kv in registry.Players)
            {
                if (!kv.Value.IsReady)
                    return false;
            }
            return true;
        }

        public void UnreadyAll()
        {
            if (!HasStateAuthority) return;
            if (registry == null) return;

            foreach (var kv in registry.Players)
                kv.Value.IsReady = false;
        }
    }
}
