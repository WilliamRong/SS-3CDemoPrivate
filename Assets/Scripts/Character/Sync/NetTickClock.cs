using Core;
using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// 用累加器生成独立于渲染帧率的网络 Tick，并限制单帧追赶次数以避免卡顿后产生发送风暴。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class NetTickClock : MonoBehaviour
    {
        private int _tickRate = 20;
        private int _maxTicksPerFrame = 8;
        private float _accumulator;
        private int _currentTick;

        public int CurrentTick => _currentTick;
        public float TickInterval => 1f / Mathf.Max(1, _tickRate);

        public float TickProgress01 => Mathf.Min(1f, _accumulator / TickInterval);
        public int TickCountThisFrame { get; private set; }

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            var sync = GameDataManager.Instance.NetworkSync;
            _tickRate = sync.tickRate;
            _maxTicksPerFrame = Mathf.Max(1, sync.maxTicksPerFrame);
        }

        /// <summary>
        /// 累加器保留未消费时间并限制单帧追帧数量，卡顿后不会用无限循环阻塞主线程。
        /// </summary>
        private void Update()
        {
            TickCountThisFrame = 0;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _accumulator += dt;
            float interval = TickInterval;

            int safety = _maxTicksPerFrame;
            while (_accumulator >= interval && safety > 0)
            {
                _accumulator -= interval;
                _currentTick++;
                TickCountThisFrame++;
                safety--;
            }
        }
    }
}
