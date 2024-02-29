using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class DecalStencilLightingDrawSystem : DecalDrawSystem
    {
        public DecalStencilLightingDrawSystem(DecalEntityManager entityManager)
            : base("DecalStencilLightingDrawSystem.Execute", entityManager)
        {
            
        }
        protected override int GetPassIndex(DecalCachedChunk decalCachedChunk) => 1;
    }
    
    public class DecalStencilLightingPass : ScriptableRenderPass
    {
        // Profiling samplers
        private readonly ProfilingSampler _decalStencilLightingProfilingSampler;

        // Draw System
        private DecalStencilLightingDrawSystem _stencilLightingDrawSystem;

        // Render Texture
        private GBufferTextures _gBufferTextures;

        public DecalStencilLightingPass(DecalStencilLightingDrawSystem stencilLightingDrawSystem)
        {
            this.profilingSampler = new ProfilingSampler(nameof(DecalStencilLightingPass));
            this.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            
            _decalStencilLightingProfilingSampler = new ProfilingSampler("Decal Stencil Deferred Lighting");

            _stencilLightingDrawSystem = stencilLightingDrawSystem;
        }
        
        public void Setup(GBufferTextures gBufferTextures)
        {
            _gBufferTextures = gBufferTextures;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _decalStencilLightingProfilingSampler))
            {
                // Still not support baked mixed lighting now
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._DEFERRED_MIXED_LIGHTING, false);

                cmd.SetGlobalTexture(GBufferTextures.GBuffer0Name, _gBufferTextures.GBuffer0.nameID);
                cmd.SetGlobalTexture(GBufferTextures.GBuffer1Name, _gBufferTextures.GBuffer1.nameID);
                //cmd.SetGlobalTexture(GBufferTextures.GBuffer2Name, _gBufferTextures.GBuffer2.nameID);

                _stencilLightingDrawSystem?.Execute(cmd);

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._DEFERRED_MIXED_LIGHTING, false);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}