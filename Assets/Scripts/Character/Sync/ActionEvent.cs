namespace Character.Sync
{
    /// <summary>
    /// 离散动作与权威生命结果放在同一消息中，保证动画反应和数值纠正以同一顺序到达。
    /// </summary>
    public struct ActionEvent
    {
        public int SeqId;
        public int Tick;
        public int ActorId;
        public ActionType Type;

        public byte HasHealthResult;

        public float AppliedDamage;

        public float CurrentHp;

        public float MaxHp;

        public uint HealthRevision;

        // 保留通用整数槽位以兼容既有动作协议，具体位布局由动作类型解释。
        public int Param;

        public ActionEvent(
            int seqId,
            int tick,
            int actorId,
            ActionType type,
            int param = 0,
            byte hasHealthResult = 0,
            float appliedDamage = 0f,
            float currentHp = 0f,
            float maxHp = 0f,
            uint healthRevision = 0)
        {
            SeqId = seqId;
            Tick = tick;
            ActorId = actorId;
            Type = type;
            Param = param;
            HasHealthResult = hasHealthResult;
            AppliedDamage = appliedDamage;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            HealthRevision = healthRevision;
        }

        public override string ToString()
        {
            return $"[ActionEvent] seq={SeqId}, tick={Tick}, actor={ActorId}, type={Type.ToDebugString()}, param={Param}";
        }
    }
}
