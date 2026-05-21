using Character.Config;
using Character.Intent;
using Character.Motor;
namespace Character.StateMachine.States
{
    
    public sealed class GuardState: ICharacterState
    {
        public enum GuardPhase : byte
        {
            None = 0,
            Start = 1,
            Loop = 2,
            Exit = 3,
        }

        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterCombatConfig _combatConfig;

        private float _phaseTimer;

        public CharacterStateId Id { get; } = CharacterStateId.Guard;
        public GuardPhase CurrentPhase { get; private set; } = GuardPhase.Start;

        public GuardState(CharacterStateMachine fsm, CharacterMotor motor, CharacterStateRegistry registry,
            CharacterCombatConfig combatConfig)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
            _combatConfig = combatConfig;
        }
        
        public void Enter()
        {
            SetPhase(GuardPhase.Start);
            _motor.SetSprintActive(false);
            _motor.SetMovementBlocked(true);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            intent.IsSprintHeld = false;
            intent.IsAttackPressed = false;
            intent.IsJumpPressed = false;

            switch (CurrentPhase)
            {
                case GuardPhase.Start:
                    TickStart(intent, deltaTime);
                    break;
                case GuardPhase.Loop:
                    TickLoop(intent, deltaTime);
                    break;
                case GuardPhase.Exit:
                    TickExit(intent, deltaTime);
                    break;
            }
        }

        public void Exit()
        {
            _motor.SetMovementBlocked(false);
            _motor.SetSprintActive(false);
            SetPhase(GuardPhase.Start);
        }
        
        private void TickStart(CharacterIntent  intent, float deltaTime)
        {
            intent.IsDodgePressed = false;
            _motor.Tick(intent, deltaTime);
            
            _phaseTimer += deltaTime;
            if (_phaseTimer >= _combatConfig.guardStartDuration)
            {
                SetPhase(GuardPhase.Loop);
                _motor.SetMovementBlocked(false);
            }
        }

        private void TickLoop(CharacterIntent intent, float deltaTime)
        {
            _motor.SetSprintActive(false);
            _motor.Tick(intent, deltaTime);

            if (intent.IsDodgePressed)
            {
                _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
                return;
            }

            if (!intent.IsGuardHeld)
            {
                SetPhase(GuardPhase.Exit);
                _motor.SetMovementBlocked(true);
            }
        }

        private void TickExit(CharacterIntent intent, float deltaTime)
        {
            intent.IsDodgePressed = false;
            _motor.Tick(intent, deltaTime);
            
            _phaseTimer += deltaTime;
            if(_phaseTimer < _combatConfig.guardExitDuration) return;
            
            bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
            _fsm.TryTransition(hasMove ? CharacterStateId.Move : CharacterStateId.Idle, _registry,
                TransitionReason.Timeout);
        }

        private void SetPhase(GuardPhase phase)
        {
            CurrentPhase = phase;
            _phaseTimer = 0f;
        }
    }
}
