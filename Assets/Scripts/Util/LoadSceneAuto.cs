using UnityEngine;
using UnityEngine.SceneManagement;

namespace Util
{
    /// <summary>
    /// 让引导场景只承担启动入口职责，避免正式场景的初始化依赖散落在多个按钮或事件中。
    /// </summary>
    public class LoadSceneAuto : MonoBehaviour
    {
        [SerializeField] private string _sceneName;

        /// <summary>
        /// 放在 Start 而不是 Awake，给同帧内更早的启动组件留出完成初始化的机会。
        /// </summary>
        private void Start()
        {
            if (string.IsNullOrEmpty(_sceneName)) return;
            SceneManager.LoadScene(_sceneName);
        }
    }
}
