using System;
using Character.Core;
using Character.Intent;
using Character.Motor;
using Character.Presentation;
using Character.StateMachine;
using Character.StateMachine.States;
using Character.Sync;
using Core;
using Input;
using Mirror;
using UnityEngine;

namespace Character.Controller
{
    /// <summary>
    /// 作为本地 Player 的组合根，把输入、状态机、生命、Motor 和表现管线接到同一权威生命周期。
    /// </summary>
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
        private PostureBrokenState _postureBrokenState;
        private HitState _hitState;
        private DeadState _deadState;

        public Vector2 LastMoveInput { get; private set; }

        public CharacterStateId CurrentStateId =>
            _fsm?.CurrentState?.Id ?? CharacterStateId.None;

        public int StateEnterVersion => _fsm?.StateEnterVersion ?? 0;

        public float CurrentHp => _context?.CurrentHp ?? 0f;
        public float MaxHp => _context?.MaxHp ?? 1f;

        public bool IsInvincible => _context?.IsInvincible ?? false;

        public event Action<float, float> HealthChanged;

        /// <summary>
        /// 在状态切换前缓存，确保同帧发布的闪避动作已经带有最终表现模式。
        /// </summary>
        public byte LastPreparedDodgeMode { get; private set; }

        public Vector3 Velocity;

        private CharacterContext _context;
        private CharacterMotor _motor;
        private CharacterLateUpdatePipeline _lateUpdatePipeline;
        private ILockOnLocomotionQuery _lockOnQuery;
        private float _forcedGuardTimer;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            _lateUpdatePipeline = GetComponent<CharacterLateUpdatePipeline>();
        }

        /// <summary>
        /// 放在 Start 组装状态机，确保更早执行的 GameDataManager 已发布完整配置。
        /// </summary>
        private void Start()
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
            HealthChanged?.Invoke(_context.CurrentHp, _context.MaxHp);

            _motor = new CharacterMotor(_context, def.locomotion);
            _motor.SetLockOnQuery(_lockOnQuery);

            _fsm = new CharacterStateMachine(def.combat);
            _stateRegistry = new CharacterStateRegistry();
            _idleState = new IdleState(_fsm, _motor, _stateRegistry, def.presentation, _lockOnQuery);
            _moveState = new MoveState(_fsm, _motor, _stateRegistry);
            _sprintState = new SprintState(_fsm, _motor, _context, _stateRegistry, def.sprint);

            _attackState = new AttackState(_fsm, _motor, _stateRegistry, def.combat);
            _dodgeState = new DodgeState(_fsm, _motor, _context, _stateRegistry, def.combat);
            _guardState = new GuardState(_fsm, _motor, _stateRegistry, def.combat, def.presentation, _lockOnQuery);
            _postureBrokenState = new PostureBrokenState(_fsm, _stateRegistry, _motor, def.combat);
            _hitState = new HitState(_fsm, _motor, _stateRegistry, def.combat);
            _deadState = new DeadState(_motor);

            _stateRegistry.Register(_idleState);
            _stateRegistry.Register(_moveState);
            _stateRegistry.Register(_sprintState);
            _stateRegistry.Register(_attackState);
            _stateRegistry.Register(_dodgeState);
            _stateRegistry.Register(_guardState);
            _stateRegistry.Register(_postureBrokenState);
            _stateRegistry.Register(_hitState);
            _stateRegistry.Register(_deadState);

            _fsm.Initialize(_idleState);
        }

        /// <summary>
        /// 本地输入、服务器强制格挡与服务器崩防计时共享状态机 Tick，但只有拥有输入权威的实例读取 InputHandler。
        /// </summary>
        private void Update()
        {
            bool canProcessLocalInput = CanProcessLocalInput();

            // Host 上的远端 Player 没有本地输入，但 Server 权威强制状态必须继续计时。
            bool shouldTickServerPostureBreak =
                NetworkServer.active &&
                CurrentStateId == CharacterStateId.PostureBroken;

            if (!canProcessLocalInput && _forcedGuardTimer <= 0f && !shouldTickServerPostureBreak) return;

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

        // ============ 权威状态入口 ============

        public bool TryEnterPostureBroken()
        {
            if (_context == null || _context.IsDead ||
                _fsm == null || _stateRegistry == null ||
                CurrentStateId == CharacterStateId.PostureBroken)
                return false;

            _forcedGuardTimer = 0f;
            return _fsm.TryTransition(CharacterStateId.PostureBroken, _stateRegistry, TransitionReason.PostureBreak);
        }

        public bool TryEnterHitReaction(bool isHeavyHit, byte hitVariant = 1)
        {
            if (_context == null || _context.IsDead || _fsm == null || _stateRegistry == null || _hitState == null) return false;

            var combat = GameDataManager.Instance.Player.combat;
            float duration = isHeavyHit ? combat.heavyHitDuration : combat.lightHitDuration;

            _hitState.Configure(duration, isHeavyHit, hitVariant);

            return _fsm.TryTransition(CharacterStateId.Hit, _stateRegistry, isHeavyHit ? TransitionReason.HitHeavy : TransitionReason.HitLight);
        }

        public bool TryEnterDead()
        {
            if (_context == null || !_context.IsDead || _fsm == null || _stateRegistry == null)
            {
                return false;
            }

            if (CurrentStateId == CharacterStateId.Dead)
                return true;

            return _fsm.TryTransition(
                CharacterStateId.Dead,
                _stateRegistry,
                TransitionReason.Death);
        }

        // ============ 生命结算 ============

        /// <summary>
        /// 将数值修改与状态反应拆开，CombatActor 才能先原子决定死亡、破势和最终表现优先级。
        /// </summary>
        public float ApplyHealthDamageOnly(float damage)
        {
            if (_context == null ||
                _context.IsDead ||
                _context.IsInvincible)
            {
                return 0f;
            }

            float previousHp = _context.CurrentHp;
            _context.ApplyDamage(damage);
            float appliedDamage = Mathf.Max(0f, previousHp - _context.CurrentHp);

            if (appliedDamage > 0f)
            {
                HealthChanged?.Invoke(_context.CurrentHp, _context.MaxHp);
            }

            return appliedDamage;
        }

        /// <summary>
        /// 保留旧调试入口的数值加反应组合；正式战斗通过 CombatActor 事务入口决定更完整的优先级。
        /// </summary>
        public void ApplyHit(float damage, bool isHeavyHit, byte hitVariant = 1)
        {
            if (_context == null ||
               _context.IsDead ||
               _context.IsInvincible)
            {
                return;
            }

            ApplyHealthDamageOnly(damage);

            if (_context.IsDead)
            {
                TryEnterDead();
                return;
            }

            TryEnterHitReaction(isHeavyHit, hitVariant);
        }

        public void ApplyGuardDamage(float damage)
        {
            if (_context == null ||
              _context.IsDead ||
              _context.IsInvincible)
            {
                return;
            }

            ApplyHealthDamageOnly(damage);

            if (_context.IsDead)
                TryEnterDead();
        }

        // ============ 远端纠正与表现 ============

        /// <summary>
        /// 远端状态由快照决定，这里只应用数值，避免客户端根据伤害自行抢跑状态机。
        /// </summary>
        public void ApplyHealthDelta(float damage)
        {
            ApplyHealthDamageOnly(damage);
        }

        public void ApplyAuthoritativeHealth(float currentHp, float maxHp)
        {
            // 使用绝对值覆盖累计误差，迟到或丢失的增量不会永久造成血量漂移。
            if (_context == null) return;

            _context.SetHealth(currentHp, maxHp);
            HealthChanged?.Invoke(_context.CurrentHp, _context.MaxHp);
        }

        /// <summary>
        /// 网络消息已经携带权威伤害结果，因此远端反应不能再次扣血。
        /// </summary>
        public void ApplyRemoteHitReaction(bool isHeavyHit, byte hitVariant, bool isDead)
        {
            if (_context == null || _fsm == null || _stateRegistry == null) return;

            if (isDead)
            {
                TryEnterDead();
                return;
            }

            if (_context.IsDead) return;

            TryEnterHitReaction(isHeavyHit, hitVariant);
        }

        // ============ 复活与 GM 控制 ============

        public void Revive(float hp)
        {
            _context.Revive(hp);
            HealthChanged?.Invoke(_context.CurrentHp, _context.MaxHp);
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

        public bool ForceExitGuardToIdle()
        {
            if (_context == null || _context.IsDead || _fsm == null || _stateRegistry == null)
                return false;

            _forcedGuardTimer = 0f;
            if (CurrentStateId != CharacterStateId.Guard)
                return false;

            return _fsm.TryTransition(CharacterStateId.Idle, _stateRegistry, TransitionReason.Timeout);
        }

        // ============ 输入与状态约束 ============

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
                or CharacterStateId.PostureBroken
                or CharacterStateId.Dead);
        }

        private bool IsInLockedCombatState()
        {
            return CurrentStateId is CharacterStateId.Dodge or CharacterStateId.PostureBroken
                or CharacterStateId.Hit
                or CharacterStateId.Dead;
        }

        // ============ 活跃状态查询 ============

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

        public bool TryGetActiveHitState(out HitState hitState)
        {
            if (_fsm?.CurrentState is HitState active)
            {
                hitState = active;
                return true;
            }

            hitState = null;
            return false;
        }

        public bool TryGetActivePostureBrokenState(out PostureBrokenState postureBrokenState)
        {
            if (_fsm?.CurrentState is PostureBrokenState active)
            {
                postureBrokenState = active;
                return true;
            }

            postureBrokenState = null;
            return false;
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

        public bool TryGetActiveIdleState(out IdleState idleState)
        {
            if (_fsm?.CurrentState is IdleState active)
            {
                idleState = active;
                return true;
            }

            idleState = null;
            return false;
        }

        // ============ 根位移接收 ============

        /// <summary>
        /// 只让明确白名单状态把 Animator 位移交给 Motor，普通移动和破势保持逻辑位置稳定。
        /// </summary>
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
