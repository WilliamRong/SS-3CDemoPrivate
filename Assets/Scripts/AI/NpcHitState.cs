using Character.Config;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    public sealed class NpcHitState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combatConfig;
        private float _timer;
        private float _duration;
        private bool _isHeavyHit;
        
        public bool IsHeavyHit => _isHeavyHit;
        
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

        public void Prepare(bool isHeavy)
        {
            _isHeavyHit = isHeavy;
            _duration = isHeavy ? _combatConfig.heavyHitDuration : _combatConfig.lightHitDuration;
        }
        
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
            _isHeavyHit = false;
        }
    }
}
