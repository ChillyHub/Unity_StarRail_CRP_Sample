using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class GBufferTextures
    {
        public static readonly string GBuffer0Name = "_CustomGBuffer0";
        public static readonly string GBuffer1Name = "_CustomGBuffer1";
        public static readonly string GBuffer2Name = "_CustomGBuffer2";
        public static readonly string GBuffer3Name = "_CustomGBuffer3";
        public static readonly string DepthTextureName = "_CustomDepthTexture";

        public RTHandle GBuffer0 => _gBuffers[0];  // albedo/lighted  metalic/
        public RTHandle GBuffer1 => _gBuffers[1];  // normal          smoothness
        public RTHandle GBuffer2 => _gBuffers[2];  // GI   GI   GI    
        //public RTHandle GBuffer3 => _gBuffers[3];  // No use

        public ref RTHandle[] GBuffers => ref _gBuffers;
        public RenderTargetIdentifier[] GBufferIds => new[]
        {
            _gBuffers[0].nameID,
            _gBuffers[1].nameID,
            _gBuffers[2].nameID,
        };

        private RTHandle[] _gBuffers = new RTHandle[3];

        public GraphicsFormat GetGBufferFormat(int index)
        {
            switch (index)
            {
                case 0:
                    return QualitySettings.activeColorSpace == ColorSpace.Linear 
                        ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
                case 1:
                    return GraphicsFormat.R8G8B8A8_UNorm;
                default:
                    return GraphicsFormat.None;
            }
        }

        public void ReAllocIfNeed(RenderTextureDescriptor descriptor)
        {
            RenderTextureDescriptor desc = descriptor;
            desc.depthBufferBits = 0;

            desc.graphicsFormat = GetGBufferFormat(0);
            RenderingUtils.ReAllocateIfNeeded(ref _gBuffers[0], desc, FilterMode.Bilinear, name: GBuffer0Name);

            desc.graphicsFormat = GetGBufferFormat(1);
            RenderingUtils.ReAllocateIfNeeded(ref _gBuffers[1], desc, FilterMode.Bilinear, name: GBuffer1Name);

            // desc.graphicsFormat = GetGBufferFormat(2);
            // RenderingUtils.ReAllocateIfNeeded(ref _gBuffers[2], desc, FilterMode.Bilinear, name: GBuffer2Name);
        }

        public void Release()
        {
            _gBuffers[0]?.Release();
            _gBuffers[1]?.Release();
            _gBuffers[2]?.Release();
        }
    }

    public class GBufferManager
    {
        private static readonly Lazy<GBufferManager> Ins =
            new Lazy<GBufferManager>(() => new GBufferManager());

        public static GBufferTextures GBuffer => Ins.Value._gBuffer;

        private readonly GBufferTextures _gBuffer = new GBufferTextures();
    }
}