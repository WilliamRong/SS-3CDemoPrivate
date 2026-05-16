namespace Character.Presentation
{
    /// <summary>
    /// 锁定目标时返回 true，表现层才使用八向 VelocityX/Z。
    /// 未挂实现或返回 false 时仅前向 Locomotion。
    /// </summary>
    public interface ILockOnLocomotionQuery
    {
        bool IsLockOnActive { get; }
    }
}