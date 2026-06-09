using Character.Config;
using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class AttackState : ICharacterState
    {
        public const byte Combo1Step = 1;
        public const byte Combo4Step = 4;
        public const byte SprintAttackStep = 5;
        public const byte DodgeAttackStep = 6;
        public const byte Heavy1StartStep = 7;
        public const byte Heavy1Step = 8;
        public const byte Heavy2Step = 9;

        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterCombatConfig _combat;

        private float _timer;
        private byte _currentAttackStep = Combo1Step;
        private byte _preparedAttackStep = Combo1Step;
        private bool _hasSampledDirection;

        public CharacterStateId Id => CharacterStateId.Attack;
        public byte CurrentComboStep => _currentAttackStep;

        public AttackState(
            CharacterStateMachine fsm,
            CharacterMotor motor,
            CharacterStateRegistry registry,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
            _combat = combat;
        }

        public void Enter()
        {
            BeginAttack(_preparedAttackStep);
            _preparedAttackStep = Combo1Step;
            _motor.SetSprintActive(false);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            float previousTimer = _timer;
            _timer += deltaTime;
            if (intent.IsDodgePressed)
            {
                if (_fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge))
                    return;
            }

            TrySampleAttackDirection(intent, previousTimer);

            if (_currentAttackStep == Heavy1StartStep && _timer >= CurrentDuration)
            {
                BeginAttack(Heavy1Step);
                _motor.Tick(intent, deltaTime);
                return;
            }

            if (intent.IsAttackPressed && CanCancelToNextHeavy())
            {
                BeginAttack(Heavy2Step);
                _motor.Tick(intent, deltaTime);
                return;
            }

            if (intent.IsAttackPressed && CanCancelToNextCombo())
            {
                BeginAttack((byte)(_currentAttackStep + 1));
                _motor.Tick(intent, deltaTime);
                return;
            }

            _motor.Tick(intent, deltaTime);

            if (_timer >= CurrentDuration)
            {
                bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
                _fsm.TryTransition(hasMove ? CharacterStateId.Move : CharacterStateId.Idle, _registry, TransitionReason.Timeout);
            }
        }

        public void Exit()
        {
            _motor.EndAttackRootMotion();
            _currentAttackStep = Combo1Step;
            _preparedAttackStep = Combo1Step;
            _timer = 0f;
            _hasSampledDirection = false;
        }

        public void PrepareSprintAttack()
        {
            _preparedAttackStep = SprintAttackStep;
        }

        public void PrepareDodgeAttack()
        {
            _preparedAttackStep = DodgeAttackStep;
        }

        public void PrepareHeavyAttack()
        {
            _preparedAttackStep = Heavy1StartStep;
        }

        public void PrepareComboAttack()
        {
            _preparedAttackStep = Combo1Step;
        }

        private float CurrentDuration => _combat.GetAttackDuration(_currentAttackStep);

        private void BeginAttack(byte attackStep)
        {
            _currentAttackStep = attackStep > Heavy2Step ? Heavy2Step : attackStep;
            _timer = 0f;
            _hasSampledDirection = false;
            _motor.BeginAttackRootMotion();
        }

        private void TrySampleAttackDirection(CharacterIntent intent, float previousTimer)
        {
            if (_currentAttackStep == SprintAttackStep)
                return;

            if (_hasSampledDirection)
                return;

            float sampleTime = _combat.attackDirectionSampleTime;
            if (previousTimer <= sampleTime && _timer >= sampleTime)
            {
                _hasSampledDirection = true;
                //锁定时不改变方向
                if (_motor.IsLockOnActive) return;
                _motor.SnapAttackDirection(intent);
            }
        }

        private bool CanCancelToNextCombo()
        {
            if (_currentAttackStep < Combo1Step || _currentAttackStep >= Combo4Step)
                return false;

            return _timer >= CurrentDuration * _combat.attackComboCancelStartRatio;
        }

        private bool CanCancelToNextHeavy()
        {
            if (_currentAttackStep != Heavy1Step)
                return false;

            return _timer >= CurrentDuration * _combat.attackComboCancelStartRatio;
        }
    }
}
