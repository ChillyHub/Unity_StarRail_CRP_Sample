using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class CRPDeferredRenderingRendererFeature : ScriptableRendererFeature
    {
        // Light
        private DeferredLight _deferredLight;
        
        // Pass
        private CharacterShadowPass _characterShadowPass;
        private CRPGBufferPass _crpGBufferPass;
        private ScreenSpaceShadowsPass _screenSpaceShadowsPass;
        private CRPStencilLightingPass _crpStencilLightingPass;
        private CRPTransparentPass _crpTransparentPass;
        private PostProcessingPass _postProcessingPass;

        private bool _haveCharacterShadowPass;

        public override void Create()
        {
            base.name = "CRP Deferred Rendering Pipeline";

            _deferredLight = new DeferredLight();
            
            _characterShadowPass = new CharacterShadowPass();

            _crpGBufferPass = new CRPGBufferPass();
            _screenSpaceShadowsPass = new ScreenSpaceShadowsPass();
            
            _crpStencilLightingPass = new CRPStencilLightingPass();
            _crpTransparentPass = new CRPTransparentPass();

            _postProcessingPass = new PostProcessingPass();
        }
        
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            _deferredLight.Setup();
            
            _haveCharacterShadowPass = _characterShadowPass.Setup(renderingData);
            
            _crpGBufferPass.Setup();
            _screenSpaceShadowsPass.Setup(_deferredLight);
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
            renderer.EnqueuePass(_screenSpaceShadowsPass);
            renderer.EnqueuePass(_crpStencilLightingPass);
            renderer.EnqueuePass(_crpTransparentPass);

            if (renderingData.cameraData.postProcessEnabled)
            {
                renderer.EnqueuePass(_postProcessingPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            OnDestroy();
        }

        public void OnDestroy()
        {
            GBufferManager.GBuffer.Release();
        }

        public void OnDisable()
        {
            OnDestroy();
        }
    }
}