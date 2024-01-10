#ifndef CRP_SCENE_LIGHTING_INCLUDED
#define CRP_SCENE_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#include "../../Deferred/HLSL/CRPGBuffer.hlsl"

half3 LightingPhysicallyScene(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirWS)
{
    half NdotL = saturate(dot(normalWS, light.direction));
    half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * NdotL);

    half metalic = MetallicFromReflectivity(brdfData.reflectivity);

    half3 diffuse = brdfData.diffuse * radiance;

    float3 halfDirWS = normalize(light.direction + viewDirWS);
    float blinnPhong = pow(saturate(dot(halfDirWS, normalWS)), 20.0);
    float stepPhong = smoothstep(1.0 - blinnPhong, 1.0 + brdfData.roughness - blinnPhong, 0.5);

    half3 lightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
    half3 NonmetalSpecular = lightColor * 0.04 * stepPhong * brdfData.diffuse;
    half3 MetalSpecular = lightColor * 0.96 * brdfData.albedo * blinnPhong * metalic;
    
    return diffuse + NonmetalSpecular + MetalSpecular;
}

half3 SceneLighting(InputData input, half4 gBuffer0, half4 gBuffer1, Light light, bool specularOff)
{
    BRDFData brdfData = SceneBRDFDataFromGBuffer(gBuffer0, gBuffer1);
    return LightingPhysicallyScene(brdfData, light, input.normalWS, input.viewDirectionWS);
    //return LightingPhysicallyBased(brdfData, light, input.normalWS, input.viewDirectionWS, specularOff);
}

half4 SceneForwardLighting(InputData inputData, SurfaceData surfaceData)
{
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
    BRDFData brdfData;

    // NOTE: can modify "surfaceData"...
    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;

    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    // Clear-coat calculation...
    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    // NOTE: We don't apply AO to the GI here because it's done in the lighting calculation below...
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);

    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = LightingPhysicallyScene(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyScene(brdfData, light, inputData.normalWS, inputData.viewDirectionWS);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyScene(brdfData, light, inputData.normalWS, inputData.viewDirectionWS);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

#if REAL_IS_HALF
    // Clamp any half.inf+ to HALF_MAX
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

#endif
