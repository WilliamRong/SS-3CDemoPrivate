using System;
using Character.Config;
using Character.Controller;
using Character.Presentation;
using Character.StateMachine;
using Character.LockOn;
using Character.Combat;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// 从本地权威 Player 采样快照和状态边沿，并用变化阈值抑制无意义网络发送。
    /// </summary>
    public sealed class LocalSyncPublisher : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private NetTickClock _clock;
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private PlayerAuthorityGate _authorityGate;
        [SerializeField] private CombatActor _combatActor;
        [SerializeField] private int _actorId = 1;
        [SerializeField] private NetworkIdentity _networkIdentity;

        // 锁定字段单独缓存，避免静止锁定时因输入微小噪声持续发包。
        [SerializeField] private PlayerLockOnController _lockOnController;
        private byte _lastSentLockOnActive;
        private uint _lastSentLockTargetNetId;
        private Vector2 _lastSentMoveInput;
        private const float MoveInputSendThresholdSqr = 0.01f;

        private const float VelocitySendThresholdSqr = 0.01f;

        [SerializeField, Min(1)]
        private int _postureCorrectionIntervalTicks = 10;

        public event Action<StateSnapshot> OnSnapshotProduced;
        public event Action<ActionEvent> OnActionEventProduced;

        private NetworkSyncConfig Sync => GameDataManager.Instance.NetworkSync;

        private int _nextSeqId = 1;

        private Vector3 _lastSentPos;
        private float _lastSentYaw;
        private bool _hasSentAnySnapshot;

        private CharacterStateId _lastStateId = CharacterStateId.None;
        private int _lastStateEnterVersion;
        private CharacterStateId _lastSentStateId = CharacterStateId.None;
        private byte _lastSentSprintPhase;
        private byte _lastSentDodgeMode;
        private byte _lastSentGuardPhase;
        private byte _lastSentIdlePhase;
        private byte _lastSentAttackComboStep;

        private Vector2 _lastSentVelocityXZ;
        private int _lastSentSnapshotTick;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            if (_clock == null) _clock = FindFirstObjectByType<NetTickClock>();
            if (_playerController == null) _playerController = GetComponent<PlayerController>();
            if (_authorityGate == null) _authorityGate = GetComponent<PlayerAuthorityGate>();
            if (_networkIdentity == null) _networkIdentity = GetComponent<NetworkIdentity>();
            if (_lockOnController == null) _lockOnController = GetComponent<PlayerLockOnController>();
            if (_combatActor == null) _combatActor = GetComponent<CombatActor>();
        }

        private void Update()
        {
            if (_clock == null || _playerController == null) return;
            if (!CanPublishFromThisInstance()) return;

            int tickCount = _clock.TickCountThisFrame;
            for (int i = 0; i < tickCount; i++)
            {
                TryProduceSnapshot(_clock.CurrentTick - (tickCount - 1 - i));
            }

            TryProduceActionEventOnStateChange(_clock.CurrentTick);
        }

        // ============ 发布权威 ============

        private bool CanPublishFromThisInstance()
        {
            if (_authorityGate == null) return true;
            return _authorityGate.CanProcessLocalInput;
        }

        // ============ 快照发布 ============

        /// <summary>
        /// 仅在运动、状态、锁定或定期架势纠正需要时发送，降低带宽同时保留最终一致性。
        /// </summary>
        private void TryProduceSnapshot(int tick)
        {
            Vector3 pos = transform.position;
            float yaw = transform.eulerAngles.y;

            Vector2 velocityXZ = Vector2.zero;
            if (_hasSentAnySnapshot)
            {
                int elapsedTicks = Mathf.Max(1, tick - _lastSentSnapshotTick);
                float dt = Mathf.Max(_clock.TickInterval * elapsedTicks, 0.0001f);
                Vector3 dp = pos - _lastSentPos;
                velocityXZ = new Vector2(dp.x / dt, dp.z / dt);
            }

            CharacterStateId stateId = _playerController.CurrentStateId;
            byte sprintPhase = ResolveSprintPhase(stateId);
            byte dodgeMode = ResolveDodgeMode(stateId);
            byte guardPhase = ResolveGuardPhase(stateId);
            byte idlePhase = ResolveIdlePhase(stateId);
            byte attackComboStep = ResolveAttackComboStep(stateId);
            velocityXZ = ResolveSnapshotVelocityXZ(stateId, velocityXZ);

            ResolveLockOnSync(out byte lockOnActive, out uint lockTargetNetId, out Vector2 moveInput);

            bool postureCorrectionDue = _hasSentAnySnapshot && tick - _lastSentSnapshotTick >= Mathf.Max(1, _postureCorrectionIntervalTicks);

            bool shouldSend = !_hasSentAnySnapshot || postureCorrectionDue;
            if (!shouldSend)
            {
                float posDelta = (pos - _lastSentPos).sqrMagnitude;
                float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw));

                shouldSend = posDelta >= Sync.minPosDeltaToSend
                    || yawDelta >= Sync.minYawDeltaToSend
                    || (velocityXZ - _lastSentVelocityXZ).sqrMagnitude >= VelocitySendThresholdSqr
                    || stateId != _lastSentStateId
                    || sprintPhase != _lastSentSprintPhase
                    || dodgeMode != _lastSentDodgeMode
                    || guardPhase != _lastSentGuardPhase
                    || idlePhase != _lastSentIdlePhase
                    || attackComboStep != _lastSentAttackComboStep
                    || lockOnActive != _lastSentLockOnActive
                    || lockTargetNetId != _lastSentLockTargetNetId
                    || (moveInput - _lastSentMoveInput).sqrMagnitude >= MoveInputSendThresholdSqr;
            }

            if (!shouldSend)
                return;


            bool hasAuthoritativePosture = _combatActor != null && (NetworkServer.active || !NetworkClient.active);

            var snapshot = new StateSnapshot(
                tick,
                ResolveActorId(),
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
                moveInput.x,
                moveInput.y,
                hasAuthoritativePosture:
                hasAuthoritativePosture ? (byte)1 : (byte)0,
                currentPosture: _combatActor != null ? _combatActor.CurrentPosture : 0f,
                maxPosture: _combatActor != null ? _combatActor.MaxPosture : 0f
            );

            OnSnapshotProduced?.Invoke(snapshot);

            _lastSentPos = pos;
            _lastSentYaw = yaw;
            _hasSentAnySnapshot = true;
            _lastSentStateId = stateId;
            _lastSentSprintPhase = sprintPhase;
            _lastSentDodgeMode = dodgeMode;
            _lastSentGuardPhase = guardPhase;
            _lastSentIdlePhase = idlePhase;
            _lastSentAttackComboStep = attackComboStep;
            _lastSentLockOnActive = lockOnActive;
            _lastSentLockTargetNetId = lockTargetNetId;
            _lastSentMoveInput = moveInput;
            _lastSentVelocityXZ = velocityXZ;
            _lastSentSnapshotTick = tick;
        }

        // ============ 快照字段解析 ============

        private Vector2 ResolveSnapshotVelocityXZ(CharacterStateId stateId, Vector2 computedVelocityXZ)
        {
            if (stateId != CharacterStateId.Dodge
                || !_playerController.TryGetDodgePresentationContext(out var ctx))
            {
                return computedVelocityXZ;
            }

            return ctx.Mode == DodgeMode.LockOn8Way ? ctx.BlendLocal : computedVelocityXZ;
        }

        private byte ResolveDodgeMode(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Dodge)
                return 0;

            if (_playerController.TryGetDodgePresentationContext(out var ctx))
                return (byte)ctx.Mode;

            return _playerController.LastPreparedDodgeMode;
        }

        private int ResolveActionEventParam(CharacterStateId stateId, ActionType actionType)
        {
            if (actionType == ActionType.Hit
                && _playerController != null
                && _playerController.TryGetActiveHitState(out var hitState))
            {
                return Combat.CombatResolver.PackHitParam(0f, hitState.IsHeavyHit, hitState.HitVariant);
            }

            if (actionType != ActionType.DodgeStart)
                return 0;

            return ResolveDodgeMode(stateId);
        }

        private static byte ResolveSprintPhase(CharacterStateId stateId, PlayerController player)
        {
            if (stateId != CharacterStateId.Sprint || player == null)
                return 0;

            return player.TryGetActiveSprintState(out var sprintState)
                ? (byte)sprintState.CurrentPhase
                : (byte)0;
        }

        private byte ResolveSprintPhase(CharacterStateId stateId)
        {
            return ResolveSprintPhase(stateId, _playerController);
        }

        private byte ResolveGuardPhase(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Guard || _playerController == null)
                return 0;

            return _playerController.TryGetActiveGuardState(out var guardState)
                ? (byte)guardState.CurrentPhase
                : (byte)0;
        }

        private byte ResolveIdlePhase(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Idle || _playerController == null)
                return 0;

            return _playerController.TryGetActiveIdleState(out var idleState)
                ? (byte)idleState.CurrentPhase
                : (byte)0;
        }

        private byte ResolveAttackComboStep(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Attack || _playerController == null)
                return 0;

            return _playerController.TryGetActiveAttackState(out var attackState)
                ? attackState.CurrentComboStep
                : (byte)1;
        }

        // ============ 动作事件发布 ============

        /// <summary>
        /// 离散动作按状态进入边沿发布；Hit 额外比较进入版本，以支持连续受击重新进入同一状态。
        /// </summary>
        private void TryProduceActionEventOnStateChange(int tick)
        {
            CharacterStateId current = _playerController.CurrentStateId;
            int currentEnterVersion = _playerController.StateEnterVersion;
            if (current == _lastStateId
                && !ShouldPublishReenteredAction(current, currentEnterVersion))
            {
                return;
            }

            ActionType actionType = CharacterStateActionMapping.MapStateToActionType(current);
            if (actionType != ActionType.None)
            {
                int param = ResolveActionEventParam(current, actionType);
                var evt = new ActionEvent(_nextSeqId++, tick, ResolveActorId(), actionType, param);
                OnActionEventProduced?.Invoke(evt);
            }

            _lastStateId = current;
            _lastStateEnterVersion = currentEnterVersion;
        }

        private bool ShouldPublishReenteredAction(CharacterStateId stateId, int stateEnterVersion)
        {
            return stateId == CharacterStateId.Hit
                && stateEnterVersion != _lastStateEnterVersion;
        }

        // ============ Actor 与锁定身份 ============

        private int ResolveActorId()
        {
            if (_networkIdentity != null && _networkIdentity.netId != 0)
                return (int)_networkIdentity.netId;

            return _actorId;
        }

        private void ResolveLockOnSync(out byte lockOnActive, out uint lockTargetNetId, out Vector2 moveInput)
        {
            lockOnActive = 0;
            lockTargetNetId = 0;
            moveInput = Vector2.zero;

            if (_lockOnController == null || !_lockOnController.TryGetSyncState(out lockTargetNetId))
                return;

            lockOnActive = 1;
            moveInput = _playerController != null
                ? _playerController.LastMoveInput
                : Vector2.zero;
        }
    }
}
