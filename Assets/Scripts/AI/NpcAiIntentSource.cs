using Character.Intent;
using Opsive.BehaviorDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// 把行为树黑板转换成角色层可消费的意图，避免 NPC 状态机直接依赖 Behavior Designer API。
    /// </summary>
    public class NpcAiIntentSource : MonoBehaviour
    {
        private SharedVariable<Vector3> _destination;

        private SharedVariable<Transform> _target;
        private Transform _gmFacingTarget;

        public bool HasGmFacingTarget => _gmFacingTarget != null;
        
        private void Awake()
        {
            var tree = GetComponent<BehaviorTree>();
            _destination = tree.GetVariable<Vector3>(new PropertyName("destination"));
            _target = tree.GetVariable<Transform>(new PropertyName("target"));
        }

        /// <summary>
        /// 保持与 PlayerController 相同的输入形状，导航则使用世界坐标目标，避免误套玩家相机坐标系。
        /// </summary>
        public CharacterIntent BuildIntent()
        {
            return new CharacterIntent
            {
                Move = Vector2.zero,
                IsSprintHeld = false,
                IsJumpPressed = false,
                IsAttackPressed = false,
                IsDodgePressed = false,
            };
        }

        // ============ 世界目标查询 ============

        /// <summary>
        /// GM 目标优先于行为树目标，保证显式调试命令不会被同帧 AI 黑板覆盖。
        /// </summary>
        public bool TryGetFacingTarget(out Vector3 worldPos)
        {
            worldPos = default;

            if (_gmFacingTarget != null)
            {
                worldPos = _gmFacingTarget.position;
                return true;
            }

            if (_target != null && _target.Value != null)
            {
                worldPos = _target.Value.position;
                return true;
            }

            return TryGetMoveDestination(out worldPos);
        }

        // ============ GM 覆盖 ============

        public void SetGmFacingTarget(Transform target)
        {
            _gmFacingTarget = target;
        }

        public void ClearGmFacingTarget()
        {
            _gmFacingTarget = null;
        }

        /// <summary>
        /// 保留世界空间目标，不写入 CharacterIntent.Move，避免相机系输入语义污染 NPC 导航。
        /// </summary>
        public bool TryGetMoveDestination(out Vector3 worldPos)
        {
            worldPos = default;
            if (_gmFacingTarget != null || _destination == null) return false;

            worldPos = _destination.Value;
            return true;
        }
    }
}
