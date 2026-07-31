using Character.Controller;
using Character.Sync;
using Mirror;
using UnityEngine;

namespace Character.Diagnostics
{
    /// <summary>
    /// 汇总本地状态、远端动作、位置误差和假网络参数，便于在同一画面判断同步问题属于哪一段链路。
    /// </summary>
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

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            if (_remoteActionApplier == null) _remoteActionApplier = FindFirstObjectByType<RemoteActionApplier>();
            if (_remoteInterpolator == null) _remoteInterpolator = FindFirstObjectByType<RemoteInterpolator>();
            if (_pipe == null) _pipe = FindFirstObjectByType<FakeNetworkPipe>();
        }

        /// <summary>
        /// 每次绘制前重新解析本地玩家，允许调试面板跨越网络重连而不持有已销毁的对象引用。
        /// </summary>
        private void OnGUI()
        {
            TryResolveLocalPlayer();

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

        // ============ 本地玩家解析 ============

        /// <summary>
        /// 联机优先绑定 Mirror localPlayer，只有离线模式才回退场景搜索，防止误显示远端 Player 状态。
        /// </summary>
        private bool TryResolveLocalPlayer()
        {
            if (NetworkClient.active && NetworkClient.localPlayer != null)
            {
                var pc = NetworkClient.localPlayer.GetComponent<PlayerController>();
                if (pc != null)
                {
                    _localPlayer = pc;
                    return true;
                }
            }

            if (_localPlayer == null)
                _localPlayer = FindFirstObjectByType<PlayerController>();

            return _localPlayer != null;
        }
    }
}
