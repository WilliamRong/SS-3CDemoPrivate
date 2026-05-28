using Mirror;
using UnityEngine;

namespace Character.LockOn
{
    public static class LockOnTargetRegistry
    {
        public static bool TryResolveLockPoint(uint netId, out Transform lockPoint)
        {
            lockPoint = null;

            if (netId == 0) return false;
            
            if(!TryGetIdentity(netId, out var identity)) return false;
            
            var target = identity.GetComponentInParent<LockOnTarget>();
            if (target == null || !target.CanBeLocked) return false;

            lockPoint = target.LockPoint;
            return lockPoint != null;
        }

        private static bool TryGetIdentity(uint netId, out NetworkIdentity identity)
        {
            identity = null;

            if (NetworkClient.active && NetworkClient.spawned.TryGetValue(netId, out identity)) return true;
            if(NetworkServer.active && NetworkServer.spawned.TryGetValue(netId, out identity)) return true;
            return false;
        }
    }
}
