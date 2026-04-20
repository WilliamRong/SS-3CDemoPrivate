using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class HitState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;

        private IdleState _idleState;
        private MoveState _moveState;
        private float _timer;
        private float _duration = 0.25f;

        public CharacterStateId Id { get; } = CharacterStateId.Hit;

        public HitState(CharacterStateMachine fsm, CharacterMotor motor)
        {
            _fsm = fsm;
            _motor = motor;
        }

        public void SetTransitions(IdleState idleState, MoveState moveState)
        {
            _idleState = idleState;
            _moveState = moveState;
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
            _fsm.ChangeState(hasMove ? _moveState : _idleState);
        }

        public void Exit()
        {
        }
    }
}

