using UnityEngine;

namespace Character.LockOn
{
    /// <summary>
    /// 锁定搜索只依赖稳定的空间与可用性契约，因此 Player、NPC 或其他实体无需共享具体组件类型。
    /// </summary>
    public interface ILockOnTarget
    {
        Transform LockPoint { get; }
        Transform Root { get; }
        bool CanBeLocked { get; }
        bool IsDead { get; }
        int LockPriority { get; }
    }
}
