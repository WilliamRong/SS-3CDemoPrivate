using Character.Controller;
using UnityEngine;

namespace Character.Sync
{
    public sealed class SyncDebugOverlay : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerController _localPlayer;
        [SerializeField] private RemoteActionApplier _remoteActionApplier;
        [SerializeField] private RemoteInterpolator _remoteInterpolator;
        [SerializeField] private FakeNetworkPipe _pipe;

        [Header("Layout")]
        [SerializeField] private Vector2 _offset = new(12f, 96f);
        [SerializeField] private int _fontSize = 16;

        private void Awake()
        {
            if (_localPlayer == null) _localPlayer = FindFirstObjectByType<PlayerController>();
            if (_remoteActionApplier == null) _remoteActionApplier = FindFirstObjectByType<RemoteActionApplier>();
            if (_remoteInterpolator == null) _remoteInterpolator = FindFirstObjectByType<RemoteInterpolator>();
            if (_pipe == null) _pipe = FindFirstObjectByType<FakeNetworkPipe>();
        }

        private void OnGUI()
        {
            var box = new GUIStyle(GUI.skin.box)
            {
                fontSize = _fontSize,
                alignment = TextAnchor.UpperLeft
            };

            string localState = _localPlayer != null ? _localPlayer.CurrentStateId.ToString() : "N/A";
            string remoteAction = _remoteActionApplier != null ? _remoteActionApplier.CurrentRemoteAction.ToString() : "N/A";
            string lastSeq = _remoteActionApplier != null ? _remoteActionApplier.LastAppliedSeqId.ToString() : "N/A";
            string posError = _remoteInterpolator != null ? $"{_remoteInterpolator.LastPosError:F2}m" : "N/A";
            string netCfg = _pipe != null ? _pipe.GetNetworkConfigString() : "N/A";

            var rect = new Rect(_offset.x, _offset.y, 520f, 110f);
            GUILayout.BeginArea(rect);
            GUILayout.Label($"LocalState: {localState}", box);
            GUILayout.Label($"RemoteAction: {remoteAction} | LastSeq: {lastSeq}", box);
            GUILayout.Label($"PosError: {posError}", box);
            GUILayout.Label($"FakeNet: {netCfg}", box);
            GUILayout.EndArea();
        }
    }
}
