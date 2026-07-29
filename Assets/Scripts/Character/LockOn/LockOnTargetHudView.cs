using Character.Combat;
using Mirror;
using UnityEngine;

namespace Character.LockOn
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerLockOnController))]
    public sealed class LockOnTargetHudView : MonoBehaviour
    {
        [SerializeField] private PlayerLockOnController _lockOn;

        private NetworkIdentity _networkIdentity;
        private ILockOnTarget _boundTarget;
        private NpcHealthBarView _boundHealthBar;

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

        private bool ShouldRenderForThisPlayer()
        {
            if (!NetworkClient.active)
                return true;

            return _networkIdentity != null && _networkIdentity.isLocalPlayer;
        }

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
