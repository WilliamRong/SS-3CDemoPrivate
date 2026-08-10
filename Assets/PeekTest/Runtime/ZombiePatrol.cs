using UnityEngine;

namespace PeekTest
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ZombiePatrol : MonoBehaviour
    {
        private static readonly int VelocityXId = Animator.StringToHash("VelocityX");
        private static readonly int VelocityZId = Animator.StringToHash("VelocityZ");
        private static readonly int LocomotionStateId = Animator.StringToHash("Locomotion");

        [SerializeField] private Transform _presentation;
        [SerializeField] private Animator _animator;
        [SerializeField, Min(0.1f)] private float _speed = 1.4f;
        [SerializeField] private float _minX = 2.5f;
        [SerializeField] private float _maxX = 9.5f;
        [SerializeField] private int _direction = 1;
        [SerializeField] private float _gravity = -20f;
        [SerializeField, Min(0f)] private float _turnSpeed = 540f;

        private CharacterController _controller;
        private float _verticalVelocity;

        public int Direction => _direction;
        public float MinX => _minX;
        public float MaxX => _maxX;

        public void Configure(
            Transform presentation,
            Animator animator,
            float speed,
            float minX,
            float maxX,
            int initialDirection)
        {
            _presentation = presentation;
            _animator = animator;
            _speed = Mathf.Max(0.1f, speed);
            _minX = Mathf.Min(minX, maxX);
            _maxX = Mathf.Max(minX, maxX);
            _direction = initialDirection < 0 ? -1 : 1;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }

            if (_presentation == null && _animator != null)
            {
                _presentation = _animator.transform;
            }

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.SetFloat(VelocityXId, 0f);
                _animator.SetFloat(VelocityZId, 1.2f);
                _animator.CrossFade(LocomotionStateId, 0.1f, 0);
            }
        }

        private void Update()
        {
            _direction = PeekVisionMath.ResolvePatrolDirection(transform.position.x, _minX, _maxX, _direction);
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity += _gravity * Time.deltaTime;
            _controller.Move(new Vector3(_direction * _speed, _verticalVelocity, 0f) * Time.deltaTime);

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, _minX, _maxX);
            position.z = 0f;
            transform.position = position;

            if (_presentation != null)
            {
                Quaternion targetRotation = Quaternion.Euler(0f, _direction > 0 ? 90f : -90f, 0f);
                _presentation.localRotation = Quaternion.RotateTowards(
                    _presentation.localRotation,
                    targetRotation,
                    _turnSpeed * Time.deltaTime);
            }
        }
    }
}
