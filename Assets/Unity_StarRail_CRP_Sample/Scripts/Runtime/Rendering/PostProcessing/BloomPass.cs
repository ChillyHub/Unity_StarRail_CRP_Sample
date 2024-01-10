using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class BloomPass : IDisposable
    {
        private static class ShaderConstants
        {
            public static readonly int Params = Shader.PropertyToID("_Params");
            public static readonly int SourceTexLowMip = Shader.PropertyToID("_SourceTexLowMip");
            public static readonly int BloomTexture = Shader.PropertyToID("_BloomTexture");
            public static readonly int AdditionalBloomColorTexture = Shader.PropertyToID("_AdditionalBloomColorTexture");
        }
        
        // Profiling samplers
        private readonly ProfilingSampler _bloomSampler;
        
        // Material
        private Material _material;
        
        // Render Targets
        private RTHandle[] _bloomMipUp;
        private RTHandle[] _bloomMipDown;

        public BloomPass()
        {
            _bloomSampler = new ProfilingSampler("CRP Bloom");
            
            const string shaderName = "Hidden/StarRail_CRP/PostProcessing/Bloom";
            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogWarning($"Can't find shader: {shaderName}");
            }
            else
            {
                _material = CoreUtils.CreateEngineMaterial(shader);
            }
            
            InitRenderTarget();
        }
        
        public void Execute(CommandBuffer cmd, RTHandle source)
        {
            var bloom = VolumeManager.instance.stack.GetComponent<CRPBloom>();

            using (new ProfilingScope(cmd, _bloomSampler))
            {
                // Start at half-res
                int downres = 1;
                switch (bloom.downscale.value)
                {
                    case BloomDownscaleMode.Half:
                        downres = 1;
                        break;
                    case BloomDownscaleMode.Quarter:
                        downres = 2;
                        break;
                    default:
                        throw new System.ArgumentOutOfRangeException();
                }

                var descriptor = source.rt.descriptor;

                int tw = descriptor.width >> downres;
                int th = descriptor.height >> downres;

                // Determine the iteration count
                int maxSize = Mathf.Max(tw, th);
                int iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f) - 1);
                int mipCount = Mathf.Clamp(iterations, 1, bloom.maxIterations.value);

                // Pre-filtering parameters
                float clamp = bloom.clamp.value;
                float threshold = Mathf.GammaToLinearSpace(bloom.threshold.value);
                float thresholdKnee = threshold * 0.5f; // Hardcoded soft knee

                // Material setup
                float scatter = Mathf.Lerp(0.05f, 0.95f, bloom.scatter.value);
                _material.SetVector(ShaderConstants.Params,
                    new Vector4(scatter, clamp, threshold, thresholdKnee));
                CoreUtils.SetKeyword(_material, ShaderKeywordStrings.BloomHQ, bloom.highQualityFiltering.value);
                CoreUtils.SetKeyword(_material, ShaderKeywordStrings.UseRGBM, 
                    descriptor.graphicsFormat != GraphicsFormat.B10G11R11_UFloatPack32);

                // Prefilter
                var desc = descriptor;
                desc.width = tw;
                desc.height = th;
                
                for (int i = 0; i < mipCount; i++)
                {
                    RenderingUtils.ReAllocateIfNeeded(ref _bloomMipUp[i], desc, FilterMode.Bilinear,
                        TextureWrapMode.Clamp, name: _bloomMipUp[i].name);
                    RenderingUtils.ReAllocateIfNeeded(ref _bloomMipDown[i], desc, FilterMode.Bilinear,
                        TextureWrapMode.Clamp, name: _bloomMipDown[i].name);
                    desc.width = Mathf.Max(1, desc.width >> 1);
                    desc.height = Mathf.Max(1, desc.height >> 1);
                }
                
                cmd.SetGlobalTexture(ShaderConstants.AdditionalBloomColorTexture, GBufferManager.GBuffer.GBuffer0.nameID);

                Blitter.BlitCameraTexture(cmd, source, _bloomMipDown[0], RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store, _material, 0);

                // Down sample - gaussian pyramid
                var lastDown = _bloomMipDown[0];
                for (int i = 1; i < mipCount; i++)
                {
                    // Classic two pass gaussian blur - use mipUp as a temporary target
                    //   First pass does 2x downsampling + 9-tap gaussian
                    //   Second pass does 9-tap gaussian using a 5-tap filter + bilinear filtering
                    Blitter.BlitCameraTexture(cmd, lastDown, _bloomMipUp[i], RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store, _material, 1);
                    Blitter.BlitCameraTexture(cmd, _bloomMipUp[i], _bloomMipDown[i], RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store, _material, 2);

                    lastDown = _bloomMipDown[i];
                }

                // Up sample (bilinear by default, HQ filtering does bicubic instead
                for (int i = mipCount - 2; i >= 0; i--)
                {
                    var lowMip = (i == mipCount - 2) ? _bloomMipDown[i + 1] : _bloomMipUp[i + 1];
                    var highMip = _bloomMipDown[i];
                    var dst = _bloomMipUp[i];

                    cmd.SetGlobalTexture(ShaderConstants.SourceTexLowMip, lowMip.nameID);
                    Blitter.BlitCameraTexture(cmd, highMip, dst, RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store, _material, 3);
                }

                // Setup bloom on uber
                cmd.SetGlobalTexture(Shader.PropertyToID("_BloomTexture"), _bloomMipUp[0].nameID);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < 16; i++)
            {
                _bloomMipUp[i]?.Release();
                _bloomMipUp[i] = null;
                
                _bloomMipDown[i]?.Release();
                _bloomMipDown[i] = null;
            }
            
            CoreUtils.Destroy(_material);
        }

        private void InitRenderTarget()
        {
            _bloomMipUp = new RTHandle[16];
            _bloomMipDown = new RTHandle[16];

            for (int i = 0; i < 16; i++)
            {
                _bloomMipUp[i] = RTHandles.Alloc(1, 1, name: $"_CRPBloomMipUp{i}");
                _bloomMipDown[i] = RTHandles.Alloc(1, 1, name: $"_CRPBloomMipDown{i}");
            }
        }
    }
}