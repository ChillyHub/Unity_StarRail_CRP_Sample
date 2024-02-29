#ifndef CRP_CHARACTER_FUNCTION_INCLUDED
#define CRP_CHARACTER_FUNCTION_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

const static float3 f3zero = float3(0.0, 0.0, 0.0);
const static float3 f3one = float3(1.0, 1.0, 1.0);
const static float4 f4zero = float4(0.0, 0.0, 0.0, 0.0);
const static float4 f4one = float4(1.0, 1.0, 1.0, 1.0);

struct Surface
{
    half3 color;
    half alpha;
    half emission;
    half specularIntensity;
    half diffuseThreshold;
    half specularThreshold;
    half materialId;
    
#ifdef _USE_NORMAL_MAP
    half3 normalTS;
#endif

#ifdef _WITH_STOCKING
    half stockMask;
    half stockThickness;
    half stockDetail;
#endif
};

struct FaceData
{
    float specularFac;
    float aoFac;
    float outlineFac;
    float sdf;
    float cheek;
    float shy;
    float shadow;
};

half4 SampleCoolRampMap(float2 uv);
half4 SampleWarmRampMap(float2 uv);
half4 SampleFaceMap(float2 uv);
half4 SampleFaceExpressionMap(float2 uv);
half GetRampV(half matId);

// Vertex Utils --------------------------------------------------------------------------------------------------- // 
// ---------------------------------------------------------------------------------------------------------------- //
float CalculateExtendWidthWS(float3 positionWS, float3 extendVectorWS, float extendWS, float minExtendSS, float maxExtendSS)
{
    float4 positionCS = TransformWorldToHClip(positionWS);
    float4 extendPositionCS = TransformWorldToHClip(positionWS + extendVectorWS * extendWS);

    float2 delta = extendPositionCS.xy / extendPositionCS.w - positionCS.xy / positionCS.w;
    delta *= GetScaledScreenParams().xy / GetScaledScreenParams().y * 1080.0f;

    const float extendLen = length(delta);
    float width = extendWS * min(1.0, maxExtendSS / extendLen) * max(1.0, minExtendSS / extendLen);

    return width;
}

float3 ExtendOutline(float3 positionWS, float3 smoothNormalWS, float width, float widthMin, float widthMax)
{
    float offsetLen = CalculateExtendWidthWS(positionWS, smoothNormalWS, width, widthMin, widthMax);

    return positionWS + smoothNormalWS * offsetLen;
}

float3 ExtendRim(float3 positionWS, float3 normalWS, float width, float widthMin, float widthMax)
{
    float3 extendWS = normalize(float3(normalWS.x, 0.0, normalWS.z));
    float offsetLen = CalculateExtendWidthWS(positionWS, extendWS, width, widthMin, widthMax);

    return positionWS + extendWS * offsetLen;
}

// Lighting Utils ------------------------------------------------------------------------------------------------- // 
// ---------------------------------------------------------------------------------------------------------------- //
half3 GrayColor(half3 color)
{
    float gray = dot(color, half3(0.3, 0.59, 0.11));
    return gray.xxx;
}

half3 RenderStocking(Surface surface, float3 normalWS, float3 viewDirWS, float3 brightColor, float3 darkColor,
    float power, float darkWidth, float thickness)
{
    half3 color = f3zero;
    
    #ifdef _WITH_STOCKING
        float fac = saturate(dot(normalWS, viewDirWS));
        fac = pow(smoothstep(darkWidth, 1.0, fac), power);
        fac = surface.stockThickness * thickness * fac;
        color = lerp(darkColor, brightColor * surface.stockDetail, fac);
    #endif
    
    return color;
}

// Lighting ------------------------------------------------------------------------------------------------------- // 
// ---------------------------------------------------------------------------------------------------------------- //
half3 CalculateGI(Surface surface, half3 sh, float intensity, float mainColorLerp)
{
    return intensity * lerp(f3one, surface.color, mainColorLerp) * lerp(GrayColor(sh), sh, mainColorLerp) * surface.diffuseThreshold;
}

half3 CalculateAdditionalLight(Surface surface, float3 positionWS)
{
    half3 color = f3zero;
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS);
        half3 lightColor = light.color * light.distanceAttenuation;
        color += lightColor * surface.color;
    LIGHT_LOOP_END

    return color;
}

half3 CalculateBaseDiffuse(out float diffuseFac, Surface surface, Light light, half3 normalWS,
    float shadowRamp, float shadowOffset, float shadowBoost, half3 stocking = f3zero)
{
    half3 lightDirWS = normalize(light.direction);
    half lambert01 = dot(lightDirWS, normalWS) * 0.5 + 0.5;
    diffuseFac = smoothstep(
        1.0 - surface.diffuseThreshold - shadowRamp,
        1.0 - surface.diffuseThreshold + shadowRamp,
        lambert01 + shadowOffset);
    // diffuseFac *= light.shadowAttenuation;

    diffuseFac *= light.shadowAttenuation;
    half rampU = diffuseFac * 0.75 + 0.25;

    half2 rampUV = half2(rampU, GetRampV(surface.materialId));
    half3 coolColor = SampleCoolRampMap(rampUV).rgb;
    half3 warmColor = SampleWarmRampMap(rampUV).rgb;
    half3 rampColor = LerpRampColor(coolColor, warmColor);
    rampColor = lerp(f3one, rampColor, shadowBoost);

    float3 lightColor = light.color * light.distanceAttenuation;
    lightColor = lerp(lightColor, GrayColor(lightColor), 0.0);

    #ifdef _WITH_STOCKING
        surface.color = surface.color * lerp(f3one, stocking, surface.stockMask);
    #endif
    
    half3 diffuse = lightColor * surface.color * rampColor * rampColor;

    return diffuse;
}

half3 CalculateFaceDiffuse(Surface surface, FaceData faceData, Light light, float3 viewDir, float2 baseUV, 
    half3 forward, half3 right, half3 up, float shadowRamp, float shadowOffset, float shadowBoost)
{
    float3 lightDirLocal = mul(float3x3(right, up, forward), light.direction);
    lightDirLocal.y = 0.0;
    lightDirLocal = SafeNormalize(lightDirLocal);

    float2 swapUV = baseUV;
    swapUV.x = 1.0 - swapUV.x;
    float2 uv = lerp(baseUV, swapUV, step(0.0, lightDirLocal.x));
    float shadowThreshold = SampleFaceMap(uv).a;
    float lightFac = lightDirLocal.z * 0.5 + 0.5;

    half diffuseFac = smoothstep(
        1.0 - shadowThreshold - shadowRamp,
        1.0 - shadowThreshold + shadowRamp,
        lightFac + shadowOffset);

    diffuseFac *= light.shadowAttenuation;
    half rampU = diffuseFac * 0.75 + 0.25;

    half2 rampUV = half2(rampU, 0.0625);
    half3 coolColor = SampleCoolRampMap(rampUV).rgb;
    half3 warmColor = SampleWarmRampMap(rampUV).rgb;
    half3 rampColor = LerpRampColor(coolColor, warmColor);
    rampColor = lerp(f3one, rampColor, shadowBoost);

    float3 lightColor = light.color * light.distanceAttenuation;
    lightColor = lerp(lightColor, GrayColor(lightColor), 0.0);

    half3 outlineFac = pow(saturate(dot(forward, viewDir)), 20.0) * smoothstep(0.0, 0.2, faceData.outlineFac);
    half3 faceColor = lerp(surface.color, half3(1.0, 0.0, 0.0), (faceData.shy + faceData.cheek + faceData.shadow));
    faceColor = lerp(faceColor, GetOutlineColor(surface.color), outlineFac);

    half3 diffuse = lightColor * faceColor * rampColor * rampColor;
    
    return diffuse;
}

half3 CalculateHairDiffuse(out float diffuseFac, Surface surface, Light light, half3 normalWS,
    float shadowRamp, float shadowOffset, float shadowBoost)
{
    half3 lightDirWS = normalize(light.direction);
    half lambert01 = dot(lightDirWS, normalWS) * 0.5 + 0.5;
    diffuseFac = smoothstep(
        1.0 - surface.diffuseThreshold - shadowRamp,
        1.0 - surface.diffuseThreshold + shadowRamp,
        lambert01 + shadowOffset);

    diffuseFac *= light.shadowAttenuation;
    half rampU = diffuseFac * 0.75 + 0.25;

    half2 rampUV = half2(rampU, GetRampV(surface.materialId));
    half3 coolColor = SampleCoolRampMap(rampUV).rgb;
    half3 warmColor = SampleWarmRampMap(rampUV).rgb;
    half3 rampColor = LerpRampColor(coolColor, warmColor);
    rampColor = lerp(f3one, rampColor, shadowBoost);

    float3 lightColor = light.color * light.distanceAttenuation;
    lightColor = lerp(lightColor, GrayColor(lightColor), 0.0);

    half3 diffuse = lightColor * surface.color * rampColor * rampColor;
    
    return diffuse;
}

half3 CalculateSpecular(Surface surface, Light light, float3 viewDirWS, half3 normalWS, 
    half3 specColor, float shininess, float roughness, float intensity, float diffuseFac, float metallic = 0.0)
{
    //roughness = lerp(1.0, roughness * roughness, metallic);
    //float smoothness = exp2(shininess * (1.0 - roughness) + 1.0) + 1.0;
    float3 halfDirWS = normalize(light.direction + viewDirWS);
    float blinnPhong = pow(saturate(dot(halfDirWS, normalWS)), shininess);
    float threshold = 1.0 - surface.specularThreshold;
    float stepPhong = smoothstep(threshold - roughness, threshold + roughness, blinnPhong);

    float3 f0 = lerp(0.04, surface.color, metallic);
    float3 fresnel = f0 + (1.0 - f0) * pow(1.0 - saturate(dot(viewDirWS, halfDirWS)), 5.0);

    half3 lightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
    half3 specular = lightColor * specColor * fresnel * stepPhong * lerp(diffuseFac, 1.0, metallic);
    
    return specular * intensity * surface.specularIntensity;
}

half3 CalculateBaseSpecular(Surface surface, Light light, float3 viewDirWS, half3 normalWS, 
    half3 specColor, float shininess, float roughness, float intensity, float diffuseFac)
{
    float metallic = step(abs(GetRampLineIndex(surface.materialId) - GetMetalIndex()), 0.001);
    return CalculateSpecular(surface, light, viewDirWS, normalWS, specColor, shininess, roughness, intensity, diffuseFac, metallic);
}

half3 CalculateFaceSpecular(Surface surface, Light light, float3 viewDirWS, half3 normalWS, 
    half3 specColor, float shininess, float roughness, float intensity)
{
    return CalculateSpecular(surface, light, viewDirWS, normalWS, specColor, shininess, roughness, 1.0, intensity);
}

half3 CalculateHairSpecular(Surface surface, Light light, float3 viewDirWS, half3 normalWS, 
    half3 specColor, float shininess, float roughness, float intensity, float diffuseFac)
{
    return CalculateSpecular(surface, light, viewDirWS, normalWS, specColor, shininess, roughness, diffuseFac, intensity);
}

half3 CalculateEmission(Surface surface, float intensity, float threshold)
{
    return surface.color * intensity * step(threshold, surface.emission);
}

half3 CalculateRim(Surface surface, half3 rimColor, float3 positionWS, float3 normalWS, float4x4 matrixVP,
    float4 zBufferParams, float currDepth, float width, float widthMin, float widthMax)
{
    float3 samplePos = ExtendRim(positionWS, normalWS, width, widthMin, widthMax);
    float2 screenUV = ComputeNormalizedDeviceCoordinates(samplePos, matrixVP);
    float mapDepth = SampleCameraDepthTexture(screenUV);
    float mapDepthEye = LinearEyeDepth(mapDepth, zBufferParams);
    float curDepthEye = LinearEyeDepth(currDepth, zBufferParams);
    float isEdge = smoothstep(0.0, 3.0, mapDepthEye - curDepthEye);
    return rimColor * isEdge * 0.5;
}

half3 CalculateRim(Surface surface, half3 rimColor, float rimWidth, float intensity,
    float3 positionWS, float3 normalVS, float4x4 matrixVP,
    float4 zBufferParams, float currDepth)
{
    float signX = lerp(-1.0, 1.0, step(0.0, normalVS.x));
    float signY = lerp(1.0, -1.0, step(0.0, normalVS.y));
    float3 screenNDC = ComputeNormalizedDeviceCoordinatesWithZ(positionWS, matrixVP);
    float depth10 = saturate((10.0 - LinearEyeDepth(screenNDC.z, _ZBufferParams)) * 0.1);
    float mapDepth = SampleCameraDepthTexture(screenNDC.xy +
        float2(signX * rcp(360.0), signY * rcp(720.0)) * depth10 * rimWidth);
    float mapDepthEye = LinearEyeDepth(mapDepth, zBufferParams);
    float curDepthEye = LinearEyeDepth(currDepth, zBufferParams);
    float isEdge = smoothstep(0.0, 3.0, mapDepthEye - curDepthEye);
    return rimColor * isEdge * intensity;
}

half3 RenderFresnel(Surface surface)
{
    return half3(0.0, 0.0, 0.0);
}

half3 RenderDissolve(Surface surface)
{
    return half3(0.0, 0.0, 0.0);
}

#endif
