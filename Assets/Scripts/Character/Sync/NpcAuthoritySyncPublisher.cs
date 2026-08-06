using AI;
using AI.NpcStates;
using Character.Config;
using Character.Presentation;
using Character.StateMachine;
using Character.Combat;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// 只在服务器采样 NPC 的运动、状态与战斗数值，并将同一份权威结果广播给所有观察端。
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(NetworkIdentity))]
    public class NpcAuthoritySyncPublisher : NetworkBehaviour
    {
        [Header("Refs")]
        [SerializeField] private NetTickClock _clock;
        [SerializeField] private MirrorSyncTransport _transport;
        [SerializeField] private NpcCharacterDriver _npcDriver;
        [SerializeField] private NpcMotor _npcMotor;
        [SerializeField] private CombatActor _combatActor;

        private NetworkSyncConfig Sync => GameDataManager.Instance.NetworkSync;

        private bool _hasSentAnySnapshot;
        private Vector3 _lastSentPos;
        private float _lastSentYaw;

        // 动作事件按状态边沿去重，快照则按完整字段变化去重，两者缓存不可共用。
        private CharacterStateId _lastStateId = CharacterStateId.None;
        private int _lastStateEnterVersion;
        private CharacterStateId _lastSentStateId = CharacterStateId.None;
        private byte _lastSentSprintPhase;
        private byte _lastSentDodgeMode;
        private byte _lastSentGuardPhase;
        private byte _lastSentParryPhase;
        private byte _lastSentIdlePhase;
        private byte _lastSentAttackComboStep;

        [SerializeField, Min(1)]
        private int _healthCorrectionIntervalTicks = 10;

        private uint _lastSentHealthRevision;
        private int _lastSentSnapshotTick;

        private float _lastSentCurrentPosture;
        private float _lastSentMaxPosture;

        private int _nextSeqId = 1;

        // ============ Unity 与 Mirror 生命周期 ============

        private void Awake()
        {
            if (_npcDriver == null)
                _npcDriver = GetComponent<NpcCharacterDriver>();
            if (_npcMotor == null)
                _npcMotor = GetComponent<NpcMotor>();
            if (_combatActor == null)
                _combatActor = GetComponent<CombatActor>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_clock == null)
                _clock = FindFirstObjectByType<NetTickClock>();
            if (_transport == null)
                _transport = FindFirstObjectByType<MirrorSyncTransport>();
            Debug.Log($"[NpcAuthoritySyncPublisher] Server started netId={netId}", this);
            if (_transport == null)
                Debug.LogError("[NpcAuthoritySyncPublisher] MirrorSyncTransport not found in scene.", this);
            if (_clock == null)
                Debug.LogWarning("[NpcAuthoritySyncPublisher] NetTickClock not found; snapshots won't tick evenly.", this);
        }

        private void LateUpdate()
        {
            if (!isServer) return;
            if (_transport == null || _clock == null) return;

            int tickCount = _clock.TickCountThisFrame;
            for (int i = 0; i < tickCount; i++)
            {
                int tick = _clock.CurrentTick - (tickCount - 1 - i);
                TrySendSnapshot(tick);
            }

            TrySendActionOnStateChange(_clock.CurrentTick);
        }

        // ============ 权威快照发布 ============

        /// <summary>
        /// revision 变化立即发送，静态 NPC 仍按间隔发送绝对生命与架势，修复丢包造成的长期数值漂移。
        /// </summary>
        private void TrySendSnapshot(int tick)
        {
            Vector3 pos = transform.position;
            float yaw = transform.eulerAngles.y;

            Vector2 velocityXZ = ResolveVelocityXZ();
            CharacterStateId stateId = ResolveCurrentStateId();

            byte sprintPhase = ResolveSprintPhase(stateId);
            byte dodgeMode = ResolveDodgeMode(stateId);
            byte guardPhase = ResolveGuardPhase(stateId);
            byte parryPhase = ResolveParryPhase(stateId);
            byte idlePhase = ResolveIdlePhase(stateId);
            byte attackComboStep = ResolveAttackComboStep(stateId);
            velocityXZ = ResolveSnapshotVelocityXZ(stateId, velocityXZ);

            // 当前 NPC 尚无锁定来源，明确清零字段，避免序列化默认值被误认为有效目标。
            byte lockOnActive = 0;
            uint lockTargetNetId = 0;
            float moveInputX = 0f;
            float moveInputY = 0f;


            float currentPosture = _combatActor != null ? _combatActor.CurrentPosture : 0f;
            float maxPosture = _combatActor != null ? _combatActor.MaxPosture : 0f;
            bool postureChanged =
                !Mathf.Approximately(currentPosture, _lastSentCurrentPosture) ||
                !Mathf.Approximately(maxPosture, _lastSentMaxPosture);

            uint healthRevision = _combatActor != null ? _combatActor.HealthRevision : 0;

            bool healthCorrectionDue = _hasSentAnySnapshot && tick - _lastSentSnapshotTick >= _healthCorrectionIntervalTicks;


            bool shouldSend = !_hasSentAnySnapshot || healthCorrectionDue;

            if (!shouldSend)
            {
                float posDelta = (pos - _lastSentPos).sqrMagnitude;
                float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw));
                shouldSend = posDelta >= Sync.minPosDeltaToSend
                             || yawDelta >= Sync.minYawDeltaToSend
                             || stateId != _lastSentStateId
                             || sprintPhase != _lastSentSprintPhase
                             || dodgeMode != _lastSentDodgeMode
                             || guardPhase != _lastSentGuardPhase
                             || parryPhase != _lastSentParryPhase
                             || idlePhase != _lastSentIdlePhase
                             || attackComboStep != _lastSentAttackComboStep
                             || healthRevision != _lastSentHealthRevision
                             || postureChanged;
            }

            if (!shouldSend) return;

            var snapshot = new StateSnapshot(
                tick,
                (int)netId,
                pos,
                yaw,
                velocityXZ,
                stateId,
                sprintPhase,
                dodgeMode,
                guardPhase,
                idlePhase,
                attackComboStep,
                lockOnActive,
                lockTargetNetId,
                moveInputX,
                moveInputY,
                _combatActor != null ? (byte)1 : (byte)0,
                _combatActor != null ? _combatActor.CurrentHp : 0f,
                _combatActor != null ? _combatActor.MaxHp : 0f,
                healthRevision,
                _combatActor != null ? (byte)1 : (byte)0,
                currentPosture,
                maxPosture,
                parryPhase
            );

            _transport.BroadcastSnapshotFromServer(snapshot);

            _lastSentPos = pos;
            _lastSentYaw = yaw;
            _hasSentAnySnapshot = true;
            _lastSentStateId = stateId;
            _lastSentSprintPhase = sprintPhase;
            _lastSentDodgeMode = dodgeMode;
            _lastSentGuardPhase = guardPhase;
            _lastSentParryPhase = parryPhase;
            _lastSentIdlePhase = idlePhase;
            _lastSentAttackComboStep = attackComboStep;
            _lastSentHealthRevision = healthRevision;
            _lastSentSnapshotTick = tick;
            _lastSentCurrentPosture = currentPosture;
            _lastSentMaxPosture = maxPosture;
        }

        // ============ 快照字段解析 ============

        private Vector2 ResolveVelocityXZ()
        {
            var agent = _npcMotor != null ? _npcMotor.Agent : null;
            if (agent != null)
                return new Vector2(agent.velocity.x, agent.velocity.z);

            if (!_hasSentAnySnapshot)
                return Vector2.zero;

            float dt = Mathf.Max(_clock.TickInterval, 0.0001f);
            Vector3 dp = transform.position - _lastSentPos;
            return new Vector2(dp.x / dt, dp.z / dt);
        }

        private Vector2 ResolveSnapshotVelocityXZ(CharacterStateId stateId, Vector2 velocityXZ)
        {
            if (stateId != CharacterStateId.Dodge || _npcDriver == null ||
                !_npcDriver.TryGetDodgePresentationContext(out var ctx))
            {
                return velocityXZ;
            }

            return ctx.Mode == DodgeMode.LockOn8Way ? ctx.BlendLocal : velocityXZ;
        }

        private byte ResolveSprintPhase(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Sprint || _npcDriver == null) return 0;

            return _npcDriver.TryGetActiveSprintState(out var sprintState) ? (byte)sprintState.CurrentPhase : (byte)0;
        }

        private byte ResolveDodgeMode(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Dodge || _npcDriver == null)
                return 0;
            if (_npcDriver.TryGetDodgePresentationContext(out var ctx))
                return (byte)ctx.Mode;
            return _npcDriver.LastPreparedDodgeMode;
        }

        private byte ResolveGuardPhase(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Guard || _npcDriver == null)
                return 0;
            return _npcDriver.TryGetActiveGuardState(out var guardState)
                ? (byte)guardState.CurrentPhase
                : (byte)0;
        }

        private byte ResolveParryPhase(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Parry ||
                _npcDriver == null)
            {
                return 0;
            }

            return _npcDriver.TryGetActiveParryState(
                out NpcParryState parryState)
                ? (byte)parryState.CurrentPhase
                : (byte)0;
        }

        private byte ResolveIdlePhase(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Idle || _npcDriver == null)
                return 0;

            return _npcDriver.TryGetActiveIdleState(out var idleState)
                ? (byte)idleState.CurrentPhase
                : (byte)0;
        }

        private byte ResolveAttackComboStep(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Attack || _npcDriver == null)
                return 0;
            return _npcDriver.TryGetActiveAttackState(out var attackState)
                ? attackState.CurrentComboStep
                : (byte)1;
        }

        // ============ 动作事件发布 ============

        /// <summary>
        /// NPC 动作事件与 Player 使用同一映射和重入规则，保证接收端无需区分 Actor 类型。
        /// </summary>
        private void TrySendActionOnStateChange(int tick)
        {
            CharacterStateId current = ResolveCurrentStateId();
            int currentEnterVersion = ResolveStateEnterVersion();
            if (current == _lastStateId
                && !ShouldSendReenteredAction(current, currentEnterVersion))
            {
                return;
            }

            ActionType actionType = CharacterStateActionMapping.MapStateToActionType(current);
            if (actionType != ActionType.None)
            {
                int param = ResolveActionEventParam(current, actionType);
                var evt = new ActionEvent(_nextSeqId++, tick, (int)netId, actionType, param);
                _transport.BroadcastActionFromServer(evt);
            }

            _lastStateId = current;
            _lastStateEnterVersion = currentEnterVersion;
        }

        private bool ShouldSendReenteredAction(CharacterStateId stateId, int stateEnterVersion)
        {
            return stateId == CharacterStateId.Hit
                && stateEnterVersion != _lastStateEnterVersion;
        }

        private int ResolveActionEventParam(CharacterStateId stateId, ActionType actionType)
        {
            if (actionType == ActionType.DodgeStart) return ResolveDodgeMode(stateId);

            if (actionType == ActionType.Hit && _npcDriver != null && _npcDriver.TryGetActiveHitState(out var hitState))
            {
                return Combat.CombatResolver.PackHitParam(0f, hitState.IsHeavyHit, hitState.HitVariant);
            }

            return 0;
        }

        // ============ 状态查询 ============

        private CharacterStateId ResolveCurrentStateId()
        {
            if (_npcDriver != null)
                return _npcDriver.CurrentStateId;
            return CharacterStateId.Idle;
        }

        private int ResolveStateEnterVersion()
        {
            return _npcDriver != null ? _npcDriver.StateEnterVersion : 0;
        }
    }
}
