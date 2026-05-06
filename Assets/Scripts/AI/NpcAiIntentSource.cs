using System;
using Character.Intent;
using Opsive.BehaviorDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;

namespace AI
{
    public class NpcAiIntentSource : MonoBehaviour
    {
        private SharedVariable<Vector3> _destination;
        
        private void Awake()
        {
            var tree = GetComponent<BehaviorTree>();
            _destination = tree.GetVariable<Vector3>(new PropertyName("destination"));
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
