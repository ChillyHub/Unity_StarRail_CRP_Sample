using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class CharacterShadowPass : ScriptableRenderPass
    {
        private static class CharacterShadowConstantBuffer
        {
            public static readonly int WorldToShadowId = Shader.PropertyToID("_CharacterLightWorldToShadow");
            public static readonly int ShadowParamsId = Shader.PropertyToID("_CharacterShadowParams");
            public static readonly int ShadowOffset0Id = Shader.PropertyToID("_CharacterShadowOffset0");
            public static readonly int ShadowOffset1Id = Shader.PropertyToID("_CharacterShadowOffset1");
            public static readonly int ShadowmapSizeId = Shader.PropertyToID("_CharacterShadowmapSize");
        }
        
        // Profiling samplers
        private readonly ProfilingSampler _characterShadowProfilingSampler;
        
        // Shader tags
        private readonly List<ShaderTagId> _shaderTagIds;
        
        private FilteringSettings _filteringSettings;
        
        // Draw system
        private CharacterEntityManager _entityManager;
        private CharacterShadowCasterDrawSystem _drawSystem;

        // Data
        private static readonly int MaxCharacterCounts = 16;
        private static readonly int CharacterShadowSize = 4096;
        // private static readonly int CharacterShadowTileSize = 1024;

        private readonly Matrix4x4[] _characterLightShadowMatrices;
        private readonly ShadowSliceData[] _shadowSliceData;

        // State
        // private bool _createEmptyShadowmap = false;
        
        public CharacterShadowPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(CharacterShadowPass));
            this.renderPassEvent = RenderPassEvent.AfterRenderingShadows;
            
            _characterShadowProfilingSampler = new ProfilingSampler("Character Shadow");

            _shaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("CharacterShadowCaster")
            };

            _filteringSettings = new FilteringSettings(RenderQueueRange.all);

            _characterLightShadowMatrices = new Matrix4x4[MaxCharacterCounts];
            _shadowSliceData = new ShadowSliceData[MaxCharacterCounts];
        }

        public bool Setup(CharacterEntityManager entityManager, CharacterShadowCasterDrawSystem drawSystem)
        {
            Clear();
            
            _entityManager = entityManager;
            _drawSystem = drawSystem;

            int validIndex = 0;
            for (int i = 0; i < entityManager.chunkCount; i++)
            {
                CharacterEntityChunk entityChunk = entityManager.entityChunks[i];
                CharacterShadowCasterDrawCullChunk drawCullChunk = entityManager.shadowCasterDrawCallChunks[i];

                if (!entityManager.IsValid(entityChunk.entity))
                {
                    continue;
                }
                
                _shadowSliceData[validIndex] = drawSystem.GetShadowSliceData(entityChunk, drawCullChunk, validIndex);

                validIndex++;
            }

            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor desc = ShadowTextures.CharacterShadowTextureDesc;

            RenderingUtils.ReAllocateIfNeeded(
                ref ShadowTexturesManager.Textures.CharacterShadowTexture,
                desc, name: ShadowTextures.CharacterShadowTextureName);

            ConfigureTarget(ShadowTexturesManager.Textures.CharacterShadowTexture);
            ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!CheckExecute(ref renderingData))
            {
                return;
            }
            
            ref LightData lightData = ref renderingData.lightData;
            ref CullingResults cullingResults = ref renderingData.cullResults;
            ref ShadowData shadowData = ref renderingData.shadowData;

            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData,
                renderingData.cameraData.defaultOpaqueSortFlags);
            
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _characterShadowProfilingSampler))
            {
                int validIndex = 0;
                for (int i = 0; i < _entityManager.chunkCount; i++)
                {
                    CharacterEntityChunk entityChunk = _entityManager.entityChunks[i];
                    if (!_entityManager.IsValid(entityChunk.entity))
                    {
                        continue;
                    }
                    
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.CastingPunctualLightShadow, false);
                    RenderShadowSlice(cmd, ref context, ref _shadowSliceData[validIndex],
                        ref drawingSettings, cullingResults, 
                        _shadowSliceData[validIndex].projectionMatrix, _shadowSliceData[validIndex].viewMatrix, i);

                    validIndex++;
                }

                // cmd.SetViewProjectionMatrices(renderingData.cameraData.GetViewMatrix(), renderingData.cameraData.GetProjectionMatrix());

                // bool tmp = shadowData.supportsSoftShadows;
                // shadowData.supportsSoftShadows = false;

                bool isKeywordSoftShadowsEnabled = shadowData.supportsSoftShadows;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, shadowData.mainLightShadowCascadesCount == 1);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, shadowData.mainLightShadowCascadesCount > 1);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, isKeywordSoftShadowsEnabled);

                SetupMainLightShadowReceiverConstants(cmd, shadowData.supportsSoftShadows, validIndex);

                // shadowData.supportsSoftShadows = tmp;
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

        private void Clear()
        {
            for (int i = 0; i < _characterLightShadowMatrices.Length; ++i)
                _characterLightShadowMatrices[i] = Matrix4x4.identity;

            for (int i = 0; i < _shadowSliceData.Length; ++i)
                _shadowSliceData[i].Clear();
        }
        
        private Matrix4x4 GetShadowTransform(Matrix4x4 proj, Matrix4x4 view)
        {
            Matrix4x4 worldToShadow = GL.GetGPUProjectionMatrix(proj, false) * view;

            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            // textureScaleAndBias maps texture space coordinates from [-1,1] to [0,1]

            // Apply texture scale and offset to save a MAD in shader.
            return textureScaleAndBias * worldToShadow;
        }
        
        private void ApplySliceTransform(ref ShadowSliceData shadowSliceData, int atlasWidth, int atlasHeight)
        {
            Matrix4x4 sliceTransform = Matrix4x4.identity;
            float oneOverAtlasWidth = 1.0f / atlasWidth;
            float oneOverAtlasHeight = 1.0f / atlasHeight;
            sliceTransform.m00 = shadowSliceData.resolution * oneOverAtlasWidth;
            sliceTransform.m11 = shadowSliceData.resolution * oneOverAtlasHeight;
            sliceTransform.m03 = shadowSliceData.offsetX * oneOverAtlasWidth;
            sliceTransform.m13 = shadowSliceData.offsetY * oneOverAtlasHeight;

            // Apply shadow slice scale and offset
            shadowSliceData.shadowTransform = sliceTransform * shadowSliceData.shadowTransform;
        }
        
        void SetEmptyMainLightCascadeShadowmap(ref ScriptableRenderContext context)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, true);
            cmd.SetGlobalTexture(ShadowTextures.CharacterShadowTextureName, ShadowTexturesManager.Textures.CharacterShadowTexture);
            cmd.SetGlobalVector(CharacterShadowConstantBuffer.ShadowParamsId,
                new Vector4(1, 0, 1, 0));
            int width = ShadowTextures.CharacterShadowTextureDesc.width;
            int height = ShadowTextures.CharacterShadowTextureDesc.height;
            cmd.SetGlobalVector(CharacterShadowConstantBuffer.ShadowmapSizeId,
                new Vector4(1f / width, 1f / height, width, height));
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        void SetupMainLightShadowReceiverConstants(CommandBuffer cmd, bool supportsSoftShadows, int validCount)
        {
            bool softShadows = supportsSoftShadows;

            int count = validCount;
            for (int i = 0; i < count; ++i)
                _characterLightShadowMatrices[i] = _shadowSliceData[i].shadowTransform;

            // We setup and additional a no-op WorldToShadow matrix in the last index
            // because the ComputeCascadeIndex function in Shadows.hlsl can return an index
            // out of bounds. (position not inside any cascade) and we want to avoid branching
            Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
            noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;
            for (int i = count; i < MaxCharacterCounts; ++i)
                _characterLightShadowMatrices[i] = noOpShadowMatrix;

            float invShadowAtlasWidth = 1.0f / CharacterShadowSize;
            float invShadowAtlasHeight = 1.0f / CharacterShadowSize;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;
            float softShadowsProp = GetSoftShadowQuality(softShadows);

            cmd.SetGlobalTexture(ShadowTextures.CharacterShadowTextureName, ShadowTexturesManager.Textures.CharacterShadowTexture.nameID);
            cmd.SetGlobalMatrixArray(CharacterShadowConstantBuffer.WorldToShadowId, _characterLightShadowMatrices);
            cmd.SetGlobalVector(CharacterShadowConstantBuffer.ShadowParamsId,
                new Vector4(1.0f, softShadowsProp, 0.0f, 0.0f));

            // Inside shader soft shadows are controlled through global keyword.
            // If any additional light has soft shadows it will force soft shadows on main light too.
            // As it is not trivial finding out which additional light has soft shadows, we will pass main light properties if soft shadows are supported.
            // This workaround will be removed once we will support soft shadows per light.
            if (supportsSoftShadows)
            {
                cmd.SetGlobalVector(CharacterShadowConstantBuffer.ShadowOffset0Id,
                    new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight));
                cmd.SetGlobalVector(CharacterShadowConstantBuffer.ShadowOffset1Id,
                    new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, invHalfShadowAtlasWidth, invHalfShadowAtlasHeight));

                // Currently only used when !SHADER_API_MOBILE but risky to not set them as it's generic
                // enough so custom shaders might use it.
                cmd.SetGlobalVector(CharacterShadowConstantBuffer.ShadowmapSizeId, new Vector4(invShadowAtlasWidth,
                    invShadowAtlasHeight, CharacterShadowSize, CharacterShadowSize));
            }
        }
        
        private void GetScaleAndBiasForLinearDistanceFade(float fadeDistance, float border, out float scale, out float bias)
        {
            // To avoid division from zero
            // This values ensure that fade within cascade will be 0 and outside 1
            if (border < 0.0001f)
            {
                float multiplier = 1000f; // To avoid blending if difference is in fractions
                scale = multiplier;
                bias = -fadeDistance * multiplier;
                return;
            }

            border = 1 - border;
            border *= border;

            // Fade with distance calculation is just a linear fade from 90% of fade distance to fade distance. 90% arbitrarily chosen but should work well enough.
            float distanceFadeNear = border * fadeDistance;
            scale = 1.0f / (fadeDistance - distanceFadeNear);
            bias = -distanceFadeNear / (fadeDistance - distanceFadeNear);
        }
        
        private void RenderShadowSlice(CommandBuffer cmd, ref ScriptableRenderContext context,
            ref ShadowSliceData shadowSliceData, ref DrawingSettings drawingSettings, CullingResults cullingResults, 
            Matrix4x4 proj, Matrix4x4 view, int chunkIndex)
        {
            cmd.SetGlobalDepthBias(1.0f, 2.5f); // these values match HDRP defaults (see https://github.com/Unity-Technologies/Graphics/blob/9544b8ed2f98c62803d285096c91b44e9d8cbc47/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowAtlas.cs#L197 )

            cmd.SetViewport(new Rect(shadowSliceData.offsetX, shadowSliceData.offsetY, shadowSliceData.resolution, shadowSliceData.resolution));
            cmd.SetViewProjectionMatrices(view, proj);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            //context.DrawRenderers(cullingResults, ref drawingSettings, ref _filteringSettings);
            _drawSystem.Execute(cmd, chunkIndex);
            
            cmd.DisableScissorRect();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            cmd.SetGlobalDepthBias(0.0f, 0.0f); // Restore previous depth bias values
        }
        
        private float GetSoftShadowQuality(bool softShadowsEnabled)
        {
            float softShadows = softShadowsEnabled ? 1.0f : 0.0f;
            softShadows *= Math.Max((int)SoftShadowQuality.High, (int)SoftShadowQuality.Low);

            return softShadows;
        }
    }
}