using Character.Config;
using Character.Combat;
using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    /// <summary>
    /// 让连段、重击升级和方向采样共享一个权威计时器，避免每段攻击拆成状态后产生转换缝隙。
    /// </summary>
    public sealed class AttackState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterCombatConfig _combat;

        private float _timer;
        private AttackMoveId _currentAttackId = AttackMoveId.Combo1;
        private AttackMoveId _preparedAttackId = AttackMoveId.Combo1;
        private bool _hasSampledDirection;

        public CharacterStateId Id => CharacterStateId.Attack;
        public AttackMoveId CurrentAttackId => _currentAttackId;
        public byte CurrentComboStep => _currentAttackId.ToByte();

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

        // ============ 状态生命周期 ============

        public void Enter()
        {
            BeginAttack(_preparedAttackId);
            _preparedAttackId = AttackMoveId.Combo1;
            _motor.SetSprintActive(false);
        }

        /// <summary>
        /// 在同一计时轴上处理方向采样、重击升级和连段窗口，确保一次攻击每个边沿只跨越一次。
        /// </summary>
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

            if (_currentAttackId == AttackMoveId.Heavy1Start && _timer >= CurrentDuration)
            {
                BeginAttack(AttackMoveId.Heavy1);
                _motor.Tick(intent, deltaTime);
                return;
            }

            if (intent.IsAttackPressed && CanCancelToNextHeavy())
            {
                BeginAttack(AttackMoveId.Heavy2);
                _motor.Tick(intent, deltaTime);
                return;
            }

            if (intent.IsAttackPressed && CanCancelToNextCombo())
            {
                BeginAttack(_currentAttackId.NextCombo());
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
            _currentAttackId = AttackMoveId.Combo1;
            _preparedAttackId = AttackMoveId.Combo1;
            _timer = 0f;
            _hasSampledDirection = false;
        }

        // ============ 下一次攻击准备 ============

        public void PrepareSprintAttack()
        {
            _preparedAttackId = AttackMoveId.Sprint;
        }

        public void PrepareDodgeAttack()
        {
            _preparedAttackId = AttackMoveId.Dodge;
        }

        public void PrepareHeavyAttack()
        {
            _preparedAttackId = AttackMoveId.Heavy1Start;
        }

        public void PrepareComboAttack()
        {
            _preparedAttackId = AttackMoveId.Combo1;
        }

        // ============ 攻击段推进 ============

        private float CurrentDuration => _combat.GetAttackDuration(_currentAttackId);

        private void BeginAttack(AttackMoveId attackId)
        {
            _currentAttackId = attackId.ClampOrDefault();
            _timer = 0f;
            _hasSampledDirection = false;
            _motor.BeginAttackRootMotion();
        }

        /// <summary>
        /// 使用前后计时值检测跨越采样点，低帧率下也不会因为跳过精确时刻而漏掉朝向锁定。
        /// </summary>
        private void TrySampleAttackDirection(CharacterIntent intent, float previousTimer)
        {
            if (_currentAttackId == AttackMoveId.Sprint)
                return;

            if (_hasSampledDirection)
                return;

            float sampleTime = _combat.attackDirectionSampleTime;
            if (previousTimer <= sampleTime && _timer >= sampleTime)
            {
                _hasSampledDirection = true;
                // 锁定朝向由锁定系统持续维护，攻击采样不能覆盖它。
                if (_motor.IsLockOnActive) return;
                _motor.SnapAttackDirection(intent);
            }
        }

        private bool CanCancelToNextCombo()
        {
            if (!_currentAttackId.CanAdvanceCombo())
                return false;

            return _timer >= CurrentDuration * _combat.attackComboCancelStartRatio;
        }

        private bool CanCancelToNextHeavy()
        {
            if (_currentAttackId != AttackMoveId.Heavy1)
                return false;

            return _timer >= CurrentDuration * _combat.attackComboCancelStartRatio;
        }
    }
}
