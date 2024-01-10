#ifndef CRP_SSS_LIGHTING_INCLUDED
#define CRP_SSS_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#include "../../Deferred/HLSL/CRPGBuffer.hlsl"

struct SSSData
{
    half3 albedo;
    half thickness;
    half distortion;
    half power;
    half intensity;
};

// This will encode SurfaceData into GBuffer
FragmentOutput SSSBRDFDataToGBuffer(BRDFData brdfData, SSSData sssData, InputData inputData, half smoothness, half3 globalIllumination, float bloomIntensity)
{
    half3 packedNormalWS = PackNormal(inputData.normalWS);
    
    FragmentOutput output = (FragmentOutput)0;
    output.GBuffer0 = half4(brdfData.albedo.rgb, sssData.thickness);      // albedo          albedo          albedo          metallic  (sRGB render target)
    output.GBuffer1 = half4(packedNormalWS, smoothness);                  // encoded-normal  encoded-normal  encoded-normal  smoothness
    output.GBuffer2 = half4(globalIllumination, bloomIntensity);          // GI              GI              GI                        (camera color attachment)

    return output;
}

// This decodes the Gbuffer into a SurfaceData struct
BRDFData SSSBRDFDataFromGBuffer(half4 gbuffer0, half4 gbuffer1)
{
    half3 albedo = gbuffer0.rgb;
    half smoothness = gbuffer1.a;

    BRDFData brdfData = (BRDFData)0;
    half alpha = half(1.0); // NOTE: alpha can get modfied, forward writes it out (_ALPHAPREMULTIPLY_ON).

    half reflectivity = kDieletricSpec.r;
    half oneMinusReflectivity = 1.0 - reflectivity;
    half3 brdfDiffuse = albedo * kDielectricSpec.a;
    half3 brdfSpecular = kDieletricSpec.rgb;
    
    InitializeBRDFDataDirect(albedo, brdfDiffuse, brdfSpecular, reflectivity, oneMinusReflectivity, smoothness, alpha, brdfData);

    return brdfData;
}

// This decodes the Gbuffer into a SurfaceData struct
SSSData SSSDataFromGBuffer(half4 gbuffer0)
{
    SSSData sssData = (SSSData)0;
    sssData.albedo = gbuffer0.rgb;
    sssData.thickness = gbuffer0.a;
    sssData.power = 2.0;
    sssData.intensity = 1.0;

    return sssData;
}

half3 GetSubsurfaceScatteringColor(InputData inputData, SSSData sssData, Light light)
{
    float3 lightDir = light.direction;
    float3 viewDir = inputData.viewDirectionWS;
    float3 normal = inputData.normalWS;

    float3 h = normalize(-lightDir + normal * sssData.distortion);
    float scatter = pow(saturate(dot(viewDir, h)), sssData.power) * sssData.thickness * sssData.intensity;

    return sssData.albedo * scatter;
}

half3 SSSLighting(InputData input, half4 gBuffer0, half4 gBuffer1, Light light, bool specularOff)
{
    BRDFData brdfData = SSSBRDFDataFromGBuffer(gBuffer0, gBuffer1);
    SSSData sssData = SSSDataFromGBuffer(gBuffer0);
    
    half3 pbr = LightingPhysicallyBased(brdfData, light, input.normalWS, input.viewDirectionWS, specularOff);
    half3 scatter = GetSubsurfaceScatteringColor(input, sssData, light);
    return pbr + scatter;
}

#endif
