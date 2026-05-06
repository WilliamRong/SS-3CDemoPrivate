using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.Shared.Utility;
using UnityEngine;

namespace AI
{
    [Category("NPC")]
    public class BD_MoveToPoint : Action
    {
        
        [Tooltip("世界空间目标点")]
        [SerializeField] protected SharedVariable<Vector3> m_Destination;

        private NPCMotor m_Motor;
            
        public override void OnAwake()
        {
            base.OnAwake();
            m_Motor = GetComponent<NPCMotor>();
        }

        public override void OnStart()
        {
            base.OnStart();
            if (m_Motor == null || m_Destination == null)
                return;
            
        }

        public override TaskStatus OnUpdate()
        {
            if(m_Motor == null) return TaskStatus.Failure;
            
            return m_Motor.HasReachedDestination() ? TaskStatus.Success : TaskStatus.Running;
        }
        
        public override void OnEnd()
        {
            base.OnEnd();
        }
    }
}
