using System;
using System.Collections.Generic;
using Character.Config;
using Core;
using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// 在不启动 Mirror 的场景中模拟延迟、抖动和丢包，验证同步算法而不改变发布器与接收器接口。
    /// </summary>
    public sealed class FakeNetworkPipe : MonoBehaviour, ISyncTransport
    {
        private NetworkSyncConfig Sync => GameDataManager.Instance.NetworkSync;

        public event Action<StateSnapshot> OnSnapshotReceived;
        public event Action<ActionEvent> OnActionEventReceived;

        /// <summary>
        /// 将载荷与本地投递时刻绑定，避免模拟层修改业务快照本身。
        /// </summary>
        private struct QueuedSnapshot
        {
            public float DeliverTime;
            public StateSnapshot Payload;
        }

        /// <summary>
        /// 动作与快照使用独立队列，才能模拟两类消息不同的乱序关系。
        /// </summary>
        private struct QueuedAction
        {
            public float DeliverTime;
            public ActionEvent Payload;
        }

        private readonly MinHeap<QueuedSnapshot> _snapshotQueue = new((a, b) => a.DeliverTime < b.DeliverTime);
        private readonly MinHeap<QueuedAction> _actionQueue = new((a, b) => a.DeliverTime < b.DeliverTime);

        // ============ 传输接口 ============

        public void SendSnapshot(StateSnapshot snapshot)
        {
            EnqueueSnapshot(snapshot);
        }

        public void SendActionEvent(ActionEvent actionEvent)
        {
            EnqueueActionEvent(actionEvent);
        }

        public void EnqueueSnapshot(StateSnapshot snapshot)
        {
            if (ShouldDrop()) return;

            float deliverTime = Time.time + ComputeDelaySeconds();
            _snapshotQueue.Push(new QueuedSnapshot { DeliverTime = deliverTime, Payload = snapshot });
        }

        public void EnqueueActionEvent(ActionEvent actionEvent)
        {
            if (ShouldDrop()) return;

            float deliverTime = Time.time + ComputeDelaySeconds();
            _actionQueue.Push(new QueuedAction { DeliverTime = deliverTime, Payload = actionEvent });
        }

        // ============ 延迟投递 ============

        private void Update()
        {
            float now = Time.time;

            while (_snapshotQueue.Count > 0 && _snapshotQueue.Peek().DeliverTime <= now)
            {
                var packet = _snapshotQueue.Pop();
                OnSnapshotReceived?.Invoke(packet.Payload);
            }

            while (_actionQueue.Count > 0 && _actionQueue.Peek().DeliverTime <= now)
            {
                var evt = _actionQueue.Pop();
                OnActionEventReceived?.Invoke(evt.Payload);
            }
        }

        // ============ 网络条件模拟 ============

        private float ComputeDelaySeconds()
        {
            float jitter = UnityEngine.Random.Range(-Sync.jitterMs, Sync.jitterMs);
            float delayMs = Mathf.Max(0f, Sync.baseLatencyMs + jitter);
            return delayMs * 0.001f;
        }

        private bool ShouldDrop()
        {
            return UnityEngine.Random.value < Sync.packetLossRate;
        }

        public string GetNetworkConfigString()
        {
            return $"lat={Sync.baseLatencyMs:F0}ms jitter=+/-{Sync.jitterMs:F0}ms loss={Sync.packetLossRate * 100f:F1}%";
        }

        /// <summary>
        /// 优先队列让不同抖动延迟的包按实际投递时间出队，能够自然模拟乱序而无需每帧排序全部数据。
        /// </summary>
        private sealed class MinHeap<T>
        {
            private readonly List<T> _data = new();
            private readonly Func<T, T, bool> _isHigherPriority;

            public MinHeap(Func<T, T, bool> isHigherPriority)
            {
                _isHigherPriority = isHigherPriority;
            }

            public int Count => _data.Count;

            public void Push(T item)
            {
                _data.Add(item);
                SiftUp(_data.Count - 1);
            }

            public T Peek() => _data[0];

            public T Pop()
            {
                var root = _data[0];
                int last = _data.Count - 1;
                _data[0] = _data[last];
                _data.RemoveAt(last);
                if (_data.Count > 0) SiftDown(0);
                return root;
            }

            private void SiftUp(int index)
            {
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (!_isHigherPriority(_data[index], _data[parent])) break;
                    (_data[index], _data[parent]) = (_data[parent], _data[index]);
                    index = parent;
                }
            }

            /// <summary>
            /// 用调用方提供的优先级比较器维护堆序，不把队列实现绑定到某一种网络消息或时间单位。
            /// </summary>
            private void SiftDown(int index)
            {
                int count = _data.Count;
                while (true)
                {
                    int left = index * 2 + 1;
                    int right = left + 1;
                    int smallest = index;

                    if (left < count && _isHigherPriority(_data[left], _data[smallest])) smallest = left;
                    if (right < count && _isHigherPriority(_data[right], _data[smallest])) smallest = right;
                    if (smallest == index) break;

                    (_data[index], _data[smallest]) = (_data[smallest], _data[index]);
                    index = smallest;
                }
            }
        }
    }
}
