using Character.Config;
using Character.Intent;
using Character.Motor;
using Character.StateMachine;
using UnityEngine;

namespace Character.StateMachine.States
{
    public sealed class ParriedState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterMotor _motor;
        private readonly CharacterCombatConfig _combat;
        private float _elapsed;

        public CharacterStateId Id => CharacterStateId.Parried;

        public ParriedState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            CharacterMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combat = combat;
        }

        public void Enter()
        {
            _elapsed = 0f;
            _motor.SetSprintActive(false);
            _motor.SetMovementBlocked(true);
            _motor.SetTurnRotationOverride(true);
        }

        public void Exit()
        {
            _elapsed = 0f;
            _motor.SetMovementBlocked(false);
            _motor.SetTurnRotationOverride(false);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _elapsed += deltaTime;
            _motor.Tick(default, deltaTime);
            if (_elapsed >= Mathf.Max(0.01f, _combat.parriedDuration))
            {
                _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
            }
        }
    }
}
