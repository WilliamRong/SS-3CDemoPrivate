using Character.Intent;
using Character.Core;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class DodgeState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterContext _context;
    
    
        private IdleState _idleState;
        private MoveState _moveState;
    
        private float _timer;

        private readonly float _duration = 0.2f;
        private readonly float _invincibleStart = 0.05f;
        private readonly float _invincibleEnd = 0.18f;
    
        public DodgeState(CharacterStateMachine fsm, CharacterMotor motor, CharacterContext context)
        {
            _fsm = fsm;
            _motor = motor;
            _context = context;
        }
    
        public void SetTransitions(IdleState idleState, MoveState moveState)
        {
            _idleState = idleState;
            _moveState = moveState;
        }

        public CharacterStateId Id { get; } = CharacterStateId.Dodge;

        public void Enter()
        {
            _timer = 0f;
            _motor.SetSprintActive(false);
            _context.IsInvincible = false;
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            _context.IsInvincible = _timer >= _invincibleStart && _timer <= _invincibleEnd;
            _motor.Tick(intent, deltaTime);
            if (_timer >= _duration)
            {
                bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
                _fsm.ChangeState(hasMove ? _moveState : _idleState);
            }
        }

        public void Exit()
        {
            _context.IsInvincible = false;
        }
    }
}
