using Character.Config;
using Character.Presentation;
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
        /// <summary>Valid when <see cref="StateId"/> is Dodge; otherwise 0. See <see cref="Presentation.DodgeMode"/>.</summary>
        public byte DodgeMode;
        /// <summary>Valid when <see cref="StateId"/> is Guard; otherwise 0. See <see cref="GuardState.GuardPhase"/>.</summary>
        public byte GuardPhase;

        public StateSnapshot(
            int tick,
            int actorId,
            Vector3 position,
            float yaw,
            Vector2 velocityXZ,
            CharacterStateId stateId,
            byte sprintPhase = 0,
            byte dodgeMode = 0,
            byte guardPhase = 0)
        {
            Tick = tick;
            ActorId = actorId;
            Position = position;
            Yaw = yaw;
            VelocityXZ = velocityXZ;
            StateId = stateId;
            SprintPhase = sprintPhase;
            DodgeMode = dodgeMode;
            GuardPhase = guardPhase;
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

        public Presentation.DodgeMode GetDodgeModeOrDefault()
        {
            if (StateId != CharacterStateId.Dodge)
                return Presentation.DodgeMode.None;

            if (DodgeMode < (byte)Presentation.DodgeMode.NeutralBackward
                || DodgeMode > (byte)Presentation.DodgeMode.LockOn8Way)
                return Presentation.DodgeMode.None;

            return (Presentation.DodgeMode)DodgeMode;
        }

        public GuardState.GuardPhase GetGuardPhaseOrDefault()
        {
            if (StateId != CharacterStateId.Guard)
                return GuardState.GuardPhase.Start;

            if (GuardPhase > (byte)GuardState.GuardPhase.Exit)
                return GuardState.GuardPhase.Start;

            return (GuardState.GuardPhase)GuardPhase;
        }

        public StateSnapshot WithDodgeMode(byte dodgeMode)
        {
            var copy = this;
            copy.DodgeMode = dodgeMode;
            return copy;
        }

        public bool TryBuildDodgePresentationContext(
            CharacterCombatConfig combat,
            out DodgePresentationContext ctx)
        {
            ctx = default;
            var mode = GetDodgeModeOrDefault();
            if (mode == Presentation.DodgeMode.None)
                return false;

            var yawRot = Quaternion.Euler(0f, Yaw, 0f);
            Vector2 blendLocal = Vector2.zero;
            Vector3 worldDir;

            switch (mode)
            {
                case Presentation.DodgeMode.NeutralBackward:
                    worldDir = yawRot * Vector3.back;
                    break;
                case Presentation.DodgeMode.LockOn8Way:
                    blendLocal = VelocityXZ;
                    var local = new Vector3(blendLocal.x, 0f, blendLocal.y);
                    worldDir = local.sqrMagnitude > 0.0001f
                        ? yawRot * local.normalized
                        : yawRot * Vector3.forward;
                    break;
                default:
                    worldDir = yawRot * Vector3.forward;
                    break;
            }

            worldDir.y = 0f;
            if (worldDir.sqrMagnitude > 0.0001f)
                worldDir.Normalize();

            float duration = combat.GetDodgeDuration(mode);
            float moveDuration = combat.GetDodgeMoveDuration(mode);
            ctx = new DodgePresentationContext(mode, blendLocal, duration, moveDuration, worldDir);
            return ctx.IsValid;
        }

        public override string ToString()
        {
            return $"[Snapshot] tick={Tick}, actor={ActorId}, state={StateId}, sprintPhase={SprintPhase}, dodgeMode={DodgeMode}, guardPhase={GuardPhase}, " +
                   $"pos=({Position.x:F2},{Position.y:F2},{Position.z:F2}), " +
                   $"yaw={Yaw:F1}, velXZ=({VelocityXZ.x:F2},{VelocityXZ.y:F2})";
        }
    }
}
