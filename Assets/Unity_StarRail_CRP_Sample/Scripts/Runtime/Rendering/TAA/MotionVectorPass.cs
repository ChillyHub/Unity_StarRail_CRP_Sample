using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class MotionVectorPass : ScriptableRenderPass
    {
        public static class TextureName
        {
            public static readonly string MotionVectorTexture = "_MotionVectorTexture";
        }
        
        public static class ShaderIds
        {
            public static readonly int MotionVectorTexture = Shader.PropertyToID("_MotionVectorTexture");

            public static readonly int PrevViewProjMatrix = Shader.PropertyToID("_PrevViewProjMatrix");
            public static readonly int NonJitteredViewProjMatrix = Shader.PropertyToID("_NonJitteredViewProjMatrix");
        }
        
        private readonly ProfilingSampler _motionVectorSampler;
        
        // Material
        private Material _cameraMotionMaterial;
        
        // Drawing Settings
        private FilteringSettings _filteringSettings;
        private readonly List<ShaderTagId> _objectShaderTagIds;

        // Camera Data
        private TAACameraData _taaCameraData;

        // Render Texture
        private RTHandle _motionVectorTexture;
        
        public MotionVectorPass()
        {
            this.profilingSampler = new ProfilingSampler(nameof(MotionVectorPass));
            this.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            
            _motionVectorSampler = new ProfilingSampler("CRP Motion Vector");
            
            Shader cameraMotionShader = Shader.Find("Hidden/StarRail_CRP/TAA/MotionVector");
            if (cameraMotionShader == null)
            {
                Debug.LogError("Can't find Shader: Hidden/StarRail_CRP/TAA/MotionVector");
            }
            else
            {
                _cameraMotionMaterial = CoreUtils.CreateEngineMaterial(cameraMotionShader);
            }
            
            _filteringSettings = new FilteringSettings(RenderQueueRange.all);

            _objectShaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("ObjectMotionVector"),
                new ShaderTagId("ObjectOutlineMotionVector")
            };
        }

        public void Setup(TAACameraData taaCameraData)
        {
            _taaCameraData = taaCameraData;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor desc = cameraTextureDescriptor;
            desc.depthBufferBits = 0;
            desc.graphicsFormat = GraphicsFormat.R16G16_SFloat;
            
            RenderingUtils.ReAllocateIfNeeded(ref _motionVectorTexture, desc, 
                FilterMode.Point, TextureWrapMode.Clamp,
                name: TextureName.MotionVectorTexture);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!CheckExecute(ref renderingData))
            {
                return;
            }
            
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _motionVectorSampler))
            {
                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera;
                camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                cmd.SetGlobalMatrix(ShaderIds.PrevViewProjMatrix, _taaCameraData.previousViewGpuProjectionNoJitter);
                cmd.SetGlobalMatrix(ShaderIds.NonJitteredViewProjMatrix, _taaCameraData.viewGpuProjectionNoJitter);
                
                cmd.SetRenderTarget(_motionVectorTexture.nameID, 
                    cameraData.renderer.cameraDepthTargetHandle.nameID);
                
                // Draw Camera Motion Vector
                cmd.DrawProcedural(Matrix4x4.identity, _cameraMotionMaterial, 0, MeshTopology.Triangles, 3, 1);
                
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                // Draw Object Motion Vector
                var drawingSettings = CreateDrawingSettings(_objectShaderTagIds, ref renderingData, cameraData.defaultOpaqueSortFlags);
                drawingSettings.perObjectData = PerObjectData.MotionVectors;

                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings);
                
                // Set Motion Vector Texture
                cmd.SetGlobalTexture(ShaderIds.MotionVectorTexture, _motionVectorTexture.nameID);
                
                cmd.SetRenderTarget(cameraData.renderer.cameraColorTargetHandle.nameID, 
                    cameraData.renderer.cameraDepthTargetHandle.nameID);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_cameraMotionMaterial);
            _cameraMotionMaterial = null;
            
            RTHandles.Release(_motionVectorTexture);
            _motionVectorTexture = null;
        }
        
        private bool CheckExecute(ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            if (camera.cameraType == CameraType.Preview)
            {
                return false;
            }

            if (_cameraMotionMaterial == null)
            {
                return false;
            }

            return true;
        }
    }
}