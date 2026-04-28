using UnityEngine;

namespace Character.Sync
{
    public class NetTickClock : MonoBehaviour
    {

        [Header("Tick")]
        [SerializeField]
        private int _tickRate = 20;
        
        private float _accumulator;
        private int _currentTick;
    
        public int CurrentTick => _currentTick;
        public float TickInterval => 1f / Mathf.Max(1, _tickRate);
        
        public float TickProgress01 =>Mathf.Min(1f, _accumulator / TickInterval);

        //本帧跑了几个逻辑tick
        public int TickCountThisFrame {get; private set;}
        // Update is called once per frame
        void Update()
        {
            TickCountThisFrame = 0;

            float dt = Time.deltaTime;
            if(dt <= 0f) return;

            _accumulator += dt;
            float interval = TickInterval;

            //限制一帧最多跑8个tick
            int safety = 8;
            while(_accumulator >= interval && safety> 0)
            {
                _accumulator -= interval;
                _currentTick++;
                TickCountThisFrame++;
                safety--;
            }
        }
    }
}
