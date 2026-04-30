using UnityEngine;
using Core;

namespace Character.Sync
{
    /// <summary>
    /// 统一管理本地发布 -> 假网络 -> 远端接收的事件接线
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

        [Header("Remote")]
        [SerializeField] private RemoteSnapshotBuffer _remoteSnapshotBuffer;
        [SerializeField] private RemoteActionApplier _remoteActionApplier; // 可先留空

        [Header("Debug")]
        [SerializeField] private bool _logWireUp = false;
        
        private bool _isWired;

        private void Awake()
        {
            ResolvePublisher();
            if (_remoteSnapshotBuffer == null) _remoteSnapshotBuffer = FindFirstObjectByType<RemoteSnapshotBuffer>();
            if (_remoteActionApplier == null) _remoteActionApplier = FindFirstObjectByType<RemoteActionApplier>();
            
            ResolveTransport();
        }

        private void OnEnable()
        {
            TryWireUp();
        }

        private void Update()
        {
            // 支持运行时对象（如网络生成的本地玩家）出现后自动补接线
            if (_isWired) return;
            TryWireUp();
        }

        private void OnDisable()
        {
            if (!_isWired || _publisher == null || _transport == null) return;

            // 反订阅，防止重复订阅/内存泄漏
            _publisher.OnSnapshotProduced -= _transport.SendSnapshot;
            _publisher.OnActionEventProduced -= _transport.SendActionEvent;

            if (_remoteSnapshotBuffer != null)
                _transport.OnSnapshotReceived -= _remoteSnapshotBuffer.Push;

            if (_remoteActionApplier != null)
                _transport.OnActionEventReceived -= _remoteActionApplier.Apply;

            _isWired = false;
            if (_logWireUp) Debug.Log("[SyncBootstrap] Wire down done.");
        }

        private void TryWireUp()
        {
            if (_publisher == null) ResolvePublisher();
            if (_remoteSnapshotBuffer == null) _remoteSnapshotBuffer = FindFirstObjectByType<RemoteSnapshotBuffer>();
            if (_remoteActionApplier == null) _remoteActionApplier = FindFirstObjectByType<RemoteActionApplier>();
            if(_transport == null) ResolveTransport();
            
            
            if (!ValidateRefs()) return;

            // 本地发布 -> Transport
            _publisher.OnSnapshotProduced += _transport.SendSnapshot;
            _publisher.OnActionEventProduced += _transport.SendActionEvent;

            // Transport -> 远端
            _transport.OnSnapshotReceived += _remoteSnapshotBuffer.Push;

            if (_remoteActionApplier != null)
                _transport.OnActionEventReceived += _remoteActionApplier.Apply;

            _isWired = true;
            if (_logWireUp) Debug.Log("[SyncBootstrap] Wire up done.");
        }

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

            if (_remoteSnapshotBuffer == null)
            {
                Debug.LogError("[SyncBootstrap] Missing RemoteSnapshotBuffer.");
                return false;
            }

            return true;
        }
    }
}