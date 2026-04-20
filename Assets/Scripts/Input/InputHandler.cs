using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Input
{
    
    public class InputHandler : MonoBehaviour
    {
        private DemoInputActions _inputActions;
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        
        public bool IsSprinting { get; private set; }
        public bool JumpTriggered { get; private set; }

        private void Awake()
        {
            _inputActions = new DemoInputActions();
        }

        private void OnEnable()
        {
            _inputActions.Enable();
            _inputActions.Player.Jump.performed += OnJumpPerformed;
        }

        private void OnDisable()
        {
            _inputActions.Player.Jump.performed -= OnJumpPerformed;
        }
        

        private void OnJumpPerformed(InputAction.CallbackContext obj)
        {
            JumpTriggered = true;
        }
        
        
        // Update is called once per frame
        void Update()
        {
            MoveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            LookInput = _inputActions.Player.Look.ReadValue<Vector2>();
            IsSprinting = _inputActions.Player.Sprint.ReadValue<float>() > 0.5f;
        }

        private void LateUpdate()
        {
            JumpTriggered = false;
        }
    }
}
