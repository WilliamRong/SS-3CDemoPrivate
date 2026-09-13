using Mirror;
using UnityEngine;
using Character.Combat;
using Character.Controller;
using Character.Execution;
using Character.Presentation;
using Character.StateMachine;

namespace Character.Sync
{
    /// <summary>
    /// 将离散动作、权威战斗结果和快照状态边沿汇聚到单个远端实体，统一处理去重与 Host 回环保护。
    /// </summary>
    public class RemoteActionApplier : MonoBehaviour
    {
        private CombatActor _combatActor;
        private NpcHealthBarView _healthBarView;

        private const int ServerCombatSeqBase = 100000;

        [Header("Debug")]
        [SerializeField] private bool _logApply = true;

        public int LastAppliedSeqId { get; private set; } = 0;
        public int LastAppliedTick { get; private set; } = 0;
        public ActionType CurrentRemoteAction { get; private set; } = ActionType.None;
        /// <summary>缓存进入动作时的模式，避免后续快照缺失时无法还原闪避表现。</summary>
        public byte LastDodgeMode { get; private set; }
        public int LastHitParam { get; private set; }
        public int LastHitSeqId { get; private set; }
        public int LastPostureBreakSeqId { get; private set; }
        public int LastParrySeqId { get; private set; }
        public int LastParriedSeqId { get; private set; }
        public GuardReactionType LastGuardReaction { get; private set; } = GuardReactionType.None;
        public int LastGuardReactionSeqId { get; private set; }

        private int _consumedGuardReactionSeqId;
        private int _lastServerCombatSeqId;
        private bool _postureBreakAwaitingSnapshot;
        private bool _postureBreakSnapshotObserved;
        private bool _parryAwaitingSnapshot;
        private bool _parrySnapshotObserved;
        private bool _parriedAwaitingSnapshot;
        private bool _parriedSnapshotObserved;

        private ExecutionSession _executionSession;
        private bool _hasExecutionSession;
        private CharacterStateId _executionStateId = CharacterStateId.None;
        private DeathPresentationVariant _executionDeathVariant =
            DeathPresentationVariant.Default;
        private int _executionStartVersion;
        private ulong _lastExecutionResultId;
        private uint _lastExecutionResultRevision;
        // Result and completion are separate reliable edges. Keep a validated
        // result briefly when it arrives after the local execution session was
        // retired, then replay it after Start/StateReplay reconstructs the
        // session. Without this, a late result is discarded and the target
        // keeps the pre-execution HP forever.
        private ExecutionResultMsg _pendingExecutionResult;
        private bool _hasPendingExecutionResult;
        private ulong _lastExecutionCompleteId;
        private ExecutionSessionFlags _lastExecutionCompleteFlags;
        private CharacterStateId _executionPreviousStateId =
            CharacterStateId.None;
        // Complete 先于 Result 或终态快照到达时，保留会话覆盖旧快照。
        private bool _executionAwaitingCompletionSnapshot;
        private CharacterStateId _executionCompletionStateId =
            CharacterStateId.None;
        private bool _executionTerminalDead;
        private bool _executionTerminalLatched;
        private CharacterStateId _executionTerminalStateId = CharacterStateId.None;
        // Delayed recovery messages for a completed session must not recreate
        // that same session and replay its animation/suppression.
        private ulong _executionTerminalExecutionId;
        private bool _localExecutionStateEntered;
        private bool _executionStateRecoveryPending;
        private float _nextExecutionStateRecoveryTime;
        private const float ExecutionStateRecoveryRetryDelay = 0.5f;

        private NetworkIdentity _networkIdentity;
        private PlayerController _playerController;
        private CharacterLateUpdatePipeline _lateUpdatePipeline;

        public int ExecutionStartVersion => _executionStartVersion;
        public bool HasExecutionSession => _hasExecutionSession;
        public CharacterStateId ExecutionStateId => _executionStateId;
        public DeathPresentationVariant ExecutionDeathVariant => _executionDeathVariant;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            _networkIdentity = GetComponent<NetworkIdentity>();
            _playerController = GetComponent<PlayerController>();
            _lateUpdatePipeline = GetComponent<CharacterLateUpdatePipeline>();
            _combatActor = GetComponent<CombatActor>();
            _healthBarView = GetComponent<NpcHealthBarView>();
        }

        private void Update()
        {
            TryEnterLocalExecutionState();

            if (_hasPendingExecutionResult)
                TryApplyPendingExecutionResult();

            if (!_executionStateRecoveryPending ||
                Time.unscaledTime < _nextExecutionStateRecoveryTime)
            {
                return;
            }

            _executionStateRecoveryPending = false;
            RequestExecutionStateRecovery();
        }

        // ============ 动作入口 ============

        /// <summary>
        /// Locks this actor to the authoritative paired state until a later
        /// result or completion edge releases the execution session.
        /// </summary>
        public void ApplyExecutionStart(ExecutionStartMsg message)
        {
            if (!ExecutionMessageValidator.TryBuildSession(
                    message,
                    out ExecutionSession session,
                    out DeathPresentationVariant deathVariant))
            {
                return;
            }

            int actorId = ResolveActorId();
            CharacterStateId stateId;
            if (actorId == session.ExecutorActorId)
            {
                stateId = CharacterStateId.Executing;
            }
            else if (actorId == session.TargetActorId)
            {
                stateId = CharacterStateId.Executed;
            }
            else
            {
                return;
            }

            if (_executionTerminalLatched &&
                _executionTerminalExecutionId == session.ExecutionId)
            {
                return;
            }

            if (_hasExecutionSession)
            {
                if (_executionSession.ExecutionId == session.ExecutionId)
                    return;

                // A different session cannot replace an actor that is still occupied.
                return;
            }

            _executionPreviousStateId = _combatActor != null
                ? _combatActor.CurrentStateId
                : CharacterStateId.None;

            _executionAwaitingCompletionSnapshot = false;
            _executionCompletionStateId = CharacterStateId.None;
            _executionTerminalDead = false;
            _executionStateRecoveryPending = false;
            _nextExecutionStateRecoveryTime = 0f;

            _lastExecutionCompleteId = session.ExecutionId;
            _lastExecutionCompleteFlags = session.Flags;

            _executionSession = session;
            _hasExecutionSession = true;
            _executionStateId = stateId;
            _executionDeathVariant = deathVariant;
            _executionTerminalDead = false;
            _executionTerminalLatched = false;
            _executionTerminalStateId = CharacterStateId.None;
            _localExecutionStateEntered = false;
            AdvanceExecutionStartVersion();

            CurrentRemoteAction = ActionType.None;
            ClearPostureBreakTracking();
            ClearParryTracking();
            ClearParriedTracking();
            _combatActor?.CancelCurrentAttack();

            if (!NetworkServer.active)
            {
                _combatActor?.BeginExecutionCombatSuppression(
                    session.ExecutionId);

                if (_networkIdentity != null &&
                    _networkIdentity.isLocalPlayer &&
                    _playerController != null)
                {
                    bool entered = stateId == CharacterStateId.Executing
                        ? _playerController.TryEnterExecutingFromAuthoritative(session)
                        : _playerController.TryEnterExecutedFromAuthoritative(
                            session);

                    if (!entered)
                    {
                        Debug.LogWarning(
                            $"[RemoteActionApplier] Local execution state " +
                            $"entry failed. execution={session.ExecutionId} " +
                            $"actor={actorId} state={stateId}");
                    }
                }
            }

            if (_logApply)
            {
                Debug.Log(
                    $"[RemoteActionApplier] execution start " +
                    $"execution={session.ExecutionId} actor={actorId} " +
                    $"state={stateId} death={deathVariant}");
            }

            // A result may have arrived before Start (or after a previous
            // completion edge retired this applier). Replay it now that the
            // matching session is available again.
            TryApplyPendingExecutionResult();
        }

        /// <summary>
        /// Replays one authoritative execution state by feeding it through the
        /// existing Start, Result, and Complete edge handlers in order.
        /// </summary>
        public void ApplyExecutionState(ExecutionStateMsg message)
        {
            int actorId = ResolveActorId();

            if (message.RequestSeq == 0 ||
                message.RequestedActorId != actorId ||
                message.HasState > 1 ||
                double.IsNaN(message.AuthorityTimeSec) ||
                double.IsInfinity(message.AuthorityTimeSec) ||
                message.AuthorityTimeSec < 0d)
            {
                return;
            }

            if (message.HasState == 0)
            {
                _executionStateRecoveryPending = false;
                if (_logApply)
                {
                    Debug.Log(
                        $"[RemoteActionApplier] execution state empty " +
                        $"request={message.RequestSeq} actor={actorId}");
                }

                return;
            }

            if (_executionTerminalLatched &&
                _executionTerminalExecutionId == message.ExecutionId)
            {
                _executionStateRecoveryPending = false;
                return;
            }

            const ExecutionSessionFlags knownFlags =
                ExecutionSessionFlags.ResultCommitted |
                ExecutionSessionFlags.ExecutorCompleted |
                ExecutionSessionFlags.TargetCompleted |
                ExecutionSessionFlags.Cancelled;

            ExecutionSessionFlags stateFlags =
                (ExecutionSessionFlags)message.SessionFlags;
            bool hasHealthResult = message.HasHealthResult != 0;

            if (message.HasHealthResult > 1 ||
                (stateFlags & ~knownFlags) != ExecutionSessionFlags.None ||
                ((stateFlags & ExecutionSessionFlags.ResultCommitted) != 0) !=
                    hasHealthResult ||
                float.IsNaN(message.ExecutorPx) ||
                float.IsInfinity(message.ExecutorPx) ||
                float.IsNaN(message.ExecutorPy) ||
                float.IsInfinity(message.ExecutorPy) ||
                float.IsNaN(message.ExecutorPz) ||
                float.IsInfinity(message.ExecutorPz) ||
                float.IsNaN(message.ExecutorYaw) ||
                float.IsInfinity(message.ExecutorYaw))
            {
                return;
            }

            // Start is replayed without cumulative flags. Result and completion
            // then pass through their normal validation and deduplication paths.
            var startMessage = new ExecutionStartMsg
            {
                RequestSeq = message.RequestSeq,
                ExecutionId = message.ExecutionId,
                ExecutorActorId = message.ExecutorActorId,
                TargetActorId = message.TargetActorId,
                FixedTargetPx = message.FixedTargetPx,
                FixedTargetPy = message.FixedTargetPy,
                FixedTargetPz = message.FixedTargetPz,
                FixedTargetYaw = message.FixedTargetYaw,
                ExecutorAnchorPx = message.ExecutorAnchorPx,
                ExecutorAnchorPy = message.ExecutorAnchorPy,
                ExecutorAnchorPz = message.ExecutorAnchorPz,
                ExecutorAnchorYaw = message.ExecutorAnchorYaw,
                StartTimeSec = message.StartTimeSec,
                ResultTimeSec = message.ResultTimeSec,
                ExecutionDamage = message.ExecutionDamage,
                TargetWillDie = message.TargetWillDie,
                DeathVariant = message.DeathVariant,
                SessionFlags = (byte)ExecutionSessionFlags.None,
            };

            if (!ExecutionMessageValidator.TryBuildSession(
                    startMessage,
                    out ExecutionSession stateSession,
                    out _))
            {
                return;
            }

            if (actorId != stateSession.ExecutorActorId &&
                actorId != stateSession.TargetActorId)
            {
                return;
            }

            if (_hasExecutionSession &&
                (_executionSession.ExecutionId != stateSession.ExecutionId ||
                 _executionSession.ExecutorActorId !=
                    stateSession.ExecutorActorId ||
                 _executionSession.TargetActorId !=
                    stateSession.TargetActorId ||
                 _executionSession.TargetWillDie !=
                    stateSession.TargetWillDie))
            {
                return;
            }

            if (!NetworkServer.active)
            {
                Vector3 position = actorId == stateSession.ExecutorActorId
                    ? new Vector3(
                        message.ExecutorPx,
                        message.ExecutorPy,
                        message.ExecutorPz)
                    : stateSession.FixedTargetPose.Position;
                float yaw = actorId == stateSession.ExecutorActorId
                    ? message.ExecutorYaw
                    : stateSession.FixedTargetPose.Yaw;

                transform.SetPositionAndRotation(
                    position,
                    Quaternion.Euler(0f, yaw, 0f));
            }

            ApplyExecutionStart(startMessage);

            TryApplyPendingExecutionResult();

            if (!_hasExecutionSession ||
                _executionSession.ExecutionId != stateSession.ExecutionId)
            {
                return;
            }

            _executionStateRecoveryPending = false;

            if (hasHealthResult)
            {
                ApplyExecutionResult(new ExecutionResultMsg
                {
                    ExecutionId = message.ExecutionId,
                    ExecutorActorId = message.ExecutorActorId,
                    TargetActorId = message.TargetActorId,
                    AuthorityTimeSec = message.AuthorityTimeSec,
                    CurrentHp = message.CurrentHp,
                    MaxHp = message.MaxHp,
                    HealthRevision = message.HealthRevision,
                    DeathVariant = message.DeathVariant,
                });
            }

            const ExecutionSessionFlags completionFlags =
                ExecutionSessionFlags.ExecutorCompleted |
                ExecutionSessionFlags.TargetCompleted |
                ExecutionSessionFlags.Cancelled;

            if ((stateFlags & completionFlags) != ExecutionSessionFlags.None)
            {
                ApplyExecutionComplete(new ExecutionCompleteMsg
                {
                    ExecutionId = message.ExecutionId,
                    ExecutorActorId = message.ExecutorActorId,
                    TargetActorId = message.TargetActorId,
                    AuthorityTimeSec = message.AuthorityTimeSec,
                    SessionFlags = message.SessionFlags,
                    DeathVariant = message.DeathVariant,
                });
            }

            if (_logApply)
            {
                Debug.Log(
                    $"[RemoteActionApplier] execution state replay " +
                    $"request={message.RequestSeq} " +
                    $"execution={message.ExecutionId} actor={actorId} " +
                    $"flags={stateFlags}");
            }
        }

        /// <summary>
        /// Applies the authoritative execution damage result to the target actor.
        /// Host records the edge only because Server already applied the damage.
        /// </summary>
        public void ApplyExecutionResult(ExecutionResultMsg message)
        {
            int actorId = ResolveActorId();
            if (actorId != message.TargetActorId ||
                message.ExecutionId == 0 ||
                message.ExecutorActorId <= 0 ||
                message.TargetActorId <= 0 ||
                message.ExecutorActorId == message.TargetActorId ||
                float.IsNaN(message.CurrentHp) ||
                float.IsInfinity(message.CurrentHp) ||
                float.IsNaN(message.MaxHp) ||
                float.IsInfinity(message.MaxHp) ||
                message.MaxHp <= 0f ||
                message.CurrentHp < 0f ||
                message.CurrentHp > message.MaxHp ||
                double.IsNaN(message.AuthorityTimeSec) ||
                double.IsInfinity(message.AuthorityTimeSec) ||
                message.AuthorityTimeSec < 0d)
            {
                return;
            }

            DeathPresentationVariant variant =
                (DeathPresentationVariant)message.DeathVariant;

            if (variant is not (
                    DeathPresentationVariant.Default or
                    DeathPresentationVariant.Executed))
            {
                return;
            }

            bool lethal =
                variant == DeathPresentationVariant.Executed;

            if (lethal != (message.CurrentHp <= 0f))
                return;

            if (_hasExecutionSession &&
                (_executionSession.ExecutionId != message.ExecutionId ||
                 _executionSession.ExecutorActorId != message.ExecutorActorId ||
                 _executionSession.TargetActorId != message.TargetActorId ||
                 _executionSession.TargetWillDie != lethal))
            {
                return;
            }

            // Result packets can arrive after the completion edge. The
            // terminal execution has already committed health and must not be
            // applied or presented again.
            if (_executionTerminalLatched &&
                _executionTerminalExecutionId == message.ExecutionId)
            {
                return;
            }

            if (_lastExecutionResultId == message.ExecutionId &&
                message.HealthRevision <= _lastExecutionResultRevision)
            {
                return;
            }

            if (!NetworkServer.active)
            {
                if (_combatActor == null ||
                    !_combatActor.ApplyAuthoritativeHealth(
                        message.CurrentHp,
                        message.MaxHp,
                        message.HealthRevision))
                {
                    QueuePendingExecutionResult(message);
                    RequestExecutionStateRecovery();
                    return;
                }
            }

            // The message can arrive before PlayerController.Start. Retry the
            // authoritative local transition from Update until it succeeds.
            TryEnterLocalExecutionState();

            _lastExecutionResultId = message.ExecutionId;
            _lastExecutionResultRevision = message.HealthRevision;

            if (_hasExecutionSession)
            {
                _executionSession =
                    _executionSession.MarkResultCommitted();
                _executionDeathVariant = variant;
                if (variant == DeathPresentationVariant.Executed)
                    _executionTerminalDead = true;
            }

            _healthBarView?.ShowForHit();

            if (_logApply)
            {
                Debug.Log(
                    $"[RemoteActionApplier] execution result " +
                    $"execution={message.ExecutionId} " +
                    $"hp={message.CurrentHp:F1}/{message.MaxHp:F1} " +
                    $"revision={message.HealthRevision} " +
                    $"death={variant}");
            }

            // Complete can arrive before the absolute HP result. Retry the
            // owned Player FSM transition after health becomes authoritative.
            if (_hasExecutionSession)
                TryFinishCompletedExecution();
        }

        private void QueuePendingExecutionResult(
            in ExecutionResultMsg message)
        {
            if (!_hasPendingExecutionResult ||
                message.ExecutionId > _pendingExecutionResult.ExecutionId ||
                (message.ExecutionId == _pendingExecutionResult.ExecutionId &&
                 message.HealthRevision > _pendingExecutionResult.HealthRevision))
            {
                _pendingExecutionResult = message;
                _hasPendingExecutionResult = true;
            }
        }

        private void TryApplyPendingExecutionResult()
        {
            if (!_hasPendingExecutionResult)
                return;

            ExecutionResultMsg pending = _pendingExecutionResult;
            if (_hasExecutionSession &&
                pending.ExecutionId != _executionSession.ExecutionId)
                return;

            _hasPendingExecutionResult = false;
            ApplyExecutionResult(pending);

            if (_lastExecutionResultId == pending.ExecutionId &&
                _lastExecutionResultRevision >= pending.HealthRevision)
            {
                _pendingExecutionResult = default;
                _hasPendingExecutionResult = false;
            }
        }

        /// <summary>
        /// Applies cumulative completion/cancellation flags without retiring the
        /// execution until a matching authoritative snapshot confirms the result.
        /// </summary>
        public void ApplyExecutionComplete(ExecutionCompleteMsg message)
        {
            if (!ExecutionMessageValidator.TryValidateComplete(
                    message,
                    out ExecutionSessionFlags incomingFlags,
                    out DeathPresentationVariant deathVariant))
            {
                return;
            }

            int actorId = ResolveActorId();
            if (actorId != message.ExecutorActorId &&
                actorId != message.TargetActorId)
            {
                return;
            }

            if (_executionTerminalLatched &&
                _executionTerminalExecutionId == message.ExecutionId)
            {
                return;
            }

            if (!_hasExecutionSession)
            {
                RequestExecutionStateRecovery();
                return;
            }

            if (!_hasExecutionSession ||
                _executionSession.ExecutionId != message.ExecutionId ||
                _executionSession.ExecutorActorId != message.ExecutorActorId ||
                _executionSession.TargetActorId != message.TargetActorId ||
                _executionSession.TargetWillDie !=
                    (deathVariant == DeathPresentationVariant.Executed))
            {
                RequestExecutionStateRecovery();
                return;
            }

            ExecutionSessionFlags previousFlags =
                _lastExecutionCompleteId == message.ExecutionId
                    ? _lastExecutionCompleteFlags
                    : ExecutionSessionFlags.None;
            ExecutionSessionFlags addedFlags =
                incomingFlags & ~previousFlags;

            if (addedFlags == ExecutionSessionFlags.None)
                return;

            ExecutionSession merged = ExecutionMessageValidator.MergeFlags(
                _executionSession,
                incomingFlags);
            if (!merged.TryValidate(out _))
                return;

            _lastExecutionCompleteId = message.ExecutionId;
            _lastExecutionCompleteFlags |= incomingFlags;
            _executionSession = merged;
            _executionDeathVariant = deathVariant;

            bool cancelled =
                (addedFlags & ExecutionSessionFlags.Cancelled) != 0;
            bool actorCompleted = actorId == message.ExecutorActorId
                ? (addedFlags & ExecutionSessionFlags.ExecutorCompleted) != 0
                : (addedFlags & ExecutionSessionFlags.TargetCompleted) != 0;

            if (!cancelled && !actorCompleted)
                return;

            _executionCompletionStateId =
                ResolveExecutionCompletionState(actorId, merged);
            _executionAwaitingCompletionSnapshot = true;
            CurrentRemoteAction = ActionType.None;

            CharacterStateId completionState =
                _executionCompletionStateId;

            TryFinishCompletedExecution();

            if (_logApply)
            {
                Debug.Log(
                    $"[RemoteActionApplier] execution complete " +
                    $"execution={message.ExecutionId} actor={actorId} " +
                    $"flags={incomingFlags} " +
                    $"state={completionState}");
            }
        }

        /// <summary>
        /// 服务器战斗序列、格挡序列和普通动作序列分别去重，避免不同发布源的编号空间互相吞掉消息。
        /// </summary>
        public void Apply(ActionEvent evt)
        {
            if (_networkIdentity != null && _networkIdentity.netId != 0
                && evt.ActorId != (int)_networkIdentity.netId)
            {
                return;
            }


            bool isServerCombatResult =
                evt.SeqId >= ServerCombatSeqBase &&
                evt.HasHealthResult != 0;

            if (evt.SeqId >= ServerCombatSeqBase && evt.Type == ActionType.Parried)
            {
                ApplyServerParried(evt);
                return;
            }

            if (isServerCombatResult)
            {
                ApplyServerCombatResult(evt);
                return;
            }


            if (evt.Type is ActionType.GuardHit or ActionType.GuardBreak)
            {
                ApplyGuardReaction(evt);
                return;
            }


            if (evt.SeqId <= LastAppliedSeqId)
            {
                if (_logApply) Debug.Log($"RemoteActionApplier: Ignore duplicate seqId {evt.SeqId}");
                return;
            }

            LastAppliedSeqId = evt.SeqId;
            LastAppliedTick = evt.Tick;

            switch (evt.Type)
            {
                case ActionType.AttackStart:
                    CurrentRemoteAction = ActionType.AttackStart;
                    break;
                case ActionType.DodgeStart:
                    CurrentRemoteAction = ActionType.DodgeStart;
                    LastDodgeMode = (byte)evt.Param;
                    break;
                case ActionType.Hit:
                    CurrentRemoteAction = ActionType.Hit;
                    LastHitParam = evt.Param;
                    LastHitSeqId = evt.SeqId;
                    break;
                case ActionType.PostureBreak:
                    CurrentRemoteAction = ActionType.PostureBreak;
                    RegisterPostureBreakEdge(evt.SeqId);
                    break;
                case ActionType.ParryStart:
                    CurrentRemoteAction = ActionType.ParryStart;
                    RegisterParryEdge(evt.SeqId);
                    break;
                case ActionType.Parried:
                    CurrentRemoteAction = ActionType.Parried;
                    RegisterParriedEdge(evt.SeqId);
                    break;
                case ActionType.Dead:
                    CurrentRemoteAction = ActionType.Dead;
                    ClearPostureBreakTracking();
                    ClearParryTracking();
                    ClearParriedTracking();
                    break;
                case ActionType.Revive:
                    CurrentRemoteAction = ActionType.Revive;
                    _executionTerminalDead = false;
                    _executionTerminalLatched = false;
                    _executionTerminalExecutionId = 0;
                    _executionTerminalStateId = CharacterStateId.None;
                    _executionDeathVariant = DeathPresentationVariant.Default;
                    ClearPostureBreakTracking();
                    ClearParryTracking();
                    ClearParriedTracking();
                    break;
                default:
                    CurrentRemoteAction = ActionType.None;
                    break;
            }

            if (_logApply)
                Debug.Log($"[RemoteActionApplier] applied {evt}");
        }

        // ============ 权威战斗结果 ============

        /// <summary>
        /// 观察端统一显示受击者世界血条；Host 已直接结算，只记录表现边沿而不再次应用生命伤害。
        /// </summary>
        private void ApplyServerCombatResult(ActionEvent evt)
        {
            if (evt.SeqId <= _lastServerCombatSeqId)
            {
                if (_logApply) Debug.Log($"RemoteActionApplier: Ignore duplicate server combat seqId {evt.SeqId}");
                return;
            }

            _lastServerCombatSeqId = evt.SeqId;
            LastAppliedTick = evt.Tick;
            CurrentRemoteAction = evt.Type;

            bool isHealthOnly = evt.Type == ActionType.HealthResult;
            bool isPostureBreak = evt.Type == ActionType.PostureBreak;
            bool isGuard =
                evt.Type == ActionType.GuardHit ||
                evt.Type == ActionType.GuardBreak;

            if (isPostureBreak)
            {
                RegisterPostureBreakEdge(evt.SeqId);
            }
            else if (isGuard)
            {
                LastGuardReaction = evt.Type == ActionType.GuardBreak
                    ? GuardReactionType.Break
                    : ToGuardReaction(evt.Param);
                LastGuardReactionSeqId = evt.SeqId;
            }
            else if (!isHealthOnly)
            {
                LastHitParam = evt.Param;
                LastHitSeqId = evt.SeqId;
            }

            if (evt.Type == ActionType.Dead)
                ClearPostureBreakTracking();

            // 即使格挡伤害为零，观察者也应看到被命中者的世界血条。
            _healthBarView?.ShowForHit();

            // Host/Server 端 CombatResolver 已直接应用伤害，跳过以防二次扣血。
            if (NetworkServer.active)
                return;

            // 使用绝对生命值纠正累计误差，revision 会拒绝乱序旧结果。
            // ExecutionResultMsg is the sole HP commit for an active execution.
            // A generic combat result can cross it on the wire; keep the event
            // for presentation, but do not let it become a second health writer.
            if (_combatActor != null && evt.HasHealthResult != 0 &&
                !_hasExecutionSession)
            {
                _combatActor.ApplyAuthoritativeHealth(
                    evt.CurrentHp,
                    evt.MaxHp,
                    evt.HealthRevision);
            }

            if (isPostureBreak &&
                _playerController != null &&
                _networkIdentity != null &&
                _networkIdentity.isLocalPlayer)
            {
                _playerController.TryEnterPostureBroken();
            }
            else if (!isGuard && !isHealthOnly &&
                !_hasExecutionSession &&
                _playerController != null &&
                _networkIdentity != null &&
                _networkIdentity.isLocalPlayer)
            {
                CombatResolver.UnpackHitParam(
                    evt.Param,
                    out _,
                    out bool isHeavyHit,
                    out byte hitVariant);

                _playerController.ApplyRemoteHitReaction(
                    isHeavyHit,
                    hitVariant,
                    evt.Type == ActionType.Dead);
            }

            if (_logApply)
            {
                Debug.Log(
                    $"[RemoteActionApplier] combat result type={evt.Type}, " +
                    $"damage={evt.AppliedDamage:F1}, " +
                    $"hp={evt.CurrentHp:F1}/{evt.MaxHp:F1}, " +
                    $"revision={evt.HealthRevision}");
            }
        }

        // ============ 快照状态协调 ============

        /// <summary>
        /// 崩防动作可能先于快照到达，在观察到对应快照前锁存状态，避免中间普通快照让动画提前退出。
        /// </summary>
        private void ApplyServerParried(ActionEvent evt)
        {
            if (evt.SeqId <= _lastServerCombatSeqId)
                return;

            _lastServerCombatSeqId = evt.SeqId;
            LastAppliedTick = evt.Tick;
            CurrentRemoteAction = ActionType.Parried;
            RegisterParriedEdge(evt.SeqId);

            // The server event is authoritative for an owned Player as well.  Enter
            // the local FSM immediately so stale Attack input/snapshots cannot win.
            _combatActor?.CancelCurrentAttack();

            if (_playerController != null &&
                _networkIdentity != null &&
                _networkIdentity.isLocalPlayer &&
                _playerController.CurrentStateId != CharacterStateId.Parried)
            {
                _playerController.TryEnterParried();
            }
        }

        public CharacterStateId ResolveSnapshotState(CharacterStateId snapshotState)
        {
            if (_hasExecutionSession)
            {
                if (!_executionAwaitingCompletionSnapshot)
                    return _executionStateId;

                bool resultReady =
                    _executionCompletionStateId != CharacterStateId.Dead ||
                    _combatActor == null ||
                    _combatActor.IsDead;

                if (snapshotState == _executionCompletionStateId &&
                    resultReady)
                {
                    RetireCompletedExecution();
                    return snapshotState;
                }

                // The completion edge wins over stale Executing/Executed
                // snapshots until the authoritative terminal state arrives.
                return _executionCompletionStateId;
            }

            if (snapshotState == CharacterStateId.Dead)
            {
                ClearPostureBreakTracking();
                ClearParryTracking();
                ClearParriedTracking();
                return CharacterStateId.Dead;
            }

            if (_executionTerminalLatched &&
                snapshotState == CharacterStateId.Executed)
            {
                return _executionTerminalStateId is
                    CharacterStateId.Dead or CharacterStateId.Idle
                    ? _executionTerminalStateId
                    : CharacterStateId.Idle;
            }

            if (snapshotState is
                    CharacterStateId.Executing or
                    CharacterStateId.Executed)
            {
                RequestExecutionStateRecovery();
            }

            if (snapshotState == CharacterStateId.Parried)
            {
                ClearParryTracking();
                _parriedAwaitingSnapshot = false;
                _parriedSnapshotObserved = true;
                return CharacterStateId.Parried;
            }

            if (_parriedAwaitingSnapshot && !_parriedSnapshotObserved)
                return CharacterStateId.Parried;

            if (_parriedSnapshotObserved)
                ClearParriedTracking();

            if (snapshotState == CharacterStateId.PostureBroken)
            {
                _postureBreakAwaitingSnapshot = false;
                _postureBreakSnapshotObserved = true;
                return CharacterStateId.PostureBroken;
            }

            if (_postureBreakAwaitingSnapshot && !_postureBreakSnapshotObserved)
                return CharacterStateId.PostureBroken;

            if (_postureBreakSnapshotObserved)
                ClearPostureBreakTracking();

            if (snapshotState == CharacterStateId.Parry)
            {
                _parryAwaitingSnapshot = false;
                _parrySnapshotObserved = true;
                return CharacterStateId.Parry;
            }

            if (_parryAwaitingSnapshot && !_parrySnapshotObserved)
                return CharacterStateId.Parry;

            if (_parrySnapshotObserved)
                ClearParryTracking();

            return snapshotState;
        }

        // ============ 格挡反应 ============

        /// <summary>
        /// 非权威旧格式格挡事件使用独立序列去重，兼容仍未携带生命结果的发送路径。
        /// </summary>
        private void ApplyGuardReaction(ActionEvent evt)
        {
            if (evt.SeqId <= LastGuardReactionSeqId)
            {
                if (_logApply) Debug.Log($"RemoteActionApplier: Ignore duplicate guard seqId {evt.SeqId}");
                return;
            }

            LastAppliedTick = evt.Tick;
            CurrentRemoteAction = evt.Type;
            LastGuardReaction = evt.Type == ActionType.GuardBreak
                ? GuardReactionType.Break
                : ToGuardReaction(evt.Param);
            LastGuardReactionSeqId = evt.SeqId;

            if (_logApply)
                Debug.Log($"[RemoteActionApplier] applied {evt}");
        }

        public bool TryConsumeGuardReaction(out GuardReactionType reaction)
        {
            reaction = LastGuardReaction;
            if (reaction == GuardReactionType.None)
                return false;

            if (LastGuardReactionSeqId <= _consumedGuardReactionSeqId)
                return false;

            _consumedGuardReactionSeqId = LastGuardReactionSeqId;
            return true;
        }

        // ============ 状态重置与边沿跟踪 ============

        /// <summary>
        /// 网络对象复用或重新绑定时必须清空全部序列水位，否则新生命周期的小序号会被当作旧消息丢弃。
        /// </summary>
        public void ResetState()
        {
            if (_hasExecutionSession)
            {
                _combatActor?.EndExecutionCombatSuppression(
                    _executionSession.ExecutionId);
            }

            LastAppliedSeqId = 0;
            LastAppliedTick = 0;
            CurrentRemoteAction = ActionType.None;
            LastDodgeMode = 0;
            LastHitParam = 0;
            LastHitSeqId = 0;
            LastPostureBreakSeqId = 0;
            LastParrySeqId = 0;
            LastParriedSeqId = 0;
            LastGuardReaction = GuardReactionType.None;
            LastGuardReactionSeqId = 0;
            _consumedGuardReactionSeqId = 0;
            _lastServerCombatSeqId = 0;
            _postureBreakAwaitingSnapshot = false;
            _postureBreakSnapshotObserved = false;
            _parryAwaitingSnapshot = false;
            _parrySnapshotObserved = false;
            _parriedAwaitingSnapshot = false;
            _parriedSnapshotObserved = false;
            _executionSession = default;
            _hasExecutionSession = false;
            _executionStateId = CharacterStateId.None;
            _executionDeathVariant = DeathPresentationVariant.Default;
            _executionTerminalLatched = false;
            _executionTerminalExecutionId = 0;
            _executionTerminalStateId = CharacterStateId.None;
            _executionStartVersion = 0;
            _lastExecutionResultId = 0;
            _lastExecutionResultRevision = 0;
            _pendingExecutionResult = default;
            _hasPendingExecutionResult = false;
            _lastExecutionCompleteId = 0;
            _lastExecutionCompleteFlags = ExecutionSessionFlags.None;
            _executionPreviousStateId = CharacterStateId.None;
            _executionAwaitingCompletionSnapshot = false;
            _executionCompletionStateId = CharacterStateId.None;
            _localExecutionStateEntered = false;
        }

        private int ResolveActorId()
        {
            if (_networkIdentity != null && _networkIdentity.netId != 0)
                return unchecked((int)_networkIdentity.netId);

            return _combatActor != null ? _combatActor.ActorId : 0;
        }

        private void AdvanceExecutionStartVersion()
        {
            _executionStartVersion =
                _executionStartVersion == int.MaxValue
                    ? 1
                    : _executionStartVersion + 1;
        }

        private CharacterStateId ResolveExecutionCompletionState(
            int actorId,
            in ExecutionSession session)
        {
            if (!session.IsCancelled)
            {
                return actorId == session.TargetActorId &&
                       session.TargetWillDie
                    ? CharacterStateId.Dead
                    : CharacterStateId.Idle;
            }

            // Cancellation after a committed lethal result cannot revive the target.
            if (actorId == session.TargetActorId &&
                session.TargetWillDie &&
                session.IsResultCommitted)
            {
                return CharacterStateId.Dead;
            }

            bool validPreviousState = actorId == session.ExecutorActorId
                ? _executionPreviousStateId is
                    CharacterStateId.Idle or CharacterStateId.Move
                : _executionPreviousStateId is
                    CharacterStateId.Parried or CharacterStateId.PostureBroken;

            return validPreviousState
                ? _executionPreviousStateId
                : CharacterStateId.Idle;
        }

        private void TryFinishCompletedExecution()
        {
            if (!_hasExecutionSession ||
                !_executionAwaitingCompletionSnapshot)
            {
                return;
            }

            int actorId = ResolveActorId();
            ulong executionId = _executionSession.ExecutionId;

            // Host/server already owns the authoritative FSM and has completed
            // the state transition before broadcasting this edge. It will not
            // receive a separate terminal snapshot, so release the local replay
            // state immediately.
            if (NetworkServer.active)
            {
                _combatActor?.EndExecutionCombatSuppression(executionId);
                RetireCompletedExecution();
                return;
            }

            bool waitingForLethalResult =
                actorId == _executionSession.TargetActorId &&
                _executionCompletionStateId == CharacterStateId.Dead &&
                (_combatActor == null || !_combatActor.IsDead);

            if (waitingForLethalResult)
                return;

            bool changed = true;
            bool ownsLocalFsm =
                _networkIdentity != null &&
                _networkIdentity.isLocalPlayer &&
                _playerController != null;

            if (ownsLocalFsm && !_localExecutionStateEntered)
            {
                TryEnterLocalExecutionState();
                if (!_localExecutionStateEntered)
                    return;
            }

            if (ownsLocalFsm)
            {
                if (_executionSession.IsCancelled &&
                    _executionCompletionStateId != CharacterStateId.Dead)
                {
                    changed = _playerController.TryRollbackExecutionStart(
                        executionId,
                        _executionPreviousStateId);
                }
                else
                {
                    changed = actorId == _executionSession.ExecutorActorId
                        ? _playerController.TryCompleteExecuting(executionId)
                        : _playerController.TryCompleteExecuted(executionId);

                    if (!changed)
                    {
                        // A local target can receive the authoritative result
                        // before its stale FSM has entered Executed. Reconcile
                        // the terminal edge from the authoritative health/state
                        // rather than retaining a session that blocks the next
                        // execution.
                        if (actorId == _executionSession.TargetActorId &&
                            _executionCompletionStateId ==
                                CharacterStateId.Dead &&
                            _combatActor != null &&
                            _combatActor.IsDead)
                        {
                            changed = _playerController.TryEnterDead(
                                _executionDeathVariant);
                        }
                        else if (_executionCompletionStateId ==
                                     CharacterStateId.Idle &&
                                 _playerController.CurrentStateId is
                                     CharacterStateId.Idle or
                                     CharacterStateId.Move)
                        {
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                _combatActor?.EndExecutionCombatSuppression(executionId);

                // Completion is authoritative for every observer. Remote NPCs
                // and non-owned Players have no local FSM transition to wait
                // for; retaining their replay session would reject the next
                // Start when a terminal snapshot is delayed or lost.
                RetireCompletedExecution();
            }
            else if (_logApply)
            {
                Debug.LogWarning(
                    $"[RemoteActionApplier] Failed to finish execution " +
                    $"execution={executionId} actor={actorId}");
            }
        }

        private void RetireCompletedExecution()
        {
            ulong executionId = _executionSession.ExecutionId;
            CharacterStateId terminalState = _executionCompletionStateId;
            bool keepExecutedDeath =
                _executionCompletionStateId == CharacterStateId.Dead &&
                _executionDeathVariant == DeathPresentationVariant.Executed;

            // Retire can be reached from snapshot reconciliation as well as
            // the normal completion edge. Always release the execution-owned
            // suppression before clearing the session id, otherwise a remote
            // NPC keeps its HurtBoxes disabled forever after the execution.
            if (executionId != 0)
                _combatActor?.EndExecutionCombatSuppression(executionId);

            _executionSession = default;
            _hasExecutionSession = false;
            _executionStateId = CharacterStateId.None;
            _executionPreviousStateId = CharacterStateId.None;
            _executionAwaitingCompletionSnapshot = false;
            _executionCompletionStateId = CharacterStateId.None;
            CurrentRemoteAction = ActionType.None;
            _executionStateRecoveryPending = false;
            _nextExecutionStateRecoveryTime = 0f;

            if (!keepExecutedDeath)
                _executionDeathVariant = DeathPresentationVariant.Default;

            _executionTerminalDead = keepExecutedDeath;
            _executionTerminalLatched = true;
            _executionTerminalExecutionId = executionId;
            _executionTerminalStateId = terminalState is
                CharacterStateId.Dead or CharacterStateId.Idle
                ? terminalState
                : CharacterStateId.Idle;
            _localExecutionStateEntered = false;
        }

        private void TryEnterLocalExecutionState()
        {
            if (NetworkServer.active ||
                !_hasExecutionSession ||
                _networkIdentity == null ||
                !_networkIdentity.isLocalPlayer ||
                _playerController == null)
            {
                return;
            }

            if (_executionStateId == CharacterStateId.Executing)
            {
                if (_playerController.CurrentStateId == CharacterStateId.Executing)
                {
                    if (_localExecutionStateEntered)
                        return;

                    _localExecutionStateEntered = true;
                    PresentLocalExecutionState();
                    return;
                }

                if (_playerController.TryEnterExecutingFromAuthoritative(_executionSession))
                {
                    _localExecutionStateEntered = true;
                    PresentLocalExecutionState();
                }
                return;
            }

            if (_executionStateId == CharacterStateId.Executed &&
                _playerController.CurrentStateId == CharacterStateId.Executed)
            {
                if (_localExecutionStateEntered)
                    return;

                _localExecutionStateEntered = true;
                PresentLocalExecutionState();
                return;
            }

            if (_executionStateId == CharacterStateId.Executed &&
                _playerController.TryEnterExecutedFromAuthoritative(
                    _executionSession))
            {
                _localExecutionStateEntered = true;
                PresentLocalExecutionState();
            }
        }

        private void PresentLocalExecutionState()
        {
            if (_lateUpdatePipeline == null)
                _lateUpdatePipeline = GetComponent<CharacterLateUpdatePipeline>();

            _lateUpdatePipeline?.TickLateUpdate();
        }

        private void RequestExecutionStateRecovery()
        {
            if (NetworkServer.active ||
                _networkIdentity == null ||
                !NetworkClient.active ||
                !NetworkClient.isConnected ||
                !NetworkClient.ready ||
                _executionStateRecoveryPending ||
                Time.unscaledTime < _nextExecutionStateRecoveryTime)
            {
                return;
            }

            if (MirrorSyncTransport.TrySendExecutionStateRequest(
                    ResolveActorId(),
                    out _))
            {
                _executionStateRecoveryPending = true;
                _nextExecutionStateRecoveryTime =
                    Time.unscaledTime + ExecutionStateRecoveryRetryDelay;
            }
        }

        private void RegisterPostureBreakEdge(int seqId)
        {
            // CombatResolver 与 NPC publisher 可能描述同一次进入，只锁存第一个边沿。
            if (_postureBreakAwaitingSnapshot || _postureBreakSnapshotObserved)
                return;

            LastPostureBreakSeqId = seqId;
            _postureBreakAwaitingSnapshot = true;
            _postureBreakSnapshotObserved = false;
        }

        private void ClearPostureBreakTracking()
        {
            _postureBreakAwaitingSnapshot = false;
            _postureBreakSnapshotObserved = false;
        }

        private void RegisterParryEdge(int seqId)
        {
            if (seqId <= LastParrySeqId)
                return;

            LastParrySeqId = seqId;
            _parryAwaitingSnapshot = true;
            _parrySnapshotObserved = false;
        }

        private void ClearParryTracking()
        {
            _parryAwaitingSnapshot = false;
            _parrySnapshotObserved = false;
        }

        private void RegisterParriedEdge(int seqId)
        {
            if (seqId <= LastParriedSeqId)
                return;

            LastParriedSeqId = seqId;
            _parriedAwaitingSnapshot = true;
            _parriedSnapshotObserved = false;
        }

        private void ClearParriedTracking()
        {
            _parriedAwaitingSnapshot = false;
            _parriedSnapshotObserved = false;
        }

        private static GuardReactionType ToGuardReaction(int param)
        {
            GuardReactionType reaction = (GuardReactionType)param;
            return reaction is GuardReactionType.Hit1
                or GuardReactionType.Hit2
                or GuardReactionType.Hit3
                or GuardReactionType.Break
                ? reaction
                : GuardReactionType.Hit1;
        }
    }
}
