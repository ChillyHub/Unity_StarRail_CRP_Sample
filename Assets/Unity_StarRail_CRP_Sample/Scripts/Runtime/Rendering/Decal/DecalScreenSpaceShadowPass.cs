using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class DecalScreenSpaceShadowDrawSystem : DecalDrawSystem
    {
        public DecalScreenSpaceShadowDrawSystem(DecalEntityManager entityManager)
            : base("DecalScreenSpaceShadowDrawSystem.Execute", entityManager)
        {
            
        }
        protected override int GetPassIndex(DecalCachedChunk decalCachedChunk) => 0;
    }
    
    public class DecalScreenSpaceShadowPass : ScriptableRenderPass
    {
        // Profiling samplers
        private readonly ProfilingSampler _decalScreenSpaceShadowProfilingSampler;
        
        // Render Texture
        private ShadowTextures _shadowTextures;
        
        // Draw system
        private DecalScreenSpaceShadowDrawSystem _decalLightDrawSystem;

        public DecalScreenSpaceShadowPass(DecalScreenSpaceShadowDrawSystem drawSystem)
        {
            this.profilingSampler = new ProfilingSampler(nameof(DecalScreenSpaceShadowPass));
            this.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            
            _decalScreenSpaceShadowProfilingSampler = new ProfilingSampler("Decal Screen Space Shadow");
            
            _decalLightDrawSystem = drawSystem;
        }

        public void Setup(ShadowTextures shadowTextures)
        {
            _shadowTextures = shadowTextures;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.graphicsFormat = RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R8_UNorm, FormatUsage.Linear | FormatUsage.Render)
                ? GraphicsFormat.R8_UNorm
                : GraphicsFormat.B8G8R8A8_UNorm;
            
            RenderingUtils.ReAllocateIfNeeded(ref _shadowTextures.ScreenSpaceShadowTexture, desc, 
                FilterMode.Point, TextureWrapMode.Clamp, name: ShadowTextures.ScreenSpaceShadowTextureName);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!CheckExecute(ref renderingData))
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _decalScreenSpaceShadowProfilingSampler))
            {
                cmd.SetRenderTarget(_shadowTextures.ScreenSpaceShadowTexture.nameID, 
                    renderingData.cameraData.renderer.cameraDepthTargetHandle.nameID);
                
                _decalLightDrawSystem?.Execute(cmd);
                
                cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle.nameID, 
                    renderingData.cameraData.renderer.cameraDepthTargetHandle.nameID);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public void Dispose()
        {
            
        }
        
        private bool CheckExecute(ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            if (camera.cameraType == CameraType.Preview)
            {
                return false;
            }

            return true;
        }
    }
}