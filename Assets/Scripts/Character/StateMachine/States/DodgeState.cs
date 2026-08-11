using Character.Config;
using Character.Combat;
using Character.Core;
using Character.Intent;
using Character.Motor;
using Character.Presentation;
using Core;

namespace Character.StateMachine.States
{
    /// <summary>
    /// 在进入前冻结闪避模式、方向和时长，使无敌窗、位移与远端表现使用同一上下文。
    /// </summary>
    public sealed class DodgeState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterContext _context;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterCombatConfig _combat;
        private readonly CombatActor _combatActor;

        private float _timer;
        private float _duration;
        private ulong _dodgeVersion;
        private bool _ownsInvulnerability;
        private DodgePresentationContext _presentationContext;
        
        public CharacterStateId Id { get; } = CharacterStateId.Dodge;
        public DodgePresentationContext PresentationContext => _presentationContext;

        public DodgeState(
            CharacterStateMachine fsm,
            CharacterMotor motor,
            CharacterContext context,
            CharacterStateRegistry registry,
            CharacterCombatConfig combat,
            CombatActor combatActor)
        {
            _fsm = fsm;
            _motor = motor;
            _context = context;
            _registry = registry;
            _combat = combat;
            _combatActor = combatActor;
        }

        public void Prepare(CharacterIntent intent, CharacterStateId fromStateId, bool isLockOn)
        {
            var presentation = GameDataManager.Instance.Player.presentation;
            _presentationContext =
                DodgeModeResolver.Resolve(intent, fromStateId, isLockOn, _context, presentation, _combat);
            _duration = _presentationContext.Duration;
        }
        

        // ============ 状态生命周期 ============

        public void Enter()
        {
            _timer = 0f;
            _motor.SetSprintActive(false);
            SetInvulnerability(false);
            unchecked
            {
                _dodgeVersion++;
                if (_dodgeVersion == 0)
                    _dodgeVersion = 1;
            }

            if (!_presentationContext.IsValid)
                _duration = _combat.dodgeEvadeDuration;

            _motor.BeginDodge(_presentationContext, _combat);
        }

        /// <summary>
        /// 闪避期间只在配置窗口开放攻击取消，并让预先冻结的移动上下文持续驱动 Motor。
        /// </summary>
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            intent.IsDodgePressed = false;
            intent.IsJumpPressed = false;

            _timer += deltaTime;

            if (intent.IsAttackPressed && CanTransitionToDodgeAttack())
            {
                var attackState = _registry.Get(CharacterStateId.Attack) as AttackState;
                attackState?.PrepareDodgeAttack();

                bool transitioned = _fsm.TryTransition(CharacterStateId.Attack, _registry, TransitionReason.InputAttack);
                if (!transitioned)
                    attackState?.PrepareComboAttack();

                return;
            }

            intent.IsAttackPressed = false;
            bool insideInvulnerabilityWindow =
                _timer >= _combat.dodgeInvincibleStart &&
                _timer <= _combat.dodgeInvincibleEnd;
            SetInvulnerability(insideInvulnerabilityWindow);

            _motor.Tick(intent, deltaTime);

            if (_timer < _duration)
                return;
            
            bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
            if (intent.IsSprintHeld && hasMove)
            {
                _fsm.TryTransition(CharacterStateId.Sprint, _registry, TransitionReason.Timeout);
                return;
            }
            _fsm.TryTransition(hasMove ? CharacterStateId.Move : CharacterStateId.Idle, _registry,
                TransitionReason.Timeout);
        }

        public void Exit()
        {
            SetInvulnerability(false);
            _motor.EndDodge();
            _presentationContext = default;
        }

        // ============ 取消窗口 ============

        private bool CanTransitionToDodgeAttack()
        {
            float duration = _duration > 0.0001f ? _duration : _combat.dodgeEvadeDuration;
            return _timer >= duration * _combat.dodgeAttackCancelStartRatio;
        }

        private void SetInvulnerability(bool enabled)
        {
            if (_combatActor == null || enabled == _ownsInvulnerability)
                return;

            if (enabled)
            {
                _combatActor.AcquireInvulnerability(
                    InvulnerabilitySource.Dodge,
                    _dodgeVersion);
                _ownsInvulnerability = true;
                return;
            }

            _combatActor.ReleaseInvulnerability(
                InvulnerabilitySource.Dodge,
                _dodgeVersion);
            _ownsInvulnerability = false;
        }
    }
}
