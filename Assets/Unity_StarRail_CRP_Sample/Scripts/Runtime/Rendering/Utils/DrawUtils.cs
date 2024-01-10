using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public static class DrawUtils
    {
        #region Blitter

        [Obsolete("Obsolete")]
        public static void BlitToTexture(CommandBuffer cmd, RenderTargetIdentifier src, int srcId, 
            RTHandle dest, Material mat, int passIndex = -1, bool useDrawProcedural = false)
        {
            Vector2 viewportScale = new Vector2(1.0f, 1.0f);
            // Will set the correct camera viewport as well.
            CoreUtils.SetRenderTarget(cmd, dest);
            cmd.SetGlobalTexture(srcId, src);
            mat.SetVector(Shader.PropertyToID("_BlitScaleBias"), viewportScale);
            DrawFullscreenMesh(cmd, mat, null, passIndex, useDrawProcedural);
        }

        [Obsolete("Obsolete")]
        public static void DrawFullscreenMesh(CommandBuffer cmd, Material mat,
            MaterialPropertyBlock block = null, int passIndex = -1, bool useDrawProcedural = false)
        {
            if (useDrawProcedural)
            {
                cmd.DrawProcedural(Matrix4x4.identity, mat, passIndex, MeshTopology.Triangles, 3, 1, block);
            }
            else
            {
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, mat, 0, passIndex, block);
            }
        }

        #endregion

        public static void Dispatch(CommandBuffer cmd, ComputeShader cs, int kernel, float width, float height)
        {
            uint x, y, z;
            cs.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            int groupX = Mathf.CeilToInt(width / x);
            int groupY = Mathf.CeilToInt(height / y);
            cmd.DispatchCompute(cs, kernel, groupX, groupY, 1);
        }

        public static void Dispatch(CommandBuffer cmd, ComputeShader cs, int kernel, float count)
        {
            uint x, y, z;
            cs.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            int groupX = Mathf.CeilToInt(count / x);
            cmd.DispatchCompute(cs, kernel, groupX, 1, 1);
        }

        public static void Dispatch(CommandBuffer cmd, ComputeShader cs, int kernel)
        {
            cmd.DispatchCompute(cs, kernel, 1, 1, 1);
        }

        //public static bool RTHandleReAllocateIfNeeded(
        //    ref RTHandle handle,
        //    in RenderTextureDescriptor descriptor,
        //    FilterMode filterMode = FilterMode.Point,
        //    TextureWrapMode wrapMode = TextureWrapMode.Repeat,
        //    bool isShadowMap = false,
        //    int anisoLevel = 1,
        //    float mipMapBias = 0,
        //    string name = "")
        //{
        //    if (RTHandleNeedsReAlloc(handle, descriptor, filterMode, wrapMode, isShadowMap, anisoLevel, mipMapBias,
        //            name, false))
        //    {
        //        handle?.Release();
        //        handle = RTHandles.Alloc(
        //            descriptor.width,
        //            descriptor.height,
        //            descriptor.volumeDepth,
        //            (DepthBits)descriptor.depthBufferBits,
        //            descriptor.graphicsFormat,
        //            filterMode,
        //            wrapMode,
        //            descriptor.dimension,
        //            descriptor.enableRandomWrite,
        //            descriptor.useMipMap,
        //            descriptor.autoGenerateMips,
        //            isShadowMap,
        //            anisoLevel,
        //            mipMapBias,
        //            (MSAASamples)descriptor.msaaSamples,
        //            descriptor.bindMS,
        //            descriptor.useDynamicScale,
        //            descriptor.memoryless,
        //            name);
        //        return true;
        //    }
//
        //    return false;
        //}
//
        //private static bool RTHandleNeedsReAlloc(
        //    RTHandle handle,
        //    in RenderTextureDescriptor descriptor,
        //    FilterMode filterMode,
        //    TextureWrapMode wrapMode,
        //    bool isShadowMap,
        //    int anisoLevel,
        //    float mipMapBias,
        //    string name,
        //    bool scaled)
        //{
        //    if (handle == null || handle.rt == null)
        //        return true;
        //    if (handle.useScaling != scaled)
        //        return true;
        //    if (!scaled && (handle.rt.width != descriptor.width || handle.rt.height != descriptor.height))
        //        return true;
        //    return
        //        handle.rt.descriptor.depthBufferBits != descriptor.depthBufferBits ||
        //        (handle.rt.descriptor.depthBufferBits == (int)DepthBits.None && !isShadowMap &&
        //         handle.rt.descriptor.graphicsFormat != descriptor.graphicsFormat) ||
        //        handle.rt.descriptor.dimension != descriptor.dimension ||
        //        handle.rt.descriptor.enableRandomWrite != descriptor.enableRandomWrite ||
        //        handle.rt.descriptor.useMipMap != descriptor.useMipMap ||
        //        handle.rt.descriptor.autoGenerateMips != descriptor.autoGenerateMips ||
        //        handle.rt.descriptor.msaaSamples != descriptor.msaaSamples ||
        //        handle.rt.descriptor.bindMS != descriptor.bindMS ||
        //        handle.rt.descriptor.useDynamicScale != descriptor.useDynamicScale ||
        //        handle.rt.descriptor.memoryless != descriptor.memoryless ||
        //        handle.rt.filterMode != filterMode ||
        //        handle.rt.wrapMode != wrapMode ||
        //        handle.rt.anisoLevel != anisoLevel ||
        //        handle.rt.mipMapBias != mipMapBias ||
        //        handle.name != name;
        //}

        public static void SetKeyword(Material material, string name, bool enable)
        {
            if (material.IsKeywordEnabled(name) != enable)
            {
                if (enable)
                {
                    material.EnableKeyword(name);
                }
                else
                {
                    material.DisableKeyword(name);
                }
            }
        }

        public static void SetKeyword(ComputeShader cs, string name, bool enable)
        {
            if (cs.IsKeywordEnabled(name) != enable)
            {
                if (enable)
                {
                    cs.EnableKeyword(name);
                }
                else
                {
                    cs.DisableKeyword(name);
                }
            }
        }
    }
}
