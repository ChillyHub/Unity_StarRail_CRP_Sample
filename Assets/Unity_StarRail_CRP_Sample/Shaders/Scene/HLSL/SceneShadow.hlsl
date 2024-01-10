#ifndef CRP_SCENE_SHADOW_INCLUDED
#define CRP_SCENE_SHADOW_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_CharacterShadowTexture);
SAMPLER(sampler_CharacterShadowTexture);

float4x4 _CharacterLightWorldToShadow[16];
half4 _CharacterShadowOffset0;
half4 _CharacterShadowOffset1;
half4 _CharacterShadowOffset2;
half4 _CharacterShadowOffset3;
float4 _CharacterShadowmapSize;
half4 _CharacterShadowParams;

ShadowSamplingData GetCharacterShadowSamplingData()
{
    ShadowSamplingData shadowSamplingData;

    // shadowOffsets are used in SampleShadowmapFiltered #if defined(SHADER_API_MOBILE) || defined(SHADER_API_SWITCH)
    shadowSamplingData.shadowOffset0 = _CharacterShadowOffset0;
    shadowSamplingData.shadowOffset1 = _CharacterShadowOffset1;
    //shadowSamplingData.shadowOffset2 = _CharacterShadowOffset2;
    //shadowSamplingData.shadowOffset3 = _CharacterShadowOffset3;

    // shadowmapSize is used in SampleShadowmapFiltered for other platforms
    shadowSamplingData.shadowmapSize = _CharacterShadowmapSize;
    shadowSamplingData.softShadowQuality = 1;

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

    return float4(shadowCoord.xyz, 0);
}

real CustomSampleShadowmapFiltered(TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData)
{
    real attenuation;

#if defined(SHADER_API_MOBILE) || defined(SHADER_API_SWITCH)
    // 4-tap hardware comparison
    real4 attenuation4;
    float3 shadowCoord0 = shadowCoord.xyz + samplingData.shadowOffset0.xyz;
    float3 shadowCoord1 = shadowCoord.xyz + samplingData.shadowOffset1.xyz;
    float3 shadowCoord2 = shadowCoord.xyz + samplingData.shadowOffset2.xyz;
    float3 shadowCoord3 = shadowCoord.xyz + samplingData.shadowOffset3.xyz;
    attenuation4.x = real(step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, shadowCoord0.xy), shadowCorrd0.z));
    attenuation4.y = real(step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, shadowCoord1.xy), shadowCorrd1.z));
    attenuation4.z = real(step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, shadowCoord2.xy), shadowCorrd2.z));
    attenuation4.w = real(step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, shadowCoord3.xy), shadowCorrd3.z));
    attenuation = dot(attenuation4, real(0.25));
#else
    float fetchesWeights[9];
    float2 fetchesUV[9];
    SampleShadow_ComputeSamples_Tent_5x5(samplingData.shadowmapSize, shadowCoord.xy, fetchesWeights, fetchesUV);

    attenuation = fetchesWeights[0] * step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, fetchesUV[0].xy), shadowCoord.z);
    attenuation += fetchesWeights[1] * step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, fetchesUV[1].xy), shadowCoord.z);
    attenuation += fetchesWeights[2] * step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, fetchesUV[2].xy), shadowCoord.z);
    attenuation += fetchesWeights[3] * step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, fetchesUV[3].xy), shadowCoord.z);
    attenuation += fetchesWeights[4] * step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, fetchesUV[4].xy), shadowCoord.z);
    attenuation += fetchesWeights[5] * step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, fetchesUV[5].xy), shadowCoord.z);
    attenuation += fetchesWeights[6] * step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, fetchesUV[6].xy), shadowCoord.z);
    attenuation += fetchesWeights[7] * step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, fetchesUV[7].xy), shadowCoord.z);
    attenuation += fetchesWeights[8] * step(SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, fetchesUV[8].xy), shadowCoord.z);
#endif

    return attenuation;
}

bool NoSample(float4 shadowCoord, int index, float pixelSize, float texSize)
{
    float tileSize = texSize / 4;
    
    float minX = (index % 4) * pixelSize * tileSize;
    float minY = (index / 4) * pixelSize * tileSize;
    float maxX = (index % 4 + 1) * pixelSize * tileSize;
    float maxY = (index / 4 + 1) * pixelSize * tileSize;

    return shadowCoord.x <= minX || shadowCoord.x >= maxX || shadowCoord.y <= minY || shadowCoord.y >= maxY;
}

real CustomSampleShadowmap(int index, TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData, half4 shadowParams, bool isPerspectiveProjection = true)
{
    real attenuation;
    real shadowStrength = shadowParams.x;

    #ifdef _SHADOWS_SOFT
    if(shadowParams.y != 0)
    {
        attenuation = CustomSampleShadowmapFiltered(TEXTURE2D_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    }
    else
        #endif
    {
        // 1-tap hardware comparison
        float depth = SAMPLE_DEPTH_TEXTURE(ShadowMap, sampler_ShadowMap, shadowCoord.xy);
        attenuation = step(depth, shadowCoord.z);
    }

    attenuation = LerpWhiteTo(attenuation, shadowStrength);

    // Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
    // TODO: We could use branch here to save some perf on some platforms.
    return NoSample(shadowCoord, index, samplingData.shadowmapSize.x, samplingData.shadowmapSize.z) ? 1.0 : attenuation;
}

half CharacterLightRealtimeShadow(float3 positionWS)
{
    ShadowSamplingData shadowSamplingData = GetCharacterShadowSamplingData();
    half4 shadowParams = GetCharacterShadowParams();

    half shadow = 1.0;
    for (int i = 0; i < 5; i++)
    {
        // i = 2;
        float4 shadowCoord = GetCharacterShadowCoord(i, positionWS);
        shadow *= CustomSampleShadowmap(i, TEXTURE2D_ARGS(_CharacterShadowTexture, sampler_CharacterShadowTexture), shadowCoord, shadowSamplingData, shadowParams, false);
    }
    
    return shadow;
}

#endif
