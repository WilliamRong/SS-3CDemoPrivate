using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PeekTest
{
    public sealed class PeekVisionRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            [Range(1, 4)] public int downsample = 2;
            [Range(1, 6)] public int blurPasses = 3;
            [Range(0.5f, 4f)] public float blurRadius = 1.4f;
        }

        [SerializeField] private Settings _settings = new Settings();

        private Material _material;
        private PeekVisionPass _pass;

        public override void Create()
        {
            Shader shader = Shader.Find("Hidden/PeekTest/PeekVisionComposite");
            if (shader == null)
            {
                return;
            }

            if (_material == null || _material.shader != shader)
            {
                CoreUtils.Destroy(_material);
                _material = CoreUtils.CreateEngineMaterial(shader);
            }

            _pass = new PeekVisionPass(_material, _settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (_pass == null || _material == null || camera == null ||
                renderingData.cameraData.cameraType != CameraType.Game ||
                camera.GetComponent<PeekVisionController>() == null)
            {
                return;
            }

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private sealed class PeekVisionPass : ScriptableRenderPass
        {
            private static readonly int BlurOffsetId = Shader.PropertyToID("_PeekBlurOffset");
            private static readonly int BlurTexelSizeId = Shader.PropertyToID("_PeekBlurTexelSize");
            private static readonly int BlurTextureId = Shader.PropertyToID("_PeekBlurTexture");

            private readonly Material _material;
            private readonly Settings _settings;
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Peek Vision Blur");

            private RTHandle _colorCopy;
            private RTHandle _blurA;
            private RTHandle _blurB;

            public PeekVisionPass(Material material, Settings settings)
            {
                _material = material;
                _settings = settings;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor colorDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                colorDescriptor.depthBufferBits = 0;
                colorDescriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(
                    ref _colorCopy,
                    colorDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_PeekColorCopy");

                RenderTextureDescriptor blurDescriptor = colorDescriptor;
                int divisor = Mathf.Max(1, _settings.downsample);
                blurDescriptor.width = Mathf.Max(1, blurDescriptor.width / divisor);
                blurDescriptor.height = Mathf.Max(1, blurDescriptor.height / divisor);
                RenderingUtils.ReAllocateIfNeeded(
                    ref _blurA,
                    blurDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_PeekBlurA");
                RenderingUtils.ReAllocateIfNeeded(
                    ref _blurB,
                    blurDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_PeekBlurB");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (cameraColor == null || _colorCopy == null || _blurA == null || _blurB == null)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    Blitter.BlitCameraTexture(cmd, cameraColor, _colorCopy);

                    _material.SetFloat(BlurOffsetId, _settings.blurRadius);
                    cmd.SetGlobalVector(BlurTexelSizeId, GetTexelSize(_colorCopy));
                    Blitter.BlitCameraTexture(cmd, _colorCopy, _blurA, _material, 0);

                    RTHandle current = _blurA;
                    RTHandle next = _blurB;
                    int passCount = Mathf.Max(1, _settings.blurPasses);
                    for (int i = 1; i < passCount; i++)
                    {
                        _material.SetFloat(BlurOffsetId, _settings.blurRadius + i);
                        cmd.SetGlobalVector(BlurTexelSizeId, GetTexelSize(current));
                        Blitter.BlitCameraTexture(cmd, current, next, _material, 0);
                        RTHandle swap = current;
                        current = next;
                        next = swap;
                    }

                    cmd.SetGlobalTexture(BlurTextureId, current.nameID);
                    Blitter.BlitCameraTexture(cmd, _colorCopy, cameraColor, _material, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            private static Vector4 GetTexelSize(RTHandle texture)
            {
                int width = Mathf.Max(1, texture.rt.width);
                int height = Mathf.Max(1, texture.rt.height);
                return new Vector4(1f / width, 1f / height, width, height);
            }

            public void Dispose()
            {
                _colorCopy?.Release();
                _blurA?.Release();
                _blurB?.Release();
            }
        }
    }
}
