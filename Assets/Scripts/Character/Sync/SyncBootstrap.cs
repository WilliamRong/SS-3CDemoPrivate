using UnityEngine;
using Core;

namespace Character.Sync
{
    /// <summary>
    /// 将发布器与所选 Transport 在运行时接线，使 Fake 和 Mirror 模式复用完全相同的消息消费路径。
    /// </summary>
    public sealed class SyncBootstrap : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private LocalSyncPublisher _publisher;

        [Header("Transport")]
        [SerializeField] private SyncTransportMode _transportMode = SyncTransportMode.Fake;
        [SerializeField] private FakeNetworkPipe _fakePipe;
        [SerializeField] private MirrorSyncTransport _mirrorTransport;
        private ISyncTransport _transport;

        [Header("Debug")]
        [SerializeField] private bool _logWireUp = false;
        
        private bool _isWired;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            ResolvePublisher();
            ResolveTransport();
        }

        private void OnEnable()
        {
            TryWireUp();
        }

        private void Update()
        {
            // 网络生成的本地玩家可能晚于 Bootstrap 出现，因此未接线期间持续低成本重试。
            if (_isWired) return;
            TryWireUp();
        }

        private void OnDisable()
        {
            if (!_isWired || _publisher == null || _transport == null) return;

            // 成对反订阅，防止重启对象后同一消息被重复分发。
            _publisher.OnSnapshotProduced -= _transport.SendSnapshot;
            _publisher.OnActionEventProduced -= _transport.SendActionEvent;

            _transport.OnSnapshotReceived -= HandleSnapshotReceived;
            _transport.OnActionEventReceived -= HandleActionReceived;
            _transport.OnExecutionStartReceived -= HandleExecutionStartReceived;
            _transport.OnExecutionResultReceived -= HandleExecutionResultReceived;

            _isWired = false;
            if (_logWireUp) Debug.Log("[SyncBootstrap] Wire down done.");
        }

        // ============ 事件接线 ============

        /// <summary>
        /// 只有发布器和 Transport 都已解析时才一次性订阅，避免部分接线留下难以清理的中间状态。
        /// </summary>
        private void TryWireUp()
        {
            if (_publisher == null) ResolvePublisher();
            if (_transport == null) ResolveTransport();

            if (!ValidateRefs()) return;

            // 本地发布 -> Transport
            _publisher.OnSnapshotProduced += _transport.SendSnapshot;
            _publisher.OnActionEventProduced += _transport.SendActionEvent;

            // Transport -> 所有远端表现组件（挂在 PlayerPrefab/NpcPrefab）
            _transport.OnSnapshotReceived += HandleSnapshotReceived;
            _transport.OnActionEventReceived += HandleActionReceived;
            _transport.OnExecutionStartReceived += HandleExecutionStartReceived;
            _transport.OnExecutionResultReceived += HandleExecutionResultReceived;

            _isWired = true;
            if (_logWireUp) Debug.Log("[SyncBootstrap] Wire up done.");
        }

        // ============ 依赖解析 ============

        /// <summary>
        /// Transport 仅在接线边界解析一次，发布器和消费者始终保持对具体网络实现无感知。
        /// </summary>
        private void ResolveTransport()
        {
            switch (_transportMode)
            {
                case SyncTransportMode.Fake:
                    if (_fakePipe == null) _fakePipe = FindFirstObjectByType<FakeNetworkPipe>();
                    _transport = _fakePipe;
                    break;
                case SyncTransportMode.Mirror:
                    if (_mirrorTransport == null) ResolveMirrorTransport();
                    _transport = _mirrorTransport;
                    break;
                default:
                    _transport = null;
                    break;
            }
        }

        /// <summary>
        /// 优先选择有本地输入权威的发布器，场景预置对象只作为离线兼容回退。
        /// </summary>
        private void ResolvePublisher()
        {
            if (_publisher != null) return;

            var allPublishers = FindObjectsByType<LocalSyncPublisher>(FindObjectsSortMode.None);
            for (int i = 0; i < allPublishers.Length; i++)
            {
                var gate = allPublishers[i].GetComponent<PlayerAuthorityGate>();
                if (gate != null && gate.CanProcessLocalInput)
                {
                    _publisher = allPublishers[i];
                    return;
                }
            }

            if (allPublishers.Length > 0)
                _publisher = allPublishers[0];
        }

        private void ResolveMirrorTransport()
        {
            if (_mirrorTransport != null) return;

            var allTransports = FindObjectsByType<MirrorSyncTransport>(FindObjectsSortMode.None);
            if (allTransports.Length > 0)
                _mirrorTransport = allTransports[0];
        }

        private bool ValidateRefs()
        {
            if (_publisher == null)
            {
                Debug.LogError("[SyncBootstrap] Missing LocalSyncPublisher.");
                return false;
            }

            if (_transport == null)
            {
                Debug.LogError($"[SyncBootstrap] Missing transport for mode {_transportMode}.");
                return false;
            }

            return true;
        }

        // ============ 消息分发 ============

        /// <summary>
        /// 每个 Buffer 自行校验 ActorId，Bootstrap 只做广播，从而支持运行时动态生成和销毁远端对象。
        /// </summary>
        private void HandleSnapshotReceived(StateSnapshot snapshot)
        {
            var buffers = FindObjectsByType<RemoteSnapshotBuffer>(FindObjectsSortMode.None);
            for (int i = 0; i < buffers.Length; i++)
            {
                buffers[i].Push(snapshot);
            }
        }

        private void HandleActionReceived(ActionEvent actionEvent)
        {
            var appliers = FindObjectsByType<RemoteActionApplier>(FindObjectsSortMode.None);
            for (int i = 0; i < appliers.Length; i++)
            {
                appliers[i].Apply(actionEvent);
            }
        }

        private void HandleExecutionStartReceived(
            ExecutionStartMsg message)
        {
            var appliers = FindObjectsByType<RemoteActionApplier>(
                FindObjectsSortMode.None);
            for (int i = 0; i < appliers.Length; i++)
            {
                appliers[i].ApplyExecutionStart(message);
            }
        }

        private void HandleExecutionResultReceived(
            ExecutionResultMsg message)
        {
            var appliers = FindObjectsByType<RemoteActionApplier>(
                FindObjectsSortMode.None);

            for (int i = 0; i < appliers.Length; i++)
            {
                appliers[i].ApplyExecutionResult(message);
            }
        }
    }
}
