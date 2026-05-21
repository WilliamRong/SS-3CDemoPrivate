using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace AI
{
    /// <summary>
    /// Spawns NPC prefabs on the server at configured points, snapping to NavMesh.
    /// </summary>
    public class NpcSpawner : NetworkBehaviour
    {
        [FormerlySerializedAs("m_SpawnPoints")]
        [SerializeField] private Transform[] _spawnPoints;

        [FormerlySerializedAs("m_AIPrefab")]
        [SerializeField] private GameObject _npcPrefab;

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_npcPrefab == null || _spawnPoints == null)
                return;

            foreach (Transform spawnPoint in _spawnPoints)
            {
                if (spawnPoint == null) continue;

                GameObject instance = Instantiate(_npcPrefab, spawnPoint.position, spawnPoint.rotation);
                if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    instance.transform.position = hit.position;
                    NetworkServer.Spawn(instance);
                }
                else
                {
                    Debug.LogError($"[NpcSpawner] Failed to find NavMesh near {spawnPoint.position}");
                    Destroy(instance);
                }
            }
        }
    }
}
