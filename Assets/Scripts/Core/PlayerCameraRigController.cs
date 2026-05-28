using Character.Presentation;
using Cinemachine;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Drives FreeLook / LockOn Cinemachine rigs for the local player.
    /// Lock-on: camera behind player (Follow), looking at target (LookAt), with damped follow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraRigController : MonoBehaviour
    {
        [Header("Scene Paths")]
        [SerializeField] private string _freeLookPath = "Cameras/TP";
        [SerializeField] private string _lockOnPath = "Cameras/LockOn";

        [Header("Priority")]
        [SerializeField] private int _freeLookPriority = 10;
        [SerializeField] private int _lockOnActivePriority = 20;
        [SerializeField] private int _lockOnInactivePriority = 0;

        [Header("Lock-On Body (behind player)")]
        [SerializeField] private float _lockDistanceTpMultiplier = 1.2f;
        [SerializeField] private float _separationDistanceExtra = 1.5f;
        [SerializeField] private float _nearSeparation = 2f;
        [SerializeField] private float _farSeparation = 10f;
        [SerializeField] private float _bodyDamping = 1.5f;
        [SerializeField] private float _bodyYawDamping = 1.2f;

        [Header("Lock-On Aim (target on screen)")]
        [SerializeField] private float _lockScreenY = 0.6f;
        [SerializeField] private float _aimHorizontalDamping = 1.2f;
        [SerializeField] private float _aimVerticalDamping = 1.2f;
        [SerializeField] private float _aimSoftZoneWidth = 0.55f;
        [SerializeField] private float _aimSoftZoneHeight = 0.45f;

        private CinemachineFreeLook _freeLook;
        private CinemachineVirtualCamera _lockOnVcam;
        private CinemachineTransposer _lockTransposer;
        private CinemachineComposer _lockComposer;
        private ILockOnLocomotionQuery _lockOnQuery;
        private Transform _playerLookAt;

        private float _tpLockBaseDistance;
        private float _tpLockFollowHeight;
        private float _savedXAxisMaxSpeed;
        private float _savedYAxisMaxSpeed;
        private bool _freeLookInputSaved;
        private bool _wasLockOnActive;
        private bool _initialized;

        public bool IsInitialized => _initialized;

        public bool TryInitialize(Transform playerLookAt)
        {
            if (playerLookAt == null)
                return false;

            _playerLookAt = playerLookAt;
            _lockOnQuery = GetComponent<ILockOnLocomotionQuery>();

            var freeLookGo = FindSceneObject(_freeLookPath);
            var lockOnGo = FindSceneObject(_lockOnPath);
            if (freeLookGo == null || lockOnGo == null)
                return false;

            _freeLook = freeLookGo.GetComponent<CinemachineFreeLook>();
            _lockOnVcam = lockOnGo.GetComponent<CinemachineVirtualCamera>();
            if (_freeLook == null || _lockOnVcam == null)
                return false;

            _lockTransposer = _lockOnVcam.GetCinemachineComponent<CinemachineTransposer>();
            _lockComposer = _lockOnVcam.GetCinemachineComponent<CinemachineComposer>();
            if (_lockTransposer == null || _lockComposer == null)
            {
                Debug.LogWarning("[PlayerCameraRigController] LockOn vcam needs Transposer + Composer.");
                return false;
            }

            ConfigureLockOnComponents();

            _freeLook.Follow = _playerLookAt;
            _freeLook.LookAt = _playerLookAt;
            _freeLook.Priority = _freeLookPriority;

            _lockOnVcam.Follow = _playerLookAt;
            _lockOnVcam.LookAt = null;
            _lockOnVcam.Priority = _lockOnInactivePriority;

            _wasLockOnActive = _lockOnQuery != null && _lockOnQuery.IsLockOnActive;
            if (_wasLockOnActive && _lockOnQuery.CurrentTarget != null)
                ApplyLockOnState(_lockOnQuery.CurrentTarget);
            else
                ApplyLockOffState();

            _initialized = true;
            return true;
        }

        private void ConfigureLockOnComponents()
        {
            _lockTransposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
            _lockTransposer.m_XDamping = _bodyDamping;
            _lockTransposer.m_YDamping = _bodyDamping;
            _lockTransposer.m_ZDamping = _bodyDamping;
            _lockTransposer.m_YawDamping = _bodyYawDamping;
            _lockTransposer.m_PitchDamping = _bodyYawDamping;

            _lockComposer.m_ScreenX = 0.5f;
            _lockComposer.m_ScreenY = _lockScreenY;
            _lockComposer.m_HorizontalDamping = _aimHorizontalDamping;
            _lockComposer.m_VerticalDamping = _aimVerticalDamping;
            _lockComposer.m_SoftZoneWidth = _aimSoftZoneWidth;
            _lockComposer.m_SoftZoneHeight = _aimSoftZoneHeight;
            _lockComposer.m_DeadZoneWidth = 0.05f;
            _lockComposer.m_DeadZoneHeight = 0.04f;
            _lockComposer.m_CenterOnActivate = false;

            RefreshLockOnFramingFromFreeLook();
        }

        private void RefreshLockOnFramingFromFreeLook()
        {
            if (_freeLook == null)
                return;

            SampleFreeLookOrbit(out float tpRadius, out float tpHeight, out float rigOffsetY);
            _tpLockBaseDistance = tpRadius * _lockDistanceTpMultiplier;
            _tpLockFollowHeight = tpHeight + rigOffsetY;
            UpdateFollowOffset(_tpLockBaseDistance);
        }

        private void SampleFreeLookOrbit(out float radius, out float height, out float rigOffsetY)
        {
            radius = 3.25f;
            height = 0.5f;
            rigOffsetY = 1.09f;

            var orbits = _freeLook.m_Orbits;
            if (orbits == null || orbits.Length < 3)
                return;

            float yNorm = Mathf.InverseLerp(
                _freeLook.m_YAxis.m_MinValue,
                _freeLook.m_YAxis.m_MaxValue,
                _freeLook.m_YAxis.Value);

            if (yNorm < 0.5f)
            {
                float t = yNorm * 2f;
                radius = Mathf.Lerp(orbits[2].m_Radius, orbits[1].m_Radius, t);
                height = Mathf.Lerp(orbits[2].m_Height, orbits[1].m_Height, t);
            }
            else
            {
                float t = (yNorm - 0.5f) * 2f;
                radius = Mathf.Lerp(orbits[1].m_Radius, orbits[0].m_Radius, t);
                height = Mathf.Lerp(orbits[1].m_Height, orbits[0].m_Height, t);
            }

            var middleRig = _freeLook.GetRig(1);
            if (middleRig == null)
                return;

            var orbital = middleRig.GetCinemachineComponent<CinemachineOrbitalTransposer>();
            if (orbital != null)
                rigOffsetY = orbital.m_FollowOffset.y;
        }

        private void LateUpdate()
        {
            if (!_initialized || _lockOnQuery == null)
                return;

            bool active = _lockOnQuery.IsLockOnActive;
            Transform target = _lockOnQuery.CurrentTarget;

            if (active != _wasLockOnActive)
            {
                if (active && target != null)
                    ApplyLockOnState(target);
                else
                    ApplyLockOffState();

                _wasLockOnActive = active;
            }

            if (active && target != null)
                UpdateLockOnDistance(target);
        }

        private void ApplyLockOnState(Transform target)
        {
            RefreshLockOnFramingFromFreeLook();
            _lockOnVcam.Follow = _playerLookAt;
            _lockOnVcam.LookAt = target;
            UpdateLockOnDistance(target);
            SetFreeLookInputEnabled(false);
            _lockOnVcam.Priority = _lockOnActivePriority;
        }

        private void ApplyLockOffState()
        {
            _lockOnVcam.LookAt = null;
            _lockOnVcam.Priority = _lockOnInactivePriority;
            SetFreeLookInputEnabled(true);
        }

        private void UpdateLockOnDistance(Transform target)
        {
            var playerPos = _playerLookAt.position;
            var targetPos = target.position;
            float separation = Vector2.Distance(
                new Vector2(playerPos.x, playerPos.z),
                new Vector2(targetPos.x, targetPos.z));

            float maxDistance = _tpLockBaseDistance + _separationDistanceExtra;
            float distance = Mathf.Lerp(
                _tpLockBaseDistance,
                maxDistance,
                Mathf.InverseLerp(_nearSeparation, _farSeparation, separation));

            UpdateFollowOffset(distance);
        }

        private void UpdateFollowOffset(float distance)
        {
            _lockTransposer.m_FollowOffset = new Vector3(0f, _tpLockFollowHeight, -distance);
        }

        private void SetFreeLookInputEnabled(bool enabled)
        {
            if (_freeLook == null)
                return;

            if (!enabled)
            {
                if (!_freeLookInputSaved)
                {
                    _savedXAxisMaxSpeed = _freeLook.m_XAxis.m_MaxSpeed;
                    _savedYAxisMaxSpeed = _freeLook.m_YAxis.m_MaxSpeed;
                    _freeLookInputSaved = true;
                }

                _freeLook.m_XAxis.m_MaxSpeed = 0f;
                _freeLook.m_YAxis.m_MaxSpeed = 0f;
                return;
            }

            if (_freeLookInputSaved)
            {
                _freeLook.m_XAxis.m_MaxSpeed = _savedXAxisMaxSpeed;
                _freeLook.m_YAxis.m_MaxSpeed = _savedYAxisMaxSpeed;
            }
        }

        private static GameObject FindSceneObject(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var segments = path.Split('/');
            if (segments.Length == 0)
                return null;

            GameObject current = null;
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == segments[0])
                {
                    current = roots[i];
                    break;
                }
            }

            if (current == null)
                return null;

            for (int i = 1; i < segments.Length; i++)
            {
                var child = current.transform.Find(segments[i]);
                if (child == null)
                    return null;

                current = child.gameObject;
            }

            return current;
        }
    }
}
