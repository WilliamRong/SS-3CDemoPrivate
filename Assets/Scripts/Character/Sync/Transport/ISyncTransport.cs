using System;

namespace Character.Sync
{
    /// <summary>
    /// 发布与接收只依赖统一消息契约，使离线网络模拟和 Mirror 实际传输能够无条件替换。
    /// </summary>
    public interface ISyncTransport
    {
        event Action<StateSnapshot> OnSnapshotReceived;
        event Action<ActionEvent> OnActionEventReceived;
        event Action<ExecutionStartMsg> OnExecutionStartReceived;
        event Action<ExecutionResultMsg> OnExecutionResultReceived;

        void SendSnapshot(StateSnapshot snapshot);
        void SendActionEvent(ActionEvent actionEvent);
    }
}
