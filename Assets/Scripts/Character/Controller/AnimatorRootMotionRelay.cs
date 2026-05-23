using UnityEngine;

namespace Character.Controller
{
    [RequireComponent(typeof(Animator))]
    public sealed class AnimatorRootMotionRelay : MonoBehaviour
    {
        private Animator _animator;
        private PlayerController _owner;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void Initialize(PlayerController owner)
        {
            _owner = owner;
            if (_animator == null)
                _animator = GetComponent<Animator>();
        }

        private void OnAnimatorMove()
        {
            if (_owner == null || _animator == null)
                return;

            _owner.HandleAnimatorRootMotion(_animator.deltaPosition, _animator.deltaRotation);
        }
    }
}
