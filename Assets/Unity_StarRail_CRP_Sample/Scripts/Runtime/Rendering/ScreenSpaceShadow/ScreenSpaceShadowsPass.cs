using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class ScreenSpaceShadowsPass : ScriptableRenderPass
    {
        enum MaterialPass
        {
            SceneCascadeShadow = 0,
            AdditionalStencilVolume = 1,
            AdditionalShadow = 2,
            CharacterStencilVolume = 3,
            CharacterShadow = 4
        }
        
        private static class ScreenSpaceShadowConstant
        {
            public static readonly int IndexId = Shader.PropertyToID("_Index");
            
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
        
        // Profiling samplers
        private readonly ProfilingSampler _screenSpaceShadowProfilingSampler;
        
        // Material
        private Material _material;
        private MaterialPropertyBlock[] _materialPropertyBlock = new MaterialPropertyBlock[16];
        
        // Light
        private DeferredLight _deferredLight;
        
        // Data
        private Mesh _sphereMesh;
        private Mesh _hemisphereMesh;

        public ScreenSpaceShadowsPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(ScreenSpaceShadowsPass));
            this.renderPassEvent = RenderPassEvent.BeforeRenderingGbuffer;
            
            _screenSpaceShadowProfilingSampler = new ProfilingSampler("CRP Screen Space Shadow");
            
            const string shaderName = "Hidden/StarRail_CRP/Shadow/ScreenSpaceShadows";
            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogWarning($"Can't find shader: {shaderName}");
            }
            else
            {
                _material = CoreUtils.CreateEngineMaterial(shader);
            }
        }

        public void Setup(DeferredLight deferredLight)
        {
            _deferredLight = deferredLight;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.graphicsFormat = RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R8_UNorm, FormatUsage.Linear | FormatUsage.Render)
                ? GraphicsFormat.R8_UNorm
                : GraphicsFormat.B8G8R8A8_UNorm;
            
            RenderingUtils.ReAllocateIfNeeded(ref ShadowTexturesManager.Textures.ScreenSpaceShadowTexture, desc, 
                FilterMode.Point, TextureWrapMode.Clamp, name: ShadowTextures.ScreenSpaceShadowTextureName);

            cmd.SetGlobalTexture(ShadowTexturesManager.Textures.ScreenSpaceShadowTexture.name, 
                ShadowTexturesManager.Textures.ScreenSpaceShadowTexture.nameID);
            
            ConfigureClear(ClearFlag.None, Color.white);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            _deferredLight.ResolveMixedLightingMode(ref renderingData);
            _deferredLight.SetupLights(context, ref renderingData);
            
            if (!CheckExecute(ref renderingData))
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _screenSpaceShadowProfilingSampler))
            {
                // Render Main Directional Light Shadow To Screen Space Shadow Texture
                bool mainLight = RenderMainLightShadow(cmd, ref renderingData);

                cmd.SetRenderTarget(ShadowTexturesManager.Textures.ScreenSpaceShadowTexture.nameID, 
                    renderingData.cameraData.renderer.cameraDepthTargetHandle.nameID);
                
                if (!mainLight)
                {
                    cmd.ClearRenderTarget(false, true, Color.white);
                }
                
                // Render Additional Lights Shadow To Screen Space Shadow Texture
                RenderAdditionalLightsShadow(cmd, ref renderingData);

                // Render Character Shadow To Screen Space Shadow Texture
                RenderCharacterShadow(cmd, ref renderingData);
                
                cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle.nameID, 
                    renderingData.cameraData.renderer.cameraDepthTargetHandle.nameID);
                
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowScreen, true);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public void Dispose()
        {
            CoreUtils.Destroy(_material);
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
        
        private bool RenderMainLightShadow(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (renderingData.lightData.mainLightIndex == -1)
            {
                return false;
            }
            
            Blitter.BlitCameraTexture(cmd, ShadowTexturesManager.Textures.ScreenSpaceShadowTexture, 
                ShadowTexturesManager.Textures.ScreenSpaceShadowTexture, _material, (int)MaterialPass.SceneCascadeShadow);

            return true;
        }

        private void RenderAdditionalLightsShadow(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (_deferredLight.HasStencilLightsOfType(LightType.Point))
            {
                _deferredLight.RenderStencilPointLights(cmd, ref renderingData, 
                    renderingData.lightData.visibleLights, renderingData.lightData.mainLightIndex, LightShadows.None, 
                    _material,
                    new []
                    {
                        (int)MaterialPass.AdditionalStencilVolume,
                        (int)MaterialPass.AdditionalShadow
                    });
            }

            if (_deferredLight.HasStencilLightsOfType(LightType.Spot))
            {
                _deferredLight.RenderStencilSpotLights(cmd, ref renderingData, 
                    renderingData.lightData.visibleLights, renderingData.lightData.mainLightIndex, LightShadows.None, 
                    _material,
                    new []
                    {
                        (int)MaterialPass.AdditionalStencilVolume,
                        (int)MaterialPass.AdditionalShadow
                    });
            }
        }
        
        private void RenderCharacterShadow(CommandBuffer cmd, ref RenderingData renderingData)
        {
            int index = 0;
            var infos = CharacterManager.instance.CharacterInfos;
            foreach (var (key, info) in infos)
            {
                Vector3 center = info.Position + info.AABB.center;
                float radius = info.AABB.extents.magnitude;
                    
                Mesh boxMesh = CreateBoxMesh(radius);

                Matrix4x4 mat = Matrix4x4.LookAt(Vector3.zero, info.ShadowLightDirection, Vector3.up);
                mat.m03 = center.x;
                mat.m13 = center.y;
                mat.m23 = center.z;

                _materialPropertyBlock[index] = new MaterialPropertyBlock();
                _materialPropertyBlock[index].SetFloat(ScreenSpaceShadowConstant.IndexId, index);
                    
                // Stencil pass
                cmd.DrawMesh(boxMesh, mat, _material, 0, (int)MaterialPass.CharacterStencilVolume, _materialPropertyBlock[index]);
                    
                // Character Shadow pass
                cmd.DrawMesh(boxMesh, mat, _material, 0, (int)MaterialPass.CharacterShadow, _materialPropertyBlock[index]);

                index++;
            }
            
            // Debug.Log(index);
        }

        private static Mesh CreateBoxMesh(float radius)
        {
            Vector3[] positions =
            {
                new Vector3(- radius, - radius, - radius * 10.0f),
                new Vector3(- radius, - radius, + radius * 10.0f),
                new Vector3(- radius, + radius, - radius * 10.0f),
                new Vector3(- radius, + radius, + radius * 10.0f),
                new Vector3(+ radius, - radius, - radius * 10.0f),
                new Vector3(+ radius, - radius, + radius * 10.0f),
                new Vector3(+ radius, + radius, - radius * 10.0f),
                new Vector3(+ radius, + radius, + radius * 10.0f)
            };

            int[] indices =
            {
                0, 1, 2, 2, 1, 3,
                4, 6, 5, 5, 6, 7,
                0, 4, 1, 1, 4, 5,
                2, 3, 6, 6, 3, 7,
                0, 2, 4, 4, 2, 6,
                1, 5, 3, 3, 5, 7
            };

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.triangles = indices;

            return mesh;
        }
    }
}