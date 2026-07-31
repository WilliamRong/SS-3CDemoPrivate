namespace Character.Combat
{
    /// <summary>
    /// 使用离散编号同步格挡表现，避免远端根据本地角度或随机数重新选择动画。
    /// </summary>
    public enum GuardReactionType : byte
    {
        None = 0,
        Hit1 = 1,
        Hit2 = 2,
        Hit3 = 3,
        Break = 4
    }
}
