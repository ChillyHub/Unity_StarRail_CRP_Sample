using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class ScreenSpaceReflectionPass : ScriptableRenderPass
    {
        private static class TextureName
        {
            public static readonly string SSRReflectUVTexture = "_SSRReflectUVTexture";
            public static readonly string PackedNormalSmoothnessTexture = "_PackedNormalSmoothnessTexture";
            public static readonly string DepthPyramidTexture = "_DepthPyramidTexture";
            public static readonly string ColorPyramidTexture = "_ColorPyramidTexture";
        }
        
        private static class ShaderIds
        {
            public static readonly int SSRReflectUVTexture = Shader.PropertyToID("_SSRReflectUVTexture");
            public static readonly int AlbedoMetallicTexture = Shader.PropertyToID("_AlbedoMetallicTexture");
            public static readonly int PackedNormalSmoothnessTexture = Shader.PropertyToID("_PackedNormalSmoothnessTexture");
            public static readonly int DepthPyramidTexture = Shader.PropertyToID("_DepthPyramidTexture");
            public static readonly int ColorPyramidTexture = Shader.PropertyToID("_ColorPyramidTexture");
            public static readonly int StencilTexture = Shader.PropertyToID("_StencilTexture");
            
            public static readonly int SSRMaxIterCount = Shader.PropertyToID("_SSRMaxIterCount");
            public static readonly int SSRThicknessScale = Shader.PropertyToID("_SSRThicknessScale");
            public static readonly int SSRThicknessBias = Shader.PropertyToID("_SSRThicknessBias");
            
            public static readonly int DepthPyramidMipLevelMax = Shader.PropertyToID("_DepthPyramidMipLevelMax");
        }
        
        private readonly ProfilingSampler _screenSpaceReflectionSampler;
        
        // Compute Shader
        private ComputeShader _screenSpaceReflectionCS;
        private int _kernelCSScreenSpaceReflectionUVMapping;
        
        // Material
        private Material _screenSpaceReflectionMat;
        
        // Render Texture
        private GBufferTextures _gBufferTextures;
        private DepthTextures _depthTextures;
        private ColorTextures _colorTextures;

        private RTHandle _ssrReflectUVTexture;
        
        public ScreenSpaceReflectionPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(ScreenSpaceReflectionPass));
            this.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            
            _screenSpaceReflectionSampler = new ProfilingSampler("Screen Space Reflection");
            
            RecreateComputeShader();
            
            Shader screenSpaceReflectionShader = Shader.Find("Hidden/StarRail_CRP/SSGI/ScreenSpaceReflection");
            if (screenSpaceReflectionShader == null)
            {
                Debug.LogError("Can't find shader Hidden/StarRail_CRP/SSGI/ScreenSpaceReflection");
            }
            else
            {
                _screenSpaceReflectionMat = CoreUtils.CreateEngineMaterial(screenSpaceReflectionShader);
            }
        }
        
        public void Setup(GBufferTextures gBufferTextures, DepthTextures depthTextures, ColorTextures colorTextures)
        {
            _gBufferTextures = gBufferTextures;
            _depthTextures = depthTextures;
            _colorTextures = colorTextures;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor desc = cameraTextureDescriptor;
            desc.depthBufferBits = 0;
            desc.graphicsFormat = GraphicsFormat.R16G16_UNorm;
            desc.enableRandomWrite = true;
            
            RenderingUtils.ReAllocateIfNeeded(ref _ssrReflectUVTexture, desc, FilterMode.Point, TextureWrapMode.Clamp, 
                name: TextureName.SSRReflectUVTexture);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var ssr = VolumeManager.instance.stack.GetComponent<CRPScreenSpaceReflection>();
            
            if (!CheckExecute(ref renderingData, ssr))
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _screenSpaceReflectionSampler))
            {
                // Calculate SSR Thickness
                float near = renderingData.cameraData.camera.nearClipPlane;
                float far = renderingData.cameraData.camera.farClipPlane;
                float thickness = ssr.thickness.value;
                float ssrThicknessScale = 1.0f / (1.0f + thickness);
                float ssrThicknessBias = -near / (far - near) * (thickness * ssrThicknessScale);
                
                cmd.SetComputeTextureParam(_screenSpaceReflectionCS, _kernelCSScreenSpaceReflectionUVMapping, 
                    ShaderIds.SSRReflectUVTexture, _ssrReflectUVTexture.nameID);
                cmd.SetComputeTextureParam(_screenSpaceReflectionCS, _kernelCSScreenSpaceReflectionUVMapping, 
                    ShaderIds.PackedNormalSmoothnessTexture, _gBufferTextures.GBuffer1.nameID);
                cmd.SetComputeTextureParam(_screenSpaceReflectionCS, _kernelCSScreenSpaceReflectionUVMapping, 
                    ShaderIds.DepthPyramidTexture, _depthTextures.DepthPyramidTexture.nameID);
                cmd.SetComputeTextureParam(_screenSpaceReflectionCS, _kernelCSScreenSpaceReflectionUVMapping, 
                    ShaderIds.ColorPyramidTexture, _colorTextures.ColorPyramidTexture.nameID);

                if (renderingData.cameraData.renderer.cameraDepthTargetHandle.rt.stencilFormat == GraphicsFormat.None)
                {
                    cmd.SetComputeTextureParam(_screenSpaceReflectionCS, _kernelCSScreenSpaceReflectionUVMapping, 
                        ShaderIds.StencilTexture, renderingData.cameraData.renderer.cameraDepthTargetHandle.nameID);
                }
                else
                {
                    cmd.SetComputeTextureParam(_screenSpaceReflectionCS, _kernelCSScreenSpaceReflectionUVMapping, 
                        ShaderIds.StencilTexture, renderingData.cameraData.renderer.cameraDepthTargetHandle.nameID,
                        0, RenderTextureSubElement.Stencil);
                }

                cmd.SetComputeIntParam(_screenSpaceReflectionCS, ShaderIds.DepthPyramidMipLevelMax, 
                    _depthTextures.DepthPyramidMipmapInfo.mipLevelCount - 1);
                cmd.SetComputeIntParam(_screenSpaceReflectionCS, ShaderIds.SSRMaxIterCount, ssr.maxIterCount.value);
                cmd.SetComputeFloatParam(_screenSpaceReflectionCS, ShaderIds.SSRThicknessScale, ssrThicknessScale);
                cmd.SetComputeFloatParam(_screenSpaceReflectionCS, ShaderIds.SSRThicknessBias, ssrThicknessBias);

                DrawUtils.Dispatch(cmd, _screenSpaceReflectionCS, _kernelCSScreenSpaceReflectionUVMapping, 
                    _ssrReflectUVTexture.rt.width, _ssrReflectUVTexture.rt.height);
                
                _screenSpaceReflectionMat.SetTexture(ShaderIds.AlbedoMetallicTexture, 
                    _gBufferTextures.GBuffer0.rt);
                _screenSpaceReflectionMat.SetTexture(ShaderIds.PackedNormalSmoothnessTexture, 
                    _gBufferTextures.GBuffer1.rt);
                _screenSpaceReflectionMat.SetTexture(ShaderIds.ColorPyramidTexture, 
                    _colorTextures.ColorPyramidTexture.rt);
                _screenSpaceReflectionMat.SetTexture(ShaderIds.SSRReflectUVTexture, 
                    _ssrReflectUVTexture.rt);
                
                if (renderingData.cameraData.renderer.cameraDepthTargetHandle.rt.stencilFormat == GraphicsFormat.None)
                {
                    _screenSpaceReflectionMat.SetTexture(ShaderIds.StencilTexture, 
                        renderingData.cameraData.renderer.cameraDepthTargetHandle.rt);
                }
                else
                {
                    _screenSpaceReflectionMat.SetTexture(ShaderIds.StencilTexture, 
                        renderingData.cameraData.renderer.cameraDepthTargetHandle.rt, 
                        RenderTextureSubElement.Stencil);
                }
                
                Blit(cmd, ref renderingData, _screenSpaceReflectionMat);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public void Dispose()
        {
            RTHandles.Release(_ssrReflectUVTexture);
            _ssrReflectUVTexture = null;
            
            CoreUtils.Destroy(_screenSpaceReflectionMat);
            _screenSpaceReflectionMat = null;
        }

        private void RecreateComputeShader()
        {
            _screenSpaceReflectionCS = Resources.Load<ComputeShader>("ComputeShaders/ScreenSpaceReflection");
            if (_screenSpaceReflectionCS == null)
            {
                Debug.LogError("Can't find compute shader ScreenSpaceReflection.compute");
            }

            _kernelCSScreenSpaceReflectionUVMapping = 
                _screenSpaceReflectionCS.FindKernel("CSScreenSpaceReflectionUVMapping");
        }

        private bool CheckExecute(ref RenderingData renderingData, CRPScreenSpaceReflection ssr)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            //if (camera.cameraType == CameraType.Preview)
            //{
            //    return false;
            //}

            if (ssr == null || !ssr.IsActive())
            {
                return false;
            }

            if (_screenSpaceReflectionCS == null)
            {
                RecreateComputeShader();
                return false;
            }

            return true;
        }
    }
}