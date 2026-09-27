using System;
using System.Collections.Generic;
using Character.Execution;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Character.EditorTools.Execution
{
    public sealed class MotionWarpingRootMotionSample
    {
        public MotionWarpingRootMotionSample(
          AnimationClip clip,
          int sampleRate,
          IReadOnlyList<ExecutionWarpInputSample> inputSamples,
          IReadOnlyList<Vector3> accumulatedPositions,
          IReadOnlyList<float> accumulatedYaws,
          Vector3 totalPosition,
          float totalYaw)
        {
            Clip = clip;
            SampleRate = sampleRate;
            InputSamples = inputSamples;
            AccumulatedPositions = accumulatedPositions;
            AccumulatedYaws = accumulatedYaws;
            TotalPosition = totalPosition;
            TotalYaw = totalYaw;
        }

        public AnimationClip Clip { get; }
        public int SampleRate { get; }
        public float Duration => Clip != null ? Clip.length : 0f;
        public IReadOnlyList<ExecutionWarpInputSample> InputSamples { get; }

        public IReadOnlyList<Vector3>
        AccumulatedPositions
        { get; }

        public IReadOnlyList<float>
            AccumulatedYaws
        { get; }

        public Vector3 TotalPosition { get; }

        public float TotalYaw { get; }

        public bool HasRootMotion =>
            TotalPosition.sqrMagnitude > 0.000001f ||
            Mathf.Abs(TotalYaw) > 0.001f;
    }

    public static class MotionWarpingRootMotionSampler
    {
        public static bool TrySample(
            GameObject previewInstance,
            AnimationClip clip,
            int sampleRate,
            out MotionWarpingRootMotionSample result,
            out string error)
        {
                result = null;
                error = string.Empty;
                if (previewInstance == null)
                {
                    error = "预览角色实例为空。";
                    return false;
                }

                if (clip == null)
                {
                    error = "AnimationClip 为空。";
                    return false;
                }

                if (clip.length <= 0f ||
                    !IsFinite(clip.length))
                {
                    error = "AnimationClip 长度无效。";
                    return false;
                }

                if (sampleRate <= 0)
                {
                    error = "采样率必须大于 0。";
                    return false;
                }

                Animator animator =
                    previewInstance.GetComponentInChildren<Animator>(
                        true);

                if (animator == null)
                {
                    error = "预览角色缺少 Animator。";
                    return false;
                }

                if (animator.avatar == null)
                {
                    error = "预览 Animator 缺少 Avatar。";
                    return false;
                }

                PlayableGraph graph = default;

                Transform previewTransform =
                    previewInstance.transform;

                Transform animatorTransform =
                    animator.transform;

                bool animatorUsesPreviewTransform =
                    animatorTransform == previewTransform;

                bool originalActive =
                    previewInstance.activeSelf;

                bool originalAnimatorEnabled =
                    animator.enabled;

                bool originalApplyRootMotion =
                    animator.applyRootMotion;

                AnimatorCullingMode originalCullingMode =
                    animator.cullingMode;

                AnimatorUpdateMode originalUpdateMode =
                    animator.updateMode;

                Vector3 originalPreviewPosition =
                    previewTransform.position;

                Quaternion originalPreviewRotation =
                    previewTransform.rotation;

                Vector3 originalPreviewScale =
                    previewTransform.localScale;

                Vector3 originalAnimatorLocalPosition =
                    animatorTransform.localPosition;

                Quaternion originalAnimatorLocalRotation =
                    animatorTransform.localRotation;

                Vector3 originalAnimatorLocalScale =
                    animatorTransform.localScale;


                try
                {
                    if (!previewInstance.activeSelf)
                    {
                        previewInstance.SetActive(true);
                    }

                    animator.enabled = true;
                    animator.applyRootMotion = true;
                    animator.cullingMode =
                        AnimatorCullingMode.AlwaysAnimate;
                    animator.updateMode =
                        AnimatorUpdateMode.Normal;

                    int frameCount =
                        Mathf.Max(
                            1,
                            Mathf.CeilToInt(
                                clip.length * sampleRate));

                    float deltaTime =
                        clip.length / frameCount;

                    var inputSamples =
                        new List<ExecutionWarpInputSample>(
                            frameCount);

                    var positions =
                        new List<Vector3>(
                            frameCount + 1);

                    var yaws =
                        new List<float>(
                            frameCount + 1);

                    Vector3 accumulatedPosition =
                        Vector3.zero;

                    float accumulatedYaw = 0f;

                    positions.Add(accumulatedPosition);
                    yaws.Add(accumulatedYaw);

                    graph = PlayableGraph.Create(
                        $"MW Sample: {clip.name}");

                    graph.SetTimeUpdateMode(
                        DirectorUpdateMode.Manual);

                    AnimationClipPlayable clipPlayable =
                        AnimationClipPlayable.Create(
                            graph,
                            clip);

                    clipPlayable.SetApplyFootIK(false);
                    clipPlayable.SetApplyPlayableIK(false);
                    clipPlayable.SetTime(0d);
                    clipPlayable.SetSpeed(1d);

                    AnimationPlayableOutput output =
                        AnimationPlayableOutput.Create(
                            graph,
                            "Motion Warping Root Motion",
                            animator);

                    output.SetSourcePlayable(clipPlayable);
                    output.SetWeight(1f);

                    graph.Play();

                    // 初始化 Animator 和 Playable 状态。
                    graph.Evaluate(0f);

                    for (int frame = 1;
                         frame <= frameCount;
                         frame++)
                    {
                        graph.Evaluate(deltaTime);

                        Vector3 deltaPosition =
                            animator.deltaPosition;

                        Quaternion deltaRotation =
                            animator.deltaRotation;

                        float deltaYaw =
                            Mathf.DeltaAngle(
                                0f,
                                deltaRotation.eulerAngles.y);

                        if (!IsFinite(deltaPosition) ||
                            !IsFinite(deltaYaw))
                        {
                            error =
                                $"第 {frame} 帧产生了非有限 Root Motion。";

                            return false;
                        }

                        float normalizedTime =
                            frame / (float)frameCount;

                        inputSamples.Add(
                            new ExecutionWarpInputSample(
                                normalizedTime,
                                deltaPosition,
                                deltaYaw));

                        accumulatedPosition +=
                            deltaPosition;

                        accumulatedYaw +=
                            deltaYaw;

                        positions.Add(
                            accumulatedPosition);

                        yaws.Add(
                            accumulatedYaw);
                    }

                    result =
                        new MotionWarpingRootMotionSample(
                            clip,
                            sampleRate,
                            inputSamples,
                            positions,
                            yaws,
                            accumulatedPosition,
                            accumulatedYaw);

                    return true;
                }
                catch (Exception exception)
                {
                    error =
                        $"PlayableGraph 采样异常：{exception.Message}";

                    result = null;
                    return false;
                }
                finally
                {
                    if (graph.IsValid())
                    {
                        graph.Destroy();
                    }

                    animator.enabled =
                        originalAnimatorEnabled;

                    animator.applyRootMotion =
                        originalApplyRootMotion;

                    animator.cullingMode =
                        originalCullingMode;

                    animator.updateMode =
                        originalUpdateMode;

                    previewTransform.SetPositionAndRotation(
                        originalPreviewPosition,
                        originalPreviewRotation);

                    previewTransform.localScale =
                        originalPreviewScale;

                    if (!animatorUsesPreviewTransform)
                    {
                        animatorTransform.localPosition =
                            originalAnimatorLocalPosition;

                        animatorTransform.localRotation =
                            originalAnimatorLocalRotation;

                        animatorTransform.localScale =
                            originalAnimatorLocalScale;
                    }

                    if (previewInstance != null &&
                        previewInstance.activeSelf != originalActive)
                    {
                        previewInstance.SetActive(
                            originalActive);
                    }
                }

        }

        private static bool IsFinite(
            Vector3 value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
