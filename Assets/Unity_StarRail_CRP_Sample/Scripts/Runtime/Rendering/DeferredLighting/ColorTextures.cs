using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class ColorTextures
    {
        public static class ShaderIds
        {
            public static readonly int ColorPyramidTexture = Shader.PropertyToID("_ColorPyramidTexture");
        }
        
        public static class TextureName
        {
            public static readonly string ColorPyramidTexture = "_ColorPyramidTexture";
        }
        
        public ref RTHandle ColorPyramidTexture => ref _colorTextures[0];

        private readonly RTHandle[] _colorTextures = new RTHandle[1];
        
        public ColorTextures()
        {
            _colorTextures[0] = RTHandles.Alloc(1, 1);
        }
        
        public void Release()
        {
            RTHandles.Release(_colorTextures[0]);
            _colorTextures[0] = null;
        }
        
        public void ReAllocColorPyramidTextureIfNeed(RenderTextureDescriptor src, bool needMipMap = false)
        {
            RenderingUtils.ReAllocateIfNeeded(ref ColorPyramidTexture, 
                GetDepthPyramidTextureDescriptor(src, needMipMap), 
                FilterMode.Trilinear, 
                TextureWrapMode.Clamp, 
                name: TextureName.ColorPyramidTexture);
        }
        
        public RenderTextureDescriptor GetDepthPyramidTextureDescriptor(RenderTextureDescriptor src, bool needMipMap = false)
        {
            RenderTextureDescriptor desc = src;
            desc.depthBufferBits = 0;
            desc.useMipMap = needMipMap;
            desc.autoGenerateMips = false;
            
            return desc;
        }
    }
}