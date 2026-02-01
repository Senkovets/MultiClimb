using Fusion;
using UnityEngine;

namespace MultiClimb.Match
{
    public sealed class PlayerSpawner : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        [Header("References")]
        [SerializeField] private PlayerRegistry registry;

        [SerializeField] private NetworkPrefabRef playerPrefab;
        [SerializeField] private Transform spawnpoint;
        [SerializeField] private Transform spawnpointPivot;
        [SerializeField] private Vector3[] spawnPositions;

        private void Awake()
        {
            spawnPositions = new Vector3[]
         {
            new Vector3(5f, 0f, 5f),
            new Vector3(10f, 0f, 0f),
            new Vector3(0f, 0f, 10f),
            new Vector3(10f, 0f, 10f)
         };


        }
        public override void Spawned()
        {
            if (registry == null)
                Debug.LogWarning("[PlayerSpawner] Registry is not assigned.");
        }

        void IPlayerJoined.PlayerJoined(PlayerRef player)
        {
            if (!HasStateAuthority) return;

            if (registry != null && registry.Contains(player))
                return;

            GetNextSpawnpoint(90f, out var pos, out var rot);

            NetworkObject playerObj = Runner.Spawn(playerPrefab, pos, rot, player);
            var playerComp = playerObj.GetComponent<Player>();

            if (registry != null)
                registry.Register(player, playerComp);
        }

        void IPlayerLeft.PlayerLeft(PlayerRef player)
        {
            if (!HasStateAuthority) return;

            if (registry != null && registry.TryGet(player, out var playerComp))
            {
                registry.Unregister(player);
                Runner.Despawn(playerComp.Object);
                return;
            }

        }

        private void GetNextSpawnpoint(float spacingAngle, out Vector3 position, out Quaternion rotation)
        {
            if (spawnpoint == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return;
            }

            position = spawnpoint.position;
            rotation = spawnpoint.rotation;

            if (spawnpointPivot != null)
                spawnpointPivot.Rotate(0f, spacingAngle, 0f);
        }

        public void GetRandomSpawn(out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (spawnPositions != null && spawnPositions.Length > 0)
                position = spawnPositions[Random.Range(0, spawnPositions.Length)];
            else
                position = spawnpoint != null ? spawnpoint.position : Vector3.zero;
        }

    }
}
