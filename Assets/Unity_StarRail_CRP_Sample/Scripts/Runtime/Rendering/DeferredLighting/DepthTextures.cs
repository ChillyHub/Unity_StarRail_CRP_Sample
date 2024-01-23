using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class MipmapInfo
    {
        public Vector2Int textureSize;
        public int mipLevelCount;
        public Vector2Int[] mipLevelSizes;
        public Vector2Int[] mipLevelOffsets;

        private Vector2 cachedTextureScale;
        private Vector2Int cachedHardwareTextureSize;

        private bool m_OffsetBufferWillNeedUpdate;

        public MipmapInfo()
        {
            mipLevelSizes = new Vector2Int[15];
            mipLevelOffsets = new Vector2Int[15];
        }

        public Vector4[] GetMipLevelParam()
        {
            int size = mipLevelSizes.Length;
            Vector4[] mipLevelParam = new Vector4[size];
            for (int i = 0; i < size; i++)
            {
                mipLevelParam[i] = new Vector4(mipLevelOffsets[i].x, mipLevelOffsets[i].y, mipLevelSizes[i].x, mipLevelSizes[i].y);
            }

            return mipLevelParam;
        }
    }
    
    public class DepthTextures
    {
        public static class ShaderIds
        {
            public static readonly int DepthPyramidTexture = Shader.PropertyToID("_DepthPyramidTexture");
            public static readonly int MipLevelParam = Shader.PropertyToID("_MipLevelParam");
            public static readonly int DepthPyramidMipLevelMax = Shader.PropertyToID("_DepthPyramidMipLevelMax");
        }
        
        private static class TextureName
        {
            public static readonly string DepthPyramidTexture = "_DepthPyramidTexture";
        }
        
        public ref RTHandle DepthPyramidTexture => ref _depthTextures[0];
        public MipmapInfo DepthPyramidMipmapInfo => _depthMipmapInfo;
        
        private readonly RTHandle[] _depthTextures = new RTHandle[1];
        private MipmapInfo _depthMipmapInfo;
        
        public DepthTextures()
        {
            _depthTextures[0] = RTHandles.Alloc(1, 1);
        }
        
        public void Release()
        {
            RTHandles.Release(_depthTextures[0]);
            _depthTextures[0] = null;
        }
        
        public MipmapInfo ReAllocDepthPyramidTextureIfNeed(RenderTextureDescriptor src, bool needMipMap = false)
        {
            RenderingUtils.ReAllocateIfNeeded(ref DepthPyramidTexture, 
                GetDepthPyramidTextureDescriptor(src, needMipMap), 
                wrapMode: TextureWrapMode.Clamp, name: TextureName.DepthPyramidTexture);

            return _depthMipmapInfo;
        }
        
        public void SetGlobalDepthPyramidTexture(CommandBuffer cmd)
        {
            cmd.SetGlobalTexture(ShaderIds.DepthPyramidTexture, DepthPyramidTexture.nameID);
            cmd.SetGlobalVectorArray(ShaderIds.MipLevelParam, _depthMipmapInfo.GetMipLevelParam());
            cmd.SetGlobalInt(ShaderIds.DepthPyramidMipLevelMax, _depthMipmapInfo.mipLevelCount - 1);
        }
        
        public void SetMaterialDepthPyramidTexture(Material material)
        {
            material.SetTexture(ShaderIds.DepthPyramidTexture, DepthPyramidTexture.rt);
            material.SetVectorArray(ShaderIds.MipLevelParam, _depthMipmapInfo.GetMipLevelParam());
            material.SetInt(ShaderIds.DepthPyramidMipLevelMax, _depthMipmapInfo.mipLevelCount - 1);
        }

        public RenderTextureDescriptor GetDepthPyramidTextureDescriptor(RenderTextureDescriptor src, bool needMipMap = false)
        {
#if UNITY_ANDROID
            _depthMipmapInfo = GetNonMipmapInfo(new Vector2Int(src.width, src.height));
#else
            _depthMipmapInfo = needMipMap 
                ? ComputeMipmapInfo(new Vector2Int(src.width, src.height)) 
                : GetNonMipmapInfo(new Vector2Int(src.width, src.height));
#endif
            
            RenderTextureDescriptor desc = new RenderTextureDescriptor
            {
                width = _depthMipmapInfo.textureSize.x,
                height = _depthMipmapInfo.textureSize.y,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                msaaSamples = 1,
                graphicsFormat = GraphicsFormat.R32_SFloat,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true
            };
            return desc;
        }
        
        public MipmapInfo ComputeMipmapInfo(Vector2Int srcSize)
        {
            MipmapInfo mipmapInfo = new MipmapInfo();

            mipmapInfo.mipLevelSizes[0] = srcSize;
            mipmapInfo.mipLevelOffsets[0] = Vector2Int.zero;

            int mipLevel = 0;
            Vector2Int mipSize = srcSize;

            do
            {
                mipLevel++;
                
                mipSize.x = Math.Max(1, (mipSize.x + 1) >> 1);
                mipSize.y = Math.Max(1, (mipSize.y + 1) >> 1);

                mipmapInfo.mipLevelSizes[mipLevel] = mipSize;

                Vector2Int prevMipBegin = mipmapInfo.mipLevelOffsets[mipLevel - 1];
                Vector2Int prevMipEnd = prevMipBegin + mipmapInfo.mipLevelSizes[mipLevel - 1];

                Vector2Int mipBegin = new Vector2Int();

                if ((mipLevel & 1) != 0) // Odd
                {
                    mipBegin.x = prevMipBegin.x;
                    mipBegin.y = prevMipEnd.y;
                }
                else // Even
                {
                    mipBegin.x = prevMipEnd.x;
                    mipBegin.y = prevMipBegin.y;
                }

                mipmapInfo.mipLevelOffsets[mipLevel] = mipBegin;

                srcSize.x = Math.Max(srcSize.x, mipBegin.x + mipSize.x);
                srcSize.y = Math.Max(srcSize.y, mipBegin.y + mipSize.y);
                
            } while (((mipSize.x > 1) || (mipSize.y > 1)) && mipLevel < 14);

            mipmapInfo.textureSize = new Vector2Int(Mathf.CeilToInt(srcSize.x), Mathf.CeilToInt(srcSize.y));
            mipmapInfo.mipLevelCount = mipLevel + 1;

            return mipmapInfo;
        }

        public MipmapInfo GetNonMipmapInfo(Vector2Int srcSize)
        {
            var info = new MipmapInfo();
            info.mipLevelCount = 1;
            info.mipLevelOffsets[0] = Vector2Int.zero;
            info.mipLevelSizes[0] = srcSize;
            info.textureSize = srcSize;

            return info;
        }
    }
}