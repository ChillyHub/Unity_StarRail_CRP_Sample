using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class CRPGBufferPass : ScriptableRenderPass
    {
        // Profiling samplers
        private readonly ProfilingSampler _crpGBufferProfilingSampler;
        
        private FilteringSettings _filteringSettings;

        private readonly List<ShaderTagId> _characterShaderTagIds;
        private readonly List<ShaderTagId> _sceneShaderTagIds;
        private readonly List<ShaderTagId> _sssShaderTagIds;
        private readonly List<ShaderTagId> _unlitShaderTagIds;
        
        private RenderStateBlock _sceneRenderStateBlock;
        private RenderStateBlock _sssRenderStateBlock;
        private RenderStateBlock _unlitRenderStateBlock;

        public CRPGBufferPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(CRPGBufferPass));
            this.renderPassEvent = RenderPassEvent.BeforeRenderingGbuffer;

            _crpGBufferProfilingSampler = new ProfilingSampler("CRP GBuffer");
            
            _filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

            _characterShaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("CharacterGBuffer"),
                new ShaderTagId("CharacterGBuffer2"),
                new ShaderTagId("CharacterOutline")
            };

            _sceneShaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("SceneGBuffer"),
                new ShaderTagId("SceneOutline")
            };
            
            _sssShaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("SSSGBuffer"),
                new ShaderTagId("SSSOutline")
            };
            
            _unlitShaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("UnlitGBuffer"),
                new ShaderTagId("UnlitOutline")
            };

            _sceneRenderStateBlock = new RenderStateBlock
            {
                stencilState = DeferredStencil.GetStencilState(DeferredPass.SceneGBuffer),
                stencilReference = DeferredStencil.GetStencilReference(DeferredPass.SceneGBuffer),
                mask = RenderStateMask.Stencil
            };
            
            _sssRenderStateBlock = new RenderStateBlock
            {
                stencilState = DeferredStencil.GetStencilState(DeferredPass.SssGBuffer),
                stencilReference = DeferredStencil.GetStencilReference(DeferredPass.SssGBuffer),
                mask = RenderStateMask.Stencil
            };
            
            _unlitRenderStateBlock = new RenderStateBlock
            {
                stencilState = DeferredStencil.GetStencilState(DeferredPass.UnLitGBuffer),
                stencilReference = DeferredStencil.GetStencilReference(DeferredPass.UnLitGBuffer),
                mask = RenderStateMask.Stencil
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
            using (new ProfilingScope(cmd, _crpGBufferProfilingSampler))
            {
                DrawingSettings characterDrawingSettings = CreateDrawingSettings(
                    _characterShaderTagIds, ref renderingData, cameraData.defaultOpaqueSortFlags);
                DrawingSettings sceneDrawingSettings = CreateDrawingSettings(
                    _sceneShaderTagIds, ref renderingData, cameraData.defaultOpaqueSortFlags);
                DrawingSettings sssDrawingSettings = CreateDrawingSettings(
                    _sssShaderTagIds, ref renderingData, cameraData.defaultOpaqueSortFlags);
                DrawingSettings unlitDrawingSettings = CreateDrawingSettings(
                    _unlitShaderTagIds, ref renderingData, cameraData.defaultOpaqueSortFlags);

                GBufferManager.GBuffer.GBuffers[2] = cameraData.renderer.cameraColorTargetHandle;
                cmd.SetRenderTarget(GBufferManager.GBuffer.GBufferIds, cameraData.renderer.cameraDepthTargetHandle);
                
                cmd.ClearRenderTarget(true, true, Color.clear);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CoreUtils.SetKeyword(cmd, "_GBUFFER_NORMALS_OCT", true);
                
                context.DrawRenderers(renderingData.cullResults, 
                    ref characterDrawingSettings, ref _filteringSettings);
                context.DrawRenderers(renderingData.cullResults, 
                    ref sceneDrawingSettings, ref _filteringSettings, ref _sceneRenderStateBlock);
                context.DrawRenderers(renderingData.cullResults, 
                    ref sssDrawingSettings, ref _filteringSettings, ref _sssRenderStateBlock);
                context.DrawRenderers(renderingData.cullResults, 
                    ref unlitDrawingSettings, ref _filteringSettings, ref _unlitRenderStateBlock);

                cmd.SetRenderTarget(cameraData.renderer.cameraColorTargetHandle, cameraData.renderer.cameraDepthTargetHandle);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}