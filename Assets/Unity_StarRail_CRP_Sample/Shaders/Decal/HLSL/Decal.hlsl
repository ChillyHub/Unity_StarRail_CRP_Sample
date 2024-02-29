#ifndef CRP_DECAL_INCLUDED
#define CRP_DECAL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "../../Deferred/HLSL/CRPDeferred.hlsl"
    
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#include "../../Utils/HLSL/Depth.hlsl"
    
#include "../../Scene/HLSL/SceneLighting.hlsl"
#include "../../Scene/HLSL/SceneShadow.hlsl"
#include "../../SSS/HLSL/SSSLighting.hlsl"

Light GetDecalLight(float3 normalWS, float3 mask, float2 screenUV, float4 lightColor)
{
    float4 shadowCoord = float4(screenUV, 0.0, 1.0);
    
    Light unityLight = (Light)0;
    unityLight.distanceAttenuation = 1.0;
    unityLight.direction = normalWS;
    unityLight.shadowAttenuation = SampleScreenSpaceShadowmap(shadowCoord);;
    unityLight.color = mask * lightColor;

    return unityLight;
}

#endif // CRP_DECAL_INCLUDED
