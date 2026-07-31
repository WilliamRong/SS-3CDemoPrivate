using Character.Combat;
using Character.Presentation;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

namespace AI
{
    /// <summary>
    /// 把调试触发限制在服务器入口，确保验证到的是正式网络传播链路而非客户端本地动画。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NpcCharacterDriver))]
    public sealed class NpcCombatDebug : MonoBehaviour
    {
        [SerializeField] private NpcCharacterDriver _driver;
        [SerializeField] private bool _enableHotkeys = true;
        [FormerlySerializedAs("_attackComboStep")]
        [SerializeField] private AttackMoveId _attackId = AttackMoveId.Combo1;
        [SerializeField] private float _guardLoopHoldDuration = 2f;
        [SerializeField] private float _sprintHoldDuration = 1.5f;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            if (_driver == null)
                _driver = GetComponent<NpcCharacterDriver>();
        }

        private void Update()
        {
            if (!_enableHotkeys || !IsServerAuthority())
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad1)) TriggerAttack();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad2)) TriggerDodgeForward();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad3)) TriggerGuard();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad4)) TriggerHitLight();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad5)) TriggerHitHeavy();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad6)) TriggerDead();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad7)) TriggerSprint();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad8)) TriggerRevive();
        }

        // ============ 调试命令 ============

        [ContextMenu("Combat/Attack")]
        public void TriggerAttack()
        {
            if (!IsServerAuthority()) return;
            _driver.ServerTryEnterAttack(_attackId);
        }

        [ContextMenu("Combat/Dodge Forward")]
        public void TriggerDodgeForward()
        {
            if (!IsServerAuthority()) return;
            _driver.ServerTryEnterDodge(DodgeMode.ForwardAlongMove, transform.forward);
        }

        [ContextMenu("Combat/Dodge Backward")]
        public void TriggerDodgeBackward()
        {
            if (!IsServerAuthority()) return;
            _driver.ServerTryEnterDodge(DodgeMode.NeutralBackward, -transform.forward);
        }

        [ContextMenu("Combat/Guard")]
        public void TriggerGuard()
        {
            if (!IsServerAuthority()) return;
            _driver.ServerTryEnterGuard(_guardLoopHoldDuration);
        }

        [ContextMenu("Combat/Hit Light")]
        public void TriggerHitLight()
        {
            if (!IsServerAuthority()) return;
            _driver.ServerTryEnterHit(isHeavy: false);
        }

        [ContextMenu("Combat/Hit Heavy")]
        public void TriggerHitHeavy()
        {
            if (!IsServerAuthority()) return;
            _driver.ServerTryEnterHit(isHeavy: true);
        }

        [ContextMenu("Combat/Dead")]
        public void TriggerDead()
        {
            if (!IsServerAuthority()) return;
            _driver.ServerTryEnterDead();
        }

        [ContextMenu("Combat/Sprint")]
        public void TriggerSprint()
        {
            if (!IsServerAuthority()) return;
            _driver.ServerTryEnterSprint(_sprintHoldDuration);
        }

        [ContextMenu("Combat/Revive")]
        public void TriggerRevive()
        {
            if (!IsServerAuthority()) return;
            _driver.ServerTryRevive();
        }

        // ============ 权威检查 ============

        private bool IsServerAuthority()
        {
            return NetworkServer.active && _driver != null && _driver.isServer;
        }
    }
}
