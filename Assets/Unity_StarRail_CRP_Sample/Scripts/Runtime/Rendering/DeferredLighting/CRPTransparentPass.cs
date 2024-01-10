using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class CRPTransparentPass : ScriptableRenderPass
    {
        // Profiling samplers
        private readonly ProfilingSampler _crpOutlineProfilingSampler;
        
        private FilteringSettings _filteringSettings;

        private readonly List<ShaderTagId> _shaderTagIds;

        public CRPTransparentPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(CRPTransparentPass));
            this.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

            _crpOutlineProfilingSampler = new ProfilingSampler("CRP Transparent");
            
            _filteringSettings = new FilteringSettings(RenderQueueRange.transparent);

            _shaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("CharacterTransparent"),
                new ShaderTagId("CharacterTransparentOutline"),
                new ShaderTagId("SceneForward"),
                new ShaderTagId("SceneForwardOutline"),
                new ShaderTagId("SSSForward"),
                new ShaderTagId("SSSForwardOutline"),
                new ShaderTagId("UnlitForward"),
                new ShaderTagId("UnlitForwardOutline")
            };
        }

        public void Setup()
        {
            
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            GBufferManager.GBuffer.ReAllocIfNeed(cameraTextureDescriptor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _crpOutlineProfilingSampler))
            {
                DrawingSettings drawingSettings = CreateDrawingSettings(
                    _shaderTagIds, ref renderingData, SortingCriteria.CommonTransparent);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                context.DrawRenderers(renderingData.cullResults, 
                    ref drawingSettings, ref _filteringSettings);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}