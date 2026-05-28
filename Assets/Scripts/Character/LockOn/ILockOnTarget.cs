using UnityEngine;

namespace Character.LockOn
{
    public interface ILockOnTarget
    {
        Transform LockPoint { get; }
        Transform Root { get; }
        bool CanBeLocked { get; }
        int LockPriority { get; }
    }
}
