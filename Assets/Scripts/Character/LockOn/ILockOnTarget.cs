using UnityEngine;

namespace Character.LockOn
{
    public interface ILockOnTarget
    {
        Transform LockPoint { get; }
        Transform Root { get; }
        bool CanBeLocked { get; }
        bool IsDead { get; }
        int LockPriority { get; }
    }
}
