using Mirror;
using UnityEngine;

namespace Character.Sync
{
    public class RemoteInterpolator : MonoBehaviour
    {
       [Header("Refs")]
       [SerializeField] private RemoteSnapshotBuffer _buffer;

       [Header("Interpolation")]
       [Tooltip("渲染缓冲延迟（秒）")]
       [SerializeField] private float _bufferDelaySec = 0.12f;

       [Tooltip("大误差阈值（米）")]
       [SerializeField] private float _largeErrorThreshold = 1.5f;

       [Tooltip("大误差回拉速度")]
       [SerializeField] private float _snapLerpSpeed = 12f;
        
       [Header("Debug")]
       [SerializeField] private bool _logState = false;

       public StateSnapshot LastAppliedSnapshot {get; private set;}
       public float LastPosError{get; private set;}

       private NetworkIdentity _networkIdentity;

       private void Awake()
       {
          if (_buffer == null) _buffer = GetComponent<RemoteSnapshotBuffer>();
          _networkIdentity = GetComponent<NetworkIdentity>();
       }

       private void Update()
       {
            if (_buffer == null) return;

            // Host 上服务端权威物体：位置由本地仿真（如 NavMeshAgent）驱动，不做远端插值
            if (_networkIdentity != null
                && NetworkServer.active
                && NetworkClient.active
                && _networkIdentity.isServer)
                return;

            if (_buffer.Count == 0) return;

            float targetTime = Time.unscaledTime - _bufferDelaySec;

            if (!_buffer.TrySampleByArrivalTime(targetTime, out var from, out var to, out float t))
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
                 Debug.Log($"[RemoteInterp] targetTime={targetTime}, from={from.Tick}, to={to.Tick}, err={LastPosError:F2}");
            }
       }
    }
}
