using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// Debug lock-on toggle for eight-way dodge. Replace with real lock-on later.
    /// </summary>
    public sealed class PlayerLockOnQuery : MonoBehaviour, ILockOnLocomotionQuery
    {
        [SerializeField] private bool _lockOnActive;

        public bool IsLockOnActive => _lockOnActive;

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
                _lockOnActive = !_lockOnActive;
        }
    }
}
