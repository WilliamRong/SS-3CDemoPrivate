using Opsive.BehaviorDesigner.Runtime.Tasks;
using AI;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.Shared.Utility;
using UnityEngine;

namespace AI.BT
{
    /// <summary>
    /// 璁╄涓烘爲鍙喅瀹氱Щ鍔ㄦ槸鍚﹀畬鎴愶紝鍏蜂綋瀵昏矾鐩爣鍜?NavMesh 鎺у埗缁х画鐢?Motor/榛戞澘鎸佹湁銆?    /// </summary>
    [Category("NPC")]
    public class BD_MoveToPoint : Action
    {
        [Tooltip("World-space destination point.")]
        [UnityEngine.Serialization.FormerlySerializedAs("m_Destination")]
        [SerializeField] protected SharedVariable<Vector3> _destination;

        private NpcMotor _motor;

        // ============ 琛屼负鏍戠敓鍛藉懆鏈?============

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
