using Mirror;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    /// <summary>
    /// 仅在服务器运行 AI（行为树 / NavMesh / Motor）。客户端关闭本地仿真，避免与网络快照打架。
    /// 单机未启动 Mirror（AITest 等）时不介入，便于本地调试 AI。
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class NpcServerAiGate : MonoBehaviour
    {
        [SerializeField] private bool _log;

        private void Start()
        {
            if (!ShouldApplyNetworkRules())
                return;

            // 无本地 Client 的纯服进程：不可能是“需要关本地仿真”的客户端，直接保留 AI。
            if (NetworkServer.active && !NetworkClient.active)
            {
                if (_log)
                    Debug.Log($"[NpcServerAiGate] Dedicated server keeps AI: {name}", this);
                return;
            }

            var identity = GetComponent<NetworkIdentity>();
            if (identity == null)
                return;

            if (identity.isServer)
            {
                if (_log)
                    Debug.Log($"[NpcServerAiGate] Server keeps AI: {name}", this);
                return;
            }

            DisableClientSimulation();
        }

        private static bool ShouldApplyNetworkRules()
        {
            return NetworkClient.active || NetworkServer.active;
        }

        private void DisableClientSimulation()
        {
            var bt = GetComponent<BehaviorTree>();
            if (bt != null)
                bt.enabled = false;

            var motor = GetComponent<NPCMotor>();
            if (motor != null)
                motor.enabled = false;

            var agent = GetComponent<NavMeshAgent>();
            if (agent != null)
                agent.enabled = false;

            if (_log)
                Debug.Log($"[NpcServerAiGate] Client: disabled AI stack on {name}", this);
        }
    }
}
