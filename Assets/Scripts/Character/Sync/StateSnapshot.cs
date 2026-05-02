using Character.StateMachine;
using UnityEngine;

namespace Character.Sync
{
    public struct StateSnapshot
    {
        public int Tick;
        public float ArrivalTimeSec;
        public int ActorId;

        public Vector3 Position;
        public float Yaw;
        public Vector2 VelocityXZ;

        public CharacterStateId StateId;

        public StateSnapshot(
            int tick,
            int actorId,
            Vector3 position,
            float yaw,
            Vector2 velocityXZ,
            CharacterStateId stateId)
        {
            Tick = tick;
            ActorId = actorId;
            Position = position;
            Yaw = yaw;
            VelocityXZ = velocityXZ;
            StateId = stateId;
            ArrivalTimeSec = 0f;
        }

        public override string ToString()
        {
            return $"[Snapshot] tick={Tick}, actor={ActorId}, state={StateId}, " +
                   $"pos=({Position.x:F2},{Position.y:F2},{Position.z:F2}), " +
                   $"yaw={Yaw:F1}, velXZ=({VelocityXZ.x:F2},{VelocityXZ.y:F2})";
        }
    }
}
