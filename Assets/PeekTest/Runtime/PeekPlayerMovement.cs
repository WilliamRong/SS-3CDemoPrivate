using UnityEngine;
using UnityEngine.InputSystem;

namespace PeekTest
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PeekPlayerMovement : MonoBehaviour
    {
        private static readonly int VelocityXId = Animator.StringToHash("VelocityX");
        private static readonly int VelocityZId = Animator.StringToHash("VelocityZ");
        private static readonly int IdleStateId = Animator.StringToHash("Idle");
        private static readonly int LocomotionStateId = Animator.StringToHash("Locomotion");

        [SerializeField] private Transform _presentation;
        [SerializeField] private Animator _animator;
        [SerializeField, Min(0.1f)] private float _moveSpeed = 3.5f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _minX = -10.5f;
        [SerializeField] private float _maxX = -0.8f;
        [SerializeField, Min(0f)] private float _turnSpeed = 720f;

        private CharacterController _controller;
        private float _verticalVelocity;
        private bool _wasMoving;
        private int _facingDirection = 1;

        public float HorizontalInput { get; private set; }
        public int FacingDirection => _facingDirection;

        public void Configure(Transform presentation, Animator animator, float moveSpeed, float minX, float maxX)
        {
            _presentation = presentation;
            _animator = animator;
            _moveSpeed = Mathf.Max(0.1f, moveSpeed);
            _minX = Mathf.Min(minX, maxX);
            _maxX = Mathf.Max(minX, maxX);
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
            }
        }

        private void Update()
        {
            HorizontalInput = ReadHorizontalInput();
            if (Mathf.Abs(HorizontalInput) > 0.05f)
            {
                _facingDirection = HorizontalInput > 0f ? 1 : -1;
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity += _gravity * Time.deltaTime;
            Vector3 motion = new Vector3(HorizontalInput * _moveSpeed, _verticalVelocity, 0f);
            _controller.Move(motion * Time.deltaTime);

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, _minX, _maxX);
            position.z = 0f;
            transform.position = position;

            UpdatePresentation();
            UpdateAnimator();
        }

        private static float ReadHorizontalInput()
        {
            float keyboard = 0f;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                {
                    keyboard -= 1f;
                }

                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                {
                    keyboard += 1f;
                }
            }

            float gamepad = Gamepad.current != null ? Gamepad.current.leftStick.x.ReadValue() : 0f;
            return Mathf.Abs(gamepad) > Mathf.Abs(keyboard) ? gamepad : keyboard;
        }

        private void UpdatePresentation()
        {
            if (_presentation == null)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.Euler(0f, _facingDirection > 0 ? 90f : -90f, 0f);
            _presentation.localRotation = Quaternion.RotateTowards(
                _presentation.localRotation,
                targetRotation,
                _turnSpeed * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (_animator == null)
            {
                return;
            }

            bool moving = Mathf.Abs(HorizontalInput) > 0.05f;
            _animator.SetFloat(VelocityXId, 0f);
            _animator.SetFloat(VelocityZId, moving ? 1.5f : 0f);
            if (moving == _wasMoving)
            {
                return;
            }

            _wasMoving = moving;
            _animator.CrossFade(moving ? LocomotionStateId : IdleStateId, 0.12f, 0);
        }
    }
}
