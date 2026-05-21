using UnityEngine;
using UnityEngine.SceneManagement;

namespace Util
{
    /// <summary>
    /// Loads <see cref="_sceneName"/> as soon as the host scene starts.
    /// Used as a boot-time redirect (e.g. Splash → SampleScene).
    /// </summary>
    public class LoadSceneAuto : MonoBehaviour
    {
        [SerializeField] private string _sceneName;

        private void Start()
        {
            if (string.IsNullOrEmpty(_sceneName)) return;
            SceneManager.LoadScene(_sceneName);
        }
    }
}
