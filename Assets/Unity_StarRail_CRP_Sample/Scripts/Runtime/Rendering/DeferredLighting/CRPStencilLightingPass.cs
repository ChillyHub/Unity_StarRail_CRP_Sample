using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace Unity_StarRail_CRP_Sample
{
    public class CRPStencilLightingPass : ScriptableRenderPass
    {
        private static class ShaderConstants
        {
            public static readonly int ScreenToWorldId = Shader.PropertyToID("_ScreenToWorld");
        }

        // Profiling samplers
        private readonly ProfilingSampler _nprStencilLightingProfilingSampler;
        
        // Material
        private Material _material;
        private MaterialPropertyBlock _propertyBlock;
        
        private enum MaterialPass : int
        {
            StencilVolume         = 0,
            UnLitDirectional      = 1,
            CharacterDirectional  = 2,
            SceneDirectional      = 3,
            SssDirectional        = 4,
            UnLitAdditional       = 5,
            CharacterAdditional   = 6,
            SceneAdditional       = 7,
            SssAdditional         = 8
        }
        
        // Deferred Light
        private DeferredLight _deferredLight;

        public CRPStencilLightingPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(CRPStencilLightingPass));
            this.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            
            _nprStencilLightingProfilingSampler = new ProfilingSampler("CRP Stencil Deferred Lighting");
            
            const string shaderName = 
                "Hidden/StarRail_CRP/Deferred/CRPStencilLighting";
            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogWarning($"Can't find shader: {shaderName}");
            }
            else
            {
                _material = CoreUtils.CreateEngineMaterial(shader);
            }

            _propertyBlock = new MaterialPropertyBlock();
        }
        
        public void Setup(DeferredLight deferredLight)
        {
            _deferredLight = deferredLight;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _nprStencilLightingProfilingSampler))
            {
                // Still not support baked mixed lighting now
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._DEFERRED_MIXED_LIGHTING, false);
                
                if (_deferredLight.StencilVisLights.Length == 0)
                    return;
                
                // This must be set for each eye in XR mode multipass.
                SetupMatrixConstants(cmd, ref renderingData);
                
                cmd.SetGlobalTexture(GBufferTextures.GBuffer0Name, GBufferManager.GBuffer.GBuffer0);
                cmd.SetGlobalTexture(GBufferTextures.GBuffer1Name, GBufferManager.GBuffer.GBuffer1);
                cmd.SetGlobalTexture(GBufferTextures.GBuffer2Name, GBufferManager.GBuffer.GBuffer2);

                if (_deferredLight.HasStencilLightsOfType(LightType.Directional))
                {
                    _deferredLight.RenderStencilDirectionalLights(cmd, ref renderingData, 
                        renderingData.lightData.visibleLights, renderingData.lightData.mainLightIndex, _material,
                        new []
                        {
                            (int)MaterialPass.UnLitDirectional,
                            (int)MaterialPass.CharacterDirectional,
                            (int)MaterialPass.SceneDirectional,
                            (int)MaterialPass.SssDirectional
                        }, _propertyBlock);
                }

                if (_deferredLight.HasStencilLightsOfType(LightType.Point))
                {
                    _deferredLight.RenderStencilPointLights(cmd, ref renderingData, 
                        renderingData.lightData.visibleLights, renderingData.lightData.mainLightIndex, _material,
                        new []
                        {
                            (int)MaterialPass.StencilVolume,
                            (int)MaterialPass.CharacterAdditional,
                            (int)MaterialPass.SceneAdditional,
                            (int)MaterialPass.SssAdditional
                        }, _propertyBlock);
                }

                if (_deferredLight.HasStencilLightsOfType(LightType.Spot))
                {
                    _deferredLight.RenderStencilSpotLights(cmd, ref renderingData, 
                        renderingData.lightData.visibleLights, renderingData.lightData.mainLightIndex, _material,
                        new []
                        {
                            (int)MaterialPass.StencilVolume,
                            (int)MaterialPass.CharacterAdditional,
                            (int)MaterialPass.SceneAdditional,
                            (int)MaterialPass.SssAdditional
                        }, _propertyBlock);
                }

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._DEFERRED_MIXED_LIGHTING, false);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        void SetupMatrixConstants(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;

#if ENABLE_VR && ENABLE_XR_MODULE
            int eyeCount = cameraData.xr.enabled && cameraData.xr.singlePassEnabled ? 2 : 1;
#else
            int eyeCount = 1;
#endif
            Matrix4x4[] screenToWorld = new Matrix4x4[2]; // deferred shaders expects 2 elements

            for (int eyeIndex = 0; eyeIndex < eyeCount; eyeIndex++)
            {
                Matrix4x4 proj = cameraData.GetProjectionMatrix(eyeIndex);
                Matrix4x4 view = cameraData.GetViewMatrix(eyeIndex);
                Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(proj, false);

                // xy coordinates in range [-1; 1] go to pixel coordinates.
                Matrix4x4 toScreen = new Matrix4x4(
                    new Vector4(0.5f * _deferredLight.RenderWidth, 0.0f, 0.0f, 0.0f),
                    new Vector4(0.0f, 0.5f * _deferredLight.RenderHeight, 0.0f, 0.0f),
                    new Vector4(0.0f, 0.0f, 1.0f, 0.0f),
                    new Vector4(0.5f * _deferredLight.RenderWidth, 0.5f * _deferredLight.RenderHeight, 0.0f, 1.0f)
                );

                Matrix4x4 zScaleBias = Matrix4x4.identity;
                if (!SystemInfo.usesReversedZBuffer)
                {
                    // We need to manunally adjust z in NDC space from [-1; 1] to [0; 1] (storage in depth texture).
                    zScaleBias = new Matrix4x4(
                        new Vector4(1.0f, 0.0f, 0.0f, 0.0f),
                        new Vector4(0.0f, 1.0f, 0.0f, 0.0f),
                        new Vector4(0.0f, 0.0f, 0.5f, 0.0f),
                        new Vector4(0.0f, 0.0f, 0.5f, 1.0f)
                    );
                }

                screenToWorld[eyeIndex] = Matrix4x4.Inverse(toScreen * zScaleBias * gpuProj * view);
            }

            cmd.SetGlobalMatrixArray(ShaderConstants.ScreenToWorldId, screenToWorld);
        }
    }
}
