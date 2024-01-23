using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class CRPColorPyramidPass : ScriptableRenderPass
    {
        private static class TextureName
        {
            public static readonly string ColorPyramidTexture = "_ColorPyramidTexture";
            public static readonly string DownSampledTexture = "_DownSampledTexture";
            public static readonly string TempBlurTexture = "_TempBlurTexture";
        }
        
        private static class ShaderIds
        {
            public static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
            public static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int BlitMipLevel = Shader.PropertyToID("_BlitMipLevel");
            
            public static readonly int Source = Shader.PropertyToID("_Source");
            public static readonly int SrcScaleBias = Shader.PropertyToID("_SrcScaleBias");
            public static readonly int SrcUvLimits = Shader.PropertyToID("_SrcUvLimits");
            public static readonly int SourceMip = Shader.PropertyToID("_SourceMip");
        }
        
        private readonly ProfilingSampler _colorPyramidSampler;
        
        // Material
        private Material _colorPyramidMat;
        private MaterialPropertyBlock _propertyBlock;

        // Render Texture
        private ColorTextures _colorTextures;
        private RTHandle _downSampledTexture;
        private RTHandle _tempBlurTexture;
        
        // Data
        private int _srcWidth;
        private int _srcHeight;
        private int[] _srcOffset;
        private int[] _dstOffset;

        public CRPColorPyramidPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(CRPColorPyramidPass));
            this.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            
            _colorPyramidSampler = new ProfilingSampler("CRP Copy Color");
            
            Shader colorPyramidShader = Shader.Find("Hidden/StarRail_CRP/ColorPyramid");
            if (colorPyramidShader == null)
            {
                Debug.LogError("Can't find shader: Hidden/StarRail_CRP/ColorPyramid");
            }
            else
            {
                _colorPyramidMat = CoreUtils.CreateEngineMaterial(colorPyramidShader);
                _propertyBlock = new MaterialPropertyBlock();
            }
            
            _downSampledTexture = RTHandles.Alloc(1, 1);
            _tempBlurTexture = RTHandles.Alloc(1, 1);
            
            _srcOffset = new int[4];
            _dstOffset = new int[4];
        }
        
        public void Setup(ColorTextures colorTextures)
        {
            _colorTextures = colorTextures;
        }
        
        public void Dispose()
        {
            RTHandles.Release(_downSampledTexture);
            RTHandles.Release(_tempBlurTexture);
            _downSampledTexture = null;
            _tempBlurTexture = null;
            
            CoreUtils.Destroy(_colorPyramidMat);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var ssr = VolumeManager.instance.stack.GetComponent<CRPScreenSpaceReflection>();
            
            bool needMipMap = ssr != null && ssr.IsActive();

            if (_colorTextures == null)
            {
                return;
            }
            
            _colorTextures.ReAllocColorPyramidTextureIfNeed(cameraTextureDescriptor, needMipMap);
            
            _srcWidth = cameraTextureDescriptor.width;
            _srcHeight = cameraTextureDescriptor.height;

            cameraTextureDescriptor.depthBufferBits = 0;
            cameraTextureDescriptor.useDynamicScale = true;

            RenderingUtils.ReAllocateIfNeeded(ref _downSampledTexture,
                Vector2.one * 0.5f,
                cameraTextureDescriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: TextureName.DownSampledTexture);

            RenderingUtils.ReAllocateIfNeeded(ref _tempBlurTexture,
                Vector2.one * 0.5f,
                cameraTextureDescriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: TextureName.TempBlurTexture);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!CheckExecute(ref renderingData))
            {
                return;
            }
            
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _colorPyramidSampler))
            {
                var ssr = VolumeManager.instance.stack.GetComponent<CRPScreenSpaceReflection>();
                bool needMipMap = ssr != null && ssr.IsActive();
                
                int srcMipLevel = 0;
                int srcMipWidth = _srcWidth;
                int srcMipHeight = _srcHeight;
                
                bool isHardwareDrsOn = DynamicResolutionHandler.instance.HardwareDynamicResIsEnabled();
                var hardwareTextureSize = new Vector2Int(_srcWidth, _srcWidth);
                if (isHardwareDrsOn)
                    hardwareTextureSize = DynamicResolutionHandler.instance.ApplyScalesOnSize(hardwareTextureSize);

                float sourceScaleX = (float)_srcWidth / (float)hardwareTextureSize.x;
                float sourceScaleY = (float)_srcWidth / (float)hardwareTextureSize.y;

                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var destination = _colorTextures.ColorPyramidTexture;

                if (source == null)
                {
                    return;
                }

                _propertyBlock.SetTexture(ShaderIds.BlitTexture, source.rt);
                _propertyBlock.SetVector(ShaderIds.BlitScaleBias, new Vector4(sourceScaleX, sourceScaleY, 0f, 0f));
                _propertyBlock.SetFloat(ShaderIds.BlitMipLevel, 0f);
                cmd.SetRenderTarget(destination.nameID, 0, CubemapFace.Unknown, -1);
                cmd.SetViewport(new Rect(0, 0, srcMipWidth, srcMipHeight));
                cmd.DrawProcedural(Matrix4x4.identity, Blitter.GetBlitMaterial(source.rt.dimension), 0, 
                    MeshTopology.Triangles, 3, 1, _propertyBlock);

                if (!needMipMap)
                {
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                    
                    return;
                }

                var finalTargetSize = new Vector2Int(
                    destination.rt.width, 
                    destination.rt.height);
                if (destination.rt.useDynamicScale && isHardwareDrsOn)
                    finalTargetSize = DynamicResolutionHandler.instance.ApplyScalesOnSize(finalTargetSize);

                while (srcMipWidth >= 8 || srcMipHeight >= 8)
                {
                    int dstMipWidth = Mathf.Max(1, srcMipWidth >> 1);
                    int dstMipHeight = Mathf.Max(1, srcMipHeight >> 1);

                    // Scale for down sample
                    float scaleX = ((float)srcMipWidth / finalTargetSize.x);
                    float scaleY = ((float)srcMipHeight / finalTargetSize.y);

                    // Down sample.
                    _propertyBlock.SetTexture(ShaderIds.BlitTexture, destination.rt);
                    _propertyBlock.SetVector(ShaderIds.BlitScaleBias, new Vector4(scaleX, scaleY, 0f, 0f));
                    _propertyBlock.SetFloat(ShaderIds.BlitMipLevel, srcMipLevel);
                    cmd.SetRenderTarget(_downSampledTexture.nameID, 0, CubemapFace.Unknown, -1);
                    cmd.SetViewport(new Rect(0, 0, dstMipWidth, dstMipHeight));
                    cmd.DrawProcedural(Matrix4x4.identity, Blitter.GetBlitMaterial(source.rt.dimension), 1,
                        MeshTopology.Triangles, 3, 1, _propertyBlock);

                    // Scales for Blur
                    // Same size as m_TempColorTargets which is the source for vertical blur
                    var hardwareBlurSourceTextureSize = new Vector2Int(_downSampledTexture.rt.width,
                        _downSampledTexture.rt.height);
                    if (isHardwareDrsOn)
                        hardwareBlurSourceTextureSize =
                            DynamicResolutionHandler.instance.ApplyScalesOnSize(hardwareBlurSourceTextureSize);

                    float blurSourceTextureWidth = (float)hardwareBlurSourceTextureSize.x;
                    float blurSourceTextureHeight = (float)hardwareBlurSourceTextureSize.y;

                    scaleX = ((float)dstMipWidth / blurSourceTextureWidth);
                    scaleY = ((float)dstMipHeight / blurSourceTextureHeight);

                    // Blur horizontal.
                    _propertyBlock.SetTexture(ShaderIds.Source, _downSampledTexture.rt);
                    _propertyBlock.SetVector(ShaderIds.SrcScaleBias, new Vector4(scaleX, scaleY, 0f, 0f));
                    _propertyBlock.SetVector(ShaderIds.SrcUvLimits,
                        new Vector4((dstMipWidth - 0.5f) / blurSourceTextureWidth,
                            (dstMipHeight - 0.5f) / blurSourceTextureHeight, 1.0f / blurSourceTextureWidth, 0f));
                    _propertyBlock.SetFloat(ShaderIds.SourceMip, 0);
                    cmd.SetRenderTarget(_tempBlurTexture.nameID, 0, CubemapFace.Unknown, -1);
                    cmd.SetViewport(new Rect(0, 0, dstMipWidth, dstMipHeight));
                    cmd.DrawProcedural(Matrix4x4.identity, _colorPyramidMat, 0, MeshTopology.Triangles, 3, 1,
                        _propertyBlock);

                    // Blur vertical.
                    _propertyBlock.SetTexture(ShaderIds.Source, _tempBlurTexture.rt);
                    _propertyBlock.SetVector(ShaderIds.SrcScaleBias, new Vector4(scaleX, scaleY, 0f, 0f));
                    _propertyBlock.SetVector(ShaderIds.SrcUvLimits,
                        new Vector4((dstMipWidth - 0.5f) / blurSourceTextureWidth,
                            (dstMipHeight - 0.5f) / blurSourceTextureHeight, 0f, 1.0f / blurSourceTextureHeight));
                    _propertyBlock.SetFloat(ShaderIds.SourceMip, 0);
                    cmd.SetRenderTarget(destination.nameID, srcMipLevel + 1, CubemapFace.Unknown, -1);
                    cmd.SetViewport(new Rect(0, 0, dstMipWidth, dstMipHeight));
                    cmd.DrawProcedural(Matrix4x4.identity, _colorPyramidMat, 0, MeshTopology.Triangles, 3, 1,
                        _propertyBlock);

                    srcMipLevel++;
                    srcMipWidth = srcMipWidth >> 1;
                    srcMipHeight = srcMipHeight >> 1;

                    finalTargetSize.x = finalTargetSize.x >> 1;
                    finalTargetSize.y = finalTargetSize.y >> 1;
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
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