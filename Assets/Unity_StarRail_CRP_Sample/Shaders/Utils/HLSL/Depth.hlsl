#ifndef CRP_DEPTH_INCLUDED
#define CRP_DEPTH_INCLUDED

TEXTURE2D(_DepthPyramidTexture);
SAMPLER(sampler_DepthPyramidTexture);

float4 _DepthPyramidTexture_TexelSize;
float4 _MipLevelParam[15];

float SampleDepth(float2 uv, int mip)
{
    float4 param = _MipLevelParam[mip];
    int2 index = int2(param.xy) + int2(floor(uv * param.zw));
    return LOAD_TEXTURE2D(_DepthPyramidTexture, index);
}

float SampleDepth(float2 uv)
{
    return SampleDepth(uv, 0);
}

#endif
