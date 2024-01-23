using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class TemporalAAPass : ScriptableRenderPass
    {
        public static class TextureName
        {
            public static readonly string MotionVectorTexture = "_MotionVectorTexture";
            public static readonly string HistoryFrameTexture = "_HistoryFrameTexture";
        }
        
        public static class ShaderIds
        {
            public static readonly int MotionVectorTexture = Shader.PropertyToID("_MotionVectorTexture");
            public static readonly int HistoryFrameTexture = Shader.PropertyToID("_HistoryFrameTexture");

            public static readonly int TaaFrameInfluence     = Shader.PropertyToID("_TaaFrameInfluence");
            public static readonly int TaaVarianceClampScale = Shader.PropertyToID("_TaaVarianceClampScale");
        }
        
        private readonly ProfilingSampler _taaSampler;

        // Material
        private Material _taaMaterial;
        
        // Render Texture
        private RTHandle _motionVectorTexture;
        private RTHandle _historyFrameTexture;

        public TemporalAAPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(TemporalAAPass));
            this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            
            _taaSampler = new ProfilingSampler("CRP TAA");
            
            Shader taaShader = Shader.Find("Hidden/StarRail_CRP/TAA/TemporalAA");
            if (taaShader == null)
            {
                Debug.LogError("Can't find Shader: Hidden/StarRail_CRP/TAA/TemporalAA");
            }
            else
            {
                _taaMaterial = CoreUtils.CreateEngineMaterial(taaShader);
            }
        }
        
        public void Setup()
        {
            
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor desc = cameraTextureDescriptor;
            desc.depthBufferBits = 0;
            
            RenderingUtils.ReAllocateIfNeeded(ref _historyFrameTexture, desc, 
                FilterMode.Bilinear, TextureWrapMode.Clamp,
                name: TextureName.HistoryFrameTexture);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!CheckExecute(ref renderingData))
            {
                return;
            }
            
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _taaSampler))
            {
                _taaMaterial.SetTexture(ShaderIds.HistoryFrameTexture, _historyFrameTexture.rt);
                _taaMaterial.SetFloat(ShaderIds.TaaFrameInfluence, 0.1f);
                _taaMaterial.SetFloat(ShaderIds.TaaVarianceClampScale, 0.9f);
                
                CoreUtils.SetKeyword(cmd, "TAA_YCOCG", true);
                
                Blit(cmd, ref renderingData, _taaMaterial, 0);
                
                Blitter.BlitCameraTexture(cmd, 
                    renderingData.cameraData.renderer.cameraColorTargetHandle, _historyFrameTexture);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public void Dispose()
        {
            CoreUtils.Destroy(_taaMaterial);
            _taaMaterial = null;
            
            RTHandles.Release(_historyFrameTexture);
            _historyFrameTexture = null;
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