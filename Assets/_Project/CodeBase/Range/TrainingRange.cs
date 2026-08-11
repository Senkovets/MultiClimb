using Fusion;
using UnityEngine;

namespace _Project.CodeBase.Range
{
    /// <summary>
    /// Спавнит манекены в заданных точках. Вешается на пустой
    /// объект в сцене Game.
    /// </summary>
    public class TrainingRange : NetworkBehaviour
    {
        [SerializeField] private NetworkPrefabRef dummyPrefab;
 
        [Tooltip("Точки спавна. Сколько точек — столько манекенов.")]
        [SerializeField] private Transform[] spawnPoints;
 
        [Tooltip("Выключи чтобы убрать тир из матча")]
        [SerializeField] private bool enableRange = true;
 
        public override void Spawned()
        {
            if (!HasStateAuthority || !enableRange)
                return;
 
            SpawnDummies();
        }
 
        private void SpawnDummies()
        {
            if (!dummyPrefab.IsValid)
            {
                Debug.LogError("[TrainingRange] dummyPrefab не назначен.");
                return;
            }
 
            foreach (Transform point in spawnPoints)
            {
                if (point == null)
                    continue;
 
                Runner.Spawn(dummyPrefab, point.position, point.rotation);
            }
        }
 
        private void OnDrawGizmos()
        {
            if (spawnPoints == null)
                return;
 
            Gizmos.color = Color.yellow;
 
            foreach (Transform point in spawnPoints)
            {
                if (point == null)
                    continue;
 
                Gizmos.DrawWireCube(point.position + Vector3.up, new Vector3(0.8f, 2f, 0.4f));
            }
        }
    }
}