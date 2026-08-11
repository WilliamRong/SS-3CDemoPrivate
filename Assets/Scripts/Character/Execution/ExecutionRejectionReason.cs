


namespace Character.Execution
{
    public enum ExecutionRejectionReason : byte
    {
        None = 0,

        ExecutorMissing = 1,
        TargetMissing = 2,
        SameActor = 3,
        InvalidConfiguration = 4,
        InvalidExecutorPose = 5,
        InvalidTargetPose = 6,
        InvalidSpatialComputation = 7,

        ExecutorDead = 8,
        TargetDead = 9,
        ExecutorStateInvalid = 10,
        TargetStateInvalid = 11,
        ExecutorOccupied = 12,
        TargetOccupied = 13,

        DistanceExceeded = 14,
        FrontAngleExceeded = 15,
        HeightDifferenceExceeded = 16,
        LineOfSightBlocked = 17,
        PathBlocked = 18,
        WarpTranslationExceeded = 19,
        WarpYawExceeded = 20,

        Unauthorized = 21,
        ExecutorNotPlayer = 22,
        ExecutorCollisionShapeMissing = 23,
    }
}
