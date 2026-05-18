using System;
using System.Collections.Generic;
using Character.Config;
using Core;
using UnityEngine;

namespace Character.Sync
{
    public sealed class FakeNetworkPipe : MonoBehaviour, ISyncTransport
    {
        private NetworkSyncConfig Sync => GameDataManager.Instance.NetworkSync;

        public event Action<StateSnapshot> OnSnapshotReceived;
        public event Action<ActionEvent> OnActionEventReceived;

        private struct QueuedSnapshot
        {
            public float DeliverTime;
            public StateSnapshot Payload;
        }

        private struct QueuedAction
        {
            public float DeliverTime;
            public ActionEvent Payload;
        }

        private readonly MinHeap<QueuedSnapshot> _snapshotQueue = new((a, b) => a.DeliverTime < b.DeliverTime);
        private readonly MinHeap<QueuedAction> _actionQueue = new((a, b) => a.DeliverTime < b.DeliverTime);

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
