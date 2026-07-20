using Character.Intent;
using Opsive.BehaviorDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;

namespace AI
{
    public class NpcAiIntentSource : MonoBehaviour
    {
        private SharedVariable<Vector3> _destination;

        private SharedVariable<Transform> _target;
        private Transform _gmFacingTarget;
        
        private void Awake()
        {
            var tree = GetComponent<BehaviorTree>();
            _destination = tree.GetVariable<Vector3>(new PropertyName("destination"));
            _target = tree.GetVariable<Transform>(new PropertyName("target"));
        }

        /// <summary>
        /// 与 PlayerController 一样形状的意图；导航不走 intent.Move（相机系），见 TryGetMoveDestination。
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

        public void SetGmFacingTarget(Transform target)
        {
            _gmFacingTarget = target;
        }

        public void ClearGmFacingTarget()
        {
            _gmFacingTarget = null;
        }

        /// <summary>
        /// NPC 专用：世界空间移动目标（来自黑板 destination）。
        /// </summary>
        public bool TryGetMoveDestination(out Vector3 worldPos)
        {
            worldPos = default;
            if (_destination == null) return false;

            worldPos = _destination.Value;
            return true;
        }

        private void LateUpdate()
        {
            
        }
    }
}
