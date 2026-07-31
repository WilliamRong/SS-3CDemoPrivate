namespace Character.Sync
{
    /// <summary>
    /// Transport 选择保持显式编号，防止 Inspector 已序列化值因枚举重排改变含义。
    /// </summary>
    public enum SyncTransportMode
    {
        Fake = 0,
        Mirror = 1
    }
}
