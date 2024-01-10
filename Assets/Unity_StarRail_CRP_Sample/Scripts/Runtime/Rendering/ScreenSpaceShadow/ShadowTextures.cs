using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class ShadowTextures
    {
        public static readonly string ScreenSpaceShadowTextureName = "_ScreenSpaceShadowmapTexture";
        public static readonly string CharacterShadowTextureName = "_CharacterShadowmapTexture";
        public static readonly string MainLightShadowTextureName = "_MainLightShadowmapTexture";
        public static readonly string AddLightShadowTextureName = "_AdditionalLightsShadowmapTexture";

        public static GraphicsFormat ScreenSpaceShadowTextureFormat => GraphicsFormat.R8_UNorm;
        public static GraphicsFormat CharacterShadowTextureFormat => GraphicsFormat.None;

        public static RenderTextureDescriptor ScreenSpaceShadowTextureDesc =>
            new RenderTextureDescriptor(1, 1, ScreenSpaceShadowTextureFormat, 0);

        public static RenderTextureDescriptor CharacterShadowTextureDesc =>
            new RenderTextureDescriptor(4096, 4096, CharacterShadowTextureFormat, 16);
        
        public RenderTargetIdentifier ScreenSpaceShadowTextureId => ScreenSpaceShadowTexture;
        public RenderTargetIdentifier CharacterShadowTextureId => CharacterShadowTexture;
        public RenderTargetIdentifier MainLightShadowTextureId => new RenderTargetIdentifier(MainLightShadowTextureName);
        public RenderTargetIdentifier AddLightShadowTextureId => new RenderTargetIdentifier(AddLightShadowTextureName);

        public ref RTHandle ScreenSpaceShadowTexture => ref _shadowTextures[0];
        public ref RTHandle CharacterShadowTexture => ref _shadowTextures[1];

        private RTHandle[] _shadowTextures = new RTHandle[2];
        
        public void ReAllocIfNeed(RenderTextureDescriptor descriptor)
        {
            RenderTextureDescriptor desc = descriptor;
            desc.depthBufferBits = 0;

            desc.graphicsFormat = ScreenSpaceShadowTextureFormat;
            RenderingUtils.ReAllocateIfNeeded(ref _shadowTextures[0], desc, FilterMode.Bilinear, 
                name: ScreenSpaceShadowTextureName);

            desc.graphicsFormat = CharacterShadowTextureFormat;
            RenderingUtils.ReAllocateIfNeeded(ref _shadowTextures[1], desc, name: CharacterShadowTextureName);
        }

        public void Release()
        {
            _shadowTextures[0]?.Release();
            _shadowTextures[1]?.Release();
        }
    }

    public class CharacterShadowData
    {
        //public readonly Matrix4x4[] 
    }
    
    public class ShadowTexturesManager
    {
        private static readonly Lazy<ShadowTexturesManager> Ins =
            new Lazy<ShadowTexturesManager>(() => new ShadowTexturesManager());

        public static ShadowTextures Textures => Ins.Value.ShadowTextures;

        public readonly ShadowTextures ShadowTextures = new ShadowTextures();
    }
}