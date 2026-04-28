using System.Collections.Generic;
using UnityEngine;

namespace Character.Sync
{
    public sealed class RemoteSnapshotBuffer : MonoBehaviour
    {
       [SerializeField] private int _maxBufferSize = 64;

       private readonly List<StateSnapshot> _buffer = new();

       public int Count => _buffer.Count;

       public void Push(StateSnapshot snapshot){
            // 插入保持按 Tick 升序
            int idx = _buffer.Count;
            while (idx > 0 && _buffer[idx - 1].Tick > snapshot.Tick)
                idx--;
            _buffer.Insert(idx, snapshot);
            // 去重：同 tick 保留后来的（简单策略）
            for (int i = _buffer.Count - 2; i >= 0; i--)
            {
                if (_buffer[i].Tick == snapshot.Tick)
                    _buffer.RemoveAt(i);
                else
                    break;
            }
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
    }

}
