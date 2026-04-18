using Character.Controller;
using Character.StateMachine;
using UnityEngine;

namespace Character.Diagnostics
{
    /// <summary>
    /// 左上角显示当前 / 上一帧切换前的状态（CharacterStateId 枚举名）。
    /// 拖同一物体上的 PlayerController；上一状态在“发生切换”时更新。
    /// </summary>
    public sealed class CharacterStateDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;

        [Header("Layout")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(12f, 12f);
        [SerializeField] private int _fontSize = 20;
        [SerializeField] private bool _showNumericId = true;

        private CharacterStateId _lastSeen;
        private CharacterStateId _previous;

        private void Awake()
        {
            if (_player == null)
                _player = GetComponent<PlayerController>();
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

            CharacterStateId current = _player.CurrentStateId;

            var box = new GUIStyle(GUI.skin.box)
            {
                fontSize = _fontSize,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };

            string curText = FormatState(current);
            string prevText = FormatState(_previous);

            float w = 420f;
            float h = 72f;
            var rect = new Rect(_screenOffset.x, _screenOffset.y, w, h);

            GUILayout.BeginArea(rect);
            GUILayout.Label($"<b>Current</b>:  {curText}", box);
            GUILayout.Label($"<b>Previous</b>: {prevText}", box);
            GUILayout.EndArea();
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