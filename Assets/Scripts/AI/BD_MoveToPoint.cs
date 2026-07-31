using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.Shared.Utility;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// 让行为树只决定移动是否完成，具体寻路目标和 NavMesh 控制继续由 Motor/黑板持有。
    /// </summary>
    [Category("NPC")]
    public class BD_MoveToPoint : Action
    {
        [Tooltip("世界空间目标点")]
        [UnityEngine.Serialization.FormerlySerializedAs("m_Destination")]
        [SerializeField] protected SharedVariable<Vector3> _destination;

        private NpcMotor _motor;

        // ============ 行为树生命周期 ============

        public override void OnAwake()
        {
            base.OnAwake();
            _motor = GetComponent<NpcMotor>();
        }

        public override void OnStart()
        {
            base.OnStart();
            if (_motor == null || _destination == null)
                return;
        }

        public override TaskStatus OnUpdate()
        {
            if (_motor == null) return TaskStatus.Failure;

            return _motor.HasReachedDestination() ? TaskStatus.Success : TaskStatus.Running;
        }

        public override void OnEnd()
        {
            base.OnEnd();
        }
    }
}
