using Character.Core;
using Character.Intent;
using Character.Motor;
using Character.StateMachine;
using Character.StateMachine.States;
using Character.Sync;
using Core;
using Input;
using UnityEngine;

namespace Character.Controller
{
    public class PlayerController : MonoBehaviour
    {
        private InputHandler _inputHandler;
        private PlayerAuthorityGate _authorityGate;
        private CharacterController _characterController;
        private Camera _camera;

        private CharacterStateMachine _fsm;
        private CharacterStateRegistry _stateRegistry;
        private IdleState _idleState;
        private MoveState _moveState;
        private SprintState _sprintState;
        private AttackState _attackState;
        private DodgeState _dodgeState;
        private HitState _hitState;
        private DeadState _deadState;

        public CharacterStateId CurrentStateId =>
            _fsm?.CurrentState?.Id ?? CharacterStateId.None;

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

        private void Awake()
        {
            _lateUpdatePipeline = GetComponent<CharacterLateUpdatePipeline>();
        }

        void Start()
        {
            _inputHandler = GetComponent<InputHandler>();
            _authorityGate = GetComponent<PlayerAuthorityGate>();
            _characterController = GetComponent<CharacterController>();
            _camera = Camera.main;

            var def = GameDataManager.Instance.Player;

            _context = new CharacterContext(_characterController, transform, _camera);
            _context.ConfigureHealth(def.combat.maxHp);

            _motor = new CharacterMotor(_context, def.locomotion);

            _fsm = new CharacterStateMachine(def.combat);
            _stateRegistry = new CharacterStateRegistry();
            _idleState = new IdleState(_fsm, _motor, _stateRegistry);
            _moveState = new MoveState(_fsm, _motor, _stateRegistry);
            _sprintState = new SprintState(_fsm, _motor, _context, _stateRegistry, def.sprint);

            _attackState = new AttackState(_fsm, _motor, _stateRegistry, def.combat);
            _dodgeState = new DodgeState(_fsm, _motor, _context, _stateRegistry, def.combat);
            _hitState = new HitState(_fsm, _motor, _stateRegistry, def.combat);
            _deadState = new DeadState(_motor);

            _stateRegistry.Register(_idleState);
            _stateRegistry.Register(_moveState);
            _stateRegistry.Register(_sprintState);
            _stateRegistry.Register(_attackState);
            _stateRegistry.Register(_dodgeState);
            _stateRegistry.Register(_hitState);
            _stateRegistry.Register(_deadState);

            _fsm.Initialize(_idleState);
        }

        void Update()
        {
            if (!CanProcessLocalInput()) return;

            var intent = new CharacterIntent
            {
                Move = _inputHandler.MoveInput,
                IsSprintHeld = _inputHandler.IsSprinting,
                IsJumpPressed = _inputHandler.JumpTriggered,
                IsAttackPressed = _inputHandler.AttackTriggered,
                IsDodgePressed = _inputHandler.DodgeTriggered,
            };

            if (_context.IsDead)
            {
                intent.IsAttackPressed = false;
                intent.IsDodgePressed = false;
                intent.IsJumpPressed = false;
                intent.IsSprintHeld = false;
            }

            _fsm.Tick(intent, Time.deltaTime);
            Velocity = _context.Velocity;

            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                ApplyHit(10, false);
            }
        }

        private void LateUpdate()
        {
            if (!CanProcessLocalInput())
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

        public void Revive(float hp)
        {
            _context.Revive(hp);
            _fsm.TryTransition(CharacterStateId.Idle, _stateRegistry, TransitionReason.Revive);
        }

        private bool CanProcessLocalInput()
        {
            if (_authorityGate == null) return true;
            return _authorityGate.CanProcessLocalInput;
        }
    }
}
