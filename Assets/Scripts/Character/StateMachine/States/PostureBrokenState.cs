using Character.Config;
using Character.Intent;
using Character.Motor;
using UnityEngine;

namespace Character.StateMachine.States
{
    public sealed class PostureBrokenState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterMotor _motor;
        private readonly CharacterCombatConfig _combat;
        private float _timer;

        public CharacterStateId Id => CharacterStateId.PostureBroken;

        public PostureBrokenState(
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
            _timer = 0f;
            _motor.SetSprintActive(false);
            _motor.SetMovementBlocked(true);
        }

        public void Exit()
        {
            _timer = 0f;
            _motor.SetMovementBlocked(false);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            _motor.Tick(intent, deltaTime);

            if (_timer < Mathf.Max(0.01f, _combat.postureBreakDuration))
                return;

            CharacterStateId target = intent.Move.sqrMagnitude > 0.0001f
                ? CharacterStateId.Move
                : CharacterStateId.Idle;

            _fsm.TryTransition(target, _registry, TransitionReason.Timeout);
        }
    }
}
