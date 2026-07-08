using Character.Core;
using Character.Intent;
using Character.Motor;
using Character.Presentation;
using Character.StateMachine;
using Character.StateMachine.States;
using Character.Sync;
using Core;
using Input;
using UnityEngine;

namespace Character.Controller
{
    public class PlayerController : MonoBehaviour, IAnimatorRootMotionReceiver
    {
        private InputHandler _inputHandler;
        private PlayerAuthorityGate _authorityGate;
        private CharacterController _characterController;
        private Animator _animator;
        private Camera _camera;

        private CharacterStateMachine _fsm;
        private CharacterStateRegistry _stateRegistry;
        private IdleState _idleState;
        private MoveState _moveState;
        private SprintState _sprintState;
        private AttackState _attackState;
        private DodgeState _dodgeState;
        private GuardState _guardState;
        private HitState _hitState;
        private DeadState _deadState;

        public Vector2 LastMoveInput { get; private set; }

        public CharacterStateId CurrentStateId =>
            _fsm?.CurrentState?.Id ?? CharacterStateId.None;

        /// <summary>Set on dodge <see cref="DodgeState.Prepare"/>; used for network sync before FSM enters Dodge.</summary>
        public byte LastPreparedDodgeMode { get; private set; }

        public bool TryGetActiveSprintState(out SprintState sprintState)
        {
            if (_fsm?.CurrentState is SprintState active)
            {
                sprintState = active;
                return true;
            }

            sprintState = null;
            return false;
        }

        public Vector3 Velocity;

        private CharacterContext _context;
        private CharacterMotor _motor;
        private CharacterLateUpdatePipeline _lateUpdatePipeline;
        private ILockOnLocomotionQuery _lockOnQuery;
        private float _forcedGuardTimer;

        private void Awake()
        {
            _lateUpdatePipeline = GetComponent<CharacterLateUpdatePipeline>();
        }

        void Start()
        {
            _inputHandler = GetComponent<InputHandler>();
            _authorityGate = GetComponent<PlayerAuthorityGate>();
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _camera = Camera.main;

            if (_animator != null)
            {
                _animator.applyRootMotion = true;
                var relay = _animator.GetComponent<AnimatorRootMotionRelay>();
                if (relay == null)
                    relay = _animator.gameObject.AddComponent<AnimatorRootMotionRelay>();
                relay.Initialize(this);
            }

            var def = GameDataManager.Instance.Player;

            _lockOnQuery = GetComponent<ILockOnLocomotionQuery>();

            _context = new CharacterContext(_characterController, transform, _camera);
            _context.ConfigureHealth(def.combat.maxHp);

            _motor = new CharacterMotor(_context, def.locomotion);
            _motor.SetLockOnQuery(_lockOnQuery);

            _fsm = new CharacterStateMachine(def.combat);
            _stateRegistry = new CharacterStateRegistry();
            _idleState = new IdleState(_fsm, _motor, _stateRegistry);
            _moveState = new MoveState(_fsm, _motor, _stateRegistry);
            _sprintState = new SprintState(_fsm, _motor, _context, _stateRegistry, def.sprint);

            _attackState = new AttackState(_fsm, _motor, _stateRegistry, def.combat);
            _dodgeState = new DodgeState(_fsm, _motor, _context, _stateRegistry, def.combat);
            _guardState = new GuardState(_fsm, _motor, _stateRegistry, def.combat);
            _hitState = new HitState(_fsm, _motor, _stateRegistry, def.combat);
            _deadState = new DeadState(_motor);

            _stateRegistry.Register(_idleState);
            _stateRegistry.Register(_moveState);
            _stateRegistry.Register(_sprintState);
            _stateRegistry.Register(_attackState);
            _stateRegistry.Register(_dodgeState);
            _stateRegistry.Register(_guardState);
            _stateRegistry.Register(_hitState);
            _stateRegistry.Register(_deadState);

            _fsm.Initialize(_idleState);
        }

        public bool TryGetDodgePresentationContext(out DodgePresentationContext ctx)
        {
            ctx = default;
            if (_fsm?.CurrentState is not DodgeState dodge)
                return false;

            ctx = dodge.PresentationContext;
            return ctx.IsValid;
        }

        public bool TryGetActiveAttackState(out AttackState attackState)
        {
            if (_fsm?.CurrentState is AttackState active)
            {
                attackState = active;
                return true;
            }

            attackState = null;
            return false;
        }

        void Update()
        {
            bool canProcessLocalInput = CanProcessLocalInput();
            if (!canProcessLocalInput && _forcedGuardTimer <= 0f) return;

            TickForcedGuardTimer();

            var intent = new CharacterIntent();
            if (canProcessLocalInput && _inputHandler != null)
            {
                intent.Move = _inputHandler.MoveInput;
                intent.IsSprintHeld = _inputHandler.IsSprinting;
                intent.IsJumpPressed = _inputHandler.JumpTriggered;
                intent.IsAttackPressed = _inputHandler.AttackTriggered;
                intent.IsDodgePressed = _inputHandler.DodgeTriggered;
                intent.IsGuardHeld = _inputHandler.IsGuardHeld;
            }

            if (_forcedGuardTimer > 0f)
            {
                intent.Move = Vector2.zero;
                intent.IsSprintHeld = false;
                intent.IsJumpPressed = false;
                intent.IsAttackPressed = false;
                intent.IsDodgePressed = false;
                intent.IsGuardHeld = true;
            }

            if (_context.IsDead)
            {
                intent.IsAttackPressed = false;
                intent.IsDodgePressed = false;
                intent.IsJumpPressed = false;
                intent.IsSprintHeld = false;
                intent.IsGuardHeld = false;
            }

            if (intent.IsDodgePressed && CanPrepareDodgeFromCurrentState())
            {
                intent.IsJumpPressed = false;
                bool lockOn = _lockOnQuery != null && _lockOnQuery.IsLockOnActive;
                _dodgeState.Prepare(intent, CurrentStateId, lockOn);
                LastPreparedDodgeMode = (byte)_dodgeState.PresentationContext.Mode;
            }
            else if (IsInLockedCombatState())
            {
                intent.IsDodgePressed = false;
                if (CurrentStateId != CharacterStateId.Dodge)
                    intent.IsAttackPressed = false;
                intent.IsJumpPressed = false;
            }

            LastMoveInput = intent.Move;
            _fsm.Tick(intent, Time.deltaTime);
            Velocity = _context.Velocity;

            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                ApplyHit(10, false);
            }
        }

        private void LateUpdate()
        {
            if (!CanProcessLocalInput() && _forcedGuardTimer <= 0f)
                return;

            if (_lateUpdatePipeline == null)
                _lateUpdatePipeline = GetComponent<CharacterLateUpdatePipeline>();

            _lateUpdatePipeline?.TickLateUpdate();
        }

        public void ApplyHit(float damage, bool isHeavyHit)
        {
            if (_context.IsDead || _context.IsInvincible) return;

            var combat = GameDataManager.Instance.Player.combat;

            _context.ApplyDamage(damage);
            if (_context.IsDead)
            {
                _fsm.TryTransition(CharacterStateId.Dead, _stateRegistry, TransitionReason.Death);
                return;
            }

            _hitState.ConfigureDuration(isHeavyHit ? combat.heavyHitDuration : combat.lightHitDuration);
            _fsm.TryTransition(CharacterStateId.Hit, _stateRegistry, isHeavyHit ? TransitionReason.HitHeavy : TransitionReason.HitLight);
        }


        public void ApplyGuardDamage(float damage)
        {
            if (_context.IsDead || _context.IsInvincible) return;

            _context.ApplyDamage(damage);

            if (_context.IsDead)
            {
                _fsm.TryTransition(CharacterStateId.Dead, _stateRegistry, TransitionReason.Death);
            }
        }

        public void Revive(float hp)
        {
            _context.Revive(hp);
            _fsm.TryTransition(CharacterStateId.Idle, _stateRegistry, TransitionReason.Revive);
        }

        public bool ForceEnterGuard(float holdDuration)
        {
            if (_context == null || _context.IsDead || _fsm == null || _stateRegistry == null)
                return false;

            _forcedGuardTimer = Mathf.Max(_forcedGuardTimer, Mathf.Max(0.1f, holdDuration));
            return _fsm.TryTransition(CharacterStateId.Guard, _stateRegistry, TransitionReason.InputGuard)
                || CurrentStateId == CharacterStateId.Guard;
        }

        private bool CanProcessLocalInput()
        {
            if (_authorityGate == null) return true;
            return _authorityGate.CanProcessLocalInput;
        }

        private void TickForcedGuardTimer()
        {
            if (_forcedGuardTimer <= 0f)
                return;

            _forcedGuardTimer = Mathf.Max(0f, _forcedGuardTimer - Time.deltaTime);
        }

        private bool CanPrepareDodgeFromCurrentState()
        {
            return CurrentStateId is not (
                CharacterStateId.Dodge
                or CharacterStateId.Hit
                or CharacterStateId.Dead);
        }

        private bool IsInLockedCombatState()
        {
            return CurrentStateId is CharacterStateId.Dodge
                or CharacterStateId.Hit
                or CharacterStateId.Dead;
        }

        public bool TryGetActiveGuardState(out GuardState guardState)
        {
            if (_fsm?.CurrentState is GuardState active)
            {
                guardState = active;
                return true;
            }

            guardState = null;
            return false;
        }

        public void HandleAnimatorRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (!CanProcessLocalInput()) return;
            if (_motor == null) return;

            if (CurrentStateId is not (
                    CharacterStateId.Attack
                    or CharacterStateId.Hit
                    or CharacterStateId.Dead))
                return;

            _motor.SetAttackRootMotionDelta(deltaPosition, deltaRotation);
        }
    }
}
