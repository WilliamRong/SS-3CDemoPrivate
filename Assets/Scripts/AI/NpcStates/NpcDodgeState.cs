using Character.Config;
using AI;
using Character.Intent;
using Character.Presentation;
using Character.StateMachine;
using UnityEngine;

namespace AI.NpcStates
{
    /// <summary>
    /// 在进入状态前冻结闪避表现上下文，使服务器计时和远端动画读取同一组方向与时长�?    /// </summary>
    public sealed class NpcDodgeState : ICharacterState
    {

        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combatConfig;

        private float _timer;
        private float _duration;
        private DodgePresentationContext _presentationContext;
        
        public DodgePresentationContext PresentationContext => _presentationContext;
        
        public CharacterStateId Id { get; } = CharacterStateId.Dodge;
        
        public NpcDodgeState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combatConfig = combat;
        }

        public void Prepare(DodgeMode mode, Vector3 worldDir, Vector2 blendLocal = default)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude > 0.0001f) worldDir.Normalize();
            else worldDir = _motor != null ? _motor.transform.forward : Vector3.forward;

            float duration = _combatConfig.GetDodgeDuration(mode);
            float moveDuration = _combatConfig.GetDodgeMoveDuration(mode);
            _presentationContext = new DodgePresentationContext(mode, blendLocal, duration, moveDuration, worldDir);
            _duration = duration;
        }

        // ============ 状态生命周�?============

        public void Enter()
        {
            _timer = 0f;
            _motor?.Stop();
            if (!_presentationContext.IsValid)
                _duration = _combatConfig.dodgeEvadeDuration;
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer < _duration) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
            _presentationContext = default;
        }
    }
}
