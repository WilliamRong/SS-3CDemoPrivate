using System;
using System.Collections.Generic;
using Mirror;
using Character.Combat;
using Character.Controller;
using Character.StateMachine;
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
        public byte DodgeMode;
        public byte GuardPhase;
        public byte IdlePhase;
        public byte AttackComboStep;
        public byte LockOnActive;
        public uint LockTargetNetId;
        public float MoveInputX;
        public float MoveInputY;
        public byte HasAuthoritativeHealth;
        public float CurrentHp;
        public float MaxHp;
        public uint HealthRevision;
        public byte HasAuthoritativePosture;
        public float CurrentPosture;
        public float MaxPosture;
    }

    public struct ActionMsg : NetworkMessage
    {
        public int SeqId;
        public int Tick;
        public int ActorId;
        public int Type;
        public int Param;

        public byte HasHealthResult;
        public float AppliedDamage;
        public float CurrentHp;
        public float MaxHp;
        public uint HealthRevision;
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
            if (conn?.identity == null || msg.ActorId <= 0)
                return;

            uint actorNetId = unchecked((uint)msg.ActorId);

            // Client 只能发布自己所属 Player 的快照。
            if (conn.identity.netId != actorNetId ||
                !NetworkServer.spawned.TryGetValue(
                    actorNetId,
                    out NetworkIdentity identity) ||
                identity != conn.identity)
            {
                Debug.LogWarning(
                    $"[MirrorTransport] Rejected snapshot actor={msg.ActorId} " +
                    $"from conn={conn.connectionId}.");
                return;
            }

            // Player 架势由 Server 上的 CombatActor 覆盖，不能信任 Client。
            CombatActor actor = identity.GetComponent<CombatActor>();
            msg.HasAuthoritativePosture =
                actor != null ? (byte)1 : (byte)0;
            msg.CurrentPosture =
                actor != null ? actor.CurrentPosture : 0f;
            msg.MaxPosture =
                actor != null ? actor.MaxPosture : 0f;

            // 破势由 Server FSM 决定。Client 只能上报普通表现状态，不能提前退出或伪造破势。
            PlayerController playerController = identity.GetComponent<PlayerController>();
            if (playerController != null)
            {
                CharacterStateId clientState = (CharacterStateId)msg.StateId;
                CharacterStateId serverState = playerController.CurrentStateId;

                if (serverState == CharacterStateId.PostureBroken ||
                    clientState == CharacterStateId.PostureBroken)
                {
                    msg.StateId = (int)serverState;

                    if (serverState == CharacterStateId.PostureBroken)
                    {
                        msg.Vx = 0f;
                        msg.Vz = 0f;
                        msg.MoveInputX = 0f;
                        msg.MoveInputY = 0f;
                    }
                }
            }

            if (_activeInstance != null && _activeInstance._logRelay)
                Debug.Log($"[MirrorTransport] RelaySnapshot tick={msg.Tick} from conn={conn.connectionId}");



            // Server 本机也要吃一份客户端快照，否则 Server 无法用远端快照判断 Guard/LockOn/Attack 等表现状态。
            if (_activeInstance != null)
            {
                StateSnapshot snapshot = FromMsg(msg);
                snapshot.ArrivalTimeSec = Time.unscaledTime;
                _activeInstance.OnSnapshotReceived?.Invoke(snapshot);
            }


            // 必须回发给原发送者，使所属 Client 收到 Server 架势纠正。
            // Host 已在上方以 Server 身份消费，不再走本地 Client 一次。
            foreach (KeyValuePair<int, NetworkConnectionToClient> kv in NetworkServer.connections)
            {
                NetworkConnectionToClient target = kv.Value;
                if (target == null || !target.isReady || target == NetworkServer.localConnection) continue;
                target.Send(msg);
            }
        }

        private static void OnServerAction(NetworkConnectionToClient conn, ActionMsg msg)
        {
            // 权威 HP 结果和破势边沿只能由 Server 战斗结算发布。
            if (msg.HasHealthResult != 0 ||
                msg.Type == (int)ActionType.PostureBreak ||
                msg.Type == (int)ActionType.HealthResult)
                return;

            if (_activeInstance != null && _activeInstance._logRelay)
                Debug.Log($"[MirrorTransport] RelayAction seq={msg.SeqId} from conn={conn.connectionId}");

            if (_activeInstance != null)
            {
                ActionEvent evt = FromMsg(msg);
                _activeInstance.OnActionEventReceived?.Invoke(evt);
            }

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
                SprintPhase = s.SprintPhase,
                DodgeMode = s.DodgeMode,
                GuardPhase = s.GuardPhase,
                IdlePhase = s.IdlePhase,
                AttackComboStep = s.AttackComboStep,
                LockOnActive = s.LockOnActive,
                LockTargetNetId = s.LockTargetNetId,
                MoveInputX = s.MoveInputX,
                MoveInputY = s.MoveInputY,
                HasAuthoritativeHealth = s.HasAuthoritativeHealth,
                CurrentHp = s.CurrentHp,
                MaxHp = s.MaxHp,
                HealthRevision = s.HealthRevision,
                HasAuthoritativePosture = s.HasAuthoritativePosture,
                CurrentPosture = s.CurrentPosture,
                MaxPosture = s.MaxPosture,
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
                m.SprintPhase,
                m.DodgeMode,
                m.GuardPhase,
                m.IdlePhase,
                m.AttackComboStep,
                m.LockOnActive,
                m.LockTargetNetId,
                m.MoveInputX,
                m.MoveInputY,
                m.HasAuthoritativeHealth,
                m.CurrentHp,
                m.MaxHp,
                m.HealthRevision,
                m.HasAuthoritativePosture,
                m.CurrentPosture,
                m.MaxPosture
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
                Param = e.Param,
                HasHealthResult = e.HasHealthResult,
                AppliedDamage = e.AppliedDamage,
                CurrentHp = e.CurrentHp,
                MaxHp = e.MaxHp,
                HealthRevision = e.HealthRevision,
            };
        }

        private static ActionEvent FromMsg(ActionMsg m)
        {
            return new ActionEvent(
                m.SeqId,
                m.Tick,
                m.ActorId,
                (ActionType)m.Type,
                m.Param,
                m.HasHealthResult,
                m.AppliedDamage,
                m.CurrentHp,
                m.MaxHp,
                m.HealthRevision
            );
        }
    }
}
