#ifndef CRP_UNLIT_PASS_INCLUDED
#define CRP_UNLIT_PASS_INCLUDED

#include "UnLitInput.hlsl"
#include "../../Deferred/HLSL/CRPGBuffer.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 uv           : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    float3 normalWS                 : TEXCOORD1;
    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings UnLitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.uv = GetTransformedUV(input.uv);
    output.normalWS = normalInput.normalWS;
    output.positionCS = vertexInput.positionCS;

    return output;
}

half4 UnLitForwardPassFragment(Varyings input) : SV_Target
{
    half4 color = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    half4 emission = _EmissionColor * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv);
    color += emission;
    return color;
}

FragmentOutputs UnLitGBufferPassFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    half4 color = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    half4 emission = _EmissionColor * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv);

    input.normalWS = IS_FRONT_VFACE(face, input.normalWS, -input.normalWS);

    half3 packedNormalWS = PackNormal(input.normalWS);

    FragmentOutputs output;
    output.GBuffer0 = half4(color.rgb, 0.0);
    output.GBuffer1 = half4(packedNormalWS, 0.0);      
    output.GBuffer2 = half4(color.rgb + emission.rgb, color.r);           
    
    return output;
}

#endif
