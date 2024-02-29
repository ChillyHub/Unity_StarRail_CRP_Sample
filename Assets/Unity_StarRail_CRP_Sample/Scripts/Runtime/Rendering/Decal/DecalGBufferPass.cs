using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace Unity_StarRail_CRP_Sample
{
    public enum DecalNormalBlend
    {
        [Tooltip("Low quality of normal reconstruction (Uses 1 sample).")]
        Low,
        [Tooltip("Medium quality of normal reconstruction (Uses 5 samples).")]
        Medium,
        [Tooltip("High quality of normal reconstruction (Uses 9 samples).")]
        High,
    }
    
    public class DecalGBufferDrawSystem : DecalDrawSystem
    {
        public DecalGBufferDrawSystem(DecalEntityManager entityManager)
            : base("DecalDrawGBufferSystem.Execute", entityManager)
        {
            
        }
        protected override int GetPassIndex(DecalCachedChunk decalCachedChunk) => 0;
    }
    
    public class DecalGBufferPass : ScriptableRenderPass
    {
        private FilteringSettings m_FilteringSettings;
        private ProfilingSampler m_ProfilingSampler;
        private List<ShaderTagId> m_ShaderTagIdList;
        private DecalGBufferDrawSystem m_GBufferDrawSystem;
        //private DecalScreenSpaceSettings m_Settings;
        //private DeferredLight m_DeferredLight;
        //private RTHandle[] m_GbufferAttachments;
        //private bool m_DecalLayers;
        
        // Render Texture
        private GBufferTextures _gBufferTextures;
        private DepthTextures _depthTextures;

        public DecalGBufferPass(DecalGBufferDrawSystem gBufferDrawSystem)
        {
            this.profilingSampler = new ProfilingSampler(nameof(DecalGBufferPass));
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;

            m_GBufferDrawSystem = gBufferDrawSystem;
            //m_Settings = settings;
            m_ProfilingSampler = new ProfilingSampler("Decal GBuffer");
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);
            //m_DecalLayers = decalLayers;

            m_ShaderTagIdList = new List<ShaderTagId>();
            m_ShaderTagIdList.Add(new ShaderTagId("DecalGBuffer"));
            //if (gBufferDrawSystem == null)
                //m_ShaderTagIdList.Add(new ShaderTagId("DecalGBuffer"));
            //else
                //m_ShaderTagIdList.Add(new ShaderTagId(DecalShaderPassNames.DecalGBufferMesh));
        }

        internal void Setup(GBufferTextures gBufferTextures, DepthTextures depthTextures)
        {
            _gBufferTextures = gBufferTextures;
            _depthTextures = depthTextures;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            _gBufferTextures?.ReAllocIfNeed(cameraTextureDescriptor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                if (_gBufferTextures != null)
                {
                    _gBufferTextures.GBuffers[2] = renderingData.cameraData.renderer.cameraColorTargetHandle;
                    cmd.SetRenderTarget(_gBufferTextures.GBufferIds, renderingData.cameraData.renderer.cameraDepthTargetHandle.nameID);
                }

                NormalReconstruction.SetupProperties(cmd, renderingData.cameraData);

                //CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendLow, m_Settings.normalBlend == DecalNormalBlend.Low);
                //CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendMedium, m_Settings.normalBlend == DecalNormalBlend.Medium);
                //CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendHigh, m_Settings.normalBlend == DecalNormalBlend.High);
                //CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalLayers, m_DecalLayers);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                m_GBufferDrawSystem?.Execute(cmd);

                //context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);
                
                cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle.nameID, 
                    renderingData.cameraData.renderer.cameraDepthTargetHandle.nameID);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
            {
                throw new System.ArgumentNullException("cmd");
            }

            //CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendLow, false);
            //CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendMedium, false);
            //CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendHigh, false);
            //CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalLayers, false);
        }
    }
}