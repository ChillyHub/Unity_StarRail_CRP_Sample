#ifndef CRP_CHARACTER_SHADOW_INCLUDED
#define CRP_CHARACTER_SHADOW_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_CharacterShadowmapTexture);

float4x4 _CharacterLightWorldToShadow[16];
half4 _CharacterShadowOffset0;
half4 _CharacterShadowOffset1;
float4 _CharacterShadowmapSize;
half4 _CharacterShadowParams;

ShadowSamplingData GetCharacterShadowSamplingData()
{
    ShadowSamplingData shadowSamplingData;

    // shadowOffsets are used in SampleShadowmapFiltered for low quality soft shadows.
    shadowSamplingData.shadowOffset0 = _CharacterShadowOffset0;
    shadowSamplingData.shadowOffset1 = _CharacterShadowOffset1;

    // shadowmapSize is used in SampleShadowmapFiltered otherwise
    shadowSamplingData.shadowmapSize = _CharacterShadowmapSize;
    shadowSamplingData.softShadowQuality = _CharacterShadowParams.y;

    return shadowSamplingData;
}

// ShadowParams
// x: ShadowStrength
// y: 1.0 if shadow is soft, 0.0 otherwise
half4 GetCharacterShadowParams()
{
    return _CharacterShadowParams;
}

float4 GetCharacterShadowCoord(int characterIndex, float3 positionWS)
{
    float4 shadowCoord = mul(_CharacterLightWorldToShadow[characterIndex], float4(positionWS, 1.0));

    shadowCoord = saturate(shadowCoord);
    shadowCoord.z += 0.0001;

    return float4(shadowCoord.xyz, 0);
}

bool NoSample(float4 shadowCoord, uint index, float pixelSize, float texSize)
{
    float tileSize = texSize / 4;
    
    float minX = (index % 4) * pixelSize * tileSize;
    float minY = (index / 4) * pixelSize * tileSize;
    float maxX = (index % 4 + 1) * pixelSize * tileSize;
    float maxY = (index / 4 + 1) * pixelSize * tileSize;

    return shadowCoord.x <= minX || shadowCoord.x >= maxX || shadowCoord.y <= minY || shadowCoord.y >= maxY;
}

real SampleCharacterShadowmap(uint index, TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData, half4 shadowParams, bool isPerspectiveProjection = true)
{
    // Compiler will optimize this branch away as long as isPerspectiveProjection is known at compile time
    if (isPerspectiveProjection)
        shadowCoord.xyz /= shadowCoord.w;
    
    real attenuation;
    real shadowStrength = shadowParams.x;

    #if defined(_CHARACTER_SHADOWS_SOFT)
    if (shadowParams.y > SOFT_SHADOW_QUALITY_OFF)
    {
        attenuation = SampleShadowmapFiltered(TEXTURE2D_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    }
    else
    #endif
    {
        // 1-tap hardware comparison
        attenuation = real(SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz));
    }

    attenuation = LerpWhiteTo(attenuation, shadowStrength);

    // Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
    // TODO: We could use branch here to save some perf on some platforms.
    return NoSample(shadowCoord, index, samplingData.shadowmapSize.x, samplingData.shadowmapSize.z) ? 1.0 : attenuation;
}

half CharacterRealtimeShadow(float3 positionWS)
{
    ShadowSamplingData shadowSamplingData = GetCharacterShadowSamplingData();
    half4 shadowParams = GetCharacterShadowParams();

    half shadow = 1.0;
    for (uint i = 0; i < 5; ++i)
    {
        float4 shadowCoord = GetCharacterShadowCoord(i, positionWS);
        shadow *= SampleCharacterShadowmap(i, TEXTURE2D_ARGS(_CharacterShadowmapTexture, sampler_LinearClampCompare),
            shadowCoord, shadowSamplingData, shadowParams, false);
    }
    
    return shadow;
}

half CharacterRealtimeShadow(float3 positionWS, float index)
{
    ShadowSamplingData shadowSamplingData = GetCharacterShadowSamplingData();
    half4 shadowParams = GetCharacterShadowParams();

    half shadow = 1.0;
    const int i = int(index);
    float4 shadowCoord = GetCharacterShadowCoord(i, positionWS);
    shadow *= SampleCharacterShadowmap(i, TEXTURE2D_ARGS(_CharacterShadowmapTexture, sampler_LinearClampCompare),
        shadowCoord, shadowSamplingData, shadowParams, false);
    
    return shadow;
}

#endif
