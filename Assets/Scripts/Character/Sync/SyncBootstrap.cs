using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// 统一管理本地发布 -> 假网络 -> 远端接收的事件接线
    /// </summary>
    public sealed class SyncBootstrap : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private LocalSyncPublisher _publisher;

        [Header("Pipe")]
        [SerializeField] private FakeNetworkPipe _pipe;

        [Header("Remote")]
        [SerializeField] private RemoteSnapshotBuffer _remoteSnapshotBuffer;
        [SerializeField] private RemoteActionApplier _remoteActionApplier; // 可先留空

        [Header("Debug")]
        [SerializeField] private bool _logWireUp = false;

        private void Awake()
        {
            // 可选自动找，推荐Inspector手拖更稳定
            if (_publisher == null) _publisher = FindFirstObjectByType<LocalSyncPublisher>();
            if (_pipe == null) _pipe = FindFirstObjectByType<FakeNetworkPipe>();
            if (_remoteSnapshotBuffer == null) _remoteSnapshotBuffer = FindFirstObjectByType<RemoteSnapshotBuffer>();
            // _remoteActionApplier 可为空，后续接上
        }

        private void OnEnable()
        {
            if (!ValidateRefs()) return;

            // 本地发布 -> Pipe
            _publisher.OnSnapshotProduced += _pipe.EnqueueSnapshot;
            _publisher.OnActionEventProduced += _pipe.EnqueueActionEvent;

            // Pipe -> 远端
            _pipe.OnSnapshotReceived += _remoteSnapshotBuffer.Push;

            if (_remoteActionApplier != null)
                _pipe.OnActionEventReceived += _remoteActionApplier.Apply;

            if (_logWireUp) Debug.Log("[SyncBootstrap] Wire up done.");
        }

        private void OnDisable()
        {
            if (_publisher == null || _pipe == null) return;

            // 反订阅，防止重复订阅/内存泄漏
            _publisher.OnSnapshotProduced -= _pipe.EnqueueSnapshot;
            _publisher.OnActionEventProduced -= _pipe.EnqueueActionEvent;

            if (_remoteSnapshotBuffer != null)
                _pipe.OnSnapshotReceived -= _remoteSnapshotBuffer.Push;

            if (_remoteActionApplier != null)
                _pipe.OnActionEventReceived -= _remoteActionApplier.Apply;

            if (_logWireUp) Debug.Log("[SyncBootstrap] Wire down done.");
        }

        private bool ValidateRefs()
        {
            if (_publisher == null)
            {
                Debug.LogError("[SyncBootstrap] Missing LocalSyncPublisher.");
                return false;
            }

            if (_pipe == null)
            {
                Debug.LogError("[SyncBootstrap] Missing FakeNetworkPipe.");
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