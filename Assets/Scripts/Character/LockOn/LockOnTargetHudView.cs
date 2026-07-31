using Character.Combat;
using Mirror;
using UnityEngine;

namespace Character.LockOn
{
    /// <summary>
    /// 将本地锁定状态绑定到目标已有的世界血条，只切换可见性而不创建第二个锁定专用血条。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerLockOnController))]
    public sealed class LockOnTargetHudView : MonoBehaviour
    {
        [SerializeField] private PlayerLockOnController _lockOn;

        private NetworkIdentity _networkIdentity;
        private ILockOnTarget _boundTarget;
        private NpcHealthBarView _boundHealthBar;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            if (_lockOn == null)
                _lockOn = GetComponent<PlayerLockOnController>();

            _networkIdentity = GetComponent<NetworkIdentity>();
        }

        private void OnDisable()
        {
            UnbindTarget();
        }

        /// <summary>
        /// 在锁定控制器完成本帧目标维护后再绑定世界血条，避免切换目标时短暂显示上一目标。
        /// </summary>
        private void LateUpdate()
        {
            if (!ShouldRenderForThisPlayer() || _lockOn == null || !_lockOn.IsLockOnActive)
            {
                UnbindTarget();
                return;
            }

            ILockOnTarget target = _lockOn.CurrentLockOnTarget;
            if (target == null)
            {
                UnbindTarget();
                return;
            }

            if (!ReferenceEquals(_boundTarget, target))
                BindTarget(target);
        }

        // ============ 本地玩家约束 ============

        private bool ShouldRenderForThisPlayer()
        {
            if (!NetworkClient.active)
                return true;

            return _networkIdentity != null && _networkIdentity.isLocalPlayer;
        }

        // ============ 世界血条绑定 ============

        /// <summary>
        /// 切换目标前先解除旧绑定，防止多个目标因锁定状态残留而长期显示血条。
        /// </summary>
        private void BindTarget(ILockOnTarget target)
        {
            UnbindTarget();
            _boundTarget = target;

            Transform root = target.Root;
            if (root == null)
                return;

            _boundHealthBar = root.GetComponentInParent<NpcHealthBarView>();
            if (_boundHealthBar != null)
                _boundHealthBar.SetLockOnVisible(true);
        }

        private void UnbindTarget()
        {
            if (_boundHealthBar != null)
                _boundHealthBar.SetLockOnVisible(false);

            _boundTarget = null;
            _boundHealthBar = null;
        }
    }
}
