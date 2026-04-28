using UnityEngine;

namespace Character.Sync
{
    public class RemoteInterpolator : MonoBehaviour
    {
       [Header("Refs")]
       [SerializeField] private NetTickClock _clock;
       [SerializeField] private RemoteSnapshotBuffer _buffer;

       [Header("Interpolation")]
       [Tooltip("渲染时落后多少tick，给插值留缓冲")]
       [SerializeField] private int _interpBackTicks = 2;

       [Tooltip("大误差阈值（米）")]
       [SerializeField] private float _largeErrorThreshold = 1.5f;

       [Tooltip("大误差回拉速度")]
       [SerializeField] private float _snapLerpSpeed = 12f;
        
       [Header("Debug")]
       [SerializeField] private bool _logState = false;

       public StateSnapshot LastAppliedSnapshot {get; private set;}
       public float LastPosError{get; private set;}

       private void Awake(){
          if (_clock == null) _clock = FindFirstObjectByType<NetTickClock>();
          if (_buffer == null) _buffer = GetComponent<RemoteSnapshotBuffer>();
       }

       private void Update(){
            if (_clock == null || _buffer == null) return;
            if (_buffer.Count == 0) return;

            float targetTick = _clock.CurrentTick - _interpBackTicks - (1f - _clock.TickProgress01);

            if (!_buffer.TrySample(targetTick, out var from, out var to, out float t))
            {
                return;
            }

            Vector3 targetPos;
            float  targetYaw;

            if(from.Tick == to.Tick){
                targetPos = from.Position;
                targetYaw = from.Yaw;
            }
            else
            {
                targetPos = Vector3.Lerp(from.Position, to.Position, t);
                targetYaw = Mathf.LerpAngle(from.Yaw, to.Yaw, t);
            }

             // 误差监控
            LastPosError = Vector3.Distance(transform.position, targetPos);

            // 小误差：普通插值；大误差：快速回拉
            float lerpSpeed = LastPosError > _largeErrorThreshold ? _snapLerpSpeed : 8f;
            float k = Mathf.Clamp01(lerpSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, k);

            var curEuler = transform.rotation.eulerAngles;
            float yaw = Mathf.LerpAngle(curEuler.y, targetYaw, k);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            LastAppliedSnapshot = to;

            if(_logState){
                 Debug.Log($"[RemoteInterp] targetTick={targetTick}, from={from.Tick}, to={to.Tick}, err={LastPosError:F2}");
            }
       }
    }
}
