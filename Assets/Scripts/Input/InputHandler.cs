using Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Input
{
    /// <summary>
    /// 把 Input System 的持续值和单帧脉冲收敛到一个入口，避免状态机直接依赖输入回调时序。
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        private DemoInputActions _inputActions;
        private PlayerAuthorityGate _authorityGate;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool JumpTriggered { get; private set; }
        public bool AttackTriggered { get; private set; }
        public bool DodgeTriggered { get; private set; }
        public bool LockOnTriggered { get; private set; }
        public bool IsGuardHeld { get; private set; }

        // ============ Unity 生命周期 ============

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
            _inputActions.Player.LockOn.performed += OnLockOnPerformed;
        }

        private void OnDisable()
        {
            _inputActions.Player.Jump.performed -= OnJumpPerformed;
            _inputActions.Player.Attack.performed -= OnAttackPerformed;
            _inputActions.Player.Dodge.performed -= OnDodgePerformed;
            _inputActions.Player.LockOn.performed -= OnLockOnPerformed;
            _inputActions.Disable();
            // 组件可能在触发帧被禁用，必须清理脉冲，避免重新启用后补消费旧输入。
            ClearTriggers();
        }

        /// <summary>
        /// 持续值每帧轮询，离散动作由 performed 回调锁存，避免 Input System 更新模式差异丢失单帧按键。
        /// </summary>
        private void Update()
        {
            if (!CanProcessLocalInput())
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                IsSprinting = false;
                IsGuardHeld = false;
                ClearTriggers();
                return;
            }

            MoveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            LookInput = _inputActions.Player.Look.ReadValue<Vector2>();
            IsSprinting = _inputActions.Player.Sprint.ReadValue<float>() > 0.5f;
            IsGuardHeld = _inputActions.Player.Guard.ReadValue<float>() > 0.5f;
        }

        private void LateUpdate()
        {
            // 让同帧的 PlayerController/锁定逻辑都有机会读取一次脉冲，再统一失效。
            ClearTriggers();
        }

        // ============ 输入回调 ============

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

        private void OnLockOnPerformed(InputAction.CallbackContext obj)
        {
            if (!CanProcessLocalInput()) return;
            LockOnTriggered = true;
        }

        // ============ 输入状态辅助 ============

        private void ClearTriggers()
        {
            JumpTriggered = false;
            AttackTriggered = false;
            DodgeTriggered = false;
            LockOnTriggered = false;
        }

        private bool CanProcessLocalInput()
        {
            // 没有权限门时保留离线和测试场景的旧行为。
            if (_authorityGate == null) return true;
            return _authorityGate.CanProcessLocalInput;
        }
    }
}
