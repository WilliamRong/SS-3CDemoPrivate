using AI;
using Character.StateMachine;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
    //需要让NpcCharacterDriver 组件在NpcAuthoritySyncPublisher 之前运行
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(NetworkIdentity))]
    public class NpcAuthoritySyncPublisher : NetworkBehaviour
    {
        [Header("Refs")]
        [SerializeField] private NetTickClock _clock;
        [SerializeField] private MirrorSyncTransport _transport;
        [SerializeField] private NpcCharacterDriver _npcDriver;

        private bool _hasSentAnySnapshot;
        private Vector3 _lastSentPos;
        private float _lastSentYaw;
        private CharacterStateId _lastStateId = CharacterStateId.None;
        private int _nextSeqId = 1;

        private const float MinPosDelta = 0.001f;
        private const float MinYawDelta = 0.1f;

        private void Awake()
        {
            if (_npcDriver == null)
                _npcDriver = GetComponent<NpcCharacterDriver>();
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
            if(_transport == null || _clock == null) return;

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

            Vector2 velocityXZ = Vector2.zero;
            if (_hasSentAnySnapshot)
            {
                float dt = Mathf.Max(_clock.TickInterval, 0.0001f);
                Vector3 dp = pos - _lastSentPos;
                velocityXZ = new Vector2(dp.x / dt, dp.z / dt);
            }
            
            bool shouldSend = !_hasSentAnySnapshot;

            if (!shouldSend)
            {
                float posDelta = (pos - _lastSentPos).sqrMagnitude;
                float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw));
                shouldSend = posDelta > MinPosDelta || yawDelta > MinYawDelta;
            }

            if (!shouldSend) return;

            var snapshot = new StateSnapshot(
                tick,
                (int)netId,
                pos,
                yaw,
                velocityXZ,
                ResolveCurrentStateId()
            );
            
            _transport.BroadcastSnapshotFromServer(snapshot);

            _lastSentPos = pos;
            _lastSentYaw = yaw;
            _hasSentAnySnapshot = true;
        }

        private void TrySendActionOnStateChange(int tick)
        {
            CharacterStateId currentStateId = ResolveCurrentStateId();
            if (currentStateId == _lastStateId) return;

            ActionType actionType = CharacterStateActionMapping.MapStateToActionType(currentStateId);
            if (actionType != ActionType.None)
            {
                var evt = new ActionEvent(
                    _nextSeqId,
                    tick,
                    (int)netId,
                    actionType);
                _transport.BroadcastActionFromServer(evt);
                _nextSeqId++;
            }

            _lastStateId = currentStateId;
        }

        private CharacterStateId ResolveCurrentStateId()
        {
            if (_npcDriver != null)
                return _npcDriver.CurrentStateId;
            return CharacterStateId.Idle;
        }
    }
}
