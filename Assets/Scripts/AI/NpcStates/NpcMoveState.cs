using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// 让移动状态只桥接世界目标与 NavMesh，抵达判定统一由 NpcMotor 提供。
    /// </summary>
    public sealed class NpcMoveState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcAiIntentSource _intentSource;
        private readonly NpcMotor _motor;
        
        public CharacterStateId Id => CharacterStateId.Move;
        
        public NpcMoveState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcAiIntentSource intentSource,
            NpcMotor motor)
        {
            _fsm = fsm;
            _registry = registry;
            _intentSource = intentSource;
            _motor = motor;
        }
        
        public void Enter() { }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            if (_motor == null) return;
            if (_intentSource != null && _intentSource.TryGetMoveDestination(out var dest))
            {
                _motor.SetDestination(dest);
            }

            if (_motor.HasReachedDestination())
            {
                _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.InputMove);
            }
        }

        public void Exit() { }
    }
}
