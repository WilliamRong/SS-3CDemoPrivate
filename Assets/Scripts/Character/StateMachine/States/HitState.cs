using Character.Config;
using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class HitState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterCombatConfig _combat;

        private float _timer;
        private float _duration;

        public CharacterStateId Id { get; } = CharacterStateId.Hit;

        public HitState(
            CharacterStateMachine fsm,
            CharacterMotor motor,
            CharacterStateRegistry registry,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
            _combat = combat;
            _duration = _combat.lightHitDuration;
        }

        public void ConfigureDuration(float duration)
        {
            _duration = duration > 0f ? duration : _combat.lightHitDuration;
        }

        public void Enter()
        {
            _timer = 0f;
            _motor.SetSprintActive(false);
            _motor.BeginReactionRootMotion();
            _motor.SetMovementBlocked(true);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            _motor.Tick(intent, deltaTime);

            if (_timer < _duration)
                return;

            bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
            if (intent.IsSprintHeld && hasMove)
            {
                _fsm.TryTransition(CharacterStateId.Sprint, _registry, TransitionReason.Timeout);
                return;
            }

            _fsm.TryTransition(
                hasMove ? CharacterStateId.Move : CharacterStateId.Idle,
                _registry,
                TransitionReason.Timeout);
        }

        public void Exit()
        {
            _motor.EndReactionRootMotion();
            _motor.SetMovementBlocked(false);
        }
    }
}
