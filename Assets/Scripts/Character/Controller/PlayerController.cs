using Character.Config;
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

        [Header("Move")]
        public float MoveSpeed = 5f;
        public float SprintSpeed = 10f;
        public float RotationSlerpSpeed = 30f;
        public float SmoothTime = 0.1f;

        [Header("Vertical")]
        public float gravity = -9.81f;
        public float JumpHeight = 1f;

        [Header("Combat")]
        public float MaxHp = 100f;
        public float LightHitDuration = 0.25f;
        public float HeavyHitDuration = 0.45f;

        [Header("Sprint")]
        [SerializeField] private SprintPhaseConfig _sprintPhaseConfig;
        
        [Header("State")]
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

        // Start is called before the first frame update
        void Start()
        {
            _inputHandler = GetComponent<InputHandler>();
            _authorityGate = GetComponent<PlayerAuthorityGate>();
            _characterController = GetComponent<CharacterController>();
            _camera = Camera.main;
            
            // // 仅本地权威玩家控制光标状态
            // if (CanProcessLocalInput())
            // {
            //     Cursor.visible = false;
            //     Cursor.lockState = CursorLockMode.Locked;
            // }


            _context = new CharacterContext(_characterController, transform, _camera);
            _context.ConfigureHealth(MaxHp);

            _motor = new CharacterMotor(_context){
                MoveSpeed = MoveSpeed,
                SprintSpeed = SprintSpeed,
                RotationSlerpSpeed = RotationSlerpSpeed,
                SmoothTime = SmoothTime,
                Gravity = gravity,
                JumpHeight = JumpHeight,
            };
            
            
            //状态机
            _fsm = new CharacterStateMachine();
            _stateRegistry = new CharacterStateRegistry();
            _idleState = new IdleState(_fsm, _motor, _stateRegistry);
            _moveState = new MoveState(_fsm, _motor, _stateRegistry);
            _sprintState = new SprintState(_fsm, _motor, _context, _stateRegistry, _sprintPhaseConfig);

            _attackState = new AttackState(_fsm, _motor, _stateRegistry);
            _dodgeState = new DodgeState(_fsm, _motor, _context, _stateRegistry);
            _hitState = new HitState(_fsm, _motor, _stateRegistry);
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

            // 水平移动（SmoothDamp 平滑过渡）
            var intent = new CharacterIntent{
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

            _context.ApplyDamage(damage);
            if (_context.IsDead)
            {
                _fsm.TryTransition(CharacterStateId.Dead, _stateRegistry, TransitionReason.Death);
                return;
            }

            _hitState.ConfigureDuration(isHeavyHit ? HeavyHitDuration : LightHitDuration);
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
