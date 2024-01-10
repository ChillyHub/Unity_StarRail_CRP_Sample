#ifndef CRP_CUSTOM_SHADOW_INCLUDED
#define CRP_CUSTOM_SHADOW_INCLUDED

#include "CharacterShadow.hlsl"

half CustomMainLightShadow(float4 shadowCoord, float3 positionWS, half4 shadowMask, half4 occlusionProbeChannels)
{
    half realtimeShadow = MainLightRealtimeShadow(shadowCoord);
    half characterShadow = CharacterRealtimeShadow(positionWS);

    #ifdef CALCULATE_BAKED_SHADOWS
    half bakedShadow = BakedShadow(shadowMask, occlusionProbeChannels);
    #else
    half bakedShadow = half(1.0);
    #endif

    #ifdef MAIN_LIGHT_CALCULATE_SHADOWS
    half shadowFade = GetMainLightShadowFade(positionWS);
    #else
    half shadowFade = half(1.0);
    #endif

    return MixRealtimeAndBakedShadows(realtimeShadow * characterShadow, bakedShadow, shadowFade);
}

Light CustomGetMainLight(float4 shadowCoord, float3 positionWS, half4 shadowMask)
{
    Light light = GetMainLight();

    #if defined(LIGHTMAP_ON) && defined(_MIXED_LIGHTING_SUBTRACTIVE)
    light.shadowAttenuation = CustomMainLightShadow(shadowCoord, positionWS, shadowMask, _MainLightOcclusionProbes);
    #endif

    #if defined(_LIGHT_COOKIES)
    real3 cookieColor = SampleMainLightCookie(positionWS);
    light.color *= cookieColor;
    #endif

    return light;
}

#endif //CRP_CUSTOM_SHADOW_INCLUDED
