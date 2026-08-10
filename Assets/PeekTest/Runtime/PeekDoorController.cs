using UnityEngine;

namespace PeekTest
{
    [DisallowMultipleComponent]
    public sealed class PeekDoorController : MonoBehaviour
    {
        [SerializeField] private Transform _player;
        [SerializeField] private Transform _doorPivot;
        [SerializeField] private PeekVisionController _vision;
        [SerializeField, Min(0.1f)] private float _triggerDistance = 2.1f;
        [SerializeField, Range(-90f, 90f)] private float _openAngle = 20f;
        [SerializeField, Min(1f)] private float _angularSpeed = 80f;

        private Quaternion _closedLocalRotation;

        public bool IsOpen { get; private set; }
        public float CurrentOpenAngle
        {
            get
            {
                EnsureClosedRotation();
                return _doorPivot != null
                    ? Quaternion.Angle(_closedLocalRotation, _doorPivot.localRotation)
                    : 0f;
            }
        }

        public void Configure(
            Transform player,
            Transform doorPivot,
            PeekVisionController vision,
            float triggerDistance,
            float openAngle,
            float angularSpeed)
        {
            _player = player;
            _doorPivot = doorPivot;
            _vision = vision;
            _triggerDistance = Mathf.Max(0.1f, triggerDistance);
            _openAngle = Mathf.Clamp(openAngle, -90f, 90f);
            _angularSpeed = Mathf.Max(1f, angularSpeed);
            if (_doorPivot != null)
            {
                _closedLocalRotation = _doorPivot.localRotation;
            }
        }

        private void Awake()
        {
            if (_doorPivot == null)
            {
                _doorPivot = transform;
            }

            _closedLocalRotation = _doorPivot.localRotation;
        }

        private void Update()
        {
            EnsureClosedRotation();
            if (_doorPivot == null)
            {
                return;
            }

            bool shouldOpen = IsPlayerWithinRange();
            if (shouldOpen != IsOpen)
            {
                IsOpen = shouldOpen;
                if (_vision != null)
                {
                    _vision.SetPeeking(IsOpen);
                }
            }

            Quaternion openRotation = _closedLocalRotation * Quaternion.Euler(0f, _openAngle, 0f);
            Quaternion targetRotation = IsOpen ? openRotation : _closedLocalRotation;
            _doorPivot.localRotation = Quaternion.RotateTowards(
                _doorPivot.localRotation,
                targetRotation,
                _angularSpeed * Time.deltaTime);
        }

        private void OnDisable()
        {
            if (_vision != null)
            {
                _vision.SetPeeking(false);
            }
        }

        private bool IsPlayerWithinRange()
        {
            if (_player == null || _doorPivot == null)
            {
                return false;
            }

            Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
            Vector2 doorPosition = new Vector2(_doorPivot.position.x, _doorPivot.position.z);
            return Vector2.Distance(playerPosition, doorPosition) <= _triggerDistance;
        }

        private void EnsureClosedRotation()
        {
            if (_doorPivot == null)
            {
                _doorPivot = transform;
            }

            float rotationLengthSquared =
                (_closedLocalRotation.x * _closedLocalRotation.x) +
                (_closedLocalRotation.y * _closedLocalRotation.y) +
                (_closedLocalRotation.z * _closedLocalRotation.z) +
                (_closedLocalRotation.w * _closedLocalRotation.w);
            if (rotationLengthSquared < 0.5f)
            {
                _closedLocalRotation = _doorPivot.localRotation;
            }
        }
    }
}
