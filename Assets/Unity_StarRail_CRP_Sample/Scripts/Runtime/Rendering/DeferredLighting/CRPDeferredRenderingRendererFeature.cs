using UnityEngine;
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
        
        // Render Texture
        //private GBufferTextures _gBufferTextures;
        private DepthTextures _depthTextures;
        
        // Pass
        private CharacterShadowPass _characterShadowPass;
        private CRPGBufferPass _crpGBufferPass;
        private CRPDepthPyramidPass _crpDepthPyramidPass;
        private ScreenSpaceShadowsPass _screenSpaceShadowsPass;
        private CRPStencilLightingPass _crpStencilLightingPass;
        private CRPTransparentPass _crpTransparentPass;
        private PostProcessingPass _postProcessingPass;

        private bool _haveCharacterShadowPass;

        public override void Create()
        {
            base.name = "CRP Deferred Rendering Pipeline";

            _recreateSystem = true;
            
            _deferredLight = new DeferredLight();
            
            _depthTextures = new DepthTextures();
            
            _characterShadowPass = new CharacterShadowPass();
            _crpGBufferPass = new CRPGBufferPass();
            _crpDepthPyramidPass = new CRPDepthPyramidPass();
            _screenSpaceShadowsPass = new ScreenSpaceShadowsPass();
            _crpStencilLightingPass = new CRPStencilLightingPass();
            _crpTransparentPass = new CRPTransparentPass();
            _postProcessingPass = new PostProcessingPass();
        }
        
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            _haveCharacterShadowPass = _characterShadowPass.Setup(
                _characterEntityManager, _characterShadowCasterDrawSystem);
            
            _crpGBufferPass.Setup();
            _crpDepthPyramidPass.Setup(_depthTextures);
            _screenSpaceShadowsPass.Setup(_deferredLight, _characterEntityManager, _characterScreenShadowDrawSystem);
            _crpStencilLightingPass.Setup(_deferredLight);
            _crpTransparentPass.Setup();
            
            _postProcessingPass.Setup();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_haveCharacterShadowPass)
            {
                renderer.EnqueuePass(_characterShadowPass);
            }
            
            renderer.EnqueuePass(_crpGBufferPass);
            renderer.EnqueuePass(_crpDepthPyramidPass);
            renderer.EnqueuePass(_screenSpaceShadowsPass);
            renderer.EnqueuePass(_crpStencilLightingPass);
            renderer.EnqueuePass(_crpTransparentPass);

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
            GBufferManager.GBuffer.Release();
            _depthTextures.Release();

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