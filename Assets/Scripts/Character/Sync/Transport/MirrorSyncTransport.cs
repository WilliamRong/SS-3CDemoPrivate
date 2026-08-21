using System;
using System.Collections.Generic;
using Mirror;
using Character.Config;
using Character.Execution;
using Character.Presentation;
using Core;
using Character.Combat;
using Character.Controller;
using Character.StateMachine;
using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// Mirror 传输结构保持为扁平字段，便于 Weaver 生成稳定序列化代码并显式审查协议布局。
    /// </summary>
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
        public byte ParryPhase;
    }

    /// <summary>
    /// 动作消息与快照分离，离散战斗反应不必等待下一次连续状态采样。
    /// </summary>
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

    /// <summary>
    /// 验证 Client 所属 Actor 后由 Server 中继消息，并在回发前覆盖服务器掌握的权威战斗字段。
    /// </summary>
    public sealed class MirrorSyncTransport : MonoBehaviour, ISyncTransport
    {
        [Header("Debug")]
        [SerializeField] private bool _logSend;
        [SerializeField] private bool _logReceive;
        [SerializeField] private bool _logRelay;

        private uint _nextExecutionRequestSeq = 1;
        private readonly Dictionary<NetworkConnectionToClient, uint> _lastExecutionRequestSeqByConnection = new();
        private ExecutionLifecycleService _boundExecutionLifecycle;

        public event Action<StateSnapshot> OnSnapshotReceived;
        public event Action<ActionEvent> OnActionEventReceived;
        public event Action<ExecutionStartMsg> OnExecutionStartReceived;
        public event Action<ExecutionResultMsg> OnExecutionResultReceived;

        private readonly HashSet<ulong> _receivedExecutionStartIds = new();

        private static MirrorSyncTransport _activeInstance;
        private static bool _handlersRegistered;

        // ============ Unity 生命周期 ============

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
            UnbindExecutionLifecycle();
            _lastExecutionRequestSeqByConnection.Clear();
            _receivedExecutionStartIds.Clear();
        }

        // ============ Client 发送 ============

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

        public static bool TrySendExecutionRequest(int targetActorId, out uint requestSeq)
        {
            requestSeq = 0;

            if (_activeInstance == null || !NetworkClient.active || !NetworkClient.ready || targetActorId <= 0)
            {
                return false;
            }

            requestSeq = _activeInstance.AllocateExecutionRequestSequence();

            NetworkClient.Send(new ExecutionRequestMsg
            {
                RequestSeq = requestSeq,
                TargetActorId = targetActorId,
            });

            if (_activeInstance._logSend)
            {
                Debug.Log(
             $"[MirrorTransport] SendExecutionRequest " +
             $"seq={requestSeq} target={targetActorId}");
            }

            return true;
        }

        private uint AllocateExecutionRequestSequence()
        {
            uint sequence = _nextExecutionRequestSeq;

            unchecked
            {
                _nextExecutionRequestSeq++;
            }

            if (_nextExecutionRequestSeq == 0) _nextExecutionRequestSeq = 1;

            return sequence;
        }

        // ============ Server 广播 ============

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

        private void BroadcastExecutionStart(uint requestSeq, in ExecutionSession session)
        {
            if (!NetworkServer.active || requestSeq == 0 || !session.TryValidate(out _))
            {
                return;
            }

            DeathPresentationVariant deathVariant = session.TargetWillDie ? DeathPresentationVariant.Executed : DeathPresentationVariant.Default;

            ExecutionStartMsg message = new ExecutionStartMsg
            {
                RequestSeq = requestSeq,

                ExecutionId = session.ExecutionId,
                ExecutorActorId = session.ExecutorActorId,
                TargetActorId = session.TargetActorId,

                FixedTargetPx = session.FixedTargetPose.Position.x,
                FixedTargetPy = session.FixedTargetPose.Position.y,
                FixedTargetPz = session.FixedTargetPose.Position.z,
                FixedTargetYaw = session.FixedTargetPose.Yaw,

                ExecutorAnchorPx = session.ExecutorAnchorPose.Position.x,
                ExecutorAnchorPy = session.ExecutorAnchorPose.Position.y,
                ExecutorAnchorPz = session.ExecutorAnchorPose.Position.z,
                ExecutorAnchorYaw = session.ExecutorAnchorPose.Yaw,

                StartTimeSec = session.StartTimeSec,
                ResultTimeSec = session.ResultTimeSec,

                ExecutionDamage = session.ExecutionDamage,
                TargetWillDie = session.TargetWillDie ? (byte)1 : (byte)0,
                DeathVariant = (byte)deathVariant,
                SessionFlags = (byte)session.Flags,
            };

            NetworkServer.SendToAll(message);

            if (_logRelay)
            {
                Debug.Log(
                    $"[MirrorTransport] BroadcastExecutionStart " +
                    $"execution={message.ExecutionId} " +
                    $"executor={message.ExecutorActorId} " +
                    $"target={message.TargetActorId} " +
                    $"start={message.StartTimeSec:F3} " +
                    $"result={message.ResultTimeSec:F3}");
            }
        }

        private void BindExecutionLifecycle(
            ExecutionLifecycleService lifecycle)
        {
            if (ReferenceEquals(_boundExecutionLifecycle, lifecycle))
                return;

            UnbindExecutionLifecycle();

            _boundExecutionLifecycle = lifecycle;
            if (_boundExecutionLifecycle != null)
            {
                _boundExecutionLifecycle.ResultCommitted +=
                    OnExecutionResultCommitted;
            }
        }

        private void UnbindExecutionLifecycle()
        {
            if (_boundExecutionLifecycle == null)
                return;

            _boundExecutionLifecycle.ResultCommitted -=
                OnExecutionResultCommitted;
            _boundExecutionLifecycle = null;
        }

        private void OnExecutionResultCommitted(
            ExecutionSession session,
            CombatActor target,
            double authorityTimeSec)
        {
            if (!NetworkServer.active ||
                target == null ||
                !session.IsResultCommitted ||
                target.ActorId != session.TargetActorId)
            {
                return;
            }

            var message = new ExecutionResultMsg
            {
                ExecutionId = session.ExecutionId,
                ExecutorActorId = session.ExecutorActorId,
                TargetActorId = session.TargetActorId,
                AuthorityTimeSec = authorityTimeSec,
                CurrentHp = target.CurrentHp,
                MaxHp = target.MaxHp,
                HealthRevision = target.HealthRevision,
                DeathVariant = (byte)(session.TargetWillDie
                    ? DeathPresentationVariant.Executed
                    : DeathPresentationVariant.Default),
            };

            NetworkServer.SendToAll(message);

            if (_logRelay)
            {
                Debug.Log(
                    $"[MirrorTransport] BroadcastExecutionResult " +
                    $"execution={message.ExecutionId} " +
                    $"hp={message.CurrentHp:F1}/{message.MaxHp:F1} " +
                    $"revision={message.HealthRevision}");
            }
        }

        // ============ Mirror Handler 生命周期 ============

        /// <summary>
        /// Handler 是 Mirror 全局注册项，因此通过静态标志保证场景重载和重复实例不会多次注册。
        /// </summary>
        private static void RegisterHandlers()
        {
            if (_handlersRegistered) return;

            NetworkServer.RegisterHandler<SnapshotMsg>(OnServerSnapshot);
            NetworkServer.RegisterHandler<ActionMsg>(OnServerAction);
            NetworkServer.RegisterHandler<ExecutionRequestMsg>(OnServerExecutionRequest);
            NetworkClient.RegisterHandler<SnapshotMsg>(OnClientSnapshot);
            NetworkClient.RegisterHandler<ActionMsg>(OnClientAction);
            NetworkClient.RegisterHandler<ExecutionStartMsg>(OnClientExecutionStart);
            NetworkClient.RegisterHandler<ExecutionResultMsg>(OnClientExecutionResult);
            _handlersRegistered = true;
        }

        private static void UnregisterHandlers()
        {
            if (!_handlersRegistered) return;

            NetworkServer.UnregisterHandler<SnapshotMsg>();
            NetworkServer.UnregisterHandler<ActionMsg>();
            NetworkServer.UnregisterHandler<ExecutionRequestMsg>();
            NetworkClient.UnregisterHandler<SnapshotMsg>();
            NetworkClient.UnregisterHandler<ActionMsg>();
            NetworkClient.UnregisterHandler<ExecutionStartMsg>();
            NetworkClient.UnregisterHandler<ExecutionResultMsg>();
            _handlersRegistered = false;
        }

        // ============ Server 校验与中继 ============

        /// <summary>
        /// 连接只能发布自己拥有的 Player；服务器再覆盖架势和崩防状态，阻断 Client 伪造权威字段。
        /// </summary>
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

            CharacterStateId clientState = (CharacterStateId)msg.StateId;
            if (clientState != CharacterStateId.Parry ||
                msg.ParryPhase < (byte)Character.StateMachine.States.ParryPhase.Startup ||
                msg.ParryPhase > (byte)Character.StateMachine.States.ParryPhase.Recovery)
            {
                msg.ParryPhase = 0;
            }

            // 破势由 Server FSM 决定。Client 只能上报普通表现状态，不能提前退出或伪造破势。
            PlayerController playerController = identity.GetComponent<PlayerController>();
            if (playerController != null)
            {
                CharacterStateId serverState = playerController.CurrentStateId;

                if (serverState is CharacterStateId.PostureBroken or CharacterStateId.Parried)
                {
                    msg.StateId = (int)serverState;
                    msg.ParryPhase = 0;
                    ZeroSnapshotMotion(ref msg);
                }
                else if (clientState is CharacterStateId.PostureBroken or CharacterStateId.Parried)
                {
                    // These states are server-only and cannot be forged or extended.
                    msg.StateId = (int)serverState;
                    msg.ParryPhase = 0;
                }
                else if (serverState == CharacterStateId.Parry)
                {
                    msg.StateId = (int)CharacterStateId.Parry;
                    msg.ParryPhase = (byte)(playerController.TryGetActiveParryState(out var serverParry)
                        ? serverParry.CurrentPhase
                        : Character.StateMachine.States.ParryPhase.None);
                    ZeroSnapshotMotion(ref msg);
                }
                else if (clientState == CharacterStateId.Parry)
                {
                    if (msg.ParryPhase == 0)
                    {
                        msg.StateId = (int)serverState;
                    }
                    else
                    {
                        // Remote Player Parry uses the latest ownership-validated
                        // client phase, matching the existing Guard trust boundary.
                        ZeroSnapshotMotion(ref msg);
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

        /// <summary>
        /// 含权威生命结果或崩防边沿的动作只能由服务器产生，Client 上报此类消息会被直接丢弃。
        /// </summary>
        private static void OnServerAction(NetworkConnectionToClient conn, ActionMsg msg)
        {
            if (conn?.identity == null || msg.ActorId != unchecked((int)conn.identity.netId))
                return;

            // 权威 HP 结果和破势边沿只能由 Server 战斗结算发布。
            if (msg.HasHealthResult != 0 ||
                msg.Type == (int)ActionType.PostureBreak ||
                msg.Type == (int)ActionType.HealthResult ||
                msg.Type == (int)ActionType.Parried)
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

        private static void ZeroSnapshotMotion(ref SnapshotMsg msg)
        {
            msg.Vx = 0f;
            msg.Vz = 0f;
            msg.MoveInputX = 0f;
            msg.MoveInputY = 0f;
        }


        private static void OnServerExecutionRequest(NetworkConnectionToClient conn, ExecutionRequestMsg msg)
        {
            MirrorSyncTransport transport = _activeInstance;

            if (!NetworkServer.active || transport == null || conn?.identity == null || !conn.isReady || msg.RequestSeq == 0 || msg.TargetActorId <= 0)
            {
                return;
            }

            NetworkIdentity executorIdentity = conn.identity;

            // 连接只能代表自己拥有的 Player。
            if (!NetworkServer.spawned.TryGetValue(executorIdentity.netId, out NetworkIdentity spawnedExecutor) || spawnedExecutor != executorIdentity)
            {
                return;
            }

            int executorActorId = unchecked((int)executorIdentity.netId);

            if (executorActorId <= 0 || executorActorId == msg.TargetActorId) return;

            if (!transport.TryAcceptExecutionRequestSequence(conn, msg.RequestSeq))
            {
                return;
            }

            CombatActor executor = executorIdentity.GetComponent<CombatActor>();

            if (executor == null || !executor.IsPlayerActor || executor.ActorId != executorActorId)
            {
                return;
            }

            uint targetNetId = unchecked((uint)msg.TargetActorId);

            if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
            {
                return;
            }

            CombatActor target = targetIdentity.GetComponent<CombatActor>();

            if (target == null || target.ActorId != msg.TargetActorId)
            {
                return;
            }

            GameDataManager data = GameDataManager.Instance;

            CharacterCombatConfig config = data != null && data.Player != null ? data.Player.combat : null;

            ExecutionRuntime runtime = ExecutionRuntime.Instance;

            if (config == null || runtime == null || !runtime.HasAuthority) return;

            transport.BindExecutionLifecycle(runtime.Lifecycle);

            if (!runtime.TryStart(executor, target, config,
                    out ExecutionSession session,
                    out ExecutionEligibilityResult eligibility,
                    out ExecutionSessionCreateFailure createFailure,
                    out ExecutionStartFailure startFailure))
            {
                if (transport._logRelay)
                {
                    Debug.Log(
                        $"[MirrorTransport] RejectExecutionRequest " +
                        $"seq={msg.RequestSeq} " +
                        $"executor={executorActorId} " +
                        $"target={msg.TargetActorId} " +
                        $"eligibility={eligibility.RejectionReason} " +
                        $"create={createFailure} start={startFailure}");
                }

                return;
            }

            if (transport._logRelay)
            {
                Debug.Log(
                    $"[MirrorTransport] AcceptExecutionRequest " +
                    $"seq={msg.RequestSeq} " +
                    $"execution={session.ExecutionId} " +
                    $"executor={session.ExecutorActorId} " +
                    $"target={session.TargetActorId}");
            }

            //在这里广播 ExecutionStartMsg
            transport.BroadcastExecutionStart(
                msg.RequestSeq,
                session);

        }

        private bool TryAcceptExecutionRequestSequence(NetworkConnectionToClient conn, uint requestSeq)
        {
            if (_lastExecutionRequestSeqByConnection.TryGetValue(conn, out uint previous))
            {
                //支持uint回绕，拒绝重复或迟到请求
                int delta = unchecked((int)(requestSeq - previous));

                if (delta <= 0) return false;
            }

            _lastExecutionRequestSeqByConnection[conn] = requestSeq;

            return true;
        }

        // ============ Client 接收 ============

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

        private static void OnClientExecutionStart(ExecutionStartMsg message)
        {
            MirrorSyncTransport transport = _activeInstance;

            if (transport == null || !transport.TryAcceptExecutionStart(message, out ExecutionSession session))
            {
                return;
            }

            if (transport._logReceive)
            {
                Debug.Log(
            $"[MirrorTransport] RecvExecutionStart " +
            $"execution={session.ExecutionId} " +
            $"executor={session.ExecutorActorId} " +
            $"target={session.TargetActorId} " +
            $"start={session.StartTimeSec:F3}");
            }

            transport.OnExecutionStartReceived?.Invoke(message);
        }

        private static void OnClientExecutionResult(
            ExecutionResultMsg message)
        {
            MirrorSyncTransport transport = _activeInstance;
            if (transport == null)
                return;

            if (transport._logReceive)
            {
                Debug.Log(
                    $"[MirrorTransport] RecvExecutionResult " +
                    $"execution={message.ExecutionId} " +
                    $"revision={message.HealthRevision}");
            }

            transport.OnExecutionResultReceived?.Invoke(message);
        }

        private bool TryAcceptExecutionStart(in ExecutionStartMsg message, out ExecutionSession session)
        {
            session = default;

            if (message.ExecutionId == 0 || message.ExecutorActorId <= 0 || message.TargetActorId <= 0 || message.ExecutorActorId == message.TargetActorId)
            {
                return false;
            }

            if (message.TargetWillDie > 1) return false;

            DeathPresentationVariant deathVariant = (DeathPresentationVariant)message.DeathVariant;

            if (deathVariant != DeathPresentationVariant.Default && deathVariant != DeathPresentationVariant.Executed) return false;

            bool targetWillDie = message.TargetWillDie != 0;

            if (targetWillDie && deathVariant != DeathPresentationVariant.Executed) return false;

            if (!targetWillDie && deathVariant != DeathPresentationVariant.Default) return false;

            ExecutionSessionFlags flags = (ExecutionSessionFlags)message.SessionFlags;

            ExecutionSessionFlags knownFlags =
                    ExecutionSessionFlags.ResultCommitted |
                    ExecutionSessionFlags.ExecutorCompleted |
                    ExecutionSessionFlags.TargetCompleted |
                    ExecutionSessionFlags.Cancelled;

            if ((flags & ~knownFlags) != ExecutionSessionFlags.None) return false;

            ExecutionPose fixedTargetPose = new ExecutionPose(new Vector3(message.FixedTargetPx, message.FixedTargetPy, message.FixedTargetPz), message.FixedTargetYaw);

            ExecutionPose executorAnchorPose =
            new ExecutionPose(
            new Vector3(
                message.ExecutorAnchorPx,
                message.ExecutorAnchorPy,
                message.ExecutorAnchorPz),
            message.ExecutorAnchorYaw);

            session = new ExecutionSession(
                message.ExecutionId,
                message.ExecutorActorId,
                message.TargetActorId,
                fixedTargetPose,
                executorAnchorPose,
                message.StartTimeSec,
                message.ResultTimeSec,
                message.ExecutionDamage,
                targetWillDie,
                flags);

            if (!session.TryValidate(out _))
            {
                session = default;
                return false;
            }

            // 同一个 executionId 的 Start 只能接受一次。
            if (!_receivedExecutionStartIds.Add(message.ExecutionId))
            {
                session = default;
                return false;
            }

            return true;

        }

        // ============ 协议对象转换 ============

        /// <summary>
        /// 业务值对象与 Mirror 消息显式逐字段转换，协议新增字段时编译审查点保持集中可见。
        /// </summary>
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
                ParryPhase = s.ParryPhase,
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
                m.MaxPosture,
                m.ParryPhase
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
