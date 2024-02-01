using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class DeferredLight
    {
        private static class ShaderConstants
        {
            public static readonly int MainLightPositionId = Shader.PropertyToID("_MainLightPosition");   // ForwardLights.LightConstantBuffer also refers to the same ShaderPropertyID - TODO: move this definition to a common location shared by other UniversalRP classes
            public static readonly int MainLightColorId = Shader.PropertyToID("_MainLightColor");         // ForwardLights.LightConstantBuffer also refers to the same ShaderPropertyID - TODO: move this definition to a common location shared by other UniversalRP classes
            public static readonly int MainLightLayerMaskId = Shader.PropertyToID("_MainLightLayerMask"); // ForwardLights.LightConstantBuffer also refers to the same ShaderPropertyID - TODO: move this definition to a common location shared by other UniversalRP classes
            
            public static readonly int ScreenToWorldId = Shader.PropertyToID("_ScreenToWorld");
            
            public static readonly int SpotLightScaleId = Shader.PropertyToID("_SpotLightScale");
            public static readonly int SpotLightBiasId = Shader.PropertyToID("_SpotLightBias");
            public static readonly int SpotLightGuardId = Shader.PropertyToID("_SpotLightGuard");
            public static readonly int LightPosWSId = Shader.PropertyToID("_LightPosWS");
            public static readonly int LightColorId = Shader.PropertyToID("_LightColor");
            public static readonly int LightAttenuationId = Shader.PropertyToID("_LightAttenuation");
            public static readonly int LightOcclusionProbInfoId = Shader.PropertyToID("_LightOcclusionProbInfo");
            public static readonly int LightDirectionId = Shader.PropertyToID("_LightDirection");
            public static readonly int LightFlagsId = Shader.PropertyToID("_LightFlags");
            public static readonly int ShadowLightIndexId = Shader.PropertyToID("_ShadowLightIndex");
            public static readonly int LightLayerMaskId = Shader.PropertyToID("_LightLayerMask");
            public static readonly int CookieLightIndexId = Shader.PropertyToID("_CookieLightIndex");
        }
        
        public int RenderWidth { get; set; }
        public int RenderHeight { get; set; }
        public bool UseRenderPass { get; set; }
        public MixedLightingSetup MixedLightingSetup { get; set; }
        
        // Visible lights indices rendered using stencil volumes.
        public NativeArray<ushort> StencilVisLights => _stencilVisLights;
        // Offset of each type of lights in m_stencilVisLights.
        public NativeArray<ushort> StencilVisLightOffsets => _stencilVisLightOffsets;
        
        private NativeArray<ushort> _stencilVisLights;
        private NativeArray<ushort> _stencilVisLightOffsets;

        private readonly ProfilingSampler _profilingSetupLightConstants;
        
        // Data
        private Mesh _triangleMesh;
        private Mesh _sphereMesh;
        private Mesh _hemisphereMesh;
        private static readonly float StencilShapeGuard = 1.06067f; 
        // stencil geometric shapes must be inflated to fit the analytic shapes.

        public DeferredLight()
        {
            _profilingSetupLightConstants = new ProfilingSampler("Setup Light Constants");

            UseRenderPass = false;
            
            _triangleMesh = CreateFullscreenMesh();
            _sphereMesh = CreateSphereMesh();
            _hemisphereMesh = CreateHemisphereMesh();
        }

        public void Setup()
        {
            
        }
        
        public void ResolveMixedLightingMode(ref RenderingData renderingData)
        {
            // Find the mixed lighting mode. This is the same logic as ForwardLights.
            this.MixedLightingSetup = MixedLightingSetup.None;

#if !UNITY_EDITOR
            // This flag is used to strip mixed lighting shader variants when a player is built.
            // All shader variants are available in the editor.
            if (renderingData.lightData.supportsMixedLighting)
#endif
            {
                NativeArray<VisibleLight> visibleLights = renderingData.lightData.visibleLights;
                for (int lightIndex = 0; lightIndex < renderingData.lightData.visibleLights.Length && this.MixedLightingSetup == MixedLightingSetup.None; ++lightIndex)
                {
                    Light light = visibleLights[lightIndex].light;

                    if (light != null
                        && light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed
                        && light.shadows != LightShadows.None)
                    {
                        switch (light.bakingOutput.mixedLightingMode)
                        {
                            case MixedLightingMode.Subtractive:
                                this.MixedLightingSetup = MixedLightingSetup.Subtractive;
                                break;
                            case MixedLightingMode.Shadowmask:
                                this.MixedLightingSetup = MixedLightingSetup.ShadowMask;
                                break;
                        }
                    }
                }
            }
        }

        public void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            // Support for dynamic resolution.
            this.RenderWidth = camera.allowDynamicResolution ? 
                Mathf.CeilToInt(ScalableBufferManager.widthScaleFactor * renderingData.cameraData.cameraTargetDescriptor.width) : 
                renderingData.cameraData.cameraTargetDescriptor.width;
            this.RenderHeight = camera.allowDynamicResolution ? 
                Mathf.CeilToInt(ScalableBufferManager.heightScaleFactor * renderingData.cameraData.cameraTargetDescriptor.height) : 
                renderingData.cameraData.cameraTargetDescriptor.height;

            // inspect lights in renderingData.lightData.visibleLights and convert them to entries in prePunctualLights OR m_stencilVisLights
            // currently we store point lights and spot lights that can be rendered by TiledDeferred, in the same prePunctualLights list
            PrecomputeLights(
                out _stencilVisLights,
                out _stencilVisLightOffsets,
                ref renderingData.lightData.visibleLights,
                renderingData.lightData.additionalLightsCount != 0 || renderingData.lightData.mainLightIndex >= 0,
                renderingData.cameraData.camera.worldToCameraMatrix,
                renderingData.cameraData.camera.orthographic,
                renderingData.cameraData.camera.nearClipPlane
            );
            
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSetupLightConstants))
            {
                // Shared uniform constants for all lights.
                SetupShaderLightConstants(cmd, ref renderingData);

#if UNITY_EDITOR
                // This flag is used to strip mixed lighting shader variants when a player is built.
                // All shader variants are available in the editor.
                bool supportsMixedLighting = true;
#else
                bool supportsMixedLighting = renderingData.lightData.supportsMixedLighting;
#endif

                // Setup global keywords.
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._GBUFFER_NORMALS_OCT, true);
                bool isShadowMask = supportsMixedLighting && this.MixedLightingSetup == MixedLightingSetup.ShadowMask;
                bool isShadowMaskAlways = isShadowMask && QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask;
                bool isSubtractive = supportsMixedLighting && this.MixedLightingSetup == MixedLightingSetup.Subtractive;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.LightmapShadowMixing, isSubtractive || isShadowMaskAlways);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ShadowsShadowMask, isShadowMask);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MixedLightingSubtractive, isSubtractive); // Backward compatibility
                // This should be moved to a more global scope when framebuffer fetch is introduced to more passes
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.RenderPassEnabled, this.UseRenderPass && renderingData.cameraData.cameraType == CameraType.Game);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public bool HasStencilLightsOfType(LightType type)
        {
            return StencilVisLightOffsets[(int)type] != 0xFFFF;
        }
        
        public void RenderStencilDirectionalLights(CommandBuffer cmd, ref RenderingData renderingData,
            NativeArray<VisibleLight> visibleLights, int mainLightIndex, 
            Material material, int[] passes, MaterialPropertyBlock propertyBlock = null)
        {
            cmd.EnableShaderKeyword(ShaderKeywordStrings._DIRECTIONAL);
            
            // Directional lights.
            bool isFirstLight = true;

            for (int offset = StencilVisLightOffsets[(int)LightType.Directional];
                 offset < StencilVisLights.Length;
                 ++offset)
            {
                ushort visLightIndex = StencilVisLights[offset];
                VisibleLight vl = visibleLights[visLightIndex];
                if (vl.lightType != LightType.Directional)
                    break;
                
                // Avoid light find on every access.
                Light light = vl.light;

                Vector4 lightDir, lightColor, lightAttenuation, lightSpotDir, lightOcclusionChannel;
                UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, visLightIndex, out lightDir, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionChannel);

                int lightFlags = 0;
                if (vl.light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed)
                    lightFlags |= 4; //(int)LightFlag.SubtractiveMixedLighting;

                var additionalLightData = vl.light.GetUniversalAdditionalLightData();
                uint lightLayerMask = additionalLightData.renderingLayers;

                // Setup shadow parameters:
                bool hasDeferredShadows;
                if (visLightIndex == mainLightIndex)
                {
                    hasDeferredShadows = light && light.shadows != LightShadows.None;
                }
                else
                {
                    int shadowLightIndex = GetShadowLightIndexFromLightIndex(visLightIndex, mainLightIndex);
                    hasDeferredShadows = light && light.shadows != LightShadows.None && shadowLightIndex >= 0;
                    cmd.SetGlobalInt(ShaderConstants.ShadowLightIndexId, shadowLightIndex);
                }
                SetAdditionalLightsShadowsKeyword(cmd, ref renderingData, hasDeferredShadows);

                bool hasSoftShadow = hasDeferredShadows && renderingData.shadowData.supportsSoftShadows && light.shadows == LightShadows.Soft;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, hasSoftShadow);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._DEFERRED_FIRST_LIGHT, isFirstLight); // First directional light applies SSAO
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._DEFERRED_MAIN_LIGHT, visLightIndex == mainLightIndex); // main directional light use different uniform constants from additional directional lights

                cmd.SetGlobalVector(ShaderConstants.LightColorId, lightColor); // VisibleLight.finalColor already returns color in active color space
                cmd.SetGlobalVector(ShaderConstants.LightDirectionId, lightDir);
                cmd.SetGlobalInt(ShaderConstants.LightFlagsId, lightFlags);
                cmd.SetGlobalInt(ShaderConstants.LightLayerMaskId, (int)lightLayerMask);

                if (_triangleMesh == null)
                {
                    _triangleMesh = CreateFullscreenMesh();
                }

                // Lighting pass.
                for (int i = 0; i < passes.Length; i++)
                {
                    cmd.DrawMesh(_triangleMesh, Matrix4x4.identity, material, 0, passes[i], propertyBlock);
                }

                isFirstLight = false;
            }
            
            cmd.DisableShaderKeyword(ShaderKeywordStrings._DIRECTIONAL);
        }
        
        public void RenderStencilPointLights(CommandBuffer cmd, ref RenderingData renderingData, 
            NativeArray<VisibleLight> visibleLights, int mainLightIndex, 
            Material material, int[] passes, MaterialPropertyBlock propertyBlock = null)
        {
            cmd.EnableShaderKeyword(ShaderKeywordStrings._POINT);

            for (int offset = StencilVisLightOffsets[(int)LightType.Point]; 
                 offset < StencilVisLights.Length; ++offset)
            {
                ushort visLightIndex = StencilVisLights[offset];
                VisibleLight vl = visibleLights[visLightIndex];
                if (vl.lightType != LightType.Point)
                    break;
                
                // Avoid light find on every access.
                Light light = vl.light;

                Vector3 posWS = vl.localToWorldMatrix.GetColumn(3);
                Matrix4x4 transformMatrix = new Matrix4x4(
                    new Vector4(vl.range, 0.0f, 0.0f, 0.0f),
                    new Vector4(0.0f, vl.range, 0.0f, 0.0f),
                    new Vector4(0.0f, 0.0f, vl.range, 0.0f),
                    new Vector4(posWS.x, posWS.y, posWS.z, 1.0f)
                );

                Vector4 lightPos, lightColor, lightAttenuation, lightSpotDir, lightOcclusionChannel;
                UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, visLightIndex, out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionChannel);

                var additionalLightData = vl.light.GetUniversalAdditionalLightData();
                uint lightLayerMask = additionalLightData.renderingLayers;

                int lightFlags = 0;
                if (vl.light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed)
                    lightFlags |= 4; // (int)LightFlag.SubtractiveMixedLighting;

                int shadowLightIndex = GetShadowLightIndexFromLightIndex(visLightIndex, mainLightIndex);
                bool hasDeferredLightShadows = light && light.shadows != LightShadows.None && shadowLightIndex >= 0;
                SetAdditionalLightsShadowsKeyword(cmd, ref renderingData, hasDeferredLightShadows);

                bool hasSoftShadow = hasDeferredLightShadows && renderingData.shadowData.supportsSoftShadows && light.shadows == LightShadows.Soft;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, hasSoftShadow);

                int cookieLightIndex = GetLightCookieShaderDataIndex(visLightIndex, visibleLights);
                // We could test this in shader (static if) a variant (shader change) is undesirable. Same for spot light.
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.LightCookies, cookieLightIndex >= 0);
                cmd.SetGlobalInt(ShaderConstants.CookieLightIndexId, cookieLightIndex);

                cmd.SetGlobalVector(ShaderConstants.LightPosWSId, lightPos);
                cmd.SetGlobalVector(ShaderConstants.LightColorId, lightColor);
                cmd.SetGlobalVector(ShaderConstants.LightAttenuationId, lightAttenuation);
                cmd.SetGlobalVector(ShaderConstants.LightOcclusionProbInfoId, lightOcclusionChannel);
                cmd.SetGlobalInt(ShaderConstants.LightFlagsId, lightFlags);
                cmd.SetGlobalInt(ShaderConstants.ShadowLightIndexId, shadowLightIndex);
                cmd.SetGlobalInt(ShaderConstants.LightLayerMaskId, (int)lightLayerMask);
                cmd.SetGlobalInt(ShaderConstants.CookieLightIndexId, cookieLightIndex);
                
                if (_sphereMesh == null)
                {
                    _sphereMesh = CreateSphereMesh();
                }
                
                // Lighting pass.
                for (int i = 0; i < passes.Length; i++)
                {
                    cmd.DrawMesh(_sphereMesh, transformMatrix, material, 0, passes[i], propertyBlock);
                }
            }

            cmd.DisableShaderKeyword(ShaderKeywordStrings._POINT);
        }
        
        public void RenderStencilPointLights(CommandBuffer cmd, ref RenderingData renderingData, 
            NativeArray<VisibleLight> visibleLights, int mainLightIndex, LightShadows cullLightShadows, 
            Material material, int[] passes, MaterialPropertyBlock propertyBlock = null)
        {
            cmd.EnableShaderKeyword(ShaderKeywordStrings._POINT);

            for (int offset = StencilVisLightOffsets[(int)LightType.Point]; 
                 offset < StencilVisLights.Length; ++offset)
            {
                ushort visLightIndex = StencilVisLights[offset];
                VisibleLight vl = visibleLights[visLightIndex];
                if (vl.lightType != LightType.Point)
                    break;
                
                // Avoid light find on every access.
                Light light = vl.light;

                if (light.shadows == cullLightShadows)
                    continue;

                Vector3 posWS = vl.localToWorldMatrix.GetColumn(3);
                Matrix4x4 transformMatrix = new Matrix4x4(
                    new Vector4(vl.range, 0.0f, 0.0f, 0.0f),
                    new Vector4(0.0f, vl.range, 0.0f, 0.0f),
                    new Vector4(0.0f, 0.0f, vl.range, 0.0f),
                    new Vector4(posWS.x, posWS.y, posWS.z, 1.0f)
                );

                Vector4 lightPos, lightColor, lightAttenuation, lightSpotDir, lightOcclusionChannel;
                UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, visLightIndex, out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionChannel);

                var additionalLightData = vl.light.GetUniversalAdditionalLightData();
                uint lightLayerMask = additionalLightData.renderingLayers;

                int lightFlags = 0;
                if (vl.light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed)
                    lightFlags |= 4; // (int)LightFlag.SubtractiveMixedLighting;

                int shadowLightIndex = GetShadowLightIndexFromLightIndex(visLightIndex, mainLightIndex);
                bool hasDeferredLightShadows = light && light.shadows != LightShadows.None && shadowLightIndex >= 0;
                SetAdditionalLightsShadowsKeyword(cmd, ref renderingData, hasDeferredLightShadows);

                bool hasSoftShadow = hasDeferredLightShadows && renderingData.shadowData.supportsSoftShadows && light.shadows == LightShadows.Soft;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, hasSoftShadow);

                int cookieLightIndex = GetLightCookieShaderDataIndex(visLightIndex, visibleLights);
                // We could test this in shader (static if) a variant (shader change) is undesirable. Same for spot light.
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.LightCookies, cookieLightIndex >= 0);
                cmd.SetGlobalInt(ShaderConstants.CookieLightIndexId, cookieLightIndex);

                cmd.SetGlobalVector(ShaderConstants.LightPosWSId, lightPos);
                cmd.SetGlobalVector(ShaderConstants.LightColorId, lightColor);
                cmd.SetGlobalVector(ShaderConstants.LightAttenuationId, lightAttenuation);
                cmd.SetGlobalVector(ShaderConstants.LightOcclusionProbInfoId, lightOcclusionChannel);
                cmd.SetGlobalInt(ShaderConstants.LightFlagsId, lightFlags);
                cmd.SetGlobalInt(ShaderConstants.ShadowLightIndexId, shadowLightIndex);
                cmd.SetGlobalInt(ShaderConstants.LightLayerMaskId, (int)lightLayerMask);
                cmd.SetGlobalInt(ShaderConstants.CookieLightIndexId, cookieLightIndex);
                
                if (_sphereMesh == null)
                {
                    _sphereMesh = CreateSphereMesh();
                }
                
                // Lighting pass.
                for (int i = 0; i < passes.Length; i++)
                {
                    cmd.DrawMesh(_sphereMesh, transformMatrix, material, 0, passes[i], propertyBlock);
                }
            }

            cmd.DisableShaderKeyword(ShaderKeywordStrings._POINT);
        }
        
        public void RenderStencilSpotLights(CommandBuffer cmd, ref RenderingData renderingData, 
            NativeArray<VisibleLight> visibleLights, int mainLightIndex, 
            Material material, int[] passes, MaterialPropertyBlock propertyBlock = null)
        {
            cmd.EnableShaderKeyword(ShaderKeywordStrings._SPOT);

            for (int offset = StencilVisLightOffsets[(int)LightType.Spot]; 
                 offset < StencilVisLights.Length; ++offset)
            {
                ushort visLightIndex = StencilVisLights[offset];
                VisibleLight vl = visibleLights[visLightIndex];
                if (vl.lightType != LightType.Spot)
                    break;
                
                // Cache light to local, avoid light find on every access.
                Light light = vl.light;

                float alpha = Mathf.Deg2Rad * vl.spotAngle * 0.5f;
                float cosAlpha = Mathf.Cos(alpha);
                float sinAlpha = Mathf.Sin(alpha);
                // Artificially inflate the geometric shape to fit the analytic spot shape.
                // The tighter the spot shape, the lesser inflation is needed.
                float guard = Mathf.Lerp(1.0f, StencilShapeGuard, sinAlpha);

                Vector4 lightPos, lightColor, lightAttenuation, lightSpotDir, lightOcclusionChannel;
                UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, visLightIndex, out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionChannel);

                var additionalLightData = vl.light.GetUniversalAdditionalLightData();
                uint lightLayerMask = additionalLightData.renderingLayers;

                int lightFlags = 0;
                if (vl.light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed)
                    lightFlags |= 4; // (int)LightFlag.SubtractiveMixedLighting;

                int shadowLightIndex = GetShadowLightIndexFromLightIndex(visLightIndex, mainLightIndex);
                bool hasDeferredLightShadows = light && light.shadows != LightShadows.None && shadowLightIndex >= 0;
                SetAdditionalLightsShadowsKeyword(cmd, ref renderingData, hasDeferredLightShadows);

                bool hasSoftShadow = hasDeferredLightShadows && renderingData.shadowData.supportsSoftShadows && light.shadows == LightShadows.Soft;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, hasSoftShadow);

                int cookieLightIndex = GetLightCookieShaderDataIndex(visLightIndex, visibleLights);
                // We could test this in shader (static if) a variant (shader change) is undesirable. Same for spot light.
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.LightCookies, cookieLightIndex >= 0);
                cmd.SetGlobalInt(ShaderConstants.CookieLightIndexId, cookieLightIndex);

                cmd.SetGlobalVector(ShaderConstants.SpotLightScaleId, new Vector4(sinAlpha, sinAlpha, 1.0f - cosAlpha, vl.range));
                cmd.SetGlobalVector(ShaderConstants.SpotLightBiasId, new Vector4(0.0f, 0.0f, cosAlpha, 0.0f));
                cmd.SetGlobalVector(ShaderConstants.SpotLightGuardId, new Vector4(guard, guard, guard, cosAlpha * vl.range));
                cmd.SetGlobalVector(ShaderConstants.LightPosWSId, lightPos);
                cmd.SetGlobalVector(ShaderConstants.LightColorId, lightColor);
                cmd.SetGlobalVector(ShaderConstants.LightAttenuationId, lightAttenuation);
                cmd.SetGlobalVector(ShaderConstants.LightDirectionId, new Vector3(lightSpotDir.x, lightSpotDir.y, lightSpotDir.z));
                cmd.SetGlobalVector(ShaderConstants.LightOcclusionProbInfoId, lightOcclusionChannel);
                cmd.SetGlobalInt(ShaderConstants.LightFlagsId, lightFlags);
                cmd.SetGlobalInt(ShaderConstants.ShadowLightIndexId, shadowLightIndex);
                cmd.SetGlobalInt(ShaderConstants.LightLayerMaskId, (int)lightLayerMask);
                cmd.SetGlobalInt(ShaderConstants.CookieLightIndexId, cookieLightIndex);

                if (_hemisphereMesh == null)
                {
                    _hemisphereMesh = CreateHemisphereMesh();
                }

                // Lighting pass.
                for (int i = 0; i < passes.Length; i++)
                {
                    cmd.DrawMesh(_hemisphereMesh, vl.localToWorldMatrix, material, 0, passes[i], propertyBlock);
                }
            }

            cmd.DisableShaderKeyword(ShaderKeywordStrings._SPOT);
        }
        
        public void RenderStencilSpotLights(CommandBuffer cmd, ref RenderingData renderingData, 
            NativeArray<VisibleLight> visibleLights, int mainLightIndex, LightShadows cullLightShadows, 
            Material material, int[] passes, MaterialPropertyBlock propertyBlock = null)
        {
            cmd.EnableShaderKeyword(ShaderKeywordStrings._SPOT);

            for (int offset = StencilVisLightOffsets[(int)LightType.Spot]; 
                 offset < StencilVisLights.Length; ++offset)
            {
                ushort visLightIndex = StencilVisLights[offset];
                VisibleLight vl = visibleLights[visLightIndex];
                if (vl.lightType != LightType.Spot)
                    break;
                
                // Cache light to local, avoid light find on every access.
                Light light = vl.light;
                
                if (light.shadows == cullLightShadows)
                    continue;

                float alpha = Mathf.Deg2Rad * vl.spotAngle * 0.5f;
                float cosAlpha = Mathf.Cos(alpha);
                float sinAlpha = Mathf.Sin(alpha);
                // Artificially inflate the geometric shape to fit the analytic spot shape.
                // The tighter the spot shape, the lesser inflation is needed.
                float guard = Mathf.Lerp(1.0f, StencilShapeGuard, sinAlpha);

                Vector4 lightPos, lightColor, lightAttenuation, lightSpotDir, lightOcclusionChannel;
                UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, visLightIndex, out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionChannel);

                var additionalLightData = vl.light.GetUniversalAdditionalLightData();
                uint lightLayerMask = additionalLightData.renderingLayers;

                int lightFlags = 0;
                if (vl.light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed)
                    lightFlags |= 4; // (int)LightFlag.SubtractiveMixedLighting;

                int shadowLightIndex = GetShadowLightIndexFromLightIndex(visLightIndex, mainLightIndex);
                bool hasDeferredLightShadows = light && light.shadows != LightShadows.None && shadowLightIndex >= 0;
                SetAdditionalLightsShadowsKeyword(cmd, ref renderingData, hasDeferredLightShadows);

                bool hasSoftShadow = hasDeferredLightShadows && renderingData.shadowData.supportsSoftShadows && light.shadows == LightShadows.Soft;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, hasSoftShadow);

                int cookieLightIndex = GetLightCookieShaderDataIndex(visLightIndex, visibleLights);
                // We could test this in shader (static if) a variant (shader change) is undesirable. Same for spot light.
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.LightCookies, cookieLightIndex >= 0);
                cmd.SetGlobalInt(ShaderConstants.CookieLightIndexId, cookieLightIndex);

                cmd.SetGlobalVector(ShaderConstants.SpotLightScaleId, new Vector4(sinAlpha, sinAlpha, 1.0f - cosAlpha, vl.range));
                cmd.SetGlobalVector(ShaderConstants.SpotLightBiasId, new Vector4(0.0f, 0.0f, cosAlpha, 0.0f));
                cmd.SetGlobalVector(ShaderConstants.SpotLightGuardId, new Vector4(guard, guard, guard, cosAlpha * vl.range));
                cmd.SetGlobalVector(ShaderConstants.LightPosWSId, lightPos);
                cmd.SetGlobalVector(ShaderConstants.LightColorId, lightColor);
                cmd.SetGlobalVector(ShaderConstants.LightAttenuationId, lightAttenuation);
                cmd.SetGlobalVector(ShaderConstants.LightDirectionId, new Vector3(lightSpotDir.x, lightSpotDir.y, lightSpotDir.z));
                cmd.SetGlobalVector(ShaderConstants.LightOcclusionProbInfoId, lightOcclusionChannel);
                cmd.SetGlobalInt(ShaderConstants.LightFlagsId, lightFlags);
                cmd.SetGlobalInt(ShaderConstants.ShadowLightIndexId, shadowLightIndex);
                cmd.SetGlobalInt(ShaderConstants.LightLayerMaskId, (int)lightLayerMask);
                cmd.SetGlobalInt(ShaderConstants.CookieLightIndexId, cookieLightIndex);

                if (_hemisphereMesh == null)
                {
                    _hemisphereMesh = CreateHemisphereMesh();
                }

                // Lighting pass.
                for (int i = 0; i < passes.Length; i++)
                {
                    cmd.DrawMesh(_hemisphereMesh, vl.localToWorldMatrix, material, 0, passes[i], propertyBlock);
                }
            }

            cmd.DisableShaderKeyword(ShaderKeywordStrings._SPOT);
        }

        private void PrecomputeLights(
            out NativeArray<ushort> stencilVisLights,
            out NativeArray<ushort> stencilVisLightOffsets,
            ref NativeArray<VisibleLight> visibleLights,
            bool hasAdditionalLights,
            Matrix4x4 view,
            bool isOrthographic,
            float zNear)
        {
            const int lightTypeCount = (int)LightType.Disc + 1;

            if (!hasAdditionalLights)
            {
                stencilVisLights = new NativeArray<ushort>(0, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                stencilVisLightOffsets = new NativeArray<ushort>(lightTypeCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < lightTypeCount; ++i)
                    stencilVisLightOffsets[i] = 0xFFFF;
                return;
            }
            
            NativeArray<int> stencilLightCounts = new NativeArray<int>(lightTypeCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
            stencilVisLightOffsets = new NativeArray<ushort>(lightTypeCount, Allocator.Temp, NativeArrayOptions.ClearMemory);

            // Count the number of lights per type.
            for (ushort visLightIndex = 0; visLightIndex < visibleLights.Length; ++visLightIndex)
            {
                VisibleLight vl = visibleLights[visLightIndex];

                // All remaining lights are processed as stencil volumes.
                ++stencilVisLightOffsets[(int)vl.lightType];
            }
            
            int totalStencilLightCount = stencilVisLightOffsets[(int)LightType.Spot] + stencilVisLightOffsets[(int)LightType.Directional] + stencilVisLightOffsets[(int)LightType.Point];
            stencilVisLights = new NativeArray<ushort>(totalStencilLightCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // Calculate correct offsets now.
            for (int i = 0, soffset = 0; i < stencilVisLightOffsets.Length; ++i)
            {
                if (stencilVisLightOffsets[i] == 0)
                    stencilVisLightOffsets[i] = 0xFFFF;
                else
                {
                    int c = stencilVisLightOffsets[i];
                    stencilVisLightOffsets[i] = (ushort)soffset;
                    soffset += c;
                }
            }
            
            // Precompute punctual light data.
            for (ushort visLightIndex = 0; visLightIndex < visibleLights.Length; ++visLightIndex)
            {
                VisibleLight vl = visibleLights[visLightIndex];
                
                // All remaining lights are processed as stencil volumes.
                int i = stencilLightCounts[(int)vl.lightType]++;
                stencilVisLights[stencilVisLightOffsets[(int)vl.lightType] + i] = visLightIndex;
            }
            stencilLightCounts.Dispose();
        }
        
        void SetupShaderLightConstants(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Main light has an optimized shader path for main light. This will benefit games that only care about a single light.
            // Universal Forward pipeline only supports a single shadow light, if available it will be the main light.
            SetupMainLightConstants(cmd, ref renderingData.lightData);
        }
        
        // adapted from ForwardLights.SetupShaderLightConstants
        void SetupMainLightConstants(CommandBuffer cmd, ref LightData lightData)
        {
            if (lightData.mainLightIndex < 0)
                return;

            Vector4 lightPos, lightColor, lightAttenuation, lightSpotDir, lightOcclusionChannel;
            UniversalRenderPipeline.InitializeLightConstants_Common(lightData.visibleLights, lightData.mainLightIndex, out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionChannel);

            var additionalLightData = lightData.visibleLights[lightData.mainLightIndex].light.GetUniversalAdditionalLightData();
            uint lightLayerMask = additionalLightData.renderingLayers;

            cmd.SetGlobalVector(ShaderConstants.MainLightPositionId, lightPos);
            cmd.SetGlobalVector(ShaderConstants.MainLightColorId, lightColor);
            cmd.SetGlobalInt(ShaderConstants.MainLightLayerMaskId, (int)lightLayerMask);
        }
        
        private int GetShadowLightIndexFromLightIndex(int visLightIndex, int mainLightIndex)
        {
            if (mainLightIndex == -1)
            {
                return visLightIndex;
            }
            if (visLightIndex == mainLightIndex)
            {
                return -1;
            }
            if (visLightIndex < mainLightIndex)
            {
                return visLightIndex;
            }
            return visLightIndex - 1;
        }
        
        private int GetLightCookieShaderDataIndex(int visLightIndex, NativeArray<VisibleLight> visibleLights)
        {
            int index = 0;
            for (int i = 0; i < visLightIndex; ++i)
            {
                if (visibleLights[i].light.cookie)
                {
                    ++index;
                }
            }

            if (visibleLights[visLightIndex].light.cookie)
            {
                return index;
            }

            return -1;
        }
        
        private void SetMainLightsShadowsKeyword(CommandBuffer cmd, ref RenderingData renderingData, bool hasDeferredShadows)
        {
            bool mainLightShadowsEnabledInAsset = renderingData.shadowData.supportsMainLightShadows;

            // AdditionalLightShadows Keyword is enabled when:
            // Shadows are enabled in Asset
            bool shouldEnable = mainLightShadowsEnabledInAsset && hasDeferredShadows;
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, shouldEnable);
        }
        
        private void SetMainLightsShadowsKeyword(Material material, ref RenderingData renderingData, bool hasDeferredShadows)
        {
            bool mainLightShadowsEnabledInAsset = renderingData.shadowData.supportsMainLightShadows;

            // AdditionalLightShadows Keyword is enabled when:
            // Shadows are enabled in Asset
            bool shouldEnable = mainLightShadowsEnabledInAsset && hasDeferredShadows;
            CoreUtils.SetKeyword(material, ShaderKeywordStrings.MainLightShadows, shouldEnable);
        }
        
        private void SetAdditionalLightsShadowsKeyword(CommandBuffer cmd, ref RenderingData renderingData, bool hasDeferredShadows)
        {
            bool additionalLightShadowsEnabledInAsset = renderingData.shadowData.supportsAdditionalLightShadows;

            // AdditionalLightShadows Keyword is enabled when:
            // Shadows are enabled in Asset
            bool shouldEnable = additionalLightShadowsEnabledInAsset && hasDeferredShadows;
            CoreUtils.SetKeyword(cmd, "_CRP_ADDITIONAL_LIGHT_SHADOWS", shouldEnable);
        }
        
        private void SetAdditionalLightsShadowsKeyword(Material material, ref RenderingData renderingData, bool hasDeferredShadows)
        {
            bool additionalLightShadowsEnabledInAsset = renderingData.shadowData.supportsAdditionalLightShadows;

            // AdditionalLightShadows Keyword is enabled when:
            // Shadows are enabled in Asset
            bool shouldEnable = additionalLightShadowsEnabledInAsset && hasDeferredShadows;
            CoreUtils.SetKeyword(material, "_CRP_ADDITIONAL_LIGHT_SHADOWS", shouldEnable);
        }

        private static Mesh CreateFullscreenMesh()
        {
            // TODO reorder for pre&post-transform cache optimisation.
            // Simple full-screen triangle.
            Vector3[] positions =
            {
                new Vector3(-1.0f,  1.0f, 0.0f),
                new Vector3(-1.0f, -3.0f, 0.0f),
                new Vector3(3.0f,  1.0f, 0.0f)
            };

            int[] indices = { 0, 1, 2 };

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.triangles = indices;

            return mesh;
        }
        
        private static Mesh CreateSphereMesh()
        {
            // This icosaedron has been been slightly inflated to fit an unit sphere.
            // This is the same geometry as built-in deferred.

            Vector3[] positions =
            {
                new Vector3(0.000f,  0.000f, -1.070f), new Vector3(0.174f, -0.535f, -0.910f),
                new Vector3(-0.455f, -0.331f, -0.910f), new Vector3(0.562f,  0.000f, -0.910f),
                new Vector3(-0.455f,  0.331f, -0.910f), new Vector3(0.174f,  0.535f, -0.910f),
                new Vector3(-0.281f, -0.865f, -0.562f), new Vector3(0.736f, -0.535f, -0.562f),
                new Vector3(0.296f, -0.910f, -0.468f), new Vector3(-0.910f,  0.000f, -0.562f),
                new Vector3(-0.774f, -0.562f, -0.478f), new Vector3(0.000f, -1.070f,  0.000f),
                new Vector3(-0.629f, -0.865f,  0.000f), new Vector3(0.629f, -0.865f,  0.000f),
                new Vector3(-1.017f, -0.331f,  0.000f), new Vector3(0.957f,  0.000f, -0.478f),
                new Vector3(0.736f,  0.535f, -0.562f), new Vector3(1.017f, -0.331f,  0.000f),
                new Vector3(1.017f,  0.331f,  0.000f), new Vector3(-0.296f, -0.910f,  0.478f),
                new Vector3(0.281f, -0.865f,  0.562f), new Vector3(0.774f, -0.562f,  0.478f),
                new Vector3(-0.736f, -0.535f,  0.562f), new Vector3(0.910f,  0.000f,  0.562f),
                new Vector3(0.455f, -0.331f,  0.910f), new Vector3(-0.174f, -0.535f,  0.910f),
                new Vector3(0.629f,  0.865f,  0.000f), new Vector3(0.774f,  0.562f,  0.478f),
                new Vector3(0.455f,  0.331f,  0.910f), new Vector3(0.000f,  0.000f,  1.070f),
                new Vector3(-0.562f,  0.000f,  0.910f), new Vector3(-0.957f,  0.000f,  0.478f),
                new Vector3(0.281f,  0.865f,  0.562f), new Vector3(-0.174f,  0.535f,  0.910f),
                new Vector3(0.296f,  0.910f, -0.478f), new Vector3(-1.017f,  0.331f,  0.000f),
                new Vector3(-0.736f,  0.535f,  0.562f), new Vector3(-0.296f,  0.910f,  0.478f),
                new Vector3(0.000f,  1.070f,  0.000f), new Vector3(-0.281f,  0.865f, -0.562f),
                new Vector3(-0.774f,  0.562f, -0.478f), new Vector3(-0.629f,  0.865f,  0.000f),
            };

            int[] indices =
            {
                0,  1,  2,  0,  3,  1,  2,  4,  0,  0,  5,  3,  0,  4,  5,  1,  6,  2,
                3,  7,  1,  1,  8,  6,  1,  7,  8,  9,  4,  2,  2,  6, 10, 10,  9,  2,
                8, 11,  6,  6, 12, 10, 11, 12,  6,  7, 13,  8,  8, 13, 11, 10, 14,  9,
                10, 12, 14,  3, 15,  7,  5, 16,  3,  3, 16, 15, 15, 17,  7, 17, 13,  7,
                16, 18, 15, 15, 18, 17, 11, 19, 12, 13, 20, 11, 11, 20, 19, 17, 21, 13,
                13, 21, 20, 12, 19, 22, 12, 22, 14, 17, 23, 21, 18, 23, 17, 21, 24, 20,
                23, 24, 21, 20, 25, 19, 19, 25, 22, 24, 25, 20, 26, 18, 16, 18, 27, 23,
                26, 27, 18, 28, 24, 23, 27, 28, 23, 24, 29, 25, 28, 29, 24, 25, 30, 22,
                25, 29, 30, 14, 22, 31, 22, 30, 31, 32, 28, 27, 26, 32, 27, 33, 29, 28,
                30, 29, 33, 33, 28, 32, 34, 26, 16,  5, 34, 16, 14, 31, 35, 14, 35,  9,
                31, 30, 36, 30, 33, 36, 35, 31, 36, 37, 33, 32, 36, 33, 37, 38, 32, 26,
                34, 38, 26, 38, 37, 32,  5, 39, 34, 39, 38, 34,  4, 39,  5,  9, 40,  4,
                9, 35, 40,  4, 40, 39, 35, 36, 41, 41, 36, 37, 41, 37, 38, 40, 35, 41,
                40, 41, 39, 41, 38, 39,
            };


            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.triangles = indices;

            return mesh;
        }
        
        private static Mesh CreateHemisphereMesh()
        {
            // TODO reorder for pre&post-transform cache optimisation.
            // This capped hemisphere shape is in unit dimensions. It will be slightly inflated in the vertex shader
            // to fit the cone analytical shape.
            Vector3[] positions =
            {
                new Vector3(0.000000f, 0.000000f, 0.000000f), new Vector3(1.000000f, 0.000000f, 0.000000f),
                new Vector3(0.923880f, 0.382683f, 0.000000f), new Vector3(0.707107f, 0.707107f, 0.000000f),
                new Vector3(0.382683f, 0.923880f, 0.000000f), new Vector3(-0.000000f, 1.000000f, 0.000000f),
                new Vector3(-0.382684f, 0.923880f, 0.000000f), new Vector3(-0.707107f, 0.707107f, 0.000000f),
                new Vector3(-0.923880f, 0.382683f, 0.000000f), new Vector3(-1.000000f, -0.000000f, 0.000000f),
                new Vector3(-0.923880f, -0.382683f, 0.000000f), new Vector3(-0.707107f, -0.707107f, 0.000000f),
                new Vector3(-0.382683f, -0.923880f, 0.000000f), new Vector3(0.000000f, -1.000000f, 0.000000f),
                new Vector3(0.382684f, -0.923879f, 0.000000f), new Vector3(0.707107f, -0.707107f, 0.000000f),
                new Vector3(0.923880f, -0.382683f, 0.000000f), new Vector3(0.000000f, 0.000000f, 1.000000f),
                new Vector3(0.707107f, 0.000000f, 0.707107f), new Vector3(0.000000f, -0.707107f, 0.707107f),
                new Vector3(0.000000f, 0.707107f, 0.707107f), new Vector3(-0.707107f, 0.000000f, 0.707107f),
                new Vector3(0.816497f, -0.408248f, 0.408248f), new Vector3(0.408248f, -0.408248f, 0.816497f),
                new Vector3(0.408248f, -0.816497f, 0.408248f), new Vector3(0.408248f, 0.816497f, 0.408248f),
                new Vector3(0.408248f, 0.408248f, 0.816497f), new Vector3(0.816497f, 0.408248f, 0.408248f),
                new Vector3(-0.816497f, 0.408248f, 0.408248f), new Vector3(-0.408248f, 0.408248f, 0.816497f),
                new Vector3(-0.408248f, 0.816497f, 0.408248f), new Vector3(-0.408248f, -0.816497f, 0.408248f),
                new Vector3(-0.408248f, -0.408248f, 0.816497f), new Vector3(-0.816497f, -0.408248f, 0.408248f),
                new Vector3(0.000000f, -0.923880f, 0.382683f), new Vector3(0.923880f, 0.000000f, 0.382683f),
                new Vector3(0.000000f, -0.382683f, 0.923880f), new Vector3(0.382683f, 0.000000f, 0.923880f),
                new Vector3(0.000000f, 0.923880f, 0.382683f), new Vector3(0.000000f, 0.382683f, 0.923880f),
                new Vector3(-0.923880f, 0.000000f, 0.382683f), new Vector3(-0.382683f, 0.000000f, 0.923880f)
            };

            int[] indices =
            {
                0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 6, 5, 0,
                7, 6, 0, 8, 7, 0, 9, 8, 0, 10, 9, 0, 11, 10, 0, 12,
                11, 0, 13, 12, 0, 14, 13, 0, 15, 14, 0, 16, 15, 0, 1, 16,
                22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 14, 24, 34, 35,
                22, 16, 36, 23, 37, 2, 27, 35, 38, 25, 4, 37, 26, 39, 6, 30,
                38, 40, 28, 8, 39, 29, 41, 10, 33, 40, 34, 31, 12, 41, 32, 36,
                15, 22, 24, 18, 23, 22, 19, 24, 23, 3, 25, 27, 20, 26, 25, 18,
                27, 26, 7, 28, 30, 21, 29, 28, 20, 30, 29, 11, 31, 33, 19, 32,
                31, 21, 33, 32, 13, 14, 34, 15, 24, 14, 19, 34, 24, 1, 35, 16,
                18, 22, 35, 15, 16, 22, 17, 36, 37, 19, 23, 36, 18, 37, 23, 1,
                2, 35, 3, 27, 2, 18, 35, 27, 5, 38, 4, 20, 25, 38, 3, 4,
                25, 17, 37, 39, 18, 26, 37, 20, 39, 26, 5, 6, 38, 7, 30, 6,
                20, 38, 30, 9, 40, 8, 21, 28, 40, 7, 8, 28, 17, 39, 41, 20,
                29, 39, 21, 41, 29, 9, 10, 40, 11, 33, 10, 21, 40, 33, 13, 34,
                12, 19, 31, 34, 11, 12, 31, 17, 41, 36, 21, 32, 41, 19, 36, 32
            };

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.triangles = indices;

            return mesh;
        }
    }
}