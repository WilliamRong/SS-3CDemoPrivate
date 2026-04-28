namespace Character.Sync
{
    public struct ActionEvent
    {
        public int SeqId;
        public int Tick;
        public int ActorId;
        public ActionType Type;

        //预留参数
        public int Param;
        
        public ActionEvent(
            int seqId,
            int tick,
            int actorId,
            ActionType type,
            int param = 0)
        {
            SeqId = seqId;
            Tick = tick;
            ActorId = actorId;
            Type = type;
            Param = param;
        }

        public override string ToString()
        {
            return $"[ActionEvent] seq={SeqId}, tick={Tick}, actor={ActorId}, type={Type.ToDebugString()}, param={Param}";
        }
    }
}
