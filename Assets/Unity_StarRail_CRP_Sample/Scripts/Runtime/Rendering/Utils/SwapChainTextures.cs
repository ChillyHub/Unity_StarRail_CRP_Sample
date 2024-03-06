using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class SwapChainTextures
    {
        public static class TextureName
        {
            public static string Texture1;
            public static string Texture2;
        }
        
        public ref RTHandle FrontBuffer => ref _swapChainTextures[_frontIndex];
        public ref RTHandle BackBuffer => ref _swapChainTextures[_backIndex];
        
        private readonly RTHandle[] _swapChainTextures = new RTHandle[2];
        private int _frontIndex = 0;
        private int _backIndex = 1;
        
        public SwapChainTextures(string name)
        {
            _swapChainTextures[0] = RTHandles.Alloc(1, 1);
            _swapChainTextures[1] = RTHandles.Alloc(1, 1);
            
            TextureName.Texture1 = name + "_SwapChainTexture1";
            TextureName.Texture2 = name + "_SwapChainTexture2";
        }
        
        public void Release()
        {
            RTHandles.Release(_swapChainTextures[0]);
            RTHandles.Release(_swapChainTextures[1]);
            _swapChainTextures[0] = null;
            _swapChainTextures[1] = null;
        }

        public void SwapBuffer()
        {
            (_frontIndex, _backIndex) = (_backIndex, _frontIndex);
        }
        
        public void ReAllocSwapChainTexturesIfNeed(RenderTextureDescriptor src)
        {
            RenderingUtils.ReAllocateIfNeeded(ref _swapChainTextures[0], 
                src, 
                FilterMode.Bilinear, 
                TextureWrapMode.Clamp, 
                name: TextureName.Texture1);
            RenderingUtils.ReAllocateIfNeeded(ref _swapChainTextures[1], 
                src, 
                FilterMode.Bilinear, 
                TextureWrapMode.Clamp, 
                name: TextureName.Texture2);
        }
    }
}