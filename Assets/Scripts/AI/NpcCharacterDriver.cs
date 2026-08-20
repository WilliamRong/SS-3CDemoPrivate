using Character.Combat;
using Character.Config;
using Character.Controller;
using Character.Intent;
using Character.Execution;
using Character.Presentation;
using Character.StateMachine;
using AI.NpcStates;
using Core;
using Mirror;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// 作为服务器 NPC 的状态机组合根，集中状态创建、权威转换和根位移消费，客户端只接收同步结果。
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(CombatActor))]
    public class NpcCharacterDriver : NetworkBehaviour, IAnimatorRootMotionReceiver
    {
        [SerializeField] private NpcAiIntentSource _intentSource;
        [SerializeField] private NpcMotor _motor;
        [SerializeField] private Animator _animator;
        [SerializeField] private BehaviorTree _behaviorTree;

        private CharacterStateMachine _fsm;
        private CharacterStateRegistry _registry;
        private CombatActor _combatActor;

        private NpcIdleState _idle;
        private NpcMoveState _move;
        private NpcSprintState _sprint;
        private NpcAttackState _attack;
        private NpcDodgeState _dodge;
        private NpcGuardState _guard;
        private NpcParryState _parry;
        private NpcParriedState _parried;
        private NpcExecutedState _executed;
        private NpcPostureBrokenState _postureBroken;
        private NpcHitState _hit;
        private NpcDeadState _dead;

        private bool _gmGuardOverrideActive;
        private bool _gmFacingOverrideActive;
        private float _gmGuardHoldDuration = 30f;
        private bool _gmPausedBehaviorTree;
        private bool _behaviorTreeEnabledBeforeGmPause;

        public DeathPresentationVariant CurrentDeathPresentationVariant
        {
            get;
            private set;
        } = DeathPresentationVariant.Default;

        private CharacterCombatConfig combatConfig => GameDataManager.Instance.Npc.combat;
        private CharacterPresentationConfig presentationConfig => GameDataManager.Instance.Npc.presentation;

        public CharacterStateId CurrentStateId => _fsm?.CurrentState?.Id ?? CharacterStateId.None;
        public int StateEnterVersion => _fsm?.StateEnterVersion ?? 0;

        public byte LastPreparedDodgeMode { get; private set; }

        // ============ Unity 与 Mirror 生命周期 ============

        private void Awake()
        {
            if (_intentSource == null) _intentSource = GetComponent<NpcAiIntentSource>();
            if (_motor == null) _motor = GetComponent<NpcMotor>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_behaviorTree == null) _behaviorTree = GetComponent<BehaviorTree>();
            if (_combatActor == null) _combatActor = GetComponent<CombatActor>();

            if (_animator != null)
            {
                _animator.applyRootMotion = true;
                var relay = _animator.GetComponent<AnimatorRootMotionRelay>();
                if (relay == null)
                    relay = _animator.gameObject.AddComponent<AnimatorRootMotionRelay>();
                relay.Initialize(this);
            }
        }

        /// <summary>
        /// 状态机只在服务器身份确认后构建，避免客户端镜像创建一套会与权威快照竞争的 NPC 状态。
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            _fsm = new CharacterStateMachine();
            _registry = new CharacterStateRegistry();
            _idle = new NpcIdleState(_fsm, _registry, _intentSource, _motor, presentationConfig);
            _move = new NpcMoveState(_fsm, _registry, _intentSource, _motor);
            _sprint = new NpcSprintState(_fsm, _registry, _motor, combatConfig);
            _attack = new NpcAttackState(_fsm, _registry, _motor, combatConfig);
            _dodge = new NpcDodgeState(_fsm, _registry, _motor, combatConfig);
            _guard = new NpcGuardState(_fsm, _registry, _motor, combatConfig, presentationConfig, _intentSource);
            _parry = new NpcParryState(_fsm, _registry, _motor, combatConfig);
            _parried = new NpcParriedState(_fsm, _registry, _motor, combatConfig);
            _executed = new NpcExecutedState(_motor, combatConfig);
            _postureBroken = new NpcPostureBrokenState(_fsm, _registry, _motor, combatConfig);
            _hit = new NpcHitState(_fsm, _registry, _motor, combatConfig);
            _dead = new NpcDeadState(_fsm, _registry, _motor);

            _registry.Register(_idle);
            _registry.Register(_move);
            _registry.Register(_sprint);
            _registry.Register(_attack);
            _registry.Register(_dodge);
            _registry.Register(_guard);
            _registry.Register(_parry);
            _registry.Register(_parried);
            _registry.Register(_postureBroken);
            _registry.Register(_executed);
            _registry.Register(_hit);
            _registry.Register(_dead);

            _fsm.Initialize(_idle);
        }

        public override void OnStopServer()
        {
            _gmGuardOverrideActive = false;
            _gmFacingOverrideActive = false;
            _intentSource?.ClearGmFacingTarget();
            RestoreBehaviorTreeAfterGmOverride();
            base.OnStopServer();
        }

        /// <summary>
        /// 放在 LateUpdate，确保行为树先写完本帧黑板意图，再由状态机消费稳定快照。
        /// </summary>
        private void LateUpdate()
        {
            if (!isServer || _fsm == null) return;

            RefreshExpiredGmFacingOverride();
            TryApplyPendingGmGuard();

            CharacterIntent intent = _intentSource != null ? _intentSource.BuildIntent() : default;

            _fsm.Tick(intent, Time.deltaTime);
        }


        public bool CanEnterExecuted()
        {
            return isServer &&
                   _fsm != null &&
                   _registry != null &&
                   _combatActor != null &&
                   _executed != null &&
                   CurrentStateId is
                       CharacterStateId.Parried or
                       CharacterStateId.PostureBroken &&
                   _fsm.CanTransition(
                       CharacterStateId.Executed,
                       _registry,
                       TransitionReason.ExecutionAccepted);
        }

        // ============ 活跃状态查询 ============

        public bool TryGetActiveIdleState(out NpcIdleState idleState)
        {
            if (_fsm?.CurrentState is NpcIdleState active)
            {
                idleState = active;
                return true;
            }

            idleState = null;
            return false;
        }

        public bool TryGetActiveSprintState(out NpcSprintState sprintState)
        {

            if (_fsm?.CurrentState is NpcSprintState active)
            {
                sprintState = active;
                return true;
            }

            sprintState = null;
            return false;
        }
        public bool TryGetActiveGuardState(out NpcGuardState guardState)
        {

            if (_fsm?.CurrentState is NpcGuardState active)
            {
                guardState = active;
                return true;
            }

            guardState = null;
            return false;
        }
        public bool TryGetActiveAttackState(out NpcAttackState attackState)
        {

            if (_fsm?.CurrentState is NpcAttackState active)
            {
                attackState = active;
                return true;
            }

            attackState = null;
            return false;
        }
        public bool TryGetActiveHitState(out NpcHitState hitState)
        {

            if (_fsm?.CurrentState is NpcHitState active)
            {
                hitState = active;
                return true;
            }

            hitState = null;
            return false;
        }

        public bool TryGetActiveParryState(out NpcParryState parryState)
        {
            if (_fsm?.CurrentState is NpcParryState active)
            {
                parryState = active;
                return true;
            }

            parryState = null;
            return false;
        }

        public bool TryGetActiveParriedState(out NpcParriedState parriedState)
        {
            if (_fsm?.CurrentState is NpcParriedState active)
            {
                parriedState = active;
                return true;
            }

            parriedState = null;
            return false;
        }

        public bool TryGetActivePostureBrokenState(out NpcPostureBrokenState postureBrokenState)
        {
            if (_fsm?.CurrentState is NpcPostureBrokenState active)
            {
                postureBrokenState = active;
                return true;
            }

            postureBrokenState = null;
            return false;
        }

        public bool TryGetDodgePresentationContext(out DodgePresentationContext ctx)
        {

            if (_fsm?.CurrentState is NpcDodgeState dodge && dodge.PresentationContext.IsValid)
            {
                ctx = dodge.PresentationContext;
                return true;
            }

            ctx = default;
            return false;
        }
        public void SetLastPreparedDodgeMode(byte mode) => LastPreparedDodgeMode = mode;


        public bool TryGetActiveExecutedState(
    out NpcExecutedState executedState)
        {
            if (_fsm?.CurrentState is NpcExecutedState active)
            {
                executedState = active;
                return true;
            }

            executedState = null;
            return false;
        }

        // ============ 服务器状态转换 ============

        public bool ServerTryEnterAttack(AttackMoveId attackId = AttackMoveId.Combo1)
        {
            if (!isServer || _attack == null) return false;
            _attack.Prepare(attackId);
            return _fsm.TryTransition(CharacterStateId.Attack, _registry, TransitionReason.InputAttack);
        }

        public bool ServerRequestComboAttack()
        {
            if (!isServer || _fsm == null || _registry == null || _attack == null) return false;

            if (CurrentStateId == CharacterStateId.Attack)
                return _attack.TryAdvanceCombo();

            _attack.Prepare(AttackMoveId.Combo1);
            return _fsm.TryTransition(CharacterStateId.Attack, _registry, TransitionReason.InputAttack);
        }

        public bool ServerTryEnterAttack(byte attackStep)
        {
            return ServerTryEnterAttack(AttackMoveIdExtensions.FromByte(attackStep));
        }
        public bool ServerTryEnterDodge(DodgeMode mode, Vector3 worldDir, Vector2 blendLocal = default)
        {
            if (!isServer || _dodge == null) return false;
            _dodge.Prepare(mode, worldDir, blendLocal);
            SetLastPreparedDodgeMode((byte)mode);
            return _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
        }
        public bool ServerTryEnterGuard(float loopHoldDuration = 2f)
        {
            if (!isServer) return false;
            return ForceEnterGuard(loopHoldDuration);
        }
        public bool ServerTryEnterHit(bool isHeavy = false, byte hitVariant = 1)
        {
            if (!isServer || _hit == null) return false;
            _hit.Prepare(isHeavy, hitVariant);
            return _fsm.TryTransition(
                CharacterStateId.Hit,
                _registry,
                isHeavy ? TransitionReason.HitHeavy : TransitionReason.HitLight);
        }

        public bool ServerTryEnterParry()
        {
            if (!isServer || _fsm == null || _registry == null || _parry == null)
            {
                return false;
            }

            if (CurrentStateId is not (
                    CharacterStateId.Idle or
                    CharacterStateId.Move or
                    CharacterStateId.Guard))
            {
                return false;
            }

            return _fsm.TryTransition(
                CharacterStateId.Parry,
                _registry,
                TransitionReason.InputParry);
        }

        public bool ServerTryEnterParried()
        {
            if (!isServer || _fsm == null || _registry == null || _parried == null ||
                CurrentStateId != CharacterStateId.Attack)
            {
                return false;
            }

            if (!_fsm.TryTransition(
                    CharacterStateId.Parried,
                    _registry,
                    TransitionReason.Parried))
            {
                return false;
            }

            _combatActor?.CancelCurrentAttack();
            _motor?.Stop();
            _motor?.ResetPath();
            return true;
        }


        public bool ServerTryEnterExecuted(
            in ExecutionSession session)
        {
            if (!CanEnterExecuted() ||
                session.TargetActorId != _combatActor.ActorId ||
                !_executed.TryPrepare(
                    session,
                    _combatActor.ActorId))
            {
                return false;
            }

            DeathPresentationVariant previousVariant =
                CurrentDeathPresentationVariant;
            CurrentDeathPresentationVariant = session.TargetWillDie
                ? DeathPresentationVariant.Executed
                : DeathPresentationVariant.Default;

            if (_fsm.TryTransition(
                    CharacterStateId.Executed,
                    _registry,
                    TransitionReason.ExecutionAccepted))
            {
                return true;
            }

            CurrentDeathPresentationVariant = previousVariant;
            _executed.CancelPreparation(session.ExecutionId);
            return false;
        }


        public bool ServerTryRollbackExecutionStart(
            ulong executionId,
            CharacterStateId previousState)
        {
            if (!isServer ||
                CurrentStateId != CharacterStateId.Executed ||
                !_executed.IsBoundTo(executionId) ||
                previousState is not (
                    CharacterStateId.Parried or
                    CharacterStateId.PostureBroken))
            {
                return false;
            }

            bool restored = _fsm.TryTransition(
                previousState,
                _registry,
                TransitionReason.ExecutionCancelled);
            if (restored)
                CurrentDeathPresentationVariant = DeathPresentationVariant.Default;
            return restored;
        }

        public bool ServerTryCompleteExecuted(
    ulong executionId)
        {
            if (!isServer ||
                _combatActor == null ||
                CurrentStateId != CharacterStateId.Executed ||
                !_executed.IsBoundTo(executionId))
            {
                return false;
            }

            bool targetWillDie = _executed.Session.TargetWillDie;
            if (_combatActor.IsDead != targetWillDie)
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
                    _registry,
                    TransitionReason.ExecutionCompleted))
            {
                return true;
            }

            CurrentDeathPresentationVariant = previousVariant;
            return false;
        }

        public bool ServerTryEnterPostureBroken()
        {
            if (!isServer || _fsm == null || _registry == null ||
                CurrentStateId is CharacterStateId.Dead or CharacterStateId.PostureBroken)
                return false;

            return _fsm.TryTransition(
                CharacterStateId.PostureBroken,
                _registry,
                TransitionReason.PostureBreak);
        }

        public bool ServerTryEnterDead()
        {
            if (!isServer)
                return false;

            if (CurrentStateId == CharacterStateId.Dead)
                return true;

            DeathPresentationVariant previousVariant =
                CurrentDeathPresentationVariant;

            CurrentDeathPresentationVariant =
                DeathPresentationVariant.Default;

            if (_fsm.TryTransition(
                    CharacterStateId.Dead,
                    _registry,
                    TransitionReason.Death))
            {
                return true;
            }

            CurrentDeathPresentationVariant = previousVariant;
            return false;
        }
        public bool ServerTryEnterSprint(float holdDuration = 1.5f)
        {
            if (!isServer || _sprint == null) return false;
            _sprint.Prepare(holdDuration);
            return _fsm.TryTransition(CharacterStateId.Sprint, _registry, TransitionReason.InputSprint);
        }

        /// <summary>
        /// 先确认状态机接受复活转换，再恢复战斗数值，防止失败的状态转换生成活着但仍处于 Dead 的实体。
        /// </summary>
        public bool ServerTryRevive()
        {
            return isServer && TryReviveOnAuthority();
        }

        /// <summary>
        /// Allows the GM command to use the same revive transaction on Server and Offline.
        /// A pure Client never receives authority to mutate NPC state.
        /// </summary>
        public bool TryReviveForGm()
        {
            bool hasAuthority = isServer ||
                (!NetworkServer.active && !NetworkClient.active);
            return hasAuthority && TryReviveOnAuthority();
        }

        private bool TryReviveOnAuthority()
        {
            if (_fsm == null ||
                _registry == null ||
                _combatActor == null ||
                CurrentStateId != CharacterStateId.Dead ||
                !_combatActor.IsDead)
            {
                return false;
            }

            if (!_fsm.TryTransition(
                    CharacterStateId.Idle,
                    _registry,
                    TransitionReason.Revive))
            {
                return false;
            }

            if (!_combatActor.RestoreFullHealthForRevive())
                return false;

            CurrentDeathPresentationVariant =
                DeathPresentationVariant.Default;

            return true;
        }

        // ============ 根位移 ============

        /// <summary>
        /// 只允许明确消费根位移的权威状态推动 Motor，避免普通移动和破势动画绕过 NavMesh。
        /// </summary>
        public void HandleAnimatorRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (!isServer || _motor == null)
                return;

            if (CurrentStateId is not (
                    CharacterStateId.Attack
                    or CharacterStateId.Hit
                    or CharacterStateId.Dead))
                return;

            _motor.ApplyRootMotionDelta(deltaPosition, deltaRotation);
        }

        // ============ 强制调试控制 ============

        /// <summary>
        /// GM 防御是持续覆盖：当前动作不可被 Guard 打断时先锁存，回到可转换状态后再进入 Guard。
        /// </summary>
        public bool SetGmGuardOverride(bool active, float loopHoldDuration = 30f)
        {
            if (!isServer || _fsm == null || _registry == null || _guard == null)
                return false;

            if (active)
            {
                _gmGuardOverrideActive = true;
                _gmGuardHoldDuration = Mathf.Max(0.1f, loopHoldDuration);
                RefreshBehaviorTreeForGmOverride();
                TryApplyPendingGmGuard();
                return true;
            }

            _gmGuardOverrideActive = false;
            bool exited = CurrentStateId != CharacterStateId.Guard || ForceExitGuardToIdle();
            RefreshBehaviorTreeForGmOverride();
            return exited;
        }

        public bool ForceEnterGuard(float loopHoldDuration = 2f)
        {
            if (_guard == null || _fsm == null || _registry == null) return false;
            _guard.Prepare(loopHoldDuration);
            return _fsm.TryTransition(CharacterStateId.Guard, _registry, TransitionReason.InputGuard) || CurrentStateId == CharacterStateId.Guard;
        }

        public bool ForceExitGuardToIdle()
        {
            if (_fsm == null || _registry == null || CurrentStateId != CharacterStateId.Guard)
                return false;

            return _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void SetGmFacingTarget(Transform target)
        {
            _gmFacingOverrideActive = _intentSource != null && target != null;

            if (_gmFacingOverrideActive)
            {
                _intentSource.SetGmFacingTarget(target);
                RefreshBehaviorTreeForGmOverride();

                _motor?.Stop();
                _motor?.ResetPath();

                if (CurrentStateId == CharacterStateId.Move)
                {
                    _fsm?.TryTransition(
                        CharacterStateId.Idle,
                        _registry,
                        TransitionReason.Timeout);
                }

                return;
            }

            ClearGmFacingTarget();
        }

        public void ClearGmFacingTarget()
        {
            _intentSource?.ClearGmFacingTarget();
            _gmFacingOverrideActive = false;
            RefreshBehaviorTreeForGmOverride();
        }

        private void TryApplyPendingGmGuard()
        {
            if (!_gmGuardOverrideActive ||
                CurrentStateId is CharacterStateId.Guard or CharacterStateId.Dead)
            {
                return;
            }

            if (CurrentStateId is not (
                    CharacterStateId.Idle or
                    CharacterStateId.Move or
                    CharacterStateId.Sprint))
            {
                return;
            }

            ForceEnterGuard(_gmGuardHoldDuration);
        }

        private void RefreshExpiredGmFacingOverride()
        {
            if (!_gmFacingOverrideActive ||
                (_intentSource != null && _intentSource.HasGmFacingTarget))
            {
                return;
            }

            _gmFacingOverrideActive = false;
            RefreshBehaviorTreeForGmOverride();
        }

        /// <summary>
        /// 防御与朝向覆盖独立持有暂停原因，只有最后一个 GM 覆盖释放后才恢复行为树。
        /// </summary>
        private void RefreshBehaviorTreeForGmOverride()
        {
            if (_behaviorTree == null)
                return;

            bool shouldPause = _gmGuardOverrideActive || _gmFacingOverrideActive;
            if (shouldPause)
            {
                if (!_gmPausedBehaviorTree)
                {
                    _behaviorTreeEnabledBeforeGmPause = _behaviorTree.enabled;
                    _gmPausedBehaviorTree = true;
                }

                _behaviorTree.enabled = false;
                return;
            }

            RestoreBehaviorTreeAfterGmOverride();
        }

        private void RestoreBehaviorTreeAfterGmOverride()
        {
            if (!_gmPausedBehaviorTree)
                return;

            if (_behaviorTree != null)
                _behaviorTree.enabled = _behaviorTreeEnabledBeforeGmPause;

            _gmPausedBehaviorTree = false;
        }
    }
}
