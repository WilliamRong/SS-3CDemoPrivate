using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    /// <summary>
    /// 集中保护所有 NavMeshAgent 操作，避免场景卸载或纯客户端禁用 Agent 后仍调用其 API。
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcMotor : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private float _stoppingDistance = 0.15f;

        public NavMeshAgent Agent => _agent;

        public Transform Root => transform;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }
        }

        // ============ 导航控制 ============

        public void SetDestination(Vector3 destination)
        {
            if (!CanControlAgent())
                return;
            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (!CanControlAgent()) return;
            _agent.isStopped = true;
        }

        public void ResetPath()
        {
            if (!CanControlAgent()) return;
            _agent.ResetPath();
        }

        /// <summary>
        /// 同时等待路径计算、剩余距离和实际速度收敛，避免 Agent 仍在减速时状态机提前切回 Idle。
        /// </summary>
        public bool HasReachedDestination()
        {
            if (_agent == null || !_agent.isOnNavMesh)
            {
                return true;
            }

            if (_agent.pathPending)
            {
                return false;
            }

            if (_agent.remainingDistance > _stoppingDistance)
            {
                return false;
            }

            return _agent.velocity.sqrMagnitude < 0.1f;
        }

        // ============ 权威位移 ============

        /// <summary>
        /// 通过 Agent.Move 消费根位移，避免直接改 Transform 让 NavMesh 的 nextPosition 漂移。
        /// </summary>
        public void ApplyRootMotionDelta(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (!CanControlAgent())
                return;

            deltaPosition.y = 0f;
            if (deltaPosition.sqrMagnitude > 0.0000001f)
                _agent.Move(deltaPosition);

            float deltaYaw = Mathf.DeltaAngle(0f, deltaRotation.eulerAngles.y);
            if (Mathf.Abs(deltaYaw) > 0.0001f)
                transform.Rotate(0f, deltaYaw, 0f, Space.World);

            _agent.nextPosition = transform.position;
        }

        public void RotateTowards(Quaternion targetRotation, float maxDegreesDelta)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxDegreesDelta);

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.nextPosition = transform.position;
        }

        // ============ Agent 安全检查 ============

        /// <summary>
        /// Host 退出或场景卸载时 Agent 可能已经脱离 NavMesh，所有写操作必须统一短路。
        /// </summary>
        private bool CanControlAgent()
        {
            return _agent != null && _agent.enabled && _agent.isOnNavMesh;
        }
    }
}
