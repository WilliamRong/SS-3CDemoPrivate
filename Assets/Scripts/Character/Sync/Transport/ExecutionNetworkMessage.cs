using Mirror;


namespace Character.Sync
{
    /// <summary>
    /// Client只提交目标和请求序号
    /// 处决者身份必须由Server根据连接所有权确定
    /// </summary>
    public struct ExecutionRequestMsg : NetworkMessage
    {
        public uint RequestSeq;
        public int TargetActorId;
    }

    /// <summary>
    /// Server接受请求后广播的完整处决会话
    /// 字段保持扁平，便于MirrorWeaver生成稳定协议
    /// </summary>
    public struct ExecutionStartMsg : NetworkMessage
    {
        public uint RequestSeq;

        public ulong ExecutionId;
        public int ExecutorActorId;
        public int TargetActorId;

        public float FixedTargetPx;
        public float FixedTargetPy;
        public float FixedTargetPz;
        public float FixedTargetYaw;

        public float ExecutorAnchorPx;
        public float ExecutorAnchorPy;
        public float ExecutorAnchorPz;
        public float ExecutorAnchorYaw;

        public double StartTimeSec;
        public double ResultTimeSec;

        //用来在接收端重建ExecutionSession
        public float ExecutionDamage;
        public byte TargetWillDie;
        public byte DeathVariant;

        //对应ExecutionSessionFlags,允许晚加入时发送当前状态
        public byte SessionFlags;
    }


    /// <summary>
    /// Server在executionResultTime提交的绝对生命结果
    /// 重复消息由executionId和HealthRevision去重
    /// </summary>
    public struct ExecutionResultMsg : NetworkMessage
    {
        public ulong ExecutionId;
        public int ExecutorActorId;
        public int TargetActorId;

        public double AuthorityTimeSec;

        public float CurrentHp;
        public float MaxHp;
        public uint HealthRevision;

        public byte DeathVariant;
    }

    /// <summary>
    /// 广播双方独立完成，取消等累计会话边沿
    /// SessionFlags使用累计值，处理重复和乱序
    /// </summary>
    public struct ExecutionCompleteMsg : NetworkMessage
    {
        public ulong ExecutionId;
        public int ExecutorActorId;
        public int TargetActorId;

        public double AuthorityTimeSec;

        public byte SessionFlags;

        public byte DeathVariant;
    }

    /// <summary>
    /// Client 请求指定 Actor 当前关联的权威处决状态。
    /// </summary>
    public struct ExecutionStateRequestMsg : NetworkMessage
    {
        public uint RequestSeq;
        public int ActorId;
    }

    /// <summary>
    /// Server 返回活跃会话或短期缓存的终态。
    /// 使用扁平字段，保持 Mirror 协议布局明确。
    /// </summary>
    public struct ExecutionStateMsg : NetworkMessage
    {
        public uint RequestSeq;
        public int RequestedActorId;
        public byte HasState;

        public ulong ExecutionId;
        public int ExecutorActorId;
        public int TargetActorId;

        public float FixedTargetPx;
        public float FixedTargetPy;
        public float FixedTargetPz;
        public float FixedTargetYaw;

        public float ExecutorAnchorPx;
        public float ExecutorAnchorPy;
        public float ExecutorAnchorPz;
        public float ExecutorAnchorYaw;

        // 返回消息时处决者的绝对权威姿态。
        public float ExecutorPx;
        public float ExecutorPy;
        public float ExecutorPz;
        public float ExecutorYaw;

        public double StartTimeSec;
        public double ResultTimeSec;
        public double AuthorityTimeSec;

        public float ExecutionDamage;
        public byte TargetWillDie;
        public byte SessionFlags;

        public byte HasHealthResult;
        public float CurrentHp;
        public float MaxHp;
        public uint HealthRevision;

        public byte DeathVariant;
    }
}
