using System;

namespace Character.Sync
{
    public interface ISyncTransport
    {
        event Action<StateSnapshot> OnSnapshotReceived;
        event Action<ActionEvent> OnActionEventReceived;

        void SendSnapshot(StateSnapshot snapshot);
        void SendActionEvent(ActionEvent actionEvent);

    }
}
