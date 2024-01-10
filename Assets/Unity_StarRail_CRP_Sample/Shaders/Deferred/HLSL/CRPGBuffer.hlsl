#ifndef CRP_GBUFFER_INCLUDED
#define CRP_GBUFFER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// inspired from [builtin_shaders]/CGIncludes/UnityGBuffer.cginc

// Non-static meshes with real-time lighting need to write shadow mask, which in that case stores per-object occlusion probe values.
#if !defined(LIGHTMAP_ON) && defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK)
#define OUTPUT_SHADOWMASK 1 // subtractive
#elif defined(SHADOWS_SHADOWMASK)
#define OUTPUT_SHADOWMASK 2 // shadow mask
#elif defined(_DEFERRED_MIXED_LIGHTING)
#define OUTPUT_SHADOWMASK 3 // we don't know if it's subtractive or just shadowMap (from deferred lighting shader, LIGHTMAP_ON does not need to be defined)
#else
#endif

#if _RENDER_PASS_ENABLED
    #define GBUFFER_OPTIONAL_SLOT_1 GBuffer4
    #define GBUFFER_OPTIONAL_SLOT_1_TYPE float
#if OUTPUT_SHADOWMASK && defined(_LIGHT_LAYERS)
    #define GBUFFER_OPTIONAL_SLOT_2 GBuffer5
    #define GBUFFER_OPTIONAL_SLOT_3 GBuffer6
    #define GBUFFER_LIGHT_LAYERS GBuffer5
    #define GBUFFER_SHADOWMASK GBuffer6
#elif OUTPUT_SHADOWMASK
    #define GBUFFER_OPTIONAL_SLOT_2 GBuffer5
    #define GBUFFER_SHADOWMASK GBuffer5
#elif defined(_LIGHT_LAYERS)
    #define GBUFFER_OPTIONAL_SLOT_2 GBuffer5
    #define GBUFFER_LIGHT_LAYERS GBuffer5
#endif //#if OUTPUT_SHADOWMASK && defined(_LIGHT_LAYERS)
#else
    #define GBUFFER_OPTIONAL_SLOT_1_TYPE half4
#if OUTPUT_SHADOWMASK && defined(_LIGHT_LAYERS)
    #define GBUFFER_OPTIONAL_SLOT_1 GBuffer4
    #define GBUFFER_OPTIONAL_SLOT_2 GBuffer5
    #define GBUFFER_LIGHT_LAYERS GBuffer4
    #define GBUFFER_SHADOWMASK GBuffer5
#elif OUTPUT_SHADOWMASK
    #define GBUFFER_OPTIONAL_SLOT_1 GBuffer4
    #define GBUFFER_SHADOWMASK GBuffer4
#elif defined(_LIGHT_LAYERS)
    #define GBUFFER_OPTIONAL_SLOT_1 GBuffer4
    #define GBUFFER_LIGHT_LAYERS GBuffer4
#endif //#if OUTPUT_SHADOWMASK && defined(_LIGHT_LAYERS)
#endif //#if _RENDER_PASS_ENABLED
#define kLightingInvalid  -1  // No dynamic lighting: can aliase any other material type as they are skipped using stencil
#define kLightingLit       1  // lit shader
#define kLightingSimpleLit 2  // Simple lit shader
// clearcoat 3
// backscatter 4
// skin 5

// Material flags
#define kMaterialFlagReceiveShadowsOff        1 // Does not receive dynamic shadows
#define kMaterialFlagSpecularHighlightsOff    2 // Does not receivce specular
#define kMaterialFlagSubtractiveMixedLighting 4 // The geometry uses subtractive mixed lighting
#define kMaterialFlagSpecularSetup            8 // Lit material use specular setup instead of metallic setup

// Light flags.
#define kLightFlagSubtractiveMixedLighting    4 // The light uses subtractive mixed lighting.

struct FragmentOutput
{
    half4 GBuffer0 : SV_Target0;
    half4 GBuffer1 : SV_Target1;
    half4 GBuffer2 : SV_Target2; // Camera color attachment

    #ifdef GBUFFER_OPTIONAL_SLOT_1
    GBUFFER_OPTIONAL_SLOT_1_TYPE GBuffer3 : SV_Target3;
    #endif
    #ifdef GBUFFER_OPTIONAL_SLOT_2
    half4 GBuffer4 : SV_Target4;
    #endif
    #ifdef GBUFFER_OPTIONAL_SLOT_3
    half4 GBuffer5 : SV_Target5;
    #endif
};

float PackMaterialFlags(uint materialFlags)
{
    return materialFlags * (1.0h / 255.0h);
}

uint UnpackMaterialFlags(float packedMaterialFlags)
{
    return uint((packedMaterialFlags * 255.0h) + 0.5h);
}

#ifdef _GBUFFER_NORMALS_OCT
half3 PackNormal(half3 n)
{
    float2 octNormalWS = PackNormalOctQuadEncode(n);                  // values between [-1, +1], must use fp32 on some platforms.
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);   // values between [ 0, +1]
    return half3(PackFloat2To888(remappedOctNormalWS));               // values between [ 0, +1]
}

half3 UnpackNormal(half3 pn)
{
    half2 remappedOctNormalWS = half2(Unpack888ToFloat2(pn));          // values between [ 0, +1]
    half2 octNormalWS = remappedOctNormalWS.xy * half(2.0) - half(1.0);// values between [-1, +1]
    return half3(UnpackNormalOctQuadEncode(octNormalWS));              // values between [-1, +1]
}

#else
half3 PackNormal(half3 n)
{ return n; }                                                         // values between [-1, +1]

half3 UnpackNormal(half3 pn)
{ return pn; }                                                        // values between [-1, +1]
#endif

InputData InputDataFromGBufferAndWorldPosition(half4 gbuffer1, float3 wsPos)
{
    InputData inputData = (InputData)0;

    inputData.positionWS = wsPos;
    inputData.normalWS = normalize(UnpackNormal(gbuffer1.xyz)); // normalize() is required because terrain shaders use additive blending for normals (not unit-length anymore)

    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(wsPos.xyz);

    // TODO: pass this info?
    inputData.shadowCoord     = (float4)0;
    inputData.fogCoord        = (half  )0;
    inputData.vertexLighting  = (half3 )0;

    inputData.bakedGI = (half3)0; // Note: this is not made available at lighting pass in this renderer - bakedGI contribution is included (with emission) in the value GBuffer3.rgb, that is used as a renderTarget during lighting

    return inputData;
}


    

// This will encode SurfaceData into GBuffer
FragmentOutput SceneSurfaceDataToGBuffer(SurfaceData surfaceData, InputData inputData, half3 globalIllumination, float bloomIntensity)
{
    half3 packedNormalWS = PackNormal(inputData.normalWS);

    FragmentOutput output;
    output.GBuffer0 = half4(surfaceData.albedo.rgb, surfaceData.metallic);  // albedo          albedo          albedo          metallic  (sRGB render target)
    output.GBuffer1 = half4(packedNormalWS, surfaceData.smoothness);        // encoded-normal  encoded-normal  encoded-normal  smoothness
    output.GBuffer2 = half4(globalIllumination, bloomIntensity);            // GI              GI              GI              bloom intensity

    return output;
}

// This decodes the Gbuffer into a SurfaceData struct
SurfaceData SceneSurfaceDataFromGBuffer(half4 gbuffer0, half4 gbuffer1)
{
    SurfaceData surfaceData = (SurfaceData)0;

    surfaceData.albedo = gbuffer0.rgb;
    surfaceData.metallic = gbuffer0.a;

    surfaceData.smoothness = gbuffer1.a;
    
    surfaceData.occlusion = 1.0; // Not used by SimpleLit material.
    surfaceData.specular = 0.0;
    
    surfaceData.alpha = 1.0; // gbuffer only contains opaque materials

    surfaceData.emission = (half3)0; // Note: this is not made available at lighting pass in this renderer - emission contribution is included (with GI) in the value GBuffer3.rgb, that is used as a renderTarget during lighting
    surfaceData.normalTS = (half3)0; // Note: does this normalTS member need to be in SurfaceData? It looks like an intermediate value

    return surfaceData;
}

// This will encode SurfaceData into GBuffer
FragmentOutput SceneBRDFDataToGBuffer(BRDFData brdfData, InputData inputData, half smoothness, half3 globalIllumination, float bloomIntensity)
{
    half3 packedNormalWS = PackNormal(inputData.normalWS);
    
    FragmentOutput output = (FragmentOutput)0;
    output.GBuffer0 = half4(brdfData.albedo.rgb, brdfData.reflectivity);  // albedo          albedo          albedo          metallic  (sRGB render target)
    output.GBuffer1 = half4(packedNormalWS, smoothness);                  // encoded-normal  encoded-normal  encoded-normal  smoothness
    output.GBuffer2 = half4(globalIllumination, bloomIntensity);          // GI              GI              GI                        (camera color attachment)

    return output;
}

// This decodes the Gbuffer into a SurfaceData struct
BRDFData SceneBRDFDataFromGBuffer(half4 gbuffer0, half4 gbuffer1)
{
    half3 albedo = gbuffer0.rgb;
    half smoothness = gbuffer1.a;

    BRDFData brdfData = (BRDFData)0;
    half alpha = half(1.0); // NOTE: alpha can get modfied, forward writes it out (_ALPHAPREMULTIPLY_ON).

    half reflectivity = gbuffer0.a;
    half oneMinusReflectivity = 1.0 - reflectivity;
    half metallic = MetallicFromReflectivity(reflectivity);
    half3 brdfDiffuse = albedo * oneMinusReflectivity;
    half3 brdfSpecular = lerp(kDieletricSpec.rgb, albedo, metallic);
    
    InitializeBRDFDataDirect(albedo, brdfDiffuse, brdfSpecular, reflectivity, oneMinusReflectivity, smoothness, alpha, brdfData);

    return brdfData;
}
    
#endif // CRP_GBUFFER_INCLUDED
