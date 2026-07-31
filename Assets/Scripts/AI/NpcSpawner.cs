using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace AI
{
    /// <summary>
    /// 只由服务器生成并先贴合 NavMesh，避免客户端重复生成或出生点让 Agent 处于无效位置。
    /// </summary>
    public class NpcSpawner : NetworkBehaviour
    {
        [FormerlySerializedAs("m_SpawnPoints")]
        [SerializeField] private Transform[] _spawnPoints;

        [FormerlySerializedAs("m_AIPrefab")]
        [SerializeField] private GameObject _npcPrefab;

        /// <summary>
        /// 实例只有在 NavMesh 采样成功后才注册进网络，避免客户端收到一个服务器立即销毁的无效 NPC。
        /// </summary>
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
