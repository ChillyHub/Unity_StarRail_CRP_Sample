using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class CSBloomPass : IDisposable
    {
        private static class ShaderConstants
        {
            public static readonly int Params = Shader.PropertyToID("_Params");
            public static readonly int SourceTexLowMip = Shader.PropertyToID("_SourceTexLowMip");
            public static readonly int BloomTexture = Shader.PropertyToID("_BloomTexture");
            public static readonly int AdditionalBloomColorTexture = Shader.PropertyToID("_AdditionalBloomColorTexture");
            public static readonly int StencilTexture = Shader.PropertyToID("_StencilTexture");
            public static readonly int OutputTexture = Shader.PropertyToID("_OutputTexture");
            public static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
            public static readonly int OutputTextureSize = Shader.PropertyToID("_OutputTextureSize");
            public static readonly int BlitTextureSize = Shader.PropertyToID("_BlitTextureSize");
        }
        
        // Profiling samplers
        private readonly ProfilingSampler _bloomSampler;
        
        // Compute Shader
        private ComputeShader _computeShader;
        private int _preFilterKernel;
        private int _blurKernel;
        private int _upSampleKernel;
        
        // Render Targets
        private GBufferTextures _gBufferTextures;
        private RTHandle[] _bloomMip;

        public CSBloomPass()
        {
            _bloomSampler = new ProfilingSampler("CRP Bloom");

            _computeShader = Resources.Load<ComputeShader>("ComputeShaders/Bloom");
            _preFilterKernel = _computeShader.FindKernel("CSBloomPrefilter");
            _blurKernel = _computeShader.FindKernel("CSBloomBlur");
            _upSampleKernel = _computeShader.FindKernel("CSBloomUpSample");
            
            InitRenderTarget();
        }
        
        public void Setup(GBufferTextures gBufferTextures)
        {
            _gBufferTextures = gBufferTextures;
        }
        
        public void Execute(ref RenderingData renderingData, CommandBuffer cmd, RTHandle source)
        {
            if (_computeShader == null)
            {
                return;
            }
            
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
                descriptor.enableRandomWrite = true;

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

                // CS setup
                float scatter = Mathf.Lerp(0.05f, 0.95f, bloom.scatter.value);
                cmd.SetComputeVectorParam(_computeShader, ShaderConstants.Params,
                    new Vector4(scatter, clamp, threshold, thresholdKnee));
                CoreUtils.SetKeyword(_computeShader, ShaderKeywordStrings.BloomHQ, bloom.highQualityFiltering.value);
                CoreUtils.SetKeyword(_computeShader, ShaderKeywordStrings.UseRGBM, 
                    descriptor.graphicsFormat != GraphicsFormat.B10G11R11_UFloatPack32);

                // Prefilter
                var desc = descriptor;
                desc.width = tw;
                desc.height = th;
                
                for (int i = 0; i < mipCount; i++)
                {
                    RenderingUtils.ReAllocateIfNeeded(ref _bloomMip[i], desc, FilterMode.Bilinear,
                        TextureWrapMode.Clamp, name: _bloomMip[i].name);
                    desc.width = Mathf.Max(1, (desc.width + 1) >> 1);
                    desc.height = Mathf.Max(1, (desc.height + 1) >> 1);
                }

                cmd.SetComputeTextureParam(_computeShader, _preFilterKernel, 
                    ShaderConstants.OutputTexture, _bloomMip[0].nameID);
                cmd.SetComputeTextureParam(_computeShader, _preFilterKernel, 
                    ShaderConstants.BlitTexture, source.nameID);
                cmd.SetComputeTextureParam(_computeShader, _preFilterKernel, 
                    ShaderConstants.AdditionalBloomColorTexture, _gBufferTextures.GBuffer0.nameID);
                
                cmd.SetComputeVectorParam(_computeShader, ShaderConstants.OutputTextureSize, 
                    new Vector4(_bloomMip[0].rt.width, _bloomMip[0].rt.height, 
                        1.0f / _bloomMip[0].rt.width, 1.0f / _bloomMip[0].rt.height));
                
                DrawUtils.Dispatch(cmd, _computeShader, _preFilterKernel, _bloomMip[0].rt.width, _bloomMip[0].rt.height);

                // Down sample - gaussian pyramid
                for (int i = 1; i < mipCount; i++)
                {
                    cmd.SetComputeTextureParam(_computeShader, _blurKernel, 
                        ShaderConstants.OutputTexture, _bloomMip[i].nameID);
                    cmd.SetComputeTextureParam(_computeShader, _blurKernel, 
                        ShaderConstants.BlitTexture, _bloomMip[i - 1].nameID);
                    
                    cmd.SetComputeVectorParam(_computeShader, ShaderConstants.OutputTextureSize, 
                        new Vector4(_bloomMip[i].rt.width, _bloomMip[i].rt.height, 
                            1.0f / _bloomMip[i].rt.width, 1.0f / _bloomMip[i].rt.height));
                    cmd.SetComputeVectorParam(_computeShader, ShaderConstants.BlitTextureSize, 
                        new Vector4(_bloomMip[i - 1].rt.width, _bloomMip[i - 1].rt.height, 
                            1.0f / _bloomMip[i - 1].rt.width, 1.0f / _bloomMip[i - 1].rt.height));
                    
                    DrawUtils.Dispatch(cmd, _computeShader, _blurKernel, _bloomMip[i].rt.width, _bloomMip[i].rt.height);
                }

                // Up sample (bilinear by default, HQ filtering does bicubic instead
                for (int i = mipCount - 2; i >= 0; i--)
                {
                    cmd.SetComputeTextureParam(_computeShader, _upSampleKernel, 
                        ShaderConstants.OutputTexture, _bloomMip[i].nameID);
                    cmd.SetComputeTextureParam(_computeShader, _upSampleKernel, 
                        ShaderConstants.SourceTexLowMip, _bloomMip[i + 1].nameID);
                    
                    cmd.SetComputeVectorParam(_computeShader, ShaderConstants.OutputTextureSize, 
                        new Vector4(_bloomMip[i].rt.width, _bloomMip[i].rt.height, 
                            1.0f / _bloomMip[i].rt.width, 1.0f / _bloomMip[i].rt.height));
                    
                    DrawUtils.Dispatch(cmd, _computeShader, _upSampleKernel, _bloomMip[i].rt.width, _bloomMip[i].rt.height);
                }

                // Setup bloom on uber
                cmd.SetGlobalTexture(Shader.PropertyToID("_BloomTexture"), _bloomMip[0].nameID);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < 16; i++)
            {
                RTHandles.Release(_bloomMip[i]);
                _bloomMip[i] = null;
            }
        }

        private void InitRenderTarget()
        {
            _bloomMip = new RTHandle[16];

            for (int i = 0; i < 16; i++)
            {
                _bloomMip[i] = RTHandles.Alloc(1, 1, name: $"_CRPBloomMip{i}");
            }
        }
    }
}
