using UnityEngine;

namespace Character.Controller
{
    /// <summary>
    /// 让根位移接收者不依赖 Animator 所在子物体，Player 与 NPC 可以各自决定权威消费规则。
    /// </summary>
    public interface IAnimatorRootMotionReceiver
    {
        void HandleAnimatorRootMotion(Vector3 deltaPosition, Quaternion deltaRotation);
    }

    /// <summary>
    /// 从模型层 Animator 转发根位移到角色所有者，避免动画子层直接修改角色根节点。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class AnimatorRootMotionRelay : MonoBehaviour
    {
        private Animator _animator;
        private IAnimatorRootMotionReceiver _receiver;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        // ============ 接收者绑定 ============

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

        // ============ Animator 回调 ============

        private void OnAnimatorMove()
        {
            if (_receiver == null || _animator == null)
                return;

            _receiver.HandleAnimatorRootMotion(_animator.deltaPosition, _animator.deltaRotation);
        }
    }
}
