using System;
using Character.Core;
using Character.Intent;
using Character.Motor;
using Character.Combat;
using Character.Execution;
using Character.Presentation;
using Character.LockOn;
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
        private ParryState _parryState;
        private ParriedState _parriedState;
        private PostureBrokenState _postureBrokenState;
        private ExecutingState _executingState;
        private ExecutedState _executedState;
        private HitState _hitState;
        private DeadState _deadState;

        private CombatActor _combatActor;

        public DeathPresentationVariant CurrentDeathPresentationVariant
        {
            get;
            private set;
        } = DeathPresentationVariant.Default;

        public Vector2 LastMoveInput { get; private set; }

        public CharacterStateId CurrentStateId =>
            _fsm?.CurrentState?.Id ?? CharacterStateId.None;

        public int StateEnterVersion => _fsm?.StateEnterVersion ?? 0;

        public float CurrentHp => _context?.CurrentHp ?? 0f;
        public float MaxHp => _context?.MaxHp ?? 1f;

        public bool IsInvincible => _combatActor != null && _combatActor.IsInvincible;

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
        private bool _gmGuardOverrideActive;
        private bool _forcedGuardPending;
        private float _forcedGuardPendingDuration;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            _lateUpdatePipeline = GetComponent<CharacterLateUpdatePipeline>();
            _combatActor = GetComponent<CombatActor>();
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
            _dodgeState = new DodgeState(_fsm, _motor, _context, _stateRegistry, def.combat, _combatActor);
            _guardState = new GuardState(_fsm, _motor, _stateRegistry, def.combat, def.presentation, _lockOnQuery);
            _parryState = new ParryState(_fsm, _stateRegistry, _motor, def.combat);
            _parriedState = new ParriedState(_fsm, _stateRegistry, _motor, def.combat);
            _postureBrokenState = new PostureBrokenState(_fsm, _stateRegistry, _motor, def.combat);
            _executingState = new ExecutingState(_motor, def.combat);
            _executedState = new ExecutedState(_motor, def.combat);
            _hitState = new HitState(_fsm, _motor, _stateRegistry, def.combat);
            _deadState = new DeadState(_motor);

            _stateRegistry.Register(_idleState);
            _stateRegistry.Register(_moveState);
            _stateRegistry.Register(_sprintState);
            _stateRegistry.Register(_attackState);
            _stateRegistry.Register(_dodgeState);
            _stateRegistry.Register(_guardState);
            _stateRegistry.Register(_parryState);
            _stateRegistry.Register(_parriedState);
            _stateRegistry.Register(_postureBrokenState);
            _stateRegistry.Register(_executingState);
            _stateRegistry.Register(_executedState);
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
            bool shouldTickServerForcedReaction =
                NetworkServer.active &&
                CurrentStateId is
                    CharacterStateId.Parried or
                    CharacterStateId.Parry or
                    CharacterStateId.Executing or
                    CharacterStateId.Executed;

            if (!canProcessLocalInput && _forcedGuardTimer <= 0f &&
                !shouldTickServerPostureBreak && !shouldTickServerForcedReaction) return;

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
                intent.IsParryPressed = _inputHandler.ParryTriggered;
            }

            if (_forcedGuardTimer > 0f)
            {
                intent.Move = Vector2.zero;
                intent.IsSprintHeld = false;
                intent.IsJumpPressed = false;
                intent.IsAttackPressed = false;
                intent.IsDodgePressed = false;
                intent.IsParryPressed = false;
                intent.IsGuardHeld = true;
            }

            if (_context.IsDead)
            {
                _forcedGuardPending = false;
                _forcedGuardTimer = 0f;
                intent.IsAttackPressed = false;
                intent.IsDodgePressed = false;
                intent.IsJumpPressed = false;
                intent.IsSprintHeld = false;
                intent.IsGuardHeld = false;
                intent.IsParryPressed = false;
            }


            if (CurrentStateId is
                    CharacterStateId.Executing or
                    CharacterStateId.Executed)
            {
                // Look 不在 CharacterIntent 中，因此镜头输入仍然保留。
                intent = default;
            }
            else if (intent.IsDodgePressed && CanPrepareDodgeFromCurrentState())
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
            else if (TryStartExecutionFromAttack(intent))
            {
                // 会话已经让状态切换到 Executing。
                // 清空本帧剩余角色输入，但 Look 仍由相机系统独立读取。
                intent = default;
            }

            LastMoveInput = intent.Move;
            _fsm.Tick(intent, Time.deltaTime);
            Velocity = _context.Velocity;

            TryApplyPendingForceGuard();

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


        public bool CanEnterExecuting()
        {
            return _context != null &&
                   !_context.IsDead &&
                   _combatActor != null &&
                   _executingState != null &&
                   CurrentStateId is
                       CharacterStateId.Idle or
                       CharacterStateId.Move &&
                   _fsm.CanTransition(
                       CharacterStateId.Executing,
                       _stateRegistry,
                       TransitionReason.ExecutionAccepted);
        }

        public bool CanEnterExecuted()
        {
            return _context != null &&
                   !_context.IsDead &&
                   _combatActor != null &&
                   _executedState != null &&
                   CurrentStateId is
                       CharacterStateId.Parried or
                       CharacterStateId.PostureBroken &&
                   _fsm.CanTransition(
                       CharacterStateId.Executed,
                       _stateRegistry,
                       TransitionReason.ExecutionAccepted);
        }

        public bool TryEnterExecuting(in ExecutionSession session)
        {
            if (!CanEnterExecuting() ||
                session.ExecutorActorId != _combatActor.ActorId ||
                !_executingState.TryPrepare(
                    session,
                    _combatActor.ActorId))
            {
                return false;
            }

            _forcedGuardTimer = 0f;
            _forcedGuardPending = false;

            if (_fsm.TryTransition(
                    CharacterStateId.Executing,
                    _stateRegistry,
                    TransitionReason.ExecutionAccepted))
            {
                return true;
            }

            _executingState.CancelPreparation(session.ExecutionId);
            return false;
        }

        public bool TryEnterExecuted(in ExecutionSession session)
        {
            if (!CanEnterExecuted() ||
                session.TargetActorId != _combatActor.ActorId ||
                !_executedState.TryPrepare(
                    session,
                    _combatActor.ActorId))
            {
                return false;
            }

            _forcedGuardTimer = 0f;
            _forcedGuardPending = false;
            DeathPresentationVariant previousVariant =
                CurrentDeathPresentationVariant;
            CurrentDeathPresentationVariant = session.TargetWillDie
                ? DeathPresentationVariant.Executed
                : DeathPresentationVariant.Default;

            if (_fsm.TryTransition(
                    CharacterStateId.Executed,
                    _stateRegistry,
                    TransitionReason.ExecutionAccepted))
            {
                return true;
            }

            CurrentDeathPresentationVariant = previousVariant;
            _executedState.CancelPreparation(session.ExecutionId);
            return false;
        }

        /// <summary>
        /// Enters the authoritative executed state when the local owner has
        /// not observed the target's preceding Parried/PostureBroken edge yet.
        /// Server execution messages are authoritative, so an Idle/Move local
        /// state may be staged through PostureBroken before entering Executed.
        /// </summary>
        public bool TryEnterExecutedFromAuthoritative(
            in ExecutionSession session)
        {
            if (CanEnterExecuted())
                return TryEnterExecuted(session);

            CharacterStateId previousState = CurrentStateId;
            if (previousState is not (
                    CharacterStateId.Idle or
                    CharacterStateId.Move))
            {
                return false;
            }

            if (!_fsm.TryTransition(
                    CharacterStateId.PostureBroken,
                    _stateRegistry,
                    TransitionReason.ExecutionAccepted))
            {
                return false;
            }

            if (TryEnterExecuted(session))
                return true;

            // Do not leave a synthetic PostureBroken state behind if the
            // authoritative execution could not be prepared.
            _fsm.TryTransition(
                previousState,
                _stateRegistry,
                TransitionReason.ExecutionCancelled);
            return false;
        }

        public bool TryRollbackExecutionStart(
            ulong executionId,
            CharacterStateId previousState)
        {
            if (_fsm == null || _stateRegistry == null)
                return false;

            if (CurrentStateId == CharacterStateId.Executing)
            {
                if (!_executingState.IsBoundTo(executionId) ||
                    previousState is not (
                        CharacterStateId.Idle or
                        CharacterStateId.Move))
                {
                    return false;
                }

                return _fsm.TryTransition(
                    previousState,
                    _stateRegistry,
                    TransitionReason.ExecutionCancelled);
            }

            if (CurrentStateId == CharacterStateId.Executed)
            {
                if (!_executedState.IsBoundTo(executionId) ||
                    previousState is not (
                        CharacterStateId.Parried or
                        CharacterStateId.PostureBroken or
                        CharacterStateId.Idle or
                        CharacterStateId.Move))
                {
                    return false;
                }

                // A client may have been staged through PostureBroken because
                // its local state lagged behind the authoritative start. The
                // only safe locomotion rollback target from Executed is Idle.
                CharacterStateId rollbackState = previousState is
                    CharacterStateId.Idle or
                    CharacterStateId.Move
                    ? CharacterStateId.Idle
                    : previousState;

                bool restored = _fsm.TryTransition(
                    rollbackState,
                    _stateRegistry,
                    TransitionReason.ExecutionCancelled);
                if (restored)
                    CurrentDeathPresentationVariant = DeathPresentationVariant.Default;
                return restored;
            }

            return false;
        }


        public bool TryCompleteExecuting(ulong executionId)
        {
            if (_fsm == null ||
                _stateRegistry == null ||
                CurrentStateId != CharacterStateId.Executing ||
                !_executingState.IsBoundTo(executionId))
            {
                return false;
            }

            return _fsm.TryTransition(
                CharacterStateId.Idle,
                _stateRegistry,
                TransitionReason.ExecutionCompleted);
        }

        public bool TryCompleteExecuted(ulong executionId)
        {
            if (_context == null ||
                _fsm == null ||
                _stateRegistry == null ||
                CurrentStateId != CharacterStateId.Executed ||
                !_executedState.IsBoundTo(executionId))
            {
                return false;
            }

            bool targetWillDie = _executedState.Session.TargetWillDie;
            if (_context.IsDead != targetWillDie)
                return false;

            DeathPresentationVariant previousVariant =
                CurrentDeathPresentationVariant;

            CurrentDeathPresentationVariant = targetWillDie
                ? DeathPresentationVariant.Executed
                : DeathPresentationVariant.Default;

            if (_fsm.TryTransition(
                    targetWillDie
                        ? CharacterStateId.Dead
                        : CharacterStateId.Idle,
                    _stateRegistry,
                    TransitionReason.ExecutionCompleted))
            {
                return true;
            }

            CurrentDeathPresentationVariant = previousVariant;
            return false;
        }


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

        public bool TryEnterParried()
        {
            if (_context == null || _context.IsDead || _fsm == null || _stateRegistry == null)
                return false;
            if (CurrentStateId == CharacterStateId.Parried)
                return true;

            _forcedGuardTimer = 0f;
            return _fsm.TryTransition(CharacterStateId.Parried, _stateRegistry, TransitionReason.Parried);
        }

        public bool TryEnterDead()
        {
            return TryEnterDead(DeathPresentationVariant.Default);
        }

        public bool TryEnterDead(
            DeathPresentationVariant presentationVariant)
        {
            if (_context == null || !_context.IsDead || _fsm == null || _stateRegistry == null)
            {
                return false;
            }

            if (CurrentStateId == CharacterStateId.Dead)
                return true;

            _forcedGuardPending = false;
            _forcedGuardTimer = 0f;

            DeathPresentationVariant previousPresentationVariant =
                CurrentDeathPresentationVariant;

            CurrentDeathPresentationVariant = presentationVariant;

            if (_fsm.TryTransition(
                CharacterStateId.Dead,
                _stateRegistry,
                TransitionReason.Death))
            {
                return true;
            }

            CurrentDeathPresentationVariant = previousPresentationVariant;
            return false;
        }

        // ============ 生命结算 ============

        /// <summary>
        /// 将数值修改与状态反应拆开，CombatActor 才能先原子决定死亡、破势和最终表现优先级。
        /// </summary>
        public float ApplyHealthDamageOnly(float damage)
        {
            if (_context == null ||
                _context.IsDead ||
                IsInvincible)
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
              IsInvincible)
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
              IsInvincible)
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

        public bool ApplyAuthoritativeHealth(float currentHp, float maxHp)
        {
            // 使用绝对值覆盖累计误差，迟到或丢失的增量不会永久造成血量漂移。
            if (_context == null) return false;

            _context.SetHealth(currentHp, maxHp);
            HealthChanged?.Invoke(_context.CurrentHp, _context.MaxHp);
            return true;
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
            _forcedGuardPending = false;
            _forcedGuardTimer = 0f;
            _context.Revive(hp);
            HealthChanged?.Invoke(_context.CurrentHp, _context.MaxHp);
            if (_fsm.TryTransition(CharacterStateId.Idle, _stateRegistry, TransitionReason.Revive))
            {
                CurrentDeathPresentationVariant = DeathPresentationVariant.Default;
            }
        }

        public bool ForceEnterGuard(float holdDuration)
        {
            if (_context == null || _context.IsDead || _fsm == null || _stateRegistry == null)
                return false;

            float duration = SanitizeGuardDuration(holdDuration);
            CharacterStateId current = CurrentStateId;

            if (current == CharacterStateId.Guard)
            {
                _forcedGuardPending = false;
                _forcedGuardTimer = Mathf.Max(_forcedGuardTimer, duration);
                return true;
            }

            if (current is not (
                    CharacterStateId.Idle or
                    CharacterStateId.Move or
                    CharacterStateId.Sprint))
            {
                // Execution, hit, parry, and posture states must finish under
                // their authoritative session. Apply the GM intent when the
                // state machine returns to locomotion instead of interrupting it.
                _forcedGuardPending = true;
                _forcedGuardPendingDuration = duration;
                _forcedGuardTimer = 0f;
                return false;
            }

            _forcedGuardPending = false;
            _forcedGuardPendingDuration = duration;
            _forcedGuardTimer = duration;
            return _fsm.TryTransition(CharacterStateId.Guard, _stateRegistry, TransitionReason.InputGuard)
                || CurrentStateId == CharacterStateId.Guard;
        }

        /// <summary>
        /// Keeps the GM guard intent alive across authoritative reactions and
        /// execution sessions until an explicit release command arrives.
        /// </summary>
        public bool SetGmGuardOverride(
            bool active,
            float holdDuration = 30f)
        {
            _gmGuardOverrideActive = active;

            if (!active)
                return CurrentStateId != CharacterStateId.Guard ||
                       ForceExitGuardToIdle();

            float duration = SanitizeGuardDuration(holdDuration);
            _forcedGuardPendingDuration = duration;

            // Returning false here only means that the current authoritative
            // state cannot be interrupted. The override remains pending.
            ForceEnterGuard(duration);
            return true;
        }

        public bool ForceExitGuardToIdle()
        {
            if (_context == null || _context.IsDead || _fsm == null || _stateRegistry == null)
                return false;

            _forcedGuardPending = false;
            _forcedGuardPendingDuration = 0f;
            _forcedGuardTimer = 0f;
            if (CurrentStateId != CharacterStateId.Guard)
                return false;

            return _fsm.TryTransition(CharacterStateId.Idle, _stateRegistry, TransitionReason.Timeout);
        }

        private void TryApplyPendingForceGuard()
        {
            if ((!_gmGuardOverrideActive && !_forcedGuardPending) ||
                _context == null ||
                _context.IsDead ||
                CurrentStateId == CharacterStateId.Guard ||
                CurrentStateId is not (
                    CharacterStateId.Idle or
                    CharacterStateId.Move or
                    CharacterStateId.Sprint))
            {
                return;
            }

            float duration = _forcedGuardPendingDuration;
            _forcedGuardPending = false;
            ForceEnterGuard(duration);
        }

        private static float SanitizeGuardDuration(float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration))
                return 30f;

            return Mathf.Clamp(duration, 0.1f, 300f);
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
                or CharacterStateId.Parry
                or CharacterStateId.Parried
                or CharacterStateId.Executing
                or CharacterStateId.Executed
                or CharacterStateId.Dead;
        }

        private bool TryStartExecutionFromAttack(CharacterIntent intent)
        {
            if (!intent.IsAttackPressed ||
                intent.IsParryPressed ||
                intent.IsDodgePressed ||
                intent.IsGuardHeld ||
                CurrentStateId is not (
                    CharacterStateId.Idle or
                    CharacterStateId.Move) ||
                _combatActor == null)
            {
                return false;
            }

            ExecutionRuntime runtime = ExecutionRuntime.Instance;

            if (runtime == null ||
                GameDataManager.Instance == null ||
                GameDataManager.Instance.Player == null ||
                GameDataManager.Instance.Player.combat == null)
            {
                return false;
            }

            var combatConfig =
                GameDataManager.Instance.Player.combat;


            var lockOn = _lockOnQuery as PlayerLockOnController;

            if (!runtime.CandidateResolver.TrySelect(_combatActor, lockOn, combatConfig, out CombatActor target, out _, runtime.OccupancyQuery))
            {
                return false;
            }

            //Client和Host都走同一个ServerHandler
            //避免特殊路径
            if (NetworkClient.active)
            {
                return MirrorSyncTransport.TrySendExecutionRequest(target.ActorId, out _);
            }

            // 没有启动 Mirror 时走原来的 Offline 权威路径。
            if (!runtime.HasAuthority) return false;

            return runtime.TryStart(_combatActor, target, combatConfig, out _, out _, out _, out _);
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

        public bool TryGetActiveParryState(out ParryState parryState)
        {
            if (_fsm?.CurrentState is ParryState active)
            {
                parryState = active;
                return true;
            }

            parryState = null;
            return false;
        }

        public bool TryGetActiveParriedState(out ParriedState parriedState)
        {
            if (_fsm?.CurrentState is ParriedState active)
            {
                parriedState = active;
                return true;
            }

            parriedState = null;
            return false;
        }

        public bool IsParryActive =>
            TryGetActiveParryState(out var parry) &&
            parry.CurrentPhase == ParryPhase.Active;

        public ParryPhase CurrentParryPhase =>
            TryGetActiveParryState(out var parry) ? parry.CurrentPhase : ParryPhase.None;

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


        public bool TryGetActiveExecutingState(out ExecutingState executingState)
        {
            if (_fsm?.CurrentState is ExecutingState active)
            {
                executingState = active;
                return true;
            }

            executingState = null;
            return false;
        }

        public bool TryGetActiveExecutedState(out ExecutedState executedState)
        {
            if (_fsm?.CurrentState is ExecutedState active)
            {
                executedState = active;
                return true;
            }

            executedState = null;
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

            bool canConsumeExecutionRootMotion = CurrentStateId == CharacterStateId.Executing && NetworkServer.active;


            if (!CanProcessLocalInput() && !canConsumeExecutionRootMotion) return;
            if (_motor == null) return;

            if (CurrentStateId is not (
                    CharacterStateId.Attack
                    or CharacterStateId.Hit
                    or CharacterStateId.Dead
                    or CharacterStateId.Executing))
                return;

            if (CurrentStateId == CharacterStateId.Executing)
            {
                if (_executingState == null || !_executingState.TryWarpRootMotion(deltaPosition, deltaRotation, out Vector3 warpedPosition, out Quaternion warpedRotation))
                {
                    return;
                }

                deltaPosition = warpedPosition;
                deltaRotation = warpedRotation;
            }

            _motor.SetAttackRootMotionDelta(deltaPosition, deltaRotation);
        }
    }
}
