using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCMotor : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private float _stoppingDistance = 0.15f;
        
        public NavMeshAgent Agent => _agent;
        
        private void Awake()
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }
        }

        public void SetDestination(Vector3 destination){
            if (_agent == null || !_agent.isOnNavMesh)
                return;
            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }
        
        
        /// <summary>停止寻路（停在当前位置）。</summary>
        public void Stop()
        {
            if (_agent == null) return;
            _agent.isStopped = true;
        }
        /// <summary>取消路径，常用于重新设目标前清理。</summary>
        public void ResetPath()
        {
            if (_agent == null) return;
            _agent.ResetPath();
        }

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
    }
    
   
}
