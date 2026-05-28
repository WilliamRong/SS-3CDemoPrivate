using Mirror;
using UnityEngine;

namespace Character.LockOn
{
    [DisallowMultipleComponent]
    public sealed class LockOnTarget : MonoBehaviour, ILockOnTarget
    {
        [SerializeField] private Transform _lockPoint;
        [SerializeField] private Transform _root;
        [SerializeField] private int _lockPriority;
        [SerializeField] private bool _canBeLocked = true;
        
        public Transform LockPoint => _lockPoint != null ? _lockPoint : transform;
        public Transform Root => _root != null ? _root : transform;
        public bool CanBeLocked => _canBeLocked && isActiveAndEnabled;
        public int LockPriority => _lockPriority;

        public bool TryGetNetworkId(out uint netId)
        {
            netId = 0;
            
            var identity = Root.GetComponentInParent<NetworkIdentity>();
            if (identity == null || identity.netId == 0) return false;

            netId = identity.netId;
            return true;
        }
    }
}
