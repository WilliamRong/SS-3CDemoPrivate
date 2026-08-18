using Character.Controller;
using Character.Combat;
using Character.Execution;
using Character.StateMachine;
using Character.StateMachine.States;
using Character.Sync;
using Mirror;
using UnityEngine;

namespace Character.Diagnostics
{
    /// <summary>
    /// 仅为本地 Player 保留状态转换历史和关键阶段，避免联机场景多个预制体重复绘制调试面板。
    /// </summary>
    public sealed class CharacterStateDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;
        [SerializeField] private RemoteActionApplier _remoteActionApplier;

        [Header("Layout")]
        [Tooltip("x：距屏幕右边缘；y：距屏幕上边缘")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(12f, 12f);
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private float _minScale = 0.6f;
        [SerializeField] private float _maxScale = 2f;
        [SerializeField] private int _fontSize = 20;
        [SerializeField] private Vector2 _buttonSize = new Vector2(92f, 28f);
        [SerializeField] private float _buttonToPanelGap = 8f;
        [SerializeField] private bool _isOpen;
        [SerializeField] private bool _showNumericId = true;

        private CharacterStateId _lastSeen;
        private CharacterStateId _previous;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            if (_player == null)
                _player = GetComponent<PlayerController>();
            if (_remoteActionApplier == null)
                _remoteActionApplier = GetComponent<RemoteActionApplier>();
        }

        private void Update()
        {
            if (_player == null)
                return;

            CharacterStateId current = _player.CurrentStateId;

            if (_lastSeen != current)
            {
                _previous = _lastSeen;
                _lastSeen = current;
            }
        }

        /// <summary>
        /// 保存并恢复 GUI.matrix，防止调试面板的分辨率缩放污染同帧其他 IMGUI 工具。
        /// </summary>
        private void OnGUI()
        {
            if (_player == null)
                return;

            // 同一预制体也存在于远端镜像，只允许本地玩家绘制一次。
            var netId = _player.GetComponent<NetworkIdentity>();
            if (netId != null && NetworkClient.active && !netId.isLocalPlayer)
                return;

            var box = new GUIStyle(GUI.skin.box)
            {
                fontSize = _fontSize,
                alignment = TextAnchor.UpperRight,
                richText = true
            };

            var button = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(10, _fontSize - 4),
                alignment = TextAnchor.MiddleCenter
            };

            Matrix4x4 oldMatrix = GUI.matrix;
            float scale = ResolveGuiScale();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float scaledScreenWidth = Screen.width / scale;
            float buttonX = scaledScreenWidth - _buttonSize.x - _screenOffset.x;
            var buttonRect = new Rect(buttonX, _screenOffset.y, _buttonSize.x, _buttonSize.y);
            if (GUI.Button(buttonRect, _isOpen ? "State ^" : "State v", button))
                _isOpen = !_isOpen;

            if (!_isOpen)
            {
                GUI.matrix = oldMatrix;
                return;
            }

            CharacterStateId current = _player.CurrentStateId;
            string curText = FormatState(current);
            string prevText = FormatState(_previous);

            bool hasExecutingState =
                _player.TryGetActiveExecutingState(out ExecutingState executingState);
            ExecutionWarpContext warp = hasExecutingState
                ? executingState.WarpContext
                : null;

            float w = warp != null ? 720f : 420f;
            float h = warp != null ? 352f : 128f;
            float x = scaledScreenWidth - w - _screenOffset.x;
            float y = _screenOffset.y + _buttonSize.y + _buttonToPanelGap;
            var rect = new Rect(x, y, w, h);

            GUILayout.BeginArea(rect);
            GUILayout.Label($"<b>Current</b>:  {curText}", box);
            GUILayout.Label($"<b>Previous</b>: {prevText}", box);
            GUILayout.Label($"<b>Sprint Phase</b>: {FormatSprintPhase()}", box);
            GUILayout.Label($"<b>Guard Phase</b>:  {FormatGuardPhase()}", box);

            if (warp != null)
            {
                GUILayout.Label(
                    $"<b>Execution Warp</b>: #{warp.ExecutionId}  " +
                    $"time={executingState.NormalizedTime:F3}  " +
                    $"window=[{warp.WindowStartNormalized:F3}, {warp.WindowEndNormalized:F3}]",
                    box);
                GUILayout.Label(
                    $"<b>Weight</b>: {warp.LastCumulativeWeight:F3}  " +
                    $"windowComplete={warp.IsWarpWindowComplete}",
                    box);
                GUILayout.Label(
                    $"<b>Root Delta Pos</b>: {FormatVector(warp.LastOriginalDeltaPosition)}" +
                    $" -> {FormatVector(warp.LastCorrectedDeltaPosition)}",
                    box);
                GUILayout.Label(
                    $"<b>Root Delta Yaw</b>: {warp.LastOriginalDeltaYaw:F2} deg" +
                    $" -> {warp.LastCorrectedDeltaYaw:F2} deg",
                    box);
                GUILayout.Label(
                    $"<b>Requested Correction</b>: " +
                    $"pos={FormatVector(warp.RequestedTranslationCorrection)}  " +
                    $"yaw={warp.RequestedYawCorrection:F2} deg",
                    box);
                GUILayout.Label(
                    $"<b>Position Residual</b>: " +
                    $"pred={FormatVector(warp.PredictedRemainingPositionError)}  " +
                    $"actual={FormatVector(warp.ActualRemainingPositionError)}",
                    box);
                GUILayout.Label(
                    $"<b>Yaw Residual</b>: " +
                    $"pred={warp.PredictedRemainingYawError:F2} deg  " +
                    $"actual={warp.ActualRemainingYawError:F2} deg",
                    box);
                GUILayout.Label(
                    warp.HasMotorApplicationSample
                        ? $"<b>Motor Shortfall</b>: " +
                          $"pos={FormatVector(warp.LastMotorPositionShortfall)}  " +
                          $"yaw={warp.LastMotorYawShortfall:F2} deg"
                        : "<b>Motor Shortfall</b>: waiting for first applied delta",
                    box);
            }

            GUILayout.EndArea();

            GUI.matrix = oldMatrix;
        }

        // ============ 响应式布局 ============

        private float ResolveGuiScale()
        {
            float referenceWidth = Mathf.Max(1f, _referenceResolution.x);
            float referenceHeight = Mathf.Max(1f, _referenceResolution.y);
            float scale = Mathf.Min(Screen.width / referenceWidth, Screen.height / referenceHeight);
            return Mathf.Clamp(scale, Mathf.Max(0.01f, _minScale), Mathf.Max(_minScale, _maxScale));
        }

        // ============ 状态格式化 ============

        private string FormatSprintPhase()
        {
            if (_player.TryGetActiveSprintState(out SprintState sprint))
                return sprint.CurrentPhase.ToString();

            return "—";
        }

        private string FormatGuardPhase()
        {
            if (_remoteActionApplier != null)
            {
                if (_remoteActionApplier.CurrentRemoteAction == ActionType.GuardBreak)
                    return "GuardBreak";

                if (_remoteActionApplier.CurrentRemoteAction == ActionType.GuardHit)
                    return FormatGuardReaction(_remoteActionApplier.LastGuardReaction);
            }

            if (_player.TryGetActiveGuardState(out GuardState guard))
                return guard.CurrentPhase.ToString();

            return "—";
        }

        private static string FormatGuardReaction(GuardReactionType reaction)
        {
            return reaction switch
            {
                GuardReactionType.Hit1 => "GuardHit1",
                GuardReactionType.Hit2 => "GuardHit2",
                GuardReactionType.Hit3 => "GuardHit3",
                GuardReactionType.Break => "GuardBreak",
                _ => "—",
            };
        }

        private string FormatState(CharacterStateId id)
        {
            string name = id.ToString();
            if (!_showNumericId)
                return name;
            return $"{name}  <color=#888888>({(byte)id})</color>";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
        }
    }
}
