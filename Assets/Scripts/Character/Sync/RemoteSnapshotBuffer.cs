using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
    public sealed class RemoteSnapshotBuffer : MonoBehaviour
    {
       [SerializeField] private int _maxBufferSize = 64;


       [SerializeField] private bool _autoBindActorId = true;
       [SerializeField] private int _acceptActorId = -1;
       [SerializeField] private bool _lockToSelfNetworkIdentity = true;
       
       private readonly List<StateSnapshot> _buffer = new();
       private NetworkIdentity _networkIdentity;
       

       public int Count => _buffer.Count;
       public int BoundActorId => _acceptActorId;
       public float OldestArrivalTime => _buffer.Count > 0 ? _buffer[0].ArrivalTimeSec : 0f;
       public float NewestArrivalTime => _buffer.Count > 0 ? _buffer[_buffer.Count - 1].ArrivalTimeSec : 0f;

       private void Awake()
       {
           if (_networkIdentity == null) _networkIdentity = GetComponent<NetworkIdentity>();
       }

       public void Push(StateSnapshot snapshot)
       {
           int expectedActorId = ResolveExpectedActorId();
           if (expectedActorId > 0)
           {
               if (snapshot.ActorId != expectedActorId) return;
               _acceptActorId = expectedActorId;
           }
           else
           {
               if (_autoBindActorId && _acceptActorId < 0)
               {
                   _acceptActorId = snapshot.ActorId;
               }

               if (_acceptActorId >= 0 && snapshot.ActorId != _acceptActorId)
               {
                   return;
               }
           }
           
           
            // 插入保持按 ArrivalTime 升序
            int idx = _buffer.Count;
            while (idx > 0 && _buffer[idx - 1].ArrivalTimeSec > snapshot.ArrivalTimeSec)
                idx--;
            _buffer.Insert(idx, snapshot);
            // 已按ArrivalTime排序，不需要Tick去重
            // // 去重：同 tick 保留后来的（简单策略）
            // for (int i = _buffer.Count - 2; i >= 0; i--)
            // {
            //     if (_buffer[i].Tick == snapshot.Tick)
            //         _buffer.RemoveAt(i);
            //     else
            //         break;
            // }
            // 裁剪
            if (_buffer.Count > _maxBufferSize)
            {
                int removeCount = _buffer.Count - _maxBufferSize;
                _buffer.RemoveRange(0, removeCount);
            }
       }

        /// <summary>
        /// 根据目标tick找前后帧，找不到返回 false
        /// </summary>
       public bool TryGetFrames(int targetTick, out StateSnapshot from, out StateSnapshot to)
       {
            from = default;
            to = default;
            if (_buffer.Count == 0) return false;
            if (_buffer.Count == 1)
            {
                from = _buffer[0];
                to = _buffer[0];
                return true;
            }
            // 小于最早帧：夹住
            if (targetTick <= _buffer[0].Tick)
            {
                from = _buffer[0];
                to = _buffer[0];
                return true;
            }
            // 大于最晚帧：夹住
            int last = _buffer.Count - 1;
            if (targetTick >= _buffer[last].Tick)
            {
                from = _buffer[last];
                to = _buffer[last];
                return true;
            }
            // 中间区间查找
            for (int i = 0; i < _buffer.Count - 1; i++)
            {
                var a = _buffer[i];
                var b = _buffer[i + 1];
                if (a.Tick <= targetTick && targetTick <= b.Tick)
                {
                    from = a;
                    to = b;
                    return true;
                }
            }
            return false;
       }

       public bool TrySample(float targetTick, out StateSnapshot from, out StateSnapshot to, out float t)
       {
            from = default;
            to = default;
            t = 0f;

            if(_buffer.Count == 0) return false;
            if(_buffer.Count == 1){
                from = _buffer[0];
                to = _buffer[0];
                return true;
            }

             // 夹住边界
            if (targetTick <= _buffer[0].Tick)
            {
                from = _buffer[0];
                to = _buffer[0];
                return true;
            }
            int last = _buffer.Count - 1;
            if (targetTick >= _buffer[last].Tick)
            {
                from = _buffer[last];
                to = _buffer[last];
                return true;
            }


             // 找包围区间
            for (int i = 0; i < _buffer.Count - 1; i++)
            {
                var a = _buffer[i];
                var b = _buffer[i + 1];
                if (a.Tick <= targetTick && targetTick <= b.Tick)
                {
                    from = a;
                    to = b;
                    t = Mathf.InverseLerp(a.Tick, b.Tick, targetTick);
                    return true;
                }
            }

            return false;
       }
       
       //基于ArrivalTime采样
       public bool TrySampleByArrivalTime(float targetTime, out StateSnapshot from, out StateSnapshot to, out float t)
       {
           from = default;
           to = default;
           t = 0f;

           if (_buffer.Count == 0) return false;
           if (_buffer.Count == 1)
           {
               from = _buffer[0];
               to = _buffer[0];
               return true;
           }

           // 边界夹住
           if (targetTime <= _buffer[0].ArrivalTimeSec)
           {
               from = _buffer[0];
               to = _buffer[0];
               return true;
           }

           int last = _buffer.Count - 1;
           if (targetTime >= _buffer[last].ArrivalTimeSec)
           {
               from = _buffer[last];
               to = _buffer[last];
               return true;
           }

           // 区间查找
           for (int i = 0; i < _buffer.Count - 1; i++)
           {
               var a = _buffer[i];
               var b = _buffer[i + 1];

               if (a.ArrivalTimeSec <= targetTime && targetTime <= b.ArrivalTimeSec)
               {
                   from = a;
                   to = b;

                   float dt = Mathf.Max(0.0001f, b.ArrivalTimeSec - a.ArrivalTimeSec);
                   t = Mathf.Clamp01((targetTime - a.ArrivalTimeSec) / dt);
                   return true;
               }
           }

           return false;
       }
       
       public void ResetActorBinding(int actorId = -1)
       {
           _acceptActorId = actorId;
           _buffer.Clear();
       }

       private int ResolveExpectedActorId()
       {
           if (!_lockToSelfNetworkIdentity || _networkIdentity == null) return -1;
           if (_networkIdentity.netId == 0) return -1;
           return (int)_networkIdentity.netId;
       }
    }

}
