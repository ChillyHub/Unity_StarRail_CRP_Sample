﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class CRPDeferredRenderingRendererFeature : ScriptableRendererFeature
    {
        // Character Entities
        private CharacterEntityManager _characterEntityManager;
        private CharacterCreateShadowCasterDrawCallSystem _characterCreateShadowCasterDrawCallSystem;
        private CharacterCreateScreenShadowDrawCallSystem _characterCreateScreenShadowDrawCallSystem;
        private CharacterShadowCasterDrawSystem _characterShadowCasterDrawSystem;
        private CharacterScreenShadowDrawSystem _characterScreenShadowDrawSystem;
        private bool _recreateSystem;
        
        // Light
        private DeferredLight _deferredLight;
        
        // Camera
        private List<TAACameraData> _taaCameraData;
        
        // Render Texture
        private List<GBufferTextures> _gBufferTextures;
        private List<DepthTextures> _depthTextures;
        private List<ColorTextures> _colorTextures;

        // Pass
        private CharacterShadowPass _characterShadowPass;
        private CRPGBufferPass _crpGBufferPass;
        private CRPDepthPyramidPass _crpDepthPyramidPass;
        private ScreenSpaceShadowsPass _screenSpaceShadowsPass;
        private CRPStencilLightingPass _crpStencilLightingPass;
        private CRPTransparentPass _crpTransparentPass;
        private MotionVectorPass _motionVectorPass;
        private ScreenSpaceReflectionPass _screenSpaceReflectionPass;
        private CRPColorPyramidPass _crpColorPyramidPass;
        private TemporalAAPass _temporalAAPass;
        private PostProcessingPass _postProcessingPass;

        private bool _haveCharacterShadowPass;

        private Dictionary<Camera, int> _cameraIndices;

        public override void Create()
        {
            base.name = "CRP Deferred Rendering Pipeline";

            _recreateSystem = true;
            
            _deferredLight = new DeferredLight();
            
            _taaCameraData = new List<TAACameraData>();
            
            _gBufferTextures = new List<GBufferTextures>();
            _depthTextures = new List<DepthTextures>();
            _colorTextures = new List<ColorTextures>();

            _characterShadowPass = new CharacterShadowPass();
            _crpGBufferPass = new CRPGBufferPass();
            _crpDepthPyramidPass = new CRPDepthPyramidPass();
            _screenSpaceShadowsPass = new ScreenSpaceShadowsPass();
            _crpStencilLightingPass = new CRPStencilLightingPass();
            _crpTransparentPass = new CRPTransparentPass();
            _motionVectorPass = new MotionVectorPass();
            _screenSpaceReflectionPass = new ScreenSpaceReflectionPass();
            _crpColorPyramidPass = new CRPColorPyramidPass();
            _temporalAAPass = new TemporalAAPass();
            _postProcessingPass = new PostProcessingPass();
                
            _cameraIndices = new Dictionary<Camera, int>();
        }
        
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            int cameraIndex = _cameraIndices[renderingData.cameraData.camera];
            
            _haveCharacterShadowPass = _characterShadowPass.Setup(
                _characterEntityManager, _characterShadowCasterDrawSystem);
            
            _crpGBufferPass.Setup(_gBufferTextures[cameraIndex], _depthTextures[cameraIndex]);
            _crpDepthPyramidPass.Setup(_depthTextures[cameraIndex]);
            _screenSpaceShadowsPass.Setup(_deferredLight, _characterEntityManager, _characterScreenShadowDrawSystem);
            _crpStencilLightingPass.Setup(_deferredLight, _gBufferTextures[cameraIndex]);
            _crpTransparentPass.Setup(_gBufferTextures[cameraIndex]);
            
            _motionVectorPass.Setup(_taaCameraData[cameraIndex]);
            
#if !UNITY_ANDROID
            _screenSpaceReflectionPass.Setup(_gBufferTextures[cameraIndex], _depthTextures[cameraIndex], 
                _colorTextures[cameraIndex]);
            _crpColorPyramidPass.Setup(_colorTextures[cameraIndex]);
#endif

            _temporalAAPass.Setup();
            _postProcessingPass.Setup(_gBufferTextures[cameraIndex]);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (_cameraIndices.TryAdd(camera, _taaCameraData.Count))
            {
                _taaCameraData.Add(new TAACameraData());
                _gBufferTextures.Add(new GBufferTextures());
                _depthTextures.Add(new DepthTextures());
                _colorTextures.Add(new ColorTextures());
            }

            int cameraIndex = _cameraIndices[camera];

            var taa = VolumeManager.instance.stack.GetComponent<TemporalAA>();

            _taaCameraData[cameraIndex].Update(ref renderingData.cameraData, taa.IsActive());
            
            if (_haveCharacterShadowPass)
            {
                renderer.EnqueuePass(_characterShadowPass);
            }
            
            renderer.EnqueuePass(_crpGBufferPass);
            renderer.EnqueuePass(_crpDepthPyramidPass);
            renderer.EnqueuePass(_screenSpaceShadowsPass);
            renderer.EnqueuePass(_crpStencilLightingPass);
            renderer.EnqueuePass(_crpTransparentPass);

            if (taa.IsActive())
            {
                renderer.EnqueuePass(_motionVectorPass);
            }

#if !UNITY_ANDROID
            if (camera.cameraType != CameraType.Preview)
            {
                renderer.EnqueuePass(_screenSpaceReflectionPass);
                renderer.EnqueuePass(_crpColorPyramidPass);
            }
#endif

            if (taa.IsActive())
            {
                renderer.EnqueuePass(_temporalAAPass);
            }

            if (renderingData.cameraData.postProcessEnabled)
            {
                renderer.EnqueuePass(_postProcessingPass);
            }
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            if (cameraData.cameraType == CameraType.Preview)
            {
                return;
            }

            bool isValid = RecreateSystemIfNeeded(renderer);
            if (!isValid)
            {
                return;
            }
            
            _characterEntityManager.UpdateEntityManager();
            _characterEntityManager.UpdateAllCharacterCachedData();
            _characterCreateShadowCasterDrawCallSystem.Execute();
            _characterCreateScreenShadowDrawCallSystem.Execute();
        }

        protected override void Dispose(bool disposing)
        {
            for (int i = 0; i < _gBufferTextures.Count; i++)
            {
                _gBufferTextures[i]?.Release();
                _depthTextures[i]?.Release();
                _colorTextures[i]?.Release();
                _gBufferTextures[i] = null;
                _depthTextures[i] = null;
                _colorTextures[i] = null;
                _taaCameraData[i] = null;
            }
            
            _cameraIndices.Clear();
            _cameraIndices = null;

            _characterShadowPass.Dispose();
            _crpGBufferPass.Dispose();
            _crpDepthPyramidPass.Dispose();
            _screenSpaceShadowsPass.Dispose();
            _crpStencilLightingPass.Dispose();
            _crpTransparentPass.Dispose();
            _motionVectorPass.Dispose();
            _screenSpaceReflectionPass.Dispose();
            _crpColorPyramidPass.Dispose();
            _temporalAAPass.Dispose();
            _postProcessingPass.Dispose();

            if (_characterEntityManager != null)
            {
                _characterEntityManager = null;
                CharacterEntityManagerFactory.instance.Release(_characterEntityManager);
            }
        }

        private bool RecreateSystemIfNeeded(ScriptableRenderer renderer)
        {
            if (!_recreateSystem)
            {
                return true;
            }

            if (_characterEntityManager == null)
            {
                _characterEntityManager = CharacterEntityManagerFactory.instance.Get();
            }
            
            _characterCreateShadowCasterDrawCallSystem = new CharacterCreateShadowCasterDrawCallSystem(_characterEntityManager);
            _characterCreateScreenShadowDrawCallSystem = new CharacterCreateScreenShadowDrawCallSystem(_characterEntityManager);
            
            _characterShadowCasterDrawSystem = new CharacterShadowCasterDrawSystem(_characterEntityManager);
            _characterScreenShadowDrawSystem = new CharacterScreenShadowDrawSystem(_characterEntityManager);

            _recreateSystem = false;

            return true;
        }
    }
}