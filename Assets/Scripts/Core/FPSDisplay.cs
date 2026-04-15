using UnityEngine;

namespace Core
{
    public class FPSDisplay : MonoBehaviour
    {
        private float _deltaTime;

        void Update()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        }

        void OnGUI()
        {
            float fps = 1f / _deltaTime;
            GUIStyle style = new GUIStyle();
            style.fontSize = 30;
            style.normal.textColor = fps >= 50 ? Color.green : fps >= 30 ? Color.yellow : Color.red;
            GUI.Label(new Rect(10, 10, 200, 50), $"FPS: {fps:F0}");
        }
    }
}
