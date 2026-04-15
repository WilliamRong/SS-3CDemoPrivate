using UnityEngine;

namespace Util
{
    public class LoadSceneAuto : MonoBehaviour
    {
    
        [SerializeField] private string _sceneName;
    
        // Start is called before the first frame update
        void Start()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneName);
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
