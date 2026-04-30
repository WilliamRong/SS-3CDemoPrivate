using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Input
{
    
    public class InputHandler : MonoBehaviour
    {
        private DemoInputActions _inputActions;
        private PlayerAuthorityGate _authorityGate;
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        
        public bool IsSprinting { get; private set; }
        
        //单帧触发，下一帧清零
        public bool JumpTriggered { get; private set; }
        public bool AttackTriggered { get; private set; }
        public bool DodgeTriggered { get; private set; }

        private void Awake()
        {
            _inputActions = new DemoInputActions();
            _authorityGate = GetComponent<PlayerAuthorityGate>();
        }

        private void OnEnable()
        {
            _inputActions.Enable();
            _inputActions.Player.Jump.performed += OnJumpPerformed;
            _inputActions.Player.Attack.performed += OnAttackPerformed;
            _inputActions.Player.Dodge.performed += OnDodgePerformed;
        }

        private void OnDisable()
        {
            _inputActions.Player.Jump.performed -= OnJumpPerformed;
            _inputActions.Player.Attack.performed -= OnAttackPerformed;
            _inputActions.Player.Dodge.performed -= OnDodgePerformed;
            _inputActions.Disable();
            
            // 补充：禁用时清理脉冲，避免残留
            ClearTriggers();
        }
        

        private void OnJumpPerformed(InputAction.CallbackContext obj)
        {
            if (!CanProcessLocalInput()) return;
            JumpTriggered = true;
        }
        
        
        private void OnAttackPerformed(InputAction.CallbackContext obj)
        {
            if (!CanProcessLocalInput()) return;
            AttackTriggered = true;
        }
        
        private void OnDodgePerformed(InputAction.CallbackContext obj)
        {
            if (!CanProcessLocalInput()) return;
            DodgeTriggered = true;
        }
        
        // Update is called once per frame
        void Update()
        {
            if (!CanProcessLocalInput())
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                IsSprinting = false;
                ClearTriggers();
                return;
            }

            MoveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            LookInput = _inputActions.Player.Look.ReadValue<Vector2>();
            IsSprinting = _inputActions.Player.Sprint.ReadValue<float>() > 0.5f;
        }

        private void LateUpdate()
        {
            ClearTriggers();
        }

        private void ClearTriggers()
        {
            JumpTriggered = false;
            AttackTriggered = false;
            DodgeTriggered = false;
        }

        private bool CanProcessLocalInput()
        {
            if (_authorityGate == null) return true;
            return _authorityGate.CanProcessLocalInput;
        }
    }
}
