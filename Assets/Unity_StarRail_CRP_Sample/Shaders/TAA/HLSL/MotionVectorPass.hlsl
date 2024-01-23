#ifndef CRP_MOTION_VECTOR_PASS_INCLUDED
#define CRP_MOTION_VECTOR_PASS_INCLUDED

// -------------------------------------
// Includes
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

//#ifndef HAVE_VFX_MODIFICATION
//    #pragma multi_compile _ DOTS_INSTANCING_ON
//    #if UNITY_PLATFORM_ANDROID || UNITY_PLATFORM_WEBGL || UNITY_PLATFORM_UWP
//        #pragma target 3.5 DOTS_INSTANCING_ON
//    #else
//        #pragma target 4.5 DOTS_INSTANCING_ON
//    #endif
//#endif // HAVE_VFX_MODIFICATION
//    #if defined(_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
//        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
//    #endif

#include "../../Utils/HLSL/Depth.hlsl"

// -------------------------------------
// Structs
struct AttributesMV
{
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsMV
{
    float4 position : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

// -------------------------------------
// Vertex
VaryingsMV CameraMotionVectorVertex(AttributesMV input)
{
    VaryingsMV output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // TODO: Use Core Blitter vert.
    output.position = GetFullScreenTriangleVertexPosition(input.vertexID);
    return output;
}

#if defined(_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
// Non-uniform raster needs to keep the posNDC values in float to avoid additional conversions
// since uv remap functions use floats
#define POS_NDC_TYPE float2 
#else
#define POS_NDC_TYPE half2
#endif

// -------------------------------------
// Fragment
half4 CameraMotionVectorFragment(VaryingsMV input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 uv = input.position.xy / _ScaledScreenParams.xy;

    float depth = SampleDepth(uv);

    #if !UNITY_REVERSED_Z
    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleDepth(uv));
    #endif

    // Reconstruct world position
    float3 posWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

    // Multiply with current and previous non-jittered view projection
    float4 posCS = mul(_NonJitteredViewProjMatrix, float4(posWS.xyz, 1.0));
    float4 prevPosCS = mul(_PrevViewProjMatrix, float4(posWS.xyz, 1.0));

    half2 posNDC = posCS.xy * rcp(posCS.w);
    half2 prevPosNDC = prevPosCS.xy * rcp(prevPosCS.w);

    // Calculate forward velocity
    half2 velocity = (posNDC - prevPosNDC);

    // TODO: test that velocity.y is correct
    #if UNITY_UV_STARTS_AT_TOP
    velocity.y = -velocity.y;
    #endif

    // Convert velocity from NDC space (-1..1) to screen UV 0..1 space
    // Note: It doesn't mean we don't have negative values, we store negative or positive offset in the UV space.
    // Note: ((posNDC * 0.5 + 0.5) - (prevPosNDC * 0.5 + 0.5)) = (velocity * 0.5)
    velocity.xy *= 0.5;

    return half4(velocity, 0, 0);
}

// Per Object Motion Vector
// ------------------------------------------------------------------------------------------------------------
struct PerObjectMotionVectorPassVertexInput
{
    float4 positionOS;
    float4 positionCS;
    float3 positionOld;
};

struct PerObjectMotionVectorPassVertexOutput
{
    float4 positionCS                 : SV_POSITION;
    float4 positionCSNoJitter         : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

PerObjectMotionVectorPassVertexOutput ObjectMotionVectorPassVertex(PerObjectMotionVectorPassVertexInput input)
{
    PerObjectMotionVectorPassVertexOutput output = (PerObjectMotionVectorPassVertexOutput)0;
    
    // Jittered. Match the frame.
    output.positionCS = input.positionCS;

    // This is required to avoid artifacts ("gaps" in the _MotionVectorTexture) on some platforms
    #if defined(UNITY_REVERSED_Z)
        output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
    #else
        output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
    #endif

    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));

    const float4 prevPos = (unity_MotionVectorsParams.x == 1) ? float4(input.positionOld, 1) : input.positionOS;
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, mul(UNITY_PREV_MATRIX_M, prevPos));

    return output;
}

half4 ObjectMotionVectorPassFragment(PerObjectMotionVectorPassVertexOutput input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    
    // Note: unity_MotionVectorsParams.y is 0 is forceNoMotion is enabled
    bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
    if (forceNoMotion)
    {
        return half4(0.0, 0.0, 0.0, 0.0);
    }

    // Calculate positions
    float4 posCS = input.positionCSNoJitter;
    float4 prevPosCS = input.previousPositionCSNoJitter;

    POS_NDC_TYPE posNDC = posCS.xy * rcp(posCS.w);
    POS_NDC_TYPE prevPosNDC = prevPosCS.xy * rcp(prevPosCS.w);

    #if defined(_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
        // Convert velocity from NDC space (-1..1) to screen UV 0..1 space since FoveatedRendering remap needs that range.
        half2 posUV = RemapFoveatedRenderingResolve(posNDC * 0.5 + 0.5);
        half2 prevPosUV = RemapFoveatedRenderingPrevFrameResolve(prevPosNDC * 0.5 + 0.5);
                    
        // Calculate forward velocity
        half2 velocity = (posUV - prevPosUV);
    #if UNITY_UV_STARTS_AT_TOP
        velocity.y = -velocity.y;
    #endif
    #else
    // Calculate forward velocity
    half2 velocity = (posNDC.xy - prevPosNDC.xy);
    #if UNITY_UV_STARTS_AT_TOP
    velocity.y = -velocity.y;
    #endif

    // Convert velocity from NDC space (-1..1) to UV 0..1 space
    // Note: It doesn't mean we don't have negative values, we store negative or positive offset in UV space.
    // Note: ((posNDC * 0.5 + 0.5) - (prevPosNDC * 0.5 + 0.5)) = (velocity * 0.5)
    velocity.xy *= 0.5;
    #endif
    return half4(velocity, 0, 0);
}

#endif
