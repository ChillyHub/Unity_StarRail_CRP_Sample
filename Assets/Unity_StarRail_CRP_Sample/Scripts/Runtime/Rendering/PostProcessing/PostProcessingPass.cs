using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class PostProcessingPass : ScriptableRenderPass
    {
        // Profiling samplers
        private readonly ProfilingSampler _postProcessingSampler;

        // Sub Pass
        private BloomPass _bloomPass;
        private ToneMappingPass _toneMappingPass;

        public PostProcessingPass()
        {
            this._postProcessingSampler = new ProfilingSampler(nameof(PostProcessingPass));
            this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            
            _postProcessingSampler = new ProfilingSampler("CRP Post Processing");
            
            _bloomPass = new BloomPass();
            _toneMappingPass = new ToneMappingPass();
        }

        public void Setup(GBufferTextures gBufferTextures)
        {
            _bloomPass.Setup(gBufferTextures);
            _toneMappingPass.Setup();
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!CheckExecute(ref renderingData))
            {
                return;
            }
            
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _postProcessingSampler))
            {
                _bloomPass.Execute(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle);
                _toneMappingPass.Execute(ref renderingData, cmd, this, 
                    renderingData.cameraData.renderer.cameraColorTargetHandle, 
                    renderingData.cameraData.renderer.cameraColorTargetHandle);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public void Dispose()
        {
            _bloomPass.Dispose();
            _toneMappingPass.Dispose();
        }
        
        private bool CheckExecute(ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            if (camera.cameraType == CameraType.Preview)
            {
                return false;
            }

            if (!cameraData.postProcessEnabled)
            {
                return false;
            }

            return true;
        }
    }
}