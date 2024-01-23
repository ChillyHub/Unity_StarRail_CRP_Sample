using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class ToneMappingPass : IDisposable
    {
        private static class KeywordUtils
        {
            public static readonly string BLOOM = "_BLOOM";
            public static readonly string TONEMAPPING_ACES = "_TONEMAPPING_ACES";
        }
        
        private static class ShaderConstants
        {
            public static readonly int BloomTextureId = Shader.PropertyToID("_BloomTexture");
            public static readonly int BloomParamsId = Shader.PropertyToID("_BloomParams");
            public static readonly int BloomRGBMId = Shader.PropertyToID("_BloomRGBM");

            public static readonly int ACESParamAId = Shader.PropertyToID("_ACESParamA");
            public static readonly int ACESParamBId = Shader.PropertyToID("_ACESParamB");
            public static readonly int ACESParamCId = Shader.PropertyToID("_ACESParamC");
            public static readonly int ACESParamDId = Shader.PropertyToID("_ACESParamD");
            public static readonly int ACESParamEId = Shader.PropertyToID("_ACESParamE");
        }
        
        private readonly ProfilingSampler _toneMappingSampler;

        private Material _material;

        public ToneMappingPass()
        {
            _toneMappingSampler = new ProfilingSampler("CRP Tone Mapping");

            const string shaderName = "Hidden/StarRail_CRP/PostProcessing/ToneMapping";
            Shader shader = Shader.Find(shaderName);

            if (shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(shader);
            }
            else
            {
                Debug.LogWarning($"Can't find shader: {shaderName}");
            }
        }
        
        public void Setup()
        {
            
        }

        public void Execute(ref RenderingData renderingData, CommandBuffer cmd, 
            ScriptableRenderPass renderPass, RTHandle source, RTHandle destination)
        {
            var bloom = VolumeManager.instance.stack.GetComponent<CRPBloom>();
            var toneMapping = VolumeManager.instance.stack.GetComponent<CRPToneMapping>();

            using (new ProfilingScope(cmd, _toneMappingSampler))
            {
                if (bloom.IsActive())
                {
                    // Setup bloom on uber
                    var tint = bloom.tint.value.linear;
                    var luma = ColorUtils.Luminance(tint);
                    tint = luma > 0f ? tint * (1f / luma) : Color.white;

                    bool useRGBM = source.rt.graphicsFormat != GraphicsFormat.B10G11R11_UFloatPack32;

                    var bloomParams = new Vector4(bloom.intensity.value, tint.r, tint.g, tint.b);
                    _material.SetVector(ShaderConstants.BloomParamsId, bloomParams);
                    _material.SetFloat(ShaderConstants.BloomRGBMId, useRGBM ? 1f : 0f);
                    
                    _material.EnableKeyword(KeywordUtils.BLOOM);
                }
                else
                {
                    _material.DisableKeyword(KeywordUtils.BLOOM);
                }

                if (toneMapping.IsActive())
                {
                    _material.SetFloat(ShaderConstants.ACESParamAId, toneMapping.ACESParamA.value);
                    _material.SetFloat(ShaderConstants.ACESParamBId, toneMapping.ACESParamB.value);
                    _material.SetFloat(ShaderConstants.ACESParamCId, toneMapping.ACESParamC.value);
                    _material.SetFloat(ShaderConstants.ACESParamDId, toneMapping.ACESParamD.value);
                    _material.SetFloat(ShaderConstants.ACESParamEId, toneMapping.ACESParamE.value);
                    
                    _material.EnableKeyword(KeywordUtils.TONEMAPPING_ACES);
                }
                else
                {
                    _material.DisableKeyword(KeywordUtils.TONEMAPPING_ACES);
                }

                if (source == destination)
                {
                    renderPass.Blit(cmd, ref renderingData, _material, 0);
                }
                else
                {
                    Blitter.BlitCameraTexture(cmd, source, destination, 
                        RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, _material, 0);
                }
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}