#ifndef CRP_CHARACTER_INPUT_INCLUDED
#define CRP_CHARACTER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

#include "../../Utils/HLSL/Depth.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float _NormalScale;

// Setting
float _RampV0;
float _RampV1;
float _RampV2;
float _RampV3;
float _RampV4;
float _RampV5;
float _RampV6;
float _RampV7;

// GI
float _GI_Intensity;
float _GI_Flatten;
float _GI_UseMainColor;

// Diffuse
float _ShadowRamp;
float _ShadowOffset;
float _ShadowBoost;

// Specular
float4 _SpecularColor;
float _SpecularShininess;
float _SpecularRoughness;
float _SpecularIntensity;
//// Base Specular
float4 _SpecularColor0;
float _SpecularShininess0;
float _SpecularRoughness0;
float _SpecularIntensity0;
float4 _SpecularColor1;
float _SpecularShininess1;
float _SpecularRoughness1;
float _SpecularIntensity1;
float4 _SpecularColor2;
float _SpecularShininess2;
float _SpecularRoughness2;
float _SpecularIntensity2;
float4 _SpecularColor3;
float _SpecularShininess3;
float _SpecularRoughness3;
float _SpecularIntensity3;
float4 _SpecularColor4;
float _SpecularShininess4;
float _SpecularRoughness4;
float _SpecularIntensity4;
float4 _SpecularColor5;
float _SpecularShininess5;
float _SpecularRoughness5;
float _SpecularIntensity5;
float4 _SpecularColor6;
float _SpecularShininess6;
float _SpecularRoughness6;
float _SpecularIntensity6;
float4 _SpecularColor7;
float _SpecularShininess7;
float _SpecularRoughness7;
float _SpecularIntensity7;

// Emission
float _EmissionIntensity;
float _EmissionThreshold;

// Rim
float4 _RimColor;
float _RimWidth;
float4 _RimColor0;
float _RimWidth0;
float4 _RimColor1;
float _RimWidth1;
float4 _RimColor2;
float _RimWidth2;
float4 _RimColor3;
float _RimWidth3;
float4 _RimColor4;
float _RimWidth4;
float4 _RimColor5;
float _RimWidth5;
float4 _RimColor6;
float _RimWidth6;
float4 _RimColor7;
float _RimWidth7;
float _RimIntensity;

// Expression
float _CheckIntensity;
float _ShyIntensity;
float _ShadowIntensity;

// Outline
float4 _OutlineColor;
float4 _OutlineColor0;
float4 _OutlineColor1;
float4 _OutlineColor2;
float4 _OutlineColor3;
float4 _OutlineColor4;
float4 _OutlineColor5;
float4 _OutlineColor6;
float4 _OutlineColor7;
float _OutlineWidth;
float _OutlineWidthMin;
float _OutlineWidthMax;

// Bloom
float _BloomIntensity;
float _BloomIntensity0;
float _BloomIntensity1;
float _BloomIntensity2;
float _BloomIntensity3;
float _BloomIntensity4;
float _BloomIntensity5;
float _BloomIntensity6;
float _BloomIntensity7;
float4 _BloomColor;
float4 _BloomColor0;
float4 _BloomColor1;
float4 _BloomColor2;
float4 _BloomColor3;
float4 _BloomColor4;
float4 _BloomColor5;
float4 _BloomColor6;
float4 _BloomColor7;

// Stocking
float4 _StockBrightColor;
float4 _StockDarkColor;
float _StockPower;
float _StockDarkWidth;
float _StockThickness;

// Fresnel
float4 _FresnelColor;
float _FresnelIntensity;

// Dissolve

// Shader State
float _ReceiveShadowsToggle;
CBUFFER_END

float3 _CharMainLightDirection;
float3 _CharMainLightColor;
float3 _HeadCenter;
float3 _HeadForward;
float3 _HeadRight;
float3 _HeadUp;
float _DayTime;

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_LightMap);
SAMPLER(sampler_LightMap);
TEXTURE2D(_FaceMap);
SAMPLER(sampler_FaceMap);
TEXTURE2D(_FaceExpressionMap);
SAMPLER(sampler_FaceExpressionMap);
TEXTURE2D(_CoolRampMap);
SAMPLER(sampler_CoolRampMap);
TEXTURE2D(_WarmRampMap);
SAMPLER(sampler_WarmRampMap);
TEXTURE2D(_LUTMap);
SAMPLER(sampler_LUTMap);
// Optional
TEXTURE2D(_StockingMap);
SAMPLER(sampler_StockingMap);
TEXTURE2D(_DissolveMap);
SAMPLER(sampler_DissolveMap);

// _UseNormalMapToggle("Use Normal Map", Float) = 0
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
//_NormalScale("Normal Scale", Range(0, 4)) = 1

// Render Targets
TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);
TEXTURE2D(_CameraOpaqueTexture);
SAMPLER(sampler_CameraOpaqueTexture);
TEXTURE2D(_CameraColorTexture);
SAMPLER(sampler_CameraColorTexture);
TEXTURE2D(_TempDepthTexture);
SAMPLER(sampler_TempDepthTexture);

half4 SampleMainTex(float2 uv)
{
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
}

half4 SampleLightMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, uv);
}

half4 SampleFaceMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_FaceMap, sampler_FaceMap, uv);
}

half4 SampleFaceExpressionMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_FaceExpressionMap, sampler_FaceExpressionMap, uv);
}

half4 SampleCoolRampMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CoolRampMap, sampler_CoolRampMap, uv);
}

half4 SampleWarmRampMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_WarmRampMap, sampler_WarmRampMap, uv);
}

half4 SampleStockingMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_StockingMap, sampler_StockingMap, uv);
}

half4 SampleDissolveMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, uv);
}

float3 SampleNormalMap(float2 uv)
{
    return UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv));
}

half4 SampleLUTMap(int materialId, int renderType)
{
    return _LUTMap.Load(int3(materialId, renderType, 0));
}

float SampleCameraDepthTexture(float2 uv)
{
    return SampleDepth(uv);
}

float4 SampleCameraOpaqueTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
}

float4 SampleCameraColorTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_CameraColorTexture, uv);
}

float SampleTempDepthTexture(float2 uv)
{
    return SAMPLE_DEPTH_TEXTURE(_TempDepthTexture, sampler_TempDepthTexture, uv);
}

half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
    return half4(SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv));
}

half Alpha(half albedoAlpha, half4 color, half cutoff)
{
#if !defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) && !defined(_GLOSSINESS_FROM_BASE_ALPHA)
    half alpha = albedoAlpha * color.a;
    #else
    half alpha = color.a;
#endif

    alpha = AlphaDiscard(alpha, cutoff);

return alpha;
}

// Lights
Light GetCharacterLight(float3 positionWS)
{
    float2 screenUV = ComputeNormalizedDeviceCoordinates(positionWS, UNITY_MATRIX_VP);
    
    Light light = (Light)0;
    light.direction = _CharMainLightDirection.xyz;
    light.distanceAttenuation = 1.0;
    light.color = _CharMainLightColor.rgb;
    light.shadowAttenuation = SampleScreenSpaceShadowmap(float4(screenUV, 0.0, 1.0));

    return light;
}

// Utils
half GetRampV(half matId)
{
    return 0.0625 + 0.125 * lerp(lerp(lerp(lerp(lerp(lerp(lerp(
        _RampV0,
        _RampV1, step(0.125, matId)),
        _RampV2, step(0.250, matId)),
        _RampV3, step(0.375, matId)),
        _RampV4, step(0.500, matId)),
        _RampV5, step(0.625, matId)),
        _RampV6, step(0.750, matId)),
        _RampV7, step(0.875, matId));
}

half GetRampLineIndex(half matId)
{
    return lerp(lerp(lerp(lerp(lerp(lerp(lerp(
        _RampV0,
        _RampV1, step(0.125, matId)),
        _RampV2, step(0.250, matId)),
        _RampV3, step(0.375, matId)),
        _RampV4, step(0.500, matId)),
        _RampV5, step(0.625, matId)),
        _RampV6, step(0.750, matId)),
        _RampV7, step(0.875, matId));
}

half GetMetalIndex()
{
    return _RampV4;
}

half3 LerpRampColor(half3 coolRamp, half3 warmRamp)
{
    return lerp(warmRamp, coolRamp, abs(_DayTime - 12.0) * rcp(12.0));
}

half3 GetSpecularColor(half materialId)
{
    const float4 overlayColors[8] = {
        _SpecularColor0,
        _SpecularColor1,
        _SpecularColor2,
        _SpecularColor3,
        _SpecularColor4,
        _SpecularColor5,
        _SpecularColor6,
        _SpecularColor7,
    };
    
    half3 overlayColor = overlayColors[GetRampLineIndex(materialId)].rgb;

    #ifdef _CUSTOMSPECULARVARENUM_DISABLE
        return _SpecularColor.rgb;
    #elif _CUSTOMSPECULARVARENUM_MULTIPLY
        return _SpecularColor.rgb * overlayColor;
    #elif _CUSTOMSPECULARVARENUM_OVERLAY
        return overlayColor;
    #else
        return _SpecularColor.rgb;
    #endif
}

half3 GetSpecularColor()
{
    return _SpecularColor.rgb;
}

float GetSpecularShininess(half materialId)
{
    const float overlayShininess[8] = {
        _SpecularShininess0,
        _SpecularShininess1,
        _SpecularShininess2,
        _SpecularShininess3,
        _SpecularShininess4,
        _SpecularShininess5,
        _SpecularShininess6,
        _SpecularShininess7,
    };
    
    float overlay = overlayShininess[GetRampLineIndex(materialId)];

    #ifdef _CUSTOMSPECULARVARENUM_DISABLE
        return _SpecularShininess;
    #elif _CUSTOMSPECULARVARENUM_MULTIPLY
        return _SpecularShininess * overlay;
    #elif _CUSTOMSPECULARVARENUM_OVERLAY
        return overlay;
    #else
        return _SpecularShininess;
    #endif
}

float GetSpecularShininess()
{
    return _SpecularShininess;
}

float GetSpecularRoughness(half materialId)
{
    const float overlayRoughness[8] = {
        _SpecularRoughness0,
        _SpecularRoughness1,
        _SpecularRoughness2,
        _SpecularRoughness3,
        _SpecularRoughness4,
        _SpecularRoughness5,
        _SpecularRoughness6,
        _SpecularRoughness7,
    };
    
    float overlay = overlayRoughness[GetRampLineIndex(materialId)];

    #ifdef _CUSTOMSPECULARVARENUM_DISABLE
        return _SpecularRoughness;
    #elif _CUSTOMSPECULARVARENUM_MULTIPLY
        return _SpecularRoughness * overlay;
    #elif _CUSTOMSPECULARVARENUM_OVERLAY
        return overlay;
    #else
        return _SpecularRoughness;
    #endif
}

float GetSpecularRoughness()
{
    return _SpecularRoughness;
}

float GetSpecularIntensity(half materialId)
{
    const float overlayIntensity[8] = {
        _SpecularIntensity0,
        _SpecularIntensity1,
        _SpecularIntensity2,
        _SpecularIntensity3,
        _SpecularIntensity4,
        _SpecularIntensity5,
        _SpecularIntensity6,
        _SpecularIntensity7,
    };
    
    float overlay = overlayIntensity[GetRampLineIndex(materialId)];

    #ifdef _CUSTOMSPECULARVARENUM_DISABLE
        return _SpecularIntensity;
    #elif _CUSTOMSPECULARVARENUM_MULTIPLY
        return _SpecularIntensity * overlay;
    #elif _CUSTOMSPECULARVARENUM_OVERLAY
        return overlay;
    #else
        return _SpecularIntensity;
    #endif
}

float GetSpecularIntensity()
{
    return _SpecularIntensity;
}

half3 GetRimColor(half materialId, half3 mainColor)
{
    half3 coolColor = SampleCoolRampMap(float2(0, GetRampV(materialId))).rgb;
    half3 warmColor = SampleWarmRampMap(float2(0, GetRampV(materialId))).rgb;
    half3 color = mainColor * LerpRampColor(coolColor, warmColor);
    
    const float4 overlayColors[8] = {
        _RimColor0,
        _RimColor1,
        _RimColor2,
        _RimColor3,
        _RimColor4,
        _RimColor5,
        _RimColor6,
        _RimColor7,
    };
    
    half3 overlayColor = overlayColors[GetRampLineIndex(materialId)].rgb;

    #ifdef _CUSTOMRIMVARENUM_DISABLE
        return color.rgb;
    #elif _CUSTOMRIMVARENUM_MULTIPLY
        return color.rgb * overlayColor;
    #elif _CUSTOMRIMVARENUM_OVERLAY
        return overlayColor;
    #else
        return color.rgb;
    #endif
}

half3 GetRimColor(half3 mainColor)
{
    half3 coolColor = SampleCoolRampMap(float2(0, 0)).rgb;
    half3 warmColor = SampleWarmRampMap(float2(0, 0)).rgb;
    half3 color = mainColor * LerpRampColor(coolColor, warmColor);

    half3 overlayColor = _RimColor.rgb;
    
    #ifdef _CUSTOMRIMVARENUM_DISABLE
        return color.rgb;
    #elif _CUSTOMRIMVARENUM_MULTIPLY
        return color.rgb * overlayColor;
    #elif _CUSTOMRIMVARENUM_OVERLAY
        return overlayColor;
    #else
        return _RimColor.rgb;
    #endif
}

float GetRimWidth(half materialId)
{
    const float overlayWidths[8] = {
        _RimWidth0,
        _RimWidth1,
        _RimWidth2,
        _RimWidth3,
        _RimWidth4,
        _RimWidth5,
        _RimWidth6,
        _RimWidth7,
    };
    
    float overlayWidth = overlayWidths[GetRampLineIndex(materialId)];

    #ifdef _CUSTOMRIMVARENUM_DISABLE
        return _RimWidth;
    #elif _CUSTOMRIMVARENUM_MULTIPLY
        return _RimWidth * overlayWidth;
    #elif _CUSTOMRIMVARENUM_OVERLAY
        return overlayWidth;
    #else
        return _RimWidth;
    #endif
}

float GetRimWidth()
{
    return _RimWidth;
}

half3 GetOutlineColor(half materialId, half3 mainColor)
{
    #if _USE_LUT_MAP
        half3 color = SampleLUTMap((int)GetRampLineIndex(materialId), 3).rgb;
    #else
        half3 coolColor = SampleCoolRampMap(float2(0, GetRampV(materialId))).rgb;
        half3 warmColor = SampleWarmRampMap(float2(0, GetRampV(materialId))).rgb;
        half3 color = mainColor * LerpRampColor(coolColor, warmColor);
    #endif

    const float4 overlayColors[8] = {
        _OutlineColor0,
        _OutlineColor1,
        _OutlineColor2,
        _OutlineColor3,
        _OutlineColor4,
        _OutlineColor5,
        _OutlineColor6,
        _OutlineColor7,
    };
    
    half3 overlayColor = overlayColors[GetRampLineIndex(materialId)].rgb;

    #ifdef _CUSTOMOUTLINEVARENUM_DISABLE
        return color;
    #elif _CUSTOMOUTLINEVARENUM_MULTIPLY
        return color * overlayColor;
    #elif _CUSTOMOUTLINEVARENUM_OVERLAY
        return overlayColor;
    #else
        return color;
    #endif
}

half3 GetOutlineColor(half3 mainColor)
{
    half3 coolColor = SampleCoolRampMap(float2(0, 0)).rgb;
    half3 warmColor = SampleWarmRampMap(float2(0, 0)).rgb;
    half3 color = mainColor * LerpRampColor(coolColor, warmColor) * _OutlineColor.rgb;

    return color;
}

float GetBloomIntensity(half materialId)
{
    const float overlays[8] = {
        _BloomIntensity0,
        _BloomIntensity1,
        _BloomIntensity2,
        _BloomIntensity3,
        _BloomIntensity4,
        _BloomIntensity5,
        _BloomIntensity6,
        _BloomIntensity7,
    };

    float overlay = overlays[GetRampLineIndex(materialId)];

    #ifdef _CUSTOMBLOOMVARENUM_DISABLE
        return _BloomIntensity * 0.1;
    #elif _CUSTOMBLOOMVARENUM_MULTIPLY
        return _BloomIntensity * overlay * 0.1;
    #elif _CUSTOMBLOOMVARENUM_OVERLAY
        return overlay * 0.1;
    #else
        return _BloomIntensity * 0.1;
    #endif
}

float GetBloomIntensity()
{
    return _BloomIntensity * 0.1;
}

half3 GetBloomColor(half materialId, half3 mainColor)
{
    const float4 overlays[8] = {
        _BloomColor0,
        _BloomColor1,
        _BloomColor2,
        _BloomColor3,
        _BloomColor4,
        _BloomColor5,
        _BloomColor6,
        _BloomColor7,
    };

    half3 overlay = overlays[GetRampLineIndex(materialId)].rgb;

    #ifdef _CUSTOMBLOOMCOLORVARENUM_DISABLE
    return mainColor;
    #elif _CUSTOMBLOOMCOLORVARENUM_TINT
    return mainColor * overlay * _BloomColor;
    #elif _CUSTOMBLOOMCOLORVARENUM_OVERLAY
    return overlay;
    #else
    return mainColor;
    #endif
}

half3 GetBloomColor(half3 mainColor)
{
    #ifdef _CUSTOMBLOOMCOLORVARENUM_DISABLE
    return mainColor;
    #elif _CUSTOMBLOOMCOLORVARENUM_TINT
    return mainColor * _BloomColor;
    #elif _CUSTOMBLOOMCOLORVARENUM_OVERLAY
    return _BloomColor;
    #else
    return mainColor;
    #endif
}

#endif
