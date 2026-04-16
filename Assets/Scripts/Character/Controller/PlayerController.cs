using Input;
using UnityEngine;

namespace Character.Controller
{
    public class PlayerController : MonoBehaviour
    {
        private InputHandler _inputHandler;
        private CharacterController _characterController;
        private Camera _camera;
        public float MoveSpeed = 5f;
        public float SprintSpeed = 10f;
        public float RotationSlerpSpeed = 10f;

        public Vector3 Velocity;
        public float gravity = -9.81f;
        public float SmoothTime = 0.1f;

        private Vector3 _currentHorizontalVelocity;
        private Vector3 _horizontalVelocityRef;

        public float JumpHeight = 1f;
        // Start is called before the first frame update
        void Start()
        {
            _inputHandler = GetComponent<InputHandler>();
            _characterController = GetComponent<CharacterController>();
            _camera = Camera.main;
        }

        void Update()
        {
            // 水平移动（SmoothDamp 平滑过渡）
            Vector2 moveInput = _inputHandler.MoveInput;
            float speed = _inputHandler.IsSprinting ? SprintSpeed : MoveSpeed;
            bool hasMoveInput = moveInput.sqrMagnitude > 0.0001f;

            Vector3 camForward = Vector3.forward;
            Vector3 camRight = Vector3.right;
            if (_camera != null)
            {
                camForward = GetPlanarNormalized(_camera.transform.forward);
                camRight = GetPlanarNormalized(_camera.transform.right);
            }

            Vector3 inputDir = camRight * moveInput.x + camForward * moveInput.y;
            if (hasMoveInput && inputDir.sqrMagnitude > 0.0001f)
            {
                inputDir.Normalize();
                Quaternion targetRot = Quaternion.LookRotation(inputDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationSlerpSpeed * Time.deltaTime);
            }

            Vector3 targetHorizontal = inputDir * speed;
            _currentHorizontalVelocity = Vector3.SmoothDamp(_currentHorizontalVelocity, targetHorizontal, ref _horizontalVelocityRef, SmoothTime);
            Velocity.x = _currentHorizontalVelocity.x;
            Velocity.z = _currentHorizontalVelocity.z;

            // 跳跃
            if (_inputHandler.JumpTriggered && _characterController.isGrounded)
            {
                Velocity.y = Mathf.Sqrt(JumpHeight * -2f * gravity);
            }

            // 重力
            if (_characterController.isGrounded && Velocity.y < 0)
            {
                Velocity.y = -2f;
            }
            Velocity.y += gravity * Time.deltaTime;

            // 一次 Move
            _characterController.Move(Velocity * Time.deltaTime);
        }

        private static Vector3 GetPlanarNormalized(Vector3 dir)
        {
            dir.y = 0f;
            float sqrMag = dir.sqrMagnitude;
            if (sqrMag <= 0.0001f)
            {
                return Vector3.forward;
            }

            return dir / Mathf.Sqrt(sqrMag);
        }
    }
}
