using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    public sealed class NpcIdleState : ICharacterState
    {
      
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcAiIntentSource _intentSource;
        private readonly NPCMotor _motor;
        
        private const float MinMoveDistSq = 0.0025f; // 0.05f ^2

        public CharacterStateId Id => CharacterStateId.Idle;

        public NpcIdleState(CharacterStateMachine fsm,  CharacterStateRegistry registry,  NpcAiIntentSource intentSource, NPCMotor motor)
        {
            _fsm = fsm;
            _registry = registry;
            _intentSource = intentSource;
            _motor = motor;
        }
        
        public void Enter()
        {
           _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            if (_intentSource == null || _motor == null) return;
            if (!_intentSource.TryGetMoveDestination(out var dest)) return;

            Vector3 pos = _motor.transform.position;
            var delta = dest - pos;
            delta.y = 0f;
            if (delta.magnitude < MinMoveDistSq) return;

            _fsm.TryTransition(CharacterStateId.Move, _registry, TransitionReason.InputMove);
        }

        public void Exit()
        {
           
        }
    }
}
