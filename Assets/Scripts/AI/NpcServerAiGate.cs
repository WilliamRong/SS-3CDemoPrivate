using Mirror;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    /// <summary>
    /// 只在网络会话中关闭非服务器 AI，避免客户端仿真与快照竞争，同时保留离线 AI 测试能力。
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class NpcServerAiGate : MonoBehaviour
    {
        [SerializeField] private bool _log;

        /// <summary>
        /// 等待 Mirror 完成身份初始化后再裁剪 AI 组件，否则 Awake 阶段无法可靠区分服务器实例和客户端镜像。
        /// </summary>
        private void Start()
        {
            if (!ShouldApplyNetworkRules())
                return;

            // 纯服没有客户端镜像，必须保留本地 AI 栈。
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

        // ============ 网络模式判断 ============

        private static bool ShouldApplyNetworkRules()
        {
            return NetworkClient.active || NetworkServer.active;
        }

        // ============ 客户端仿真关闭 ============

        private void DisableClientSimulation()
        {
            var bt = GetComponent<BehaviorTree>();
            if (bt != null)
                bt.enabled = false;

            var motor = GetComponent<NpcMotor>();
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
