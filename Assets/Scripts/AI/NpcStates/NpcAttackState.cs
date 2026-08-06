using Character.Config;
using AI;
using Character.Combat;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI.NpcStates
{
    /// <summary>
    /// 由服务器计时结束 NPC 攻击，避�?Animator 播放长度成为权威状态退出条件�?    /// </summary>
    public sealed class NpcAttackState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combatConfig;

        private float _timer;
        private AttackMoveId _attackId = AttackMoveId.Combo1;
        
        public AttackMoveId CurrentAttackId => _attackId;
        public byte CurrentComboStep => _attackId.ToByte();
        
        public CharacterStateId Id { get; } = CharacterStateId.Attack;

        public NpcAttackState(CharacterStateMachine fsm, CharacterStateRegistry registry, NpcMotor motor,
            CharacterCombatConfig combatConfig)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combatConfig = combatConfig;
        }
        
        public void Prepare(byte comboStep) => _attackId = AttackMoveIdExtensions.FromByte(comboStep);

        public void Prepare(AttackMoveId attackId) => _attackId = attackId.ClampOrDefault();

        public bool TryAdvanceCombo()
        {
            if (!_attackId.CanAdvanceCombo()) return false;

            float duration = _combatConfig.GetAttackDuration(_attackId);
            if (_timer < duration * _combatConfig.attackComboCancelStartRatio) return false;

            BeginAttack(_attackId.NextCombo());
            return true;
        }
        
        
        // ============ 状态生命周�?============

        public void Enter()
        {
            BeginAttack(_attackId);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if(_timer < _combatConfig.GetAttackDuration(_attackId)) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
            _attackId = AttackMoveId.Combo1;
        }

        private void BeginAttack(AttackMoveId attackId)
        {
            _attackId = attackId.ClampOrDefault();
            _timer = 0f;
            _motor?.Stop();
        }
    }
}
