using Character.StateMachine;
using Character.StateMachine.States;
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
        /// <summary>Valid when <see cref="StateId"/> is Sprint; otherwise 0.</summary>
        public byte SprintPhase;

        public StateSnapshot(
            int tick,
            int actorId,
            Vector3 position,
            float yaw,
            Vector2 velocityXZ,
            CharacterStateId stateId,
            byte sprintPhase = 0)
        {
            Tick = tick;
            ActorId = actorId;
            Position = position;
            Yaw = yaw;
            VelocityXZ = velocityXZ;
            StateId = stateId;
            SprintPhase = sprintPhase;
            ArrivalTimeSec = 0f;
        }

        public SprintState.SprintPhase GetSprintPhaseOrDefault()
        {
            if (StateId != CharacterStateId.Sprint)
                return SprintState.SprintPhase.Loop;

            return System.Enum.IsDefined(typeof(SprintState.SprintPhase), (int)SprintPhase)
                ? (SprintState.SprintPhase)SprintPhase
                : SprintState.SprintPhase.Loop;
        }

        public override string ToString()
        {
            return $"[Snapshot] tick={Tick}, actor={ActorId}, state={StateId}, sprintPhase={SprintPhase}, " +
                   $"pos=({Position.x:F2},{Position.y:F2},{Position.z:F2}), " +
                   $"yaw={Yaw:F1}, velXZ=({VelocityXZ.x:F2},{VelocityXZ.y:F2})";
        }
    }
}
