using Character.Config;
using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    /// <summary>
    /// 受击状态只接收结算后的时长和变体，避免在状态内重复判断伤害或格挡结果。
    /// </summary>
    public sealed class HitState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterCombatConfig _combat;

        private float _timer;
        private float _duration;
        private bool _isHeavyHit;
        private byte _hitVariant = 1;

        public CharacterStateId Id { get; } = CharacterStateId.Hit;
        public bool IsHeavyHit => _isHeavyHit;
        public byte HitVariant => _hitVariant;

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

        public void Configure(float duration, bool isHeavyHit = false, byte hitVariant = 1)
        {
            _isHeavyHit = isHeavyHit;
            _hitVariant = (hitVariant >= 1 && hitVariant <= 5) ? hitVariant : (byte)1;
            _duration = duration > 0f ? duration : _combat.lightHitDuration;
        }

        // ============ 状态生命周期 ============

        public void Enter()
        {
            _timer = 0f;
            _motor.SetSprintActive(false);
            _motor.BeginReactionRootMotion();
            _motor.SetMovementBlocked(true);
        }

        /// <summary>
        /// 受击只由配置时长退出，忽略普通输入，确保权威反应不会被本地操作提前取消。
        /// </summary>
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
