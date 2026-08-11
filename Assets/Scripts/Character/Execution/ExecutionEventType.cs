


namespace Character.Execution
{

    public enum ExecutionEventType : byte
    {
        None = 0,
        Request = 1,
        Started = 2,
        ResultCommitted = 3,
        Completed = 4,
        Cancelled = 5,
    }

    public static class ExecutionEventTypeExtensions
    {
        public static string ToDebugString(this ExecutionEventType type)
        {
            return type switch
            {
                ExecutionEventType.None => "None",
                ExecutionEventType.Request => "Request",
                ExecutionEventType.Started => "Started",
                ExecutionEventType.ResultCommitted => "ResultCommitted",
                ExecutionEventType.Completed => "Completed",
                ExecutionEventType.Cancelled => "Cancelled",
                _ => $"Unknown({(byte)type})",
            };
        }
    }
}