using AI;
using AI.NpcStates;
using Character.Combat;
using Character.Config;
using Character.Controller;
using Character.LockOn;
using Character.Presentation;
using Character.StateMachine;
using Character.StateMachine.States;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// 将远端插值与所有 Animator 路由集中在同一个 LateUpdate 入口，并按反应、战斗、转身、冲刺、移动的优先级只提交一种表现。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterLateUpdatePipeline : MonoBehaviour
    {
        [SerializeField] private RemoteInterpolator _remoteInterpolator;
        [SerializeField] private RemoteActionApplier _remoteActionApplier;
        [SerializeField] private Animator _animator;

        private NetworkIdentity _networkIdentity;
        private PlayerController _playerController;
        private PlayerAuthorityGate _authorityGate;
        private NpcCharacterDriver _npcDriver;
        private NpcMotor _npcMotor;
        private ILockOnLocomotionQuery _lockOnQuery;

        private CharacterLocomotionPresenter _locomotionPresenter;
        private CharacterTurnPresenter _turnPresenter;
        private CharacterSprintPresenter _sprintPresenter;
        private CharacterCombatPresenter _combatPresenter;


        private CharacterStateId _lastPresentationStateId = CharacterStateId.None;
        private int _lastPresentationStateEnterVersion;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            if (_remoteInterpolator == null)
                _remoteInterpolator = GetComponent<RemoteInterpolator>();

            if (_remoteActionApplier == null)
                _remoteActionApplier = GetComponent<RemoteActionApplier>();

            _networkIdentity = GetComponent<NetworkIdentity>();
            _playerController = GetComponent<PlayerController>();
            _authorityGate = GetComponent<PlayerAuthorityGate>();
            _npcDriver = GetComponent<NpcCharacterDriver>();
            _npcMotor = GetComponent<NpcMotor>();
            _lockOnQuery = GetComponent<ILockOnLocomotionQuery>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            EnsurePresenters();
        }

        // ============ 表现管线入口 ============

        /// <summary>
        /// 先应用远端位姿再构建表现帧，确保 Animator 读取到与本帧视觉位置一致的状态来源。
        /// </summary>
        public void TickLateUpdate()
        {
            EnsurePresenters();

            if (_remoteInterpolator != null)
                _remoteInterpolator.TickInterpolation();

            if (_animator == null)
                return;

            var frame = BuildPresentationFrame();
            ReleaseGuardPresentationIfNeeded(frame);

            if (TryPresentGuardReaction(frame))
            {
                CommitPresentationFrame(frame);
                return;
            }

            if (TryPresentCombat(frame)
                || TryPresentIdleTurn(frame)
                || TryPresentGuardTurn(frame)
                || TryPresentGuard(frame)
                || TryPresentSprint(frame)
                || TryPresentAfterSprint(frame))
            {
                CommitPresentationFrame(frame);
                return;
            }

            PresentLocomotion(frame);
            CommitPresentationFrame(frame);
        }

        private void LateUpdate()
        {
            if (IsDrivenByPlayerControllerLateUpdate())
                return;

            TickLateUpdate();
        }

        // ============ 表现帧构建 ============

        /// <summary>
        /// 把本地 Player、服务器 NPC 或远端快照归一为不可变帧，避免路由过程中多次读取到不同状态。
        /// </summary>
        private PresentationFrame BuildPresentationFrame()
        {
            ResolvePresentation(
                out var stateId,
                out var velocityXZ,
                out var moveInput,
                out var isLockOn,
                out var lockTargetNetId,
                out var sprintPhase,
                out var guardPhase,
                out var idlePhase,
                out var attackComboStep,
                out var stateEnterVersion);

            return new PresentationFrame(
                _lastPresentationStateId,
                _lastPresentationStateEnterVersion,
                stateId,
                stateEnterVersion,
                velocityXZ,
                moveInput,
                isLockOn,
                lockTargetNetId,
                sprintPhase,
                guardPhase,
                idlePhase,
                attackComboStep);
        }

        private void CommitPresentationFrame(PresentationFrame frame)
        {
            _lastPresentationStateId = frame.StateId;
            _lastPresentationStateEnterVersion = frame.StateEnterVersion;
        }

        /// <summary>
        /// Guard 离开边沿必须在所有短路路由之前释放层权重，特别是同帧进入 Idle Turn 时。
        /// </summary>
        private void ReleaseGuardPresentationIfNeeded(PresentationFrame frame)
        {
            if (frame.PreviousStateId != CharacterStateId.Guard
                || frame.StateId == CharacterStateId.Guard)
            {
                return;
            }

            _combatPresenter.ResetGuardLayers(_animator);
            _turnPresenter.Reset();
        }

        // ============ Idle 转身表现 ============

        private bool TryPresentIdleTurn(PresentationFrame frame)
        {
            if (frame.StateId != CharacterStateId.Idle)
                return false;

            IdleState.IdlePhase phase = ResolveIdlePhase(frame);
            float targetAngle = ResolveIdleTurnAngle(frame, phase);

            return _turnPresenter.TickIdleTurn(_animator, phase, targetAngle);
        }


        private IdleState.IdlePhase ResolveIdlePhase(PresentationFrame frame)
        {
            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveIdleState(out var idleState))
            {
                return idleState.CurrentPhase;
            }

            return frame.IdlePhase;
        }

        /// <summary>
        /// 权威实例读取实际计划角度，远端只有离散阶段时使用配置步长，避免依赖未同步的运行时对象。
        /// </summary>
        private float ResolveIdleTurnAngle(PresentationFrame frame, IdleState.IdlePhase phase)
        {
            if (phase is not (IdleState.IdlePhase.TurnLeft or IdleState.IdlePhase.TurnRight))
                return 0f;

            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveIdleState(out var idleState))
            {
                return idleState.GetTargetTurnAngle();
            }

            if (_npcDriver != null
                && _networkIdentity != null
                && _networkIdentity.isServer
                && _npcDriver.TryGetActiveIdleState(out var npcIdleState))
            {
                return npcIdleState.GetTargetTurnAngle();
            }

            CharacterPresentationConfig presentation = ResolvePresentationConfig();
            return presentation != null ? presentation.turnStepAngle : 180f;
        }

        // ============ 高优先级战斗表现 ============

        /// <summary>
        /// 战斗状态进入前先释放冲刺层缓存，保证离散 Combat 动画能够立即取得层所有权。
        /// </summary>
        private bool TryPresentCombat(PresentationFrame frame)
        {
            if (!IsCombatPresentationState(frame.StateId))
                return false;

            ReleaseSprintOverlayIfNeeded(frame);

            var dodgeCtx = ResolveDodgePresentationContext(frame.StateId);
            int actionParams = ResolveActionParams(frame.StateId);
            return _combatPresenter.TickCombat(
                _animator,
                frame.StateId,
                actionParams,
                dodgeCtx: dodgeCtx,
                attackComboStep: frame.AttackComboStep,
                deathPresentationVariant:
                    ResolveExecutionPresentationVariant(frame.StateId),
                forceRestart: frame.EnteredState);
        }

        /// <summary>
        /// 本地与服务器 NPC 直接读取状态实例，远端从动作消息还原同一受击变体，减少快照字段膨胀。
        /// </summary>
        private int ResolveActionParams(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Hit)
                return 0;

            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveHitState(out var playerHitState))
            {
                return playerHitState.HitVariant;
            }

            if (_npcDriver != null
                && _networkIdentity != null
                && _networkIdentity.isServer
                && _npcDriver.TryGetActiveHitState(out var npcHitState))
            {
                return npcHitState.HitVariant;
            }

            if (_remoteActionApplier == null)
                return 0;

            if (_remoteActionApplier.CurrentRemoteAction != ActionType.Hit)
                return 0;

            Combat.CombatResolver.UnpackHitParam(
                _remoteActionApplier.LastHitParam, out _, out _, out byte hitVariant);
            return hitVariant;
        }

        // ============ 格挡表现 ============

        private bool TryPresentGuard(PresentationFrame frame)
        {
            if (frame.StateId != CharacterStateId.Guard)
            {
                if (frame.PreviousStateId == CharacterStateId.Guard)
                    _combatPresenter.ResetGuardLayers(_animator);

                return false;
            }

            ReleaseSprintOverlayIfNeeded(frame);

            bool hasMove = frame.VelocityXZ.sqrMagnitude > 0.01f;
            var phase = ResolveGuardPhase(frame);
            return _combatPresenter.TickGuard(_animator, phase, hasMove);
        }


        /// <summary>
        /// 防御转身临时接管全身层并清理普通格挡层，防止两个表现路径同时向 Animator 写权重。
        /// </summary>
        private bool TryPresentGuardTurn(PresentationFrame frame)
        {
            if (frame.StateId != CharacterStateId.Guard)
            {
                if (frame.StateId != CharacterStateId.Idle)
                    _turnPresenter.Reset();

                return false;
            }

            GuardState.GuardPhase phase = ResolveGuardPhase(frame);
            if (phase is not (GuardState.GuardPhase.TurnLeft or GuardState.GuardPhase.TurnRight))
            {
                _turnPresenter.Reset();
                return false;
            }

            float targetAngle = ResolveGuardTurnAngle(frame, phase);
            _combatPresenter.ResetGuardLayers(_animator);
            return _turnPresenter.TickGuardTurn(_animator, phase, targetAngle);
        }

        /// <summary>
        /// 权威实例读取状态内的实际步进角，远端缺少该字段时使用配置回退，确保所有观察端仍能选到同一类动画。
        /// </summary>
        private float ResolveGuardTurnAngle(PresentationFrame frame, GuardState.GuardPhase phase)
        {
            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveGuardState(out var guardState))
            {
                return guardState.GetTargetTurnAngle();
            }

            if (_npcDriver != null
                && _networkIdentity != null
                && _networkIdentity.isServer
                && _npcDriver.TryGetActiveGuardState(out var npcGuardState))
            {
                return npcGuardState.GetTargetTurnAngle();
            }

            CharacterPresentationConfig presentation = ResolvePresentationConfig();
            return presentation != null ? presentation.turnStepAngle : 180f;
        }

        /// <summary>
        /// 格挡反应边沿只消费一次，但计时期间每帧维持战斗层权重，防止被普通 Guard 路由覆盖。
        /// </summary>
        private bool TryPresentGuardReaction(PresentationFrame frame)
        {
            if (_combatPresenter == null || _animator == null)
                return false;

            // 全身受击、死亡和崩防必须覆盖尚未结束的格挡受击计时。
            if (frame.StateId is CharacterStateId.Hit
                or CharacterStateId.Dead
                or CharacterStateId.PostureBroken
                or CharacterStateId.Parry
                or CharacterStateId.Parried)
            {
                return false;
            }

            if (_remoteActionApplier != null
                && _remoteActionApplier.TryConsumeGuardReaction(out var reaction))
            {
                CharacterCombatConfig combat = ResolveCombatConfig();
                float hitDuration = combat != null ? combat.guardHitReactionDuration : 0.28f;
                float breakDuration = combat != null ? combat.guardBreakReactionDuration : 0.65f;
                return _combatPresenter.PlayGuardReaction(_animator, reaction, hitDuration, breakDuration);
            }

            return _combatPresenter.TickGuardReaction(_animator);
        }

        private GuardState.GuardPhase ResolveGuardPhase(PresentationFrame frame)
        {
            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveGuardState(out var guardState))
            {
                return guardState.CurrentPhase;
            }

            return frame.GuardPhase;
        }

        // ============ 闪避上下文 ============

        /// <summary>
        /// 本地状态上下文优先，远端在快照字段缺失时用动作边沿补齐，兼容动作与快照不同到达顺序。
        /// </summary>
        private DodgePresentationContext ResolveDodgePresentationContext(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Dodge)
                return default;

            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetDodgePresentationContext(out var ctx))
            {
                return ctx;
            }

            var combat = ResolveCombatConfig();
            if (_remoteInterpolator != null)
            {
                var snapshot = ResolveRemoteDodgeSnapshot(_remoteInterpolator.LastAppliedSnapshot);
                if (snapshot.Tick > 0 && snapshot.TryBuildDodgePresentationContext(combat, out var remoteCtx))
                    return remoteCtx;
            }

            return new DodgePresentationContext(
                DodgeMode.ForwardAlongMove,
                Vector2.zero,
                combat.dodgeEvadeDuration,
                combat.GetDodgeMoveDuration(DodgeMode.ForwardAlongMove),
                transform.forward);
        }

        private CharacterCombatConfig ResolveCombatConfig()
        {
            return _playerController != null
                ? GameDataManager.Instance.Player.combat
                : GameDataManager.Instance.Npc.combat;
        }

        private StateSnapshot ResolveRemoteDodgeSnapshot(StateSnapshot snapshot)
        {
            if (snapshot.StateId != CharacterStateId.Dodge || snapshot.DodgeMode != 0)
                return snapshot;

            if (_remoteActionApplier == null || _remoteActionApplier.LastDodgeMode == 0)
                return snapshot;

            return snapshot.WithDodgeMode(_remoteActionApplier.LastDodgeMode);
        }

        // ============ 冲刺与移动表现 ============

        private bool TryPresentSprint(PresentationFrame frame)
        {
            if (frame.StateId != CharacterStateId.Sprint)
                return false;

            if (frame.LeftCombat)
                _combatPresenter.ResetAllCombatLayers(_animator);

            _locomotionPresenter.ReleaseLayerToSprint();

            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveSprintState(out var sprintState))
            {
                _sprintPresenter.Tick(_animator, sprintState);
            }
            else
            {
                _sprintPresenter.TickRemotePhase(_animator, frame.SprintPhase);
            }

            return true;
        }

        /// <summary>
        /// 离开冲刺的首帧走专用过渡，避免直接切回常规移动时沿用冲刺层残留权重。
        /// </summary>
        private bool TryPresentAfterSprint(PresentationFrame frame)
        {
            if (!frame.LeftSprint)
                return false;

            _sprintPresenter.Reset();
            _locomotionPresenter.TickLeavingSprint(
                _animator,
                transform,
                frame.StateId,
                frame.VelocityXZ,
                frame.MoveInput,
                frame.IsLockOn);

            return true;
        }

        private void PresentLocomotion(PresentationFrame frame)
        {
            if (frame.LeftCombat)
                _combatPresenter.ResetAllCombatLayers(_animator);

            _locomotionPresenter.Tick(
                _animator,
                transform,
                frame.StateId,
                frame.VelocityXZ,
                frame.MoveInput,
                frame.IsLockOn);
        }

        private void ReleaseSprintOverlayIfNeeded(PresentationFrame frame)
        {
            if (frame.PreviousStateId != CharacterStateId.Sprint)
                return;

            _sprintPresenter.Reset();
            _locomotionPresenter.ReleaseLayerToSprint();
        }

        private static bool IsCombatPresentationState(CharacterStateId id)
        {
            return id is CharacterStateId.Hit
                or CharacterStateId.Dead
                or CharacterStateId.PostureBroken
                or CharacterStateId.Attack
                or CharacterStateId.Dodge
                or CharacterStateId.Parry
                or CharacterStateId.Parried
                or CharacterStateId.Executing
or CharacterStateId.Executed;
        }

        // ============ Presenter 组装 ============

        private void EnsurePresenters()
        {
            var presentation = ResolvePresentationConfig();
            _locomotionPresenter ??= new CharacterLocomotionPresenter(presentation);
            _turnPresenter ??= new CharacterTurnPresenter(presentation);
            _sprintPresenter ??= new CharacterSprintPresenter(presentation);
            _combatPresenter ??= new CharacterCombatPresenter(
                presentation,
                ResolveCombatConfig());
        }

        private CharacterPresentationConfig ResolvePresentationConfig()
        {
            return _playerController != null
                ? GameDataManager.Instance.Player.presentation
                : GameDataManager.Instance.Npc.presentation;
        }

        // ============ 表现来源选择 ============

        /// <summary>
        /// 来源优先级固定为本地 Player、服务器 NPC、远端快照，防止 Host 上同一对象同时被两套数据驱动。
        /// </summary>
        private void ResolvePresentation(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out Vector2 moveInput,
            out bool isLockOn,
            out uint lockTargetNetId,
            out SprintState.SprintPhase sprintPhase,
            out GuardState.GuardPhase guardPhase,
            out IdleState.IdlePhase idlePhase,
            out byte attackComboStep,
            out int stateEnterVersion)
        {
            stateId = CharacterStateId.Idle;
            velocityXZ = Vector2.zero;
            moveInput = Vector2.zero;
            isLockOn = false;
            lockTargetNetId = 0;
            sprintPhase = SprintState.SprintPhase.Loop;
            guardPhase = GuardState.GuardPhase.Start;
            idlePhase = IdleState.IdlePhase.Normal;
            attackComboStep = 1;
            stateEnterVersion = 0;

            if (TryResolveFromLocalPlayer(
                    out stateId,
                    out velocityXZ,
                    out moveInput,
                    out isLockOn,
                    out lockTargetNetId,
                    out sprintPhase,
                    out guardPhase,
                    out idlePhase,
                    out attackComboStep,
                    out stateEnterVersion))
                return;

            if (TryResolveFromServerNpc(
                    out stateId,
                    out velocityXZ,
                    out moveInput,
                    out isLockOn,
                    out lockTargetNetId,
                    out sprintPhase,
                    out guardPhase,
                    out idlePhase,
                    out attackComboStep,
                    out stateEnterVersion))
                return;

            if (TryResolveFromRemoteSnapshot(
                    out stateId,
                    out velocityXZ,
                    out moveInput,
                    out isLockOn,
                    out lockTargetNetId,
                    out sprintPhase,
                    out guardPhase,
                    out idlePhase,
                    out attackComboStep,
                    out stateEnterVersion))
                return;
        }

        // ============ 本地 Player 来源 ============

        /// <summary>
        /// 本地来源直接读取输入与活跃状态对象，保留比网络快照更完整的阶段上下文。
        /// </summary>
        private bool TryResolveFromLocalPlayer(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out Vector2 moveInput,
            out bool isLockOn,
            out uint lockTargetNetId,
            out SprintState.SprintPhase sprintPhase,
            out GuardState.GuardPhase guardPhase,
            out IdleState.IdlePhase idlePhase,
            out byte attackComboStep,
            out int stateEnterVersion)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;
            moveInput = Vector2.zero;
            isLockOn = false;
            lockTargetNetId = 0;
            sprintPhase = SprintState.SprintPhase.Loop;
            guardPhase = GuardState.GuardPhase.Start;
            idlePhase = IdleState.IdlePhase.Normal;
            attackComboStep = 1;
            stateEnterVersion = 0;

            if (_playerController == null)
                return false;

            if (!HasLocalPresentationAuthority())
                return false;

            stateId = _playerController.CurrentStateId;
            stateEnterVersion = _playerController.StateEnterVersion;
            var velocity = _playerController.Velocity;
            velocityXZ = new Vector2(velocity.x, velocity.z);
            moveInput = _playerController.LastMoveInput;
            isLockOn = _lockOnQuery != null && _lockOnQuery.IsLockOnActive;

            Transform lockTarget = isLockOn ? _lockOnQuery.CurrentTarget : null;
            if (lockTarget != null)
            {
                var lockOnTarget = lockTarget.GetComponentInParent<LockOnTarget>();
                if (lockOnTarget != null)
                    lockOnTarget.TryGetNetworkId(out lockTargetNetId);
            }
            else
            {
                isLockOn = false;
            }

            if (stateId == CharacterStateId.Sprint
                && _playerController.TryGetActiveSprintState(out var sprintState))
            {
                sprintPhase = sprintState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Guard
                && _playerController.TryGetActiveGuardState(out var guardState))
            {
                guardPhase = guardState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Idle
                && _playerController.TryGetActiveIdleState(out var idleState))
            {
                idlePhase = idleState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Attack
                && _playerController.TryGetActiveAttackState(out var attackState))
            {
                attackComboStep = attackState.CurrentComboStep;
            }

            return true;
        }

        // ============ 本地表现权威 ============

        private bool HasLocalPresentationAuthority()
        {
            if (_authorityGate != null)
                return _authorityGate.CanProcessLocalInput;

            if (_networkIdentity != null)
                return _networkIdentity.isLocalPlayer;

            return true;
        }

        // ============ 服务器 NPC 来源 ============

        /// <summary>
        /// Host 上的服务器 NPC 使用 NavMesh 与状态机实时值，避免等待自身广播快照回环后再表现。
        /// </summary>
        private bool TryResolveFromServerNpc(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out Vector2 moveInput,
            out bool isLockOn,
            out uint lockTargetNetId,
            out SprintState.SprintPhase sprintPhase,
            out GuardState.GuardPhase guardPhase,
            out IdleState.IdlePhase idlePhase,
            out byte attackComboStep,
            out int stateEnterVersion)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;
            moveInput = Vector2.zero;
            isLockOn = false;
            lockTargetNetId = 0;
            sprintPhase = SprintState.SprintPhase.Loop;
            guardPhase = GuardState.GuardPhase.Start;
            idlePhase = IdleState.IdlePhase.Normal;
            attackComboStep = 1;
            stateEnterVersion = 0;

            if (_npcDriver == null || _playerController != null)
                return false;

            if (_networkIdentity == null || !_networkIdentity.isServer || !NetworkServer.active)
                return false;

            stateId = _npcDriver.CurrentStateId;
            stateEnterVersion = _npcDriver.StateEnterVersion;

            var agent = _npcMotor != null ? _npcMotor.Agent : null;
            if (agent != null)
                velocityXZ = new Vector2(agent.velocity.x, agent.velocity.z);

            if (stateId == CharacterStateId.Dodge
                && _npcDriver.TryGetDodgePresentationContext(out var dodgeCtx)
                && dodgeCtx.Mode == DodgeMode.LockOn8Way)
            {
                velocityXZ = dodgeCtx.BlendLocal;
            }

            if (stateId == CharacterStateId.Sprint
                && _npcDriver.TryGetActiveSprintState(out var sprintState))
            {
                sprintPhase = sprintState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Guard
                && _npcDriver.TryGetActiveGuardState(out var guardState))
            {
                guardPhase = guardState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Idle
                && _npcDriver.TryGetActiveIdleState(out var idleState))
            {
                idlePhase = idleState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Attack
                && _npcDriver.TryGetActiveAttackState(out var attackState))
            {
                attackComboStep = attackState.CurrentComboStep;
            }

            return true;
        }

        // ============ 远端快照来源 ============

        /// <summary>
        /// 远端只消费插值器最后应用的快照，并让动作边沿补足同状态重入版本。
        /// </summary>
        private bool TryResolveFromRemoteSnapshot(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out Vector2 moveInput,
            out bool isLockOn,
            out uint lockTargetNetId,
            out SprintState.SprintPhase sprintPhase,
            out GuardState.GuardPhase guardPhase,
            out IdleState.IdlePhase idlePhase,
            out byte attackComboStep,
            out int stateEnterVersion)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;
            moveInput = Vector2.zero;
            isLockOn = false;
            lockTargetNetId = 0;
            sprintPhase = SprintState.SprintPhase.Loop;
            guardPhase = GuardState.GuardPhase.Start;
            idlePhase = IdleState.IdlePhase.Normal;
            attackComboStep = 1;
            stateEnterVersion = 0;

            if (_remoteInterpolator == null)
                return false;

            var snapshot = _remoteInterpolator.LastAppliedSnapshot;
            if (snapshot.Tick <= 0)
                return false;

            stateId = _remoteActionApplier != null
                ? _remoteActionApplier.ResolveSnapshotState(snapshot.StateId)
                : snapshot.StateId;
            velocityXZ = snapshot.VelocityXZ;
            moveInput = snapshot.GetMoveInputOrDefault();
            isLockOn = snapshot.IsLockOnActive;
            lockTargetNetId = snapshot.LockTargetNetId;
            sprintPhase = snapshot.GetSprintPhaseOrDefault();
            guardPhase = snapshot.GetGuardPhaseOrDefault();
            idlePhase = snapshot.GetIdlePhaseOrDefault();
            attackComboStep = snapshot.GetAttackComboStepOrDefault();
            stateEnterVersion = ResolveRemoteStateEnterVersion(stateId);
            return true;
        }

        private int ResolveRemoteStateEnterVersion(CharacterStateId stateId)
        {
            if (_remoteActionApplier == null)
                return 0;

            if (stateId == CharacterStateId.Hit)
                return _remoteActionApplier.LastHitSeqId;

            if (stateId == CharacterStateId.PostureBroken)
                return _remoteActionApplier.LastPostureBreakSeqId;

            if (stateId == CharacterStateId.Parry)
                return _remoteActionApplier.LastParrySeqId;

            if (stateId == CharacterStateId.Parried)
                return _remoteActionApplier.LastParriedSeqId;

            if (stateId is
                CharacterStateId.Executing or
                CharacterStateId.Executed)
            {
                return _remoteActionApplier.ExecutionStartVersion;
            }

            return 0;
        }

        // ============ LateUpdate 驱动仲裁 ============

        /// <summary>
        /// 本地 Player 由 PlayerController 显式调用，其他实体由组件自身 LateUpdate 调用，避免同帧重复推进 Presenter 缓存。
        /// </summary>
        private bool IsDrivenByPlayerControllerLateUpdate()
        {
            if (_playerController == null)
                _playerController = GetComponent<PlayerController>();

            if (_playerController == null)
                return false;

            if (_networkIdentity != null && _networkIdentity.isLocalPlayer)
                return true;

            if (_authorityGate == null)
                _authorityGate = GetComponent<PlayerAuthorityGate>();

            return _authorityGate != null && _authorityGate.CanProcessLocalInput;
        }

        // ============ 归一化表现帧 ============

        /// <summary>
        /// 保存前后状态与进入版本，用于识别同状态重入和离开 Layer 所有权的边沿。
        /// </summary>
        private readonly struct PresentationFrame
        {
            public PresentationFrame(
                CharacterStateId previousStateId,
                int previousStateEnterVersion,
                CharacterStateId stateId,
                int stateEnterVersion,
                Vector2 velocityXZ,
                Vector2 moveInput,
                bool isLockOn,
                uint lockTargetNetId,
                SprintState.SprintPhase sprintPhase,
                GuardState.GuardPhase guardPhase,
                IdleState.IdlePhase idlePhase,
                byte attackComboStep)
            {
                PreviousStateId = previousStateId;
                PreviousStateEnterVersion = previousStateEnterVersion;
                StateId = stateId;
                StateEnterVersion = stateEnterVersion;
                VelocityXZ = velocityXZ;
                MoveInput = moveInput;
                IsLockOn = isLockOn;
                LockTargetNetId = lockTargetNetId;
                SprintPhase = sprintPhase;
                GuardPhase = guardPhase;
                IdlePhase = idlePhase;
                AttackComboStep = attackComboStep;
            }

            public CharacterStateId PreviousStateId { get; }
            public int PreviousStateEnterVersion { get; }
            public CharacterStateId StateId { get; }
            public int StateEnterVersion { get; }
            public Vector2 VelocityXZ { get; }
            public Vector2 MoveInput { get; }
            public bool IsLockOn { get; }
            public uint LockTargetNetId { get; }
            public SprintState.SprintPhase SprintPhase { get; }
            public GuardState.GuardPhase GuardPhase { get; }
            public IdleState.IdlePhase IdlePhase { get; }
            public byte AttackComboStep { get; }

            public bool LeftSprint =>
                PreviousStateId == CharacterStateId.Sprint && StateId != CharacterStateId.Sprint;

            public bool EnteredState =>
                StateId != PreviousStateId || StateEnterVersion != PreviousStateEnterVersion;

            public bool LeftCombat =>
                IsCombatState(PreviousStateId) && !IsCombatState(StateId);

            private static bool IsCombatState(CharacterStateId id)
            {
                return id is CharacterStateId.Hit
                    or CharacterStateId.Dead
                    or CharacterStateId.PostureBroken
                    or CharacterStateId.Attack
                    or CharacterStateId.Dodge
                    or CharacterStateId.Guard
                    or CharacterStateId.Parry
                    or CharacterStateId.Parried
                    or CharacterStateId.Executing
or CharacterStateId.Executed;
            }
        }

        private DeathPresentationVariant ResolveExecutionPresentationVariant(
            CharacterStateId stateId)
        {
            if (stateId is not (
                    CharacterStateId.Executed or
                    CharacterStateId.Dead))
            {
                return DeathPresentationVariant.Default;
            }

            if (_playerController != null &&
                HasLocalPresentationAuthority())
            {
                return _playerController.CurrentDeathPresentationVariant;
            }

            if (_npcDriver != null &&
                _networkIdentity != null &&
                _networkIdentity.isServer &&
                NetworkServer.active)
            {
                return _npcDriver.CurrentDeathPresentationVariant;
            }

            return _remoteActionApplier != null
                ? _remoteActionApplier.ExecutionDeathVariant
                : DeathPresentationVariant.Default;
        }
    }
}
