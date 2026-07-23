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

            float w = 420f;
            float h = 128f;
            float x = scaledScreenWidth - w - _screenOffset.x;
            float y = _screenOffset.y + _buttonSize.y + _buttonToPanelGap;
            var rect = new Rect(x, y, w, h);

            GUILayout.BeginArea(rect);
            GUILayout.Label($"<b>Current</b>:  {curText}", box);
            GUILayout.Label($"<b>Previous</b>: {prevText}", box);
            GUILayout.Label($"<b>Sprint Phase</b>: {FormatSprintPhase()}", box);
            GUILayout.Label($"<b>Guard Phase</b>:  {FormatGuardPhase()}", box);
            GUILayout.EndArea();

            GUI.matrix = oldMatrix;
        }

        private float ResolveGuiScale()
        {
            float referenceWidth = Mathf.Max(1f, _referenceResolution.x);
            float referenceHeight = Mathf.Max(1f, _referenceResolution.y);
            float scale = Mathf.Min(Screen.width / referenceWidth, Screen.height / referenceHeight);
            return Mathf.Clamp(scale, Mathf.Max(0.01f, _minScale), Mathf.Max(_minScale, _maxScale));
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
