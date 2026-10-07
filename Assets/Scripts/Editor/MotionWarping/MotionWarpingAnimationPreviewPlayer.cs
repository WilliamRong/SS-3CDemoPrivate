using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Character.EditorTools.Execution
{
    internal sealed class MotionWarpingAnimationPreviewPlayer : IDisposable
    {
        private PlayableGraph _graph;
        private AnimationClipPlayable _clipPlayable;
        private Animator _animator;
        private AnimationClip _clip;

        private bool _originalAnimatorEnabled;
        private bool _originalApplyRootMotion;
        private AnimatorCullingMode _originalCullingMode;
        private AnimatorUpdateMode _originalUpdateMode;

        public bool TrySample(
            GameObject previewInstance,
            AnimationClip clip,
            float normalizedTime,
            out string error)
        {
            error = string.Empty;

            if (previewInstance == null)
            {
                error = "预览对象不存在。";
                return false;
            }

            if (clip == null ||
                clip.length <= 0f ||
                float.IsNaN(clip.length) ||
                float.IsInfinity(clip.length))
            {
                error = "动画资源或动画时长无效。";
                return false;
            }

            Animator animator =
                previewInstance.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                error = "预览对象缺少 Animator。";
                return false;
            }

            if (animator.avatar == null ||
                !animator.avatar.isValid)
            {
                error = "预览 Animator 缺少有效 Avatar。";
                return false;
            }

            if (!IsReadyFor(animator, clip))
            {
                Dispose();

                if (!TryCreateGraph(
                        animator,
                        clip,
                        out error))
                {
                    return false;
                }
            }

            try
            {
                double sampleTime =
                    Mathf.Clamp01(normalizedTime) * clip.length;

                _clipPlayable.SetDone(false);
                _clipPlayable.SetTime(sampleTime);
                _graph.Evaluate(0f);
                return true;
            }
            catch (Exception exception)
            {
                error =
                    $"PlayableGraph 姿态采样失败：{exception.Message}";
                Dispose();
                return false;
            }
        }

        public void Dispose()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            if (_animator != null)
            {
                _animator.enabled = _originalAnimatorEnabled;
                _animator.applyRootMotion =
                    _originalApplyRootMotion;
                _animator.cullingMode = _originalCullingMode;
                _animator.updateMode = _originalUpdateMode;
            }

            _graph = default;
            _clipPlayable = default;
            _animator = null;
            _clip = null;
        }

        private bool IsReadyFor(
            Animator animator,
            AnimationClip clip)
        {
            return _graph.IsValid() &&
                   _clipPlayable.IsValid() &&
                   _animator == animator &&
                   _clip == clip;
        }

        private bool TryCreateGraph(
            Animator animator,
            AnimationClip clip,
            out string error)
        {
            error = string.Empty;

            try
            {
                _animator = animator;
                _clip = clip;
                _originalAnimatorEnabled = animator.enabled;
                _originalApplyRootMotion = animator.applyRootMotion;
                _originalCullingMode = animator.cullingMode;
                _originalUpdateMode = animator.updateMode;

                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode =
                    AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;

                _graph = PlayableGraph.Create(
                    $"MW Pose Preview: {clip.name}");

                _graph.SetTimeUpdateMode(
                    DirectorUpdateMode.Manual);

                _clipPlayable =
                    AnimationClipPlayable.Create(
                        _graph,
                        clip);

                _clipPlayable.SetApplyFootIK(false);
                _clipPlayable.SetApplyPlayableIK(false);
                _clipPlayable.SetSpeed(0d);

                AnimationPlayableOutput output =
                    AnimationPlayableOutput.Create(
                        _graph,
                        "Motion Warping Pose Preview",
                        animator);

                output.SetSourcePlayable(_clipPlayable);
                output.SetWeight(1f);

                _graph.Play();
                _graph.Evaluate(0f);
                return true;
            }
            catch (Exception exception)
            {
                error =
                    $"无法创建姿态预览 PlayableGraph：{exception.Message}";
                Dispose();
                return false;
            }
        }
    }
}
