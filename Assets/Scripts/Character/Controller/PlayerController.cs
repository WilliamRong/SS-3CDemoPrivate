using Character.Core;
using Character.Intent;
using Character.Motor;
using Input;
using UnityEngine;

namespace Character.Controller
{
    public class PlayerController : MonoBehaviour
    {
        private InputHandler _inputHandler;
        private CharacterController _characterController;
        private Camera _camera;

        [Header("Move")]
        public float MoveSpeed = 5f;
        public float SprintSpeed = 10f;
        public float RotationSlerpSpeed = 30f;
        public float SmoothTime = 0.1f;

        [Header("Vertical")]
        public float gravity = -9.81f;
        public float JumpHeight = 1f;

        public Vector3 Velocity;

        private CharacterContext _context;
        private CharacterMotor _motor;
      
        // Start is called before the first frame update
        void Start()
        {
            _inputHandler = GetComponent<InputHandler>();
            _characterController = GetComponent<CharacterController>();
            _camera = Camera.main;
            
            //临时放在这里，锁光标
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;


            _context = new CharacterContext(_characterController, _camera);

            _motor = new CharacterMotor(_context){
                MoveSpeed = MoveSpeed,
                SprintSpeed = SprintSpeed,
                RotationSlerpSpeed = RotationSlerpSpeed,
                SmoothTime = SmoothTime,
                Gravity = gravity,
                JumpHeight = JumpHeight,
            };
        }

        void Update()
        {
            // 水平移动（SmoothDamp 平滑过渡）
            var intent = new CharacterIntent{
                Move = _inputHandler.MoveInput,
                IsSprintHeld = _inputHandler.IsSprinting,
                IsJumpPressed = _inputHandler.JumpTriggered,
            };
            
            _motor.Tick(intent, Time.deltaTime, transform);

            Velocity = _context.Velocity;
        }

      }
}
