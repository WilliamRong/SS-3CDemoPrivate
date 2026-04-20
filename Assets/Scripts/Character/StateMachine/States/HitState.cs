using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class HitState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private float _timer;
        private float _duration = 0.25f;

        public CharacterStateId Id { get; } = CharacterStateId.Hit;

        public HitState(CharacterStateMachine fsm, CharacterMotor motor, CharacterStateRegistry registry)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
        }

        public void ConfigureDuration(float duration)
        {
            _duration = duration > 0f ? duration : 0.25f;
        }

        public void Enter()
        {
            _timer = 0f;
            _motor.SetSprintActive(false);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            _motor.Tick(intent, deltaTime);

            if (_timer < _duration) return;

            bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
            _fsm.TryTransition(hasMove ? CharacterStateId.Move : CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
        }
    }
}

