#ifndef CRP_UNLIT_INPUT_INCLUDED
#define CRP_UNLIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseColor;
float4 _EmissionColor;
half _Cutoff;
CBUFFER_END

float2 GetTransformedUV(float2 uv)
{
    return TRANSFORM_TEX(uv, _BaseMap);
}

#endif
