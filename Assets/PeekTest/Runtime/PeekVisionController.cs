using UnityEngine;

namespace PeekTest
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PeekVisionController : MonoBehaviour
    {
        private static readonly int VisionOriginId = Shader.PropertyToID("_PeekVisionOrigin");
        private static readonly int VisionDirectionId = Shader.PropertyToID("_PeekVisionDirection");
        private static readonly int VisionParamsId = Shader.PropertyToID("_PeekVisionParams");
        private static readonly int VisionVisualParamsId = Shader.PropertyToID("_PeekVisionVisualParams");
        private static readonly int VisionTintId = Shader.PropertyToID("_PeekVisionTint");
        private static readonly int RoomMinId = Shader.PropertyToID("_PeekRoomMin");
        private static readonly int RoomMaxId = Shader.PropertyToID("_PeekRoomMax");

        [SerializeField] private Transform _origin;
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _roomMin = new Vector3(0.25f, -0.5f, -3.5f);
        [SerializeField] private Vector3 _roomMax = new Vector3(12f, 5.5f, 3.5f);
        [SerializeField, Min(0.1f)] private float _distance = 13f;
        [SerializeField, Min(0f)] private float _distanceFeather = 0.8f;
        [SerializeField, Range(0.1f, 89f)] private float _innerAngle = 12f;
        [SerializeField, Range(0.1f, 89f)] private float _outerAngle = 22f;
        [SerializeField, Range(0.1f, 1f)] private float _idleRoomBrightness = 0.72f;
        [SerializeField, Range(0.05f, 1f)] private float _peekingOutsideBrightness = 0.4f;
        [SerializeField, Range(1f, 2f)] private float _insideBrightness = 1.18f;
        [SerializeField, Range(0f, 1f)] private float _outsideSaturation = 0.55f;
        [SerializeField, ColorUsage(false, true)] private Color _insideTint = new Color(1.08f, 0.96f, 0.72f, 1f);
        [SerializeField] private bool _isPeeking;

        public bool IsPeeking => _isPeeking;
        public Transform Origin => _origin;
        public Transform Target => _target;
        public float Distance => _distance;
        public float InnerAngle => _innerAngle;
        public float OuterAngle => _outerAngle;
        public Vector3 RoomMin => _roomMin;
        public Vector3 RoomMax => _roomMax;

        public void Configure(
            Transform origin,
            Transform target,
            Vector3 roomMin,
            Vector3 roomMax,
            float distance,
            float distanceFeather,
            float innerAngle,
            float outerAngle)
        {
            _origin = origin;
            _target = target;
            _roomMin = Vector3.Min(roomMin, roomMax);
            _roomMax = Vector3.Max(roomMin, roomMax);
            _distance = Mathf.Max(0.1f, distance);
            _distanceFeather = Mathf.Max(0f, distanceFeather);
            _innerAngle = Mathf.Clamp(Mathf.Min(innerAngle, outerAngle), 0.1f, 89f);
            _outerAngle = Mathf.Clamp(Mathf.Max(innerAngle, outerAngle), _innerAngle, 89f);
            PublishShaderGlobals();
        }

        public void SetPeeking(bool isPeeking)
        {
            _isPeeking = isPeeking;
            PublishShaderGlobals();
        }

        private void OnEnable()
        {
            PublishShaderGlobals();
        }

        private void LateUpdate()
        {
            PublishShaderGlobals();
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(VisionParamsId, new Vector4(_distance, 0f, 1f, 0f));
        }

        private void PublishShaderGlobals()
        {
            Vector3 origin = _origin != null ? _origin.position : transform.position;
            Vector3 direction = ResolveDirection(origin);
            float innerCos = Mathf.Cos(_innerAngle * Mathf.Deg2Rad);
            float outerCos = Mathf.Cos(_outerAngle * Mathf.Deg2Rad);

            Shader.SetGlobalVector(VisionOriginId, new Vector4(origin.x, origin.y, origin.z, _distanceFeather));
            Shader.SetGlobalVector(VisionDirectionId, new Vector4(direction.x, direction.y, direction.z, 0f));
            Shader.SetGlobalVector(
                VisionParamsId,
                new Vector4(_distance, outerCos, innerCos, _isPeeking ? 1f : 0f));
            Shader.SetGlobalVector(
                VisionVisualParamsId,
                new Vector4(
                    _peekingOutsideBrightness,
                    _insideBrightness,
                    _idleRoomBrightness,
                    _outsideSaturation));
            Shader.SetGlobalColor(VisionTintId, _insideTint);
            Shader.SetGlobalVector(RoomMinId, _roomMin);
            Shader.SetGlobalVector(RoomMaxId, _roomMax);
        }

        private Vector3 ResolveDirection(Vector3 origin)
        {
            if (_target != null)
            {
                Vector3 toTarget = _target.position - origin;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    return toTarget.normalized;
                }
            }

            return Vector3.right;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = _origin != null ? _origin.position : transform.position;
            Vector3 direction = ResolveDirection(origin);
            Vector3 normal = Vector3.Cross(direction, Vector3.forward);
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.up;
            }

            Vector3 upper = Quaternion.AngleAxis(_outerAngle, normal.normalized) * direction;
            Vector3 lower = Quaternion.AngleAxis(-_outerAngle, normal.normalized) * direction;
            Gizmos.color = _isPeeking ? Color.green : Color.gray;
            Gizmos.DrawLine(origin, origin + upper * _distance);
            Gizmos.DrawLine(origin, origin + lower * _distance);
            Gizmos.DrawLine(origin, origin + direction * _distance);
        }
    }
}
