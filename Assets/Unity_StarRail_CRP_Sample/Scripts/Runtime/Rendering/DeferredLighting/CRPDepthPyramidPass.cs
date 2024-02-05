using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class CRPDepthPyramidPass : ScriptableRenderPass
    {
        private static class ShaderIds
        {
            public static readonly int Offset = Shader.PropertyToID("_Offset");
            public static readonly int SampleSize = Shader.PropertyToID("_SampleSize");
            public static readonly int CameraDepthAttachment = Shader.PropertyToID("_CameraDepthAttachment");
            public static readonly int DepthPyramidTexture = Shader.PropertyToID("_DepthPyramidTexture");
            public static readonly int MipLevelParam = Shader.PropertyToID("_MipLevelParam");
            public static readonly int DepthPyramidMipLevelMax = Shader.PropertyToID("_DepthPyramidMipLevelMax");
        }
        
        // Profiling samplers
        private readonly ProfilingSampler _crpCopyDepthProfilingSampler;
        
        // Compute Shader
        private ComputeShader _depthPyramidCS;
        private int _depthPyramidKernelCSCopy;
        private int _depthPyramidKernelCSPyramid;
        
        // Render Texture
        private DepthTextures _depthTextures;
        private MipmapInfo _depthMipmapInfo;

        public CRPDepthPyramidPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(CRPDepthPyramidPass));
            this.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            
            _crpCopyDepthProfilingSampler = new ProfilingSampler("CRP Copy Depth");
            
            _depthPyramidCS = Resources.Load<ComputeShader>("ComputeShaders/DepthPyramid");
            if (_depthPyramidCS == null)
            {
                Debug.LogError("Can't find compute shader DepthPyramid.compute");
            }

            _depthPyramidKernelCSCopy = _depthPyramidCS.FindKernel("CSCopy");
            _depthPyramidKernelCSPyramid = _depthPyramidCS.FindKernel("CSPyramid");
        }
        
        public void Setup(DepthTextures depthTextures)
        {
            _depthTextures = depthTextures;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var ssr = VolumeManager.instance.stack.GetComponent<CRPScreenSpaceReflection>();
            
            bool needMipMap = ssr != null && ssr.IsActive();
            
            _depthMipmapInfo = _depthTextures?.ReAllocDepthPyramidTextureIfNeed(cameraTextureDescriptor, needMipMap);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _crpCopyDepthProfilingSampler))
            {
#if UNITY_ANDROID
                Blitter.BlitCameraTexture(cmd, 
                    renderingData.cameraData.renderer.cameraDepthTargetHandle, 
                    _depthTextures.DepthPyramidTexture);
#else
                cmd.SetComputeTextureParam(_depthPyramidCS, _depthPyramidKernelCSCopy, 
                    ShaderIds.DepthPyramidTexture, _depthTextures.DepthPyramidTexture.nameID);
                cmd.SetComputeTextureParam(_depthPyramidCS, _depthPyramidKernelCSCopy, 
                    ShaderIds.CameraDepthAttachment, renderingData.cameraData.renderer.cameraDepthTargetHandle.nameID);
                
                DrawUtils.Dispatch(cmd, _depthPyramidCS, _depthPyramidKernelCSCopy, 
                    _depthMipmapInfo.textureSize.x, _depthMipmapInfo.textureSize.y);

                for (int i = 1; i < _depthMipmapInfo.mipLevelCount; i++)
                {
                    Vector2Int dstSize = _depthMipmapInfo.mipLevelSizes[i];
                    Vector2Int dstOffset = _depthMipmapInfo.mipLevelOffsets[i];
                    Vector2Int srcSize = _depthMipmapInfo.mipLevelSizes[i - 1];
                    Vector2Int srcOffset = _depthMipmapInfo.mipLevelOffsets[i - 1];
                    Vector2Int sampleSize = srcOffset + srcSize;
                    
                    int[] offset = { srcOffset.x, srcOffset.y, dstOffset.x, dstOffset.y };
                    int[] sample = { sampleSize.x, sampleSize.y };

                    cmd.SetComputeIntParams(_depthPyramidCS, ShaderIds.Offset, offset);
                    cmd.SetComputeIntParams(_depthPyramidCS, ShaderIds.SampleSize, sample);
                    cmd.SetComputeTextureParam(_depthPyramidCS, _depthPyramidKernelCSPyramid, 
                        ShaderIds.DepthPyramidTexture, _depthTextures.DepthPyramidTexture.nameID);
                    
                    DrawUtils.Dispatch(cmd, _depthPyramidCS, _depthPyramidKernelCSPyramid, dstSize.x, dstSize.y);
                }
#endif
                
                cmd.SetGlobalTexture(ShaderIds.DepthPyramidTexture, _depthTextures.DepthPyramidTexture.nameID);
                cmd.SetGlobalVectorArray(ShaderIds.MipLevelParam, _depthMipmapInfo.GetMipLevelParam());
                cmd.SetGlobalInt(ShaderIds.DepthPyramidMipLevelMax, _depthMipmapInfo.mipLevelCount - 1);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public void Dispose()
        {
            
        }
    }
}