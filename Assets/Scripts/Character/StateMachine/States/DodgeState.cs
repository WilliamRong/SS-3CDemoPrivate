using Character.Config;
using Character.Core;
using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class DodgeState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterContext _context;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterCombatConfig _combat;

        private float _timer;

        public DodgeState(
            CharacterStateMachine fsm,
            CharacterMotor motor,
            CharacterContext context,
            CharacterStateRegistry registry,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _motor = motor;
            _context = context;
            _registry = registry;
            _combat = combat;
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
            _context.IsInvincible = _timer >= _combat.dodgeInvincibleStart && _timer <= _combat.dodgeInvincibleEnd;
            _motor.Tick(intent, deltaTime);
            if (_timer >= _combat.dodgeDuration)
            {
                bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
                _fsm.TryTransition(hasMove ? CharacterStateId.Move : CharacterStateId.Idle, _registry, TransitionReason.Timeout);
            }
        }

        public void Exit()
        {
            _context.IsInvincible = false;
        }
    }
}
