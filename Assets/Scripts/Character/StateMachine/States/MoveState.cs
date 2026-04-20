using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class MoveState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;

        private IdleState _idleState;
        private SprintState _sprintState;
        private AttackState _attackState;
        private DodgeState _dodgeState;

        public MoveState(CharacterStateMachine fsm, CharacterMotor motor)
        {
            _fsm = fsm;
            _motor = motor;
        }

        public void SetTransitions(IdleState idleState, SprintState sprintState,  AttackState attackState,
            DodgeState dodgeState)
        {
            _idleState = idleState;
            _sprintState = sprintState;
            _attackState = attackState;
            _dodgeState = dodgeState;
        }

        public CharacterStateId Id { get; } = CharacterStateId.Move;
        public void Enter() { }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _motor.SetSprintActive(false);
            _motor.Tick(intent, deltaTime);
            
            if (intent.IsDodgePressed) { _fsm.ChangeState(_dodgeState); return; }
            if (intent.IsAttackPressed) { _fsm.ChangeState(_attackState); return; }

            bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
            if (!hasMove)
            {
                _fsm.ChangeState(_idleState);
                return;
            }

            if (intent.IsSprintHeld)
            {
                _fsm.ChangeState(_sprintState);
            }
        }

        public void Exit() { }
    }
}