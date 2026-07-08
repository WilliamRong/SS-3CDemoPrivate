using Character.Controller;
using Character.Combat;
using Character.StateMachine;
using Character.StateMachine.States;
using Character.Sync;
using Mirror;
using UnityEngine;

namespace Character.Diagnostics
{
    /// <summary>
    /// 右上角显示当前 / 上一帧切换前的状态（CharacterStateId 枚举名）。
    /// 拖同一物体上的 PlayerController；上一状态在“发生切换”时更新。
    /// </summary>
    public sealed class CharacterStateDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;
        [SerializeField] private RemoteActionApplier _remoteActionApplier;

        [Header("Layout")]
        [Tooltip("x：距屏幕右边缘；y：距屏幕上边缘")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(12f, 12f);
        [SerializeField] private int _fontSize = 20;
        [SerializeField] private bool _showNumericId = true;

        private CharacterStateId _lastSeen;
        private CharacterStateId _previous;

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

        private void OnGUI()
        {
            if (_player == null)
                return;

            // 同一 Player 预制体会挂在远端镜像上；仅本地玩家绘制，避免叠多层。
            var netId = _player.GetComponent<NetworkIdentity>();
            if (netId != null && NetworkClient.active && !netId.isLocalPlayer)
                return;

            CharacterStateId current = _player.CurrentStateId;

            var box = new GUIStyle(GUI.skin.box)
            {
                fontSize = _fontSize,
                alignment = TextAnchor.UpperRight,
                richText = true
            };

            string curText = FormatState(current);
            string prevText = FormatState(_previous);

            float w = 420f;
            float h = 128f;
            float x = Screen.width - w - _screenOffset.x;
            var rect = new Rect(x, _screenOffset.y, w, h);

            GUILayout.BeginArea(rect);
            GUILayout.Label($"<b>Current</b>:  {curText}", box);
            GUILayout.Label($"<b>Previous</b>: {prevText}", box);
            GUILayout.Label($"<b>Sprint Phase</b>: {FormatSprintPhase()}", box);
            GUILayout.Label($"<b>Guard Phase</b>:  {FormatGuardPhase()}", box);
            GUILayout.EndArea();
        }

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
            // 枚举打印成文本：Idle / Move / Sprint ...
            string name = id.ToString();
            if (!_showNumericId)
                return name;
            return $"{name}  <color=#888888>({(byte)id})</color>";
        }
    }
}