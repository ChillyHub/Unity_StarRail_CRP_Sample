# Render Pipeline

#### URP Shadow Caster Pass

![AdditionalLightsShadowCasterPass](./RenderPipeline.assets/AdditionalLightsShadowCasterPass.png)

#### Per Object Shadow Caster Pass

![CharacterShadowCasterPass](./RenderPipeline.assets/CharacterShadowCasterPass.png)

#### GBuffer Pass

###### GBuffer0.rgb

![GBuffer0rgb](./RenderPipeline.assets/GBuffer0rgb.png)

###### GBuffer0.a

![GBuffer0a](./RenderPipeline.assets/GBuffer0a.png)

###### GBuffer1.rgb

![GBuffer1rgb](./RenderPipeline.assets/GBuffer1rgb.png)

###### GBuffer1.a

![GBuffer1a](./RenderPipeline.assets/GBuffer1a.png)

###### GBuffer2.rgb

![GBuffer2rgb](./RenderPipeline.assets/GBuffer2rgb.png)

###### Depth [0.0, 0.1]

![Depth_0-0_1](./RenderPipeline.assets/Depth_0-0_1.png)

###### Stencil

![Stencil](./RenderPipeline.assets/Stencil.png)

#### Copy Depth and Pyramid

> [!NOTE]
>
> Range: [0.0, 0.1]

![CopyDepthPyramidPass](./RenderPipeline.assets/CopyDepthPyramidPass.png)

#### Screen Space Shadow

![ScreenSpaceShadowPass](./RenderPipeline.assets/ScreenSpaceShadowPass.png)

#### Deferred Stencil Lighting

![StencilDeferredLightingPass](./RenderPipeline.assets/StencilDeferredLightingPass.png)

#### URP Render Skybox

Use URP Pass.



#### CRP Transparent

Use Forward Lighting.



#### CRP Motion Vector

> [!NOTE]
>
> Range: [0.0, 0.00015]

![MotionVectorPass](./RenderPipeline.assets/MotionVectorPass.png)

#### CRP SSR

###### UV Mapping Texture

![SSRMappingUV](./RenderPipeline.assets/SSRMappingUV.png)

###### Draw SSR

![SSRDrawing](./RenderPipeline.assets/SSRDrawing.png)

#### Copy Color and Pyramid

Result Mip 2:

![CopyColorPyramidPass_Mip2](./RenderPipeline.assets/CopyColorPyramidPass_Mip2.png)

#### CRP TAA

![TAAPass](./RenderPipeline.assets/TAAPass.png)

#### CRP Post Process

###### Bloom

Bloom Texture:

![BloomTexture](./RenderPipeline.assets/BloomTexture.png)

###### Tone Mapping

![ToneMappingPass](./RenderPipeline.assets/ToneMappingPass.png)

#### URP Post Process

Use URP post process.