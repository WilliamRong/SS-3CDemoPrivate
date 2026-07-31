using UnityEngine;

namespace Core
{
    /// <summary>
    /// 使用无缩放时间观察渲染帧率，使暂停或慢动作不会污染性能判断。
    /// </summary>
    public class FPSDisplay : MonoBehaviour
    {
        private float _deltaTime;

        private void Update()
        {
            // 指数平滑比逐帧倒数稳定，避免标签在相邻帧之间剧烈跳动。
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        }

        private void OnGUI()
        {
            float fps = 1f / _deltaTime;
            GUIStyle style = new GUIStyle();
            style.fontSize = 30;
            style.normal.textColor = fps >= 50 ? Color.green : fps >= 30 ? Color.yellow : Color.red;
            GUI.Label(new Rect(10, 10, 200, 50), $"FPS: {fps:F0}");
        }
    }
}
