using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
    public struct SnapshotMsg : NetworkMessage
    {
        public int Tick;
        public int ActorId;
        public float Px, Py, Pz;
        public float Yaw;
        public float Vx, Vz;
        public int StateId;
        public byte SprintPhase;
    }

    public struct ActionMsg : NetworkMessage
    {
        public int SeqId;
        public int Tick;
        public int ActorId;
        public int Type;
        public int Param;
    }

    public sealed class MirrorSyncTransport : MonoBehaviour, ISyncTransport
    {
        [Header("Debug")]
        [SerializeField] private bool _logSend;
        [SerializeField] private bool _logReceive;
        [SerializeField] private bool _logRelay;

        public event Action<StateSnapshot> OnSnapshotReceived;
        public event Action<ActionEvent> OnActionEventReceived;

        private static MirrorSyncTransport _activeInstance;
        private static bool _handlersRegistered;

        private void OnEnable()
        {
            // NetSystem 中心化：同场景仅允许一个生效实例
            if (_activeInstance != null && _activeInstance != this)
            {
                Debug.LogWarning("[MirrorTransport] Duplicate instance detected. Disable current one.");
                enabled = false;
                return;
            }

            _activeInstance = this;
            RegisterHandlers();
        }

        private void OnDisable()
        {
            if (_activeInstance == this)
                _activeInstance = null;

            UnregisterHandlers();
        }

        public void SendSnapshot(StateSnapshot snapshot)
        {
            if (!NetworkClient.active) return;

            if (_logSend) Debug.Log($"[MirrorTransport] SendSnapshot tick={snapshot.Tick}");
            NetworkClient.Send(ToMsg(snapshot));
        }

        public void SendActionEvent(ActionEvent actionEvent)
        {
            if (!NetworkClient.active) return;

            if (_logSend) Debug.Log($"[MirrorTransport] SendAction seq={actionEvent.SeqId} type={actionEvent.Type}");
            NetworkClient.Send(ToMsg(actionEvent));
        }

        public void BroadcastSnapshotFromServer(StateSnapshot snapshot)
        {
            if (!NetworkServer.active) return;
            
            NetworkServer.SendToAll(ToMsg(snapshot));
        }

        public void BroadcastActionFromServer(ActionEvent actionEvent)
        {
            if (!NetworkServer.active) return;
            
            NetworkServer.SendToAll(ToMsg(actionEvent));
        }

        private static void RegisterHandlers()
        {
            if (_handlersRegistered) return;

            NetworkServer.RegisterHandler<SnapshotMsg>(OnServerSnapshot);
            NetworkServer.RegisterHandler<ActionMsg>(OnServerAction);
            NetworkClient.RegisterHandler<SnapshotMsg>(OnClientSnapshot);
            NetworkClient.RegisterHandler<ActionMsg>(OnClientAction);
            _handlersRegistered = true;
        }

        private static void UnregisterHandlers()
        {
            if (!_handlersRegistered) return;

            NetworkServer.UnregisterHandler<SnapshotMsg>();
            NetworkServer.UnregisterHandler<ActionMsg>();
            NetworkClient.UnregisterHandler<SnapshotMsg>();
            NetworkClient.UnregisterHandler<ActionMsg>();
            _handlersRegistered = false;
        }

        private static void OnServerSnapshot(NetworkConnectionToClient conn, SnapshotMsg msg)
        {
            if (_activeInstance != null && _activeInstance._logRelay)
                Debug.Log($"[MirrorTransport] RelaySnapshot tick={msg.Tick} from conn={conn.connectionId}");

            foreach (KeyValuePair<int, NetworkConnectionToClient> kv in NetworkServer.connections)
            {
                NetworkConnectionToClient target = kv.Value;
                if (target == null || target == conn) continue; // 不回发给发送者
                target.Send(msg);
            }
        }

        private static void OnServerAction(NetworkConnectionToClient conn, ActionMsg msg)
        {
            if (_activeInstance != null && _activeInstance._logRelay)
                Debug.Log($"[MirrorTransport] RelayAction seq={msg.SeqId} from conn={conn.connectionId}");

            foreach (KeyValuePair<int, NetworkConnectionToClient> kv in NetworkServer.connections)
            {
                NetworkConnectionToClient target = kv.Value;
                if (target == null || target == conn) continue;
                target.Send(msg);
            }
        }

        private static void OnClientSnapshot(SnapshotMsg msg)
        {
            if (_activeInstance == null) return;

            StateSnapshot snapshot = FromMsg(msg);
            snapshot.ArrivalTimeSec = Time.unscaledTime;
            if (_activeInstance._logReceive) Debug.Log($"[MirrorTransport] RecvSnapshot tick={snapshot.Tick}");
            _activeInstance.OnSnapshotReceived?.Invoke(snapshot);
        }

        private static void OnClientAction(ActionMsg msg)
        {
            if (_activeInstance == null) return;

            ActionEvent evt = FromMsg(msg);
            if (_activeInstance._logReceive) Debug.Log($"[MirrorTransport] RecvAction seq={evt.SeqId} type={evt.Type}");
            _activeInstance.OnActionEventReceived?.Invoke(evt);
        }

        private static SnapshotMsg ToMsg(StateSnapshot s)
        {
            return new SnapshotMsg
            {
                Tick = s.Tick,
                ActorId = s.ActorId,
                Px = s.Position.x,
                Py = s.Position.y,
                Pz = s.Position.z,
                Yaw = s.Yaw,
                Vx = s.VelocityXZ.x,
                Vz = s.VelocityXZ.y,
                StateId = (int)s.StateId,
                SprintPhase = s.SprintPhase
            };
        }

        private static StateSnapshot FromMsg(SnapshotMsg m)
        {
            return new StateSnapshot(
                m.Tick,
                m.ActorId,
                new Vector3(m.Px, m.Py, m.Pz),
                m.Yaw,
                new Vector2(m.Vx, m.Vz),
                (StateMachine.CharacterStateId)m.StateId,
                m.SprintPhase
            );
        }

        private static ActionMsg ToMsg(ActionEvent e)
        {
            return new ActionMsg
            {
                SeqId = e.SeqId,
                Tick = e.Tick,
                ActorId = e.ActorId,
                Type = (int)e.Type,
                Param = e.Param
            };
        }

        private static ActionEvent FromMsg(ActionMsg m)
        {
            return new ActionEvent(
                m.SeqId,
                m.Tick,
                m.ActorId,
                (ActionType)m.Type,
                m.Param
            );
        }
    }
}
