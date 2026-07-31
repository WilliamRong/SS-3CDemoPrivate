using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// 表现层只依赖锁定结果而不依赖具体输入组件，使本地角色和远端快照可以复用同一套移动混合逻辑。
    /// </summary>
    public interface ILockOnLocomotionQuery
    {
        bool IsLockOnActive { get; }
        Transform CurrentTarget { get; }
    }
}
