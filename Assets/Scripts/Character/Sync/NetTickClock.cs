using Core;
using UnityEngine;

namespace Character.Sync
{
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

        private void Awake()
        {
            var sync = GameDataManager.Instance.NetworkSync;
            _tickRate = sync.tickRate;
            _maxTicksPerFrame = Mathf.Max(1, sync.maxTicksPerFrame);
        }

        void Update()
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
