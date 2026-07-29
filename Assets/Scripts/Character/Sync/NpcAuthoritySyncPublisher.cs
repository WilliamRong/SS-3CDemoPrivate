using AI;
using Character.Config;
using Character.Presentation;
using Character.StateMachine;
using Character.Combat;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
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

        // 事件去重：上一帧逻辑状态（发 ActionEvent）
        private CharacterStateId _lastStateId = CharacterStateId.None;
        private int _lastStateEnterVersion;
        // 快照去重：上次已发送的快照内容
        private CharacterStateId _lastSentStateId = CharacterStateId.None;
        private byte _lastSentSprintPhase;
        private byte _lastSentDodgeMode;
        private byte _lastSentGuardPhase;
        private byte _lastSentIdlePhase;
        private byte _lastSentAttackComboStep;

        [SerializeField, Min(1)]
        private int _healthCorrectionIntervalTicks = 10;

        private uint _lastSentHealthRevision;
        private int _lastSentSnapshotTick;

        private int _nextSeqId = 1;

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

        private void TrySendSnapshot(int tick)
        {
            Vector3 pos = transform.position;
            float yaw = transform.eulerAngles.y;

            Vector2 velocityXZ = ResolveVelocityXZ();
            CharacterStateId stateId = ResolveCurrentStateId();

            byte sprintPhase = ResolveSprintPhase(stateId);
            byte dodgeMode = ResolveDodgeMode(stateId);
            byte guardPhase = ResolveGuardPhase(stateId);
            byte idlePhase = ResolveIdlePhase(stateId);
            byte attackComboStep = ResolveAttackComboStep(stateId);
            velocityXZ = ResolveSnapshotVelocityXZ(stateId, velocityXZ);

            // NPC 当前无锁定：字段置 0；以后有 NpcLockOn 再补
            byte lockOnActive = 0;
            uint lockTargetNetId = 0;
            float moveInputX = 0f;
            float moveInputY = 0f;

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
                             || idlePhase != _lastSentIdlePhase
                             || attackComboStep != _lastSentAttackComboStep
                             || healthRevision != _lastSentHealthRevision;
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
                healthRevision
            );

            _transport.BroadcastSnapshotFromServer(snapshot);

            _lastSentPos = pos;
            _lastSentYaw = yaw;
            _hasSentAnySnapshot = true;
            _lastSentStateId = stateId;
            _lastSentSprintPhase = sprintPhase;
            _lastSentDodgeMode = dodgeMode;
            _lastSentGuardPhase = guardPhase;
            _lastSentIdlePhase = idlePhase;
            _lastSentAttackComboStep = attackComboStep;
            _lastSentHealthRevision = healthRevision;
            _lastSentSnapshotTick = tick;
        }


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
