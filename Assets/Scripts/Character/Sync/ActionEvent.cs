namespace Character.Sync
{
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

        //预留参数
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
