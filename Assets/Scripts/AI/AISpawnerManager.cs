using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    public class AISpawnerManager : NetworkBehaviour
    {
        [SerializeField]private Transform[]  m_SpawnPoints;
        [SerializeField]private GameObject m_AIPrefab;

        public override void OnStartServer()
        {
            base.OnStartServer();
            foreach (Transform spawnPoint in m_SpawnPoints)
            {
                GameObject instance = Instantiate(m_AIPrefab, spawnPoint.position, spawnPoint.rotation);
                if(NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                   instance.transform.position = hit.position;
                   NetworkServer.Spawn(instance);
                }
                else
                {
                    Debug.LogError($"Failed to spawn AI at {spawnPoint.position}");
                }
                
            }
        }
    }
}
