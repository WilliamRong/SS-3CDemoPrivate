using System;
using Character.Controller;
using Character.StateMachine;
using Mirror;
using Core;
using UnityEngine;

namespace Character.Sync
{
    public sealed class LocalSyncPublisher : MonoBehaviour
    {
        [Header("Refs")] 
        [SerializeField] private NetTickClock _clock;
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private PlayerAuthorityGate _authorityGate;
        [SerializeField] private int _actorId = 1;

        [Header("Snapshot")] 
        [SerializeField] private float _minPosDeltaToSend = 0.001f;
        [SerializeField] private float _minYawDeltaToSend = 0.1f;
        [SerializeField] private NetworkIdentity _networkIdentity;

        public event Action<StateSnapshot> OnSnapshotProduced;
        public event Action<ActionEvent> OnActionEventProduced;

        private int _nextSeqId = 1;

        private Vector3 _lastSentPos;
        private float _lastSentYaw;
        private bool _hasSentAnySnapshot;

        private CharacterStateId _lastStateId = CharacterStateId.None;
        private CharacterStateId _lastSentStateId = CharacterStateId.None;
        private byte _lastSentSprintPhase;

        private void Awake()
        {
            if (_clock == null) _clock = FindFirstObjectByType<NetTickClock>();
            if (_playerController == null) _playerController = GetComponent<PlayerController>();
            if (_authorityGate == null) _authorityGate = GetComponent<PlayerAuthorityGate>();
            if (_networkIdentity == null) _networkIdentity = GetComponent<NetworkIdentity>();
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

        private bool CanPublishFromThisInstance()
        {
            if (_authorityGate == null) return true;
            return _authorityGate.CanProcessLocalInput;
        }

        private void TryProduceSnapshot(int tick)
        {
            Vector3 pos = transform.position;
            float yaw = transform.eulerAngles.y;

            Vector2 velocityXZ = Vector2.zero;
            if (_hasSentAnySnapshot)
            {
                float dt = Mathf.Max(_clock.TickInterval, 0.0001f);
                Vector3 dp = pos - _lastSentPos;
                velocityXZ = new Vector2(dp.x / dt, dp.z / dt);
            }

            CharacterStateId stateId = _playerController.CurrentStateId;
            byte sprintPhase = ResolveSprintPhase(stateId);

            bool shouldSend = !_hasSentAnySnapshot;
            if (!shouldSend)
            {
                float posDelta = (pos - _lastSentPos).sqrMagnitude;
                float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw));
                shouldSend = posDelta >= _minPosDeltaToSend
                    || yawDelta >= _minYawDeltaToSend
                    || stateId != _lastSentStateId
                    || sprintPhase != _lastSentSprintPhase;
            }

            if (!shouldSend)
                return;

            var snapshot = new StateSnapshot(
                tick,
                ResolveActorId(),
                pos,
                yaw,
                velocityXZ,
                stateId,
                sprintPhase
            );

            OnSnapshotProduced?.Invoke(snapshot);

            _lastSentPos = pos;
            _lastSentYaw = yaw;
            _hasSentAnySnapshot = true;
            _lastSentStateId = stateId;
            _lastSentSprintPhase = sprintPhase;
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

        private void TryProduceActionEventOnStateChange(int tick)
        {
            CharacterStateId current = _playerController.CurrentStateId;
            if (current == _lastStateId) return;

            ActionType actionType = CharacterStateActionMapping.MapStateToActionType(current);
            if (actionType != ActionType.None)
            {
                var evt = new ActionEvent(_nextSeqId++, tick, ResolveActorId(), actionType);
                OnActionEventProduced?.Invoke(evt);
            }

            _lastStateId = current;
        }
        
        private int ResolveActorId()
        {
            if (_networkIdentity != null && _networkIdentity.netId != 0)
                return (int)_networkIdentity.netId;

            return _actorId;
        }
    }
}
