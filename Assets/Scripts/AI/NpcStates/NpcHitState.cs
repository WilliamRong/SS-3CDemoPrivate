using Character.Config;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// 将受击强度和动画变体在进入前固化，避免一次受击过程中被后续网络值改写。
    /// </summary>
    public sealed class NpcHitState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combatConfig;
        private float _timer;
        private float _duration;
        private bool _isHeavyHit;
        private byte _hitVariant = 1;
        
        public bool IsHeavyHit => _isHeavyHit;
        public byte HitVariant => _hitVariant;
        
        public CharacterStateId Id { get; } = CharacterStateId.Hit;

        public NpcHitState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combatConfig = combat;
            _duration = _combatConfig.lightHitDuration;

        }

        public void Prepare(bool isHeavy, byte hitVariant = 1)
        {
            _isHeavyHit = isHeavy;
            _hitVariant = (hitVariant >= 1 && hitVariant <= 5) ? hitVariant : (byte)1;
            _duration = isHeavy ? _combatConfig.heavyHitDuration : _combatConfig.lightHitDuration;
        }

        // ============ 状态生命周期 ============

        public void Enter()
        {
            _timer = 0f;
            _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if(_timer < _duration) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
        }
    }
}
