using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class SprintState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;

        private IdleState _idleState;
        private MoveState _moveState;

        public SprintState(CharacterStateMachine fsm, CharacterMotor motor)
        {
            _fsm = fsm;
            _motor = motor;
        }

        public void SetTransitions(IdleState idleState, MoveState moveState)
        {
            _idleState = idleState;
            _moveState = moveState;
        }

        public CharacterStateId Id { get; } = CharacterStateId.Sprint;
        public void Enter() { }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _motor.SetSprintActive(true);
            _motor.Tick(intent, deltaTime);

            bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
            if (!hasMove)
            {
                _fsm.ChangeState(_idleState);
                return;
            }

            if (!intent.IsSprintHeld)
            {
                _fsm.ChangeState(_moveState);
            }
        }

        public void Exit() { }
    }
}