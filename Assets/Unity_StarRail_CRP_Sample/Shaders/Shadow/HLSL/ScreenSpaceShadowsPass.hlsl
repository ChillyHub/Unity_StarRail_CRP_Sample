#ifndef CRP_SCENE_SPACE_SHADOW_INCLUDED
#define CRP_SCENE_SPACE_SHADOW_INCLUDED

//Keep compiler quiet about Shadows.hlsl.
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
// Core.hlsl for XR dependencies
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#include "../../Deferred/HLSL/CRPDeferred.hlsl"

#include "CharacterShadow.hlsl"

CBUFFER_START(UnityPerMaterial)
float _Index;
float3 _LightPosWS;
half4 _LightAttenuation; // .xy are used by DistanceAttenuation - .zw are used by AngleAttenuation *for SpotLights)
half3 _LightDirection;   // directional/spotLights support
half4 _LightOcclusionProbInfo;
float4 _SpotLightBias;
float4 _SpotLightScale;
float4 _SpotLightGuard;
int _ShadowLightIndex;
CBUFFER_END

struct Attributes1
{
    float4 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings1
{
    float4 positionCS : SV_POSITION;
    float3 screenUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings1 VolumeVertex(Attributes1 input)
{
    Varyings1 output = (Varyings1)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = input.positionOS.xyz;

    #if defined(_SPOT)
    // Spot lights have an outer angle than can be up to 180 degrees, in which case the shape
    // becomes a capped hemisphere. There is no affine transforms to handle the particular cone shape,
    // so instead we will adjust the vertices positions in the vertex shader to get the tighest fit.
    [flatten] if (any(positionOS.xyz))
    {
        // The hemisphere becomes the rounded cap of the cone.
        positionOS.xyz = _SpotLightBias.xyz + _SpotLightScale.xyz * positionOS.xyz;
        positionOS.xyz = normalize(positionOS.xyz) * _SpotLightScale.w;
        // Slightly inflate the geometry to fit the analytic cone shape.
        // We want the outer rim to be expanded along xy axis only, while the rounded cap is extended along all axis.
        positionOS.xyz = (positionOS.xyz - float3(0, 0, _SpotLightGuard.w)) * _SpotLightGuard.xyz + float3(0, 0, _SpotLightGuard.w);
    }
    #endif
    
    // Light shape geometry is projected as normal.
    VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS.xyz);
    output.positionCS = vertexInput.positionCS;

    output.screenUV = output.positionCS.xyw;
    #if UNITY_UV_STARTS_AT_TOP
        output.screenUV.xy = output.screenUV.xy * float2(0.5, -0.5) + 0.5 * output.screenUV.z;
    #else
        output.screenUV.xy = output.screenUV.xy * 0.5 + 0.5 * output.screenUV.z;
    #endif

    return output;
}

half4 WhiteFragment(Varyings1 input) : SV_Target
{
    return half4(1.0, 1.0, 1.0, 1.0);
}

half4 DirectionalShadowFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    #if UNITY_REVERSED_Z
    float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, input.texcoord.xy).r;
    #else
    float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, input.texcoord.xy).r;
    deviceDepth = deviceDepth * 2.0 - 1.0;
    #endif

    //Fetch shadow coordinates for cascade.
    float3 wpos = ComputeWorldSpacePosition(input.texcoord.xy, deviceDepth, unity_MatrixInvVP);
    float4 coords = TransformWorldToShadowCoord(wpos);

    // Screenspace shadowmap is only used for directional lights which use orthogonal projection.
    half realtimeShadow = MainLightRealtimeShadow(coords);

    return realtimeShadow;
}

half4 AdditionalShadowFragment(Varyings1 input) : SV_Target
{
    float2 uv = input.screenUV.xy / input.screenUV.z;
    
    #if UNITY_REVERSED_Z
    float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv).r;
    #else
    float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv).r;
    deviceDepth = deviceDepth * 2.0 - 1.0;
    #endif

    //Fetch shadow coordinates for cascade.
    float3 wpos = ComputeWorldSpacePosition(uv, deviceDepth, unity_MatrixInvVP);
    float3 lightVector = _LightPosWS.xyz - wpos;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);
    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));

    // full-float precision required on some platforms
    float attenuation = DistanceAttenuation(distanceSqr, _LightAttenuation.xy) *
        AngleAttenuation(_LightDirection.xyz, lightDirection, _LightAttenuation.zw);
    
    half additionalShadow = AdditionalLightShadow(_ShadowLightIndex, wpos, lightDirection, 1.0, _LightOcclusionProbInfo);

    return additionalShadow;// lerp(1.0, additionalShadow, attenuation);
}

half4 CharacterShadowFragment(Varyings1 input) : SV_Target
{
    float2 uv = input.screenUV.xy / input.screenUV.z;
    
    #if UNITY_REVERSED_Z
    float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv).r;
    #else
    float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv).r;
    deviceDepth = deviceDepth * 2.0 - 1.0;
    #endif

    //Fetch shadow coordinates for cascade.
    float3 wpos = ComputeWorldSpacePosition(uv, deviceDepth, unity_MatrixInvVP);
    half characterShadow = CharacterRealtimeShadow(wpos, _Index);

    return characterShadow;
}

#endif