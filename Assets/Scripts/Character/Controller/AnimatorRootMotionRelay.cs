using UnityEngine;

namespace Character.Controller
{
    public interface IAnimatorRootMotionReceiver
    {
        void HandleAnimatorRootMotion(Vector3 deltaPosition, Quaternion deltaRotation);
    }

    [RequireComponent(typeof(Animator))]
    public sealed class AnimatorRootMotionRelay : MonoBehaviour
    {
        private Animator _animator;
        private IAnimatorRootMotionReceiver _receiver;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void Initialize(PlayerController owner)
        {
            Initialize((IAnimatorRootMotionReceiver)owner);
        }

        public void Initialize(IAnimatorRootMotionReceiver receiver)
        {
            _receiver = receiver;
            if (_animator == null)
                _animator = GetComponent<Animator>();
        }

        private void OnAnimatorMove()
        {
            if (_receiver == null || _animator == null)
                return;

            _receiver.HandleAnimatorRootMotion(_animator.deltaPosition, _animator.deltaRotation);
        }
    }
}
