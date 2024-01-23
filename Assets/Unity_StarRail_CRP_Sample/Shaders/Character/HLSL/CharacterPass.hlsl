#ifndef CRP_CHARACTER_PASS_INCLUDED
#define CRP_CHARACTER_PASS_INCLUDED

#include "CharacterInput.hlsl"
#include "CharacterFunction.hlsl"
#include "../../Deferred/HLSL/CRPGBuffer.hlsl"
#include "../../TAA/HLSL/MotionVectorPass.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float4 color : COLOR;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 baseUV : TEXCOORD0;
    float2 addUV : TEXCOORD1;
    float2 packSmoothNormal : TEXCOORD2;
    float3 positionOld : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionVS : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float4 positionNDC : TEXCOORD2;
    float2 baseUV : TEXCOORD3;
    float2 addUV : TEXCOORD4;
    half3 color : TEXCOORD5;
    half3 normalWS : TEXCOORD6;
    half3 tangentWS : TEXCOORD7;
    half3 bitangentWS : TEXCOORD8;

    half3 sh : TEXCOORD9;

    float2 packSmoothNormal : TEXCOORD10;
    float4 positionCSNoJitter : TEXCOORD11;
    float4 previousPositionCSNoJitter : TEXCOORD12;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 GetSmoothNormalWS(Attributes input)
{
    float3 smoothNormalOS = input.normalOS;
    
    #ifdef _OUTLINENORMALCHANNEL_NORMAL
        smoothNormalOS = input.normalOS;
    #elif _OUTLINENORMALCHANNEL_TANGENT
        smoothNormalOS = input.tangentOS.xyz;
    #elif _OUTLINENORMALCHANNEL_UV2
        float3 normalOS = normalize(input.normalOS);
        float3 tangentOS = normalize(input.tangentOS.xyz);
        float3 bitangentOS = normalize(cross(normalOS, tangentOS) * (input.tangentOS.w * GetOddNegativeScale()));
        float3 smoothNormalTS = UnpackNormalOctQuadEncode(input.packSmoothNormal);
        smoothNormalOS = mul(smoothNormalTS, float3x3(tangentOS, bitangentOS, normalOS));
    #endif

    return TransformObjectToWorldNormal(smoothNormalOS);
}

Varyings CharacterCommonPassVertex(Attributes input)
{
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    Varyings output = (Varyings)0;
    output.positionCS = vertexInput.positionCS;
    output.positionVS = vertexInput.positionVS;
    output.positionWS = vertexInput.positionWS;
    output.positionNDC = vertexInput.positionNDC;
    output.baseUV = input.baseUV;
    output.addUV = input.addUV;
    output.color = input.color.rgb;
    output.normalWS = normalInput.normalWS;
    output.tangentWS = normalInput.tangentWS;
    output.bitangentWS = normalInput.bitangentWS;

    output.sh = SampleSH(lerp(output.normalWS, f3zero, _GI_Flatten));
    output.packSmoothNormal = input.packSmoothNormal;

    return output;
}

Varyings CharacterOutlinePassVertex(Attributes input)
{
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    float3 smoothNormalWS = GetSmoothNormalWS(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

    float outlineWidth = input.color.a;
    #if defined(_IS_FACE)
    outlineWidth *= lerp(1.0,
        saturate(0.4 - dot(_HeadForward.xz, normalize(GetCameraPositionWS() - positionWS).xz)), step(0.5, input.color.b));
    #endif
    
    positionWS = ExtendOutline(positionWS, smoothNormalWS,
        _OutlineWidth * outlineWidth, _OutlineWidthMin * outlineWidth, _OutlineWidthMax * outlineWidth);

    float3 positionVS = TransformWorldToView(positionWS);
    float4 positionCS = TransformWorldToHClip(positionWS);

    float4 positionNDC;
    float4 ndc = positionCS * 0.5f;
    positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    positionNDC.zw = positionCS.zw;

    Varyings output = (Varyings)0;
    output.positionCS = positionCS;
    output.positionVS = positionVS;
    output.positionWS = positionWS;
    output.positionNDC = positionNDC;
    output.baseUV = input.baseUV;
    output.color = input.color.rgb;
    output.normalWS = normalInput.normalWS;
    output.tangentWS = normalInput.tangentWS;
    output.bitangentWS = normalInput.bitangentWS;

    output.sh = 0;
    output.packSmoothNormal = input.packSmoothNormal;

    return output;
}

float3 TotalColor(float3 gi, float3 addLight, float3 diffuse, float3 specular, float3 emission,
    float3 rim, float3 stocking = f3zero)
{
    half3 resultColor = f3zero;
    #ifdef _ENABLE_GI
        resultColor += gi;
    #endif
    #ifdef _ENABLE_ADDITIONAL_LIGHT
        resultColor += addLight;
    #endif
    #ifdef _ENABLE_DIFFUSE
        resultColor += diffuse;
    #endif
    #ifdef _ENABLE_SPECULAR
        resultColor += specular;
    #endif
    #ifdef _ENABLE_EMISSION
        resultColor += emission;
    #endif
    #ifdef _ENABLE_RIM
        resultColor += rim;
    #endif
    #ifdef _WITH_STOCKING
        resultColor += stocking;
    #endif

    return resultColor;
}

float4 CharacterBaseFragment(Varyings input, half4 mainTex, half4 lightMap)
{
    // Optional
    half4 stockingMap = SampleStockingMap(input.baseUV);
    half4 stockingMapB = SampleStockingMap(input.baseUV * 100.0);

    // Construct Surface
    Surface surface;
    surface.color = mainTex.rgb;
    surface.alpha = mainTex.a;
    surface.emission = 0.0;
    surface.specularIntensity = lightMap.r;
    surface.diffuseThreshold = lightMap.g;
    surface.specularThreshold = lightMap.b;
    surface.materialId = lightMap.a;

    #ifdef _USE_NORMAL_MAP
        surface.normalTS = SampleNormalMap(input.baseUV);
    #endif

    #ifdef _WITH_STOCKING
        surface.stockMask = stockingMap.r;
        surface.stockThickness = stockingMap.g;
        surface.stockDetail = stockingMapB.b;
    #endif
    
    Light light = GetCharacterLight(input.positionWS);
    half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

    half3 stocking = RenderStocking(surface, input.normalWS, viewDirWS,
        _StockBrightColor.rgb, _StockDarkColor.rgb, _StockPower, _StockDarkWidth, _StockThickness);

    float diffuseFac = 0.0;
    half3 gi = CalculateGI(surface, input.sh, _GI_Intensity, _GI_UseMainColor);
    half3 addLight = CalculateAdditionalLight(surface, input.positionWS);
    half3 diffuse = CalculateBaseDiffuse(diffuseFac, surface, light, input.normalWS,
        _ShadowRamp, _ShadowOffset, _ShadowBoost, stocking);
    half3 specular = CalculateBaseSpecular(surface, light, viewDirWS, input.normalWS,
        GetSpecularColor(surface.materialId),
        GetSpecularShininess(surface.materialId),
        GetSpecularRoughness(surface.materialId),
        GetSpecularIntensity(surface.materialId), diffuseFac);
    half3 emission = CalculateEmission(surface, _EmissionIntensity, _EmissionThreshold);
    // half3 rim = CalculateRim(surface, GetRimColor(surface.materialId), input.positionWS, input.normalWS,
    //     UNITY_MATRIX_VP, _ZBufferParams, input.positionCS.z,
    //     _OutlineWidth, _OutlineRealWidthMin, _OutlineRealWidthMax);
    half3 rim = CalculateRim(surface, GetRimColor(surface.materialId, surface.color), GetRimWidth(surface.materialId),
        _RimIntensity, input.positionWS, TransformWorldToViewDir(input.normalWS),
        UNITY_MATRIX_VP, _ZBufferParams, input.positionCS.z);

    half3 bloom = GetBloomColor(surface.materialId, surface.color) * GetBloomIntensity(surface.materialId);
    
    // Total
    half3 resultColor = TotalColor(gi, addLight, diffuse, specular, emission, rim) + bloom;
    
    return float4(resultColor, surface.alpha);
}

float4 CharacterFaceFragment(Varyings input, half4 mainTex)
{
    // Sample textures
    half4 faceMap = SampleFaceMap(input.baseUV);
    half4 faceExpressionMap = SampleFaceExpressionMap(input.baseUV);

    // Construct Surface
    Surface surface;
    surface.color = mainTex.rgb;
    surface.alpha = 1.0;
    surface.emission = mainTex.a;
    surface.specularIntensity = faceMap.r;
    surface.diffuseThreshold = faceMap.g;
    surface.specularThreshold = 0.0;
    surface.materialId = 0.0;

    FaceData faceData;
    faceData.specularFac = faceMap.r;
    faceData.aoFac = faceMap.g;
    faceData.outlineFac = faceMap.b;
    faceData.sdf = faceMap.a;
    faceData.cheek = faceExpressionMap.r * _CheckIntensity;
    faceData.shy = faceExpressionMap.g * _ShyIntensity;
    faceData.shadow = faceExpressionMap.b * _ShadowIntensity;

    #ifdef _USE_NORMAL_MAP
        surface.normalTS = SampleNormalMap(input.baseUV);
    #endif
    
    Light light = GetCharacterLight(input.positionWS);
    half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

    half3 gi = CalculateGI(surface, input.sh, _GI_Intensity, _GI_UseMainColor);
    half3 addLight = CalculateAdditionalLight(surface, input.positionWS);
    half3 diffuse = CalculateFaceDiffuse(surface, faceData, light, viewDirWS, input.baseUV,
        _HeadForward, _HeadRight, _HeadUp, _ShadowRamp, _ShadowOffset, _ShadowBoost);
    half3 specular = CalculateFaceSpecular(surface, light, viewDirWS, input.normalWS,
        GetSpecularColor(), GetSpecularShininess(), GetSpecularRoughness(), GetSpecularIntensity());
    half3 emission = CalculateEmission(surface, _EmissionIntensity, _EmissionThreshold);
    // half3 rim = CalculateRim(surface, GetRimColor(surface.materialId), input.positionWS, input.normalWS,
    //     UNITY_MATRIX_VP, _ZBufferParams, input.positionCS.z,
    //     _OutlineWidth, _OutlineRealWidthMin, _OutlineRealWidthMax);
    half3 rim = CalculateRim(surface, GetRimColor(surface.color), GetRimWidth(), _RimIntensity, input.positionWS,
        TransformWorldToViewDir(input.normalWS), UNITY_MATRIX_VP, _ZBufferParams, input.positionCS.z);

    half3 bloom = GetBloomColor(surface.color) * GetBloomIntensity();

    // Total
    half3 resultColor = TotalColor(gi, addLight, diffuse, specular, emission, rim) + bloom;
    
    return float4(resultColor, surface.alpha);
}

float4 CharacterHairFragment(Varyings input, half4 mainTex, half4 lightMap)
{
    // Construct Surface
    Surface surface;
    surface.color = mainTex.rgb;
    surface.alpha = mainTex.a;
    surface.emission = 0.0;
    surface.specularIntensity = lightMap.r;
    surface.diffuseThreshold = lightMap.g;
    surface.specularThreshold = lightMap.b;
    surface.materialId = lightMap.a;

    #ifdef _USE_NORMAL_MAP
    surface.normalTS = SampleNormalMap(input.baseUV);
    #endif
    
    Light light = GetCharacterLight(input.positionWS);
    half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

    float diffuseFac = 0.0;
    half3 gi = CalculateGI(surface, input.sh, _GI_Intensity, _GI_UseMainColor);
    half3 addLight = CalculateAdditionalLight(surface, input.positionWS);
    half3 diffuse = CalculateHairDiffuse(diffuseFac, surface, light, input.normalWS, //normalize(input.positionWS - _HeadCenter),
        _ShadowRamp, _ShadowOffset, _ShadowBoost);
    half3 specular = CalculateHairSpecular(surface, light, viewDirWS, input.normalWS,
        GetSpecularColor(), GetSpecularShininess(), GetSpecularRoughness(), GetSpecularIntensity(), diffuseFac);
    half3 emission = CalculateEmission(surface, _EmissionIntensity, _EmissionThreshold);
    // half3 rim = CalculateRim(surface, GetRimColor(surface.materialId), input.positionWS, input.normalWS,
    //     UNITY_MATRIX_VP, _ZBufferParams, input.positionCS.z,
    //     _OutlineWidth, _OutlineRealWidthMin, _OutlineRealWidthMax);
    half3 rim = CalculateRim(surface, GetRimColor(surface.color), GetRimWidth(), _RimIntensity, input.positionWS,
        TransformWorldToViewDir(input.normalWS), UNITY_MATRIX_VP, _ZBufferParams, input.positionCS.z);

    half3 bloom = GetBloomColor(surface.color) * GetBloomIntensity();

    // Total
    half3 resultColor = TotalColor(gi, addLight, diffuse, specular, emission, rim) + bloom;
    
    return float4(resultColor, surface.alpha);
}

float4 CharacterBaseForwardPassFragment(Varyings input) : SV_Target
{
    half4 mainTex = SampleMainTex(input.baseUV);
    half4 lightMap = SampleLightMap(input.baseUV);
    
    return CharacterBaseFragment(input, mainTex, lightMap);
}

FragmentOutputs CharacterBaseGBufferPassFragment(Varyings input) : SV_Target
{
    half4 mainTex = SampleMainTex(input.baseUV);
    half4 lightMap = SampleLightMap(input.baseUV);
    
    FragmentOutputs output = (FragmentOutputs)0;
    output.GBuffer0 = half4(mainTex.rgb, 0.0);
    output.GBuffer1 = half4(PackNormal(input.normalWS), input.positionCS.z);
    output.GBuffer2 = CharacterBaseFragment(input, mainTex, lightMap);

    return output;
}

float4 CharacterBaseOutlinePassFragment(Varyings input) : SV_Target
{
    #ifndef _ENABLE_OUTLINE
        clip(-1.0);
    #endif

    Light light = GetCharacterLight(input.positionWS);
    
    half4 mainTex = SampleMainTex(input.baseUV);
    half4 lightMap = SampleLightMap(input.baseUV);

    return float4(GetOutlineColor(lightMap.a, mainTex.rgb) * light.color, 1.0);
}

FragmentOutputs CharacterBaseGBufferOutlinePassFragment(Varyings input) : SV_Target
{
    #ifndef _ENABLE_OUTLINE
    clip(-1.0);
    #endif

    Light light = GetCharacterLight(input.positionWS);
    
    half4 mainTex = SampleMainTex(input.baseUV);
    half4 lightMap = SampleLightMap(input.baseUV);

    FragmentOutputs output = (FragmentOutputs)0;
    output.GBuffer0 = half4(mainTex.rgb, 0.0);
    output.GBuffer1 = half4(PackNormal(input.normalWS), input.positionCS.z);
    output.GBuffer2 = float4(GetOutlineColor(lightMap.a, mainTex.rgb) * light.color, 1.0);

    return output;
}

float4 CharacterFaceForwardPassFragment(Varyings input) : SV_Target
{
    half4 mainTex = SampleMainTex(input.baseUV);
    
    return CharacterFaceFragment(input, mainTex);
}

FragmentOutputs CharacterFaceGBufferPassFragment(Varyings input) : SV_Target
{
    half4 mainTex = SampleMainTex(input.baseUV);
    
    FragmentOutputs output = (FragmentOutputs)0;
    output.GBuffer0 = half4(mainTex.rgb, 0.0);
    output.GBuffer1 = half4(PackNormal(input.normalWS), input.positionCS.z);
    output.GBuffer2 = CharacterFaceFragment(input, mainTex);
    
    return output;
}

float4 CharacterFaceForwardStencilPassFragment(Varyings input) : SV_Target
{
    half4 faceTex = SampleFaceMap(input.addUV);
    clip(faceTex.g - 0.5);
    
    return 0.0;
}

FragmentOutputs CharacterFaceGBufferStencilPassFragment(Varyings input) : SV_Target
{
    half4 faceTex = SampleFaceMap(input.addUV);
    clip(faceTex.g - 0.5);
    
    FragmentOutputs output = (FragmentOutputs)0;
    
    return output;
}

float4 CharacterFaceOutlinePassFragment(Varyings input) : SV_Target
{
    #ifndef _ENABLE_OUTLINE
        clip(-1.0);
    #endif

    Light light = GetCharacterLight(input.positionWS);
    
    half4 mainTex = SampleMainTex(input.baseUV);

    return float4(GetOutlineColor(mainTex.rgb) * light.color, 1.0);
}

FragmentOutputs CharacterFaceGBufferOutlinePassFragment(Varyings input) : SV_Target
{
    #ifndef _ENABLE_OUTLINE
        clip(-1.0);
    #endif

    Light light = GetCharacterLight(input.positionWS);
    
    half4 mainTex = SampleMainTex(input.baseUV);
    
    FragmentOutputs output = (FragmentOutputs)0;
    output.GBuffer0 = half4(mainTex.rgb, 0.0);
    output.GBuffer1 = half4(PackNormal(input.normalWS), input.positionCS.z);
    output.GBuffer2 = float4(GetOutlineColor(mainTex.rgb) * light.color, 1.0);
    
    return output;
}

float4 CharacterEyesShadowForwardPassFragment(Varyings input) : SV_Target
{
    return float4(0.0, 0.0, 0.0, 0.5);
}

FragmentOutputs CharacterEyesShadowGBufferPassFragment(Varyings input) : SV_Target
{
    FragmentOutputs output = (FragmentOutputs)0;
    output.GBuffer0 = half4(0.0, 0.0, 0.0, 0.0);
    output.GBuffer1 = half4(0.0, 0.0, 0.0, 0.0);
    output.GBuffer2 = float4(0.0, 0.0, 0.0, 0.6);
    
    return output;
}

float4 CharacterHairForwardStencilOutPassFragment(Varyings input) : SV_Target
{
    // Sample textures
    half4 mainTex = SampleMainTex(input.baseUV);
    half4 lightMap = SampleLightMap(input.baseUV);

    return CharacterHairFragment(input, mainTex, lightMap);
}

float4 CharacterHairForwardStencilInPassFragment(Varyings input) : SV_Target
{
    // Sample textures
    half4 mainTex = SampleMainTex(input.baseUV);
    half4 lightMap = SampleLightMap(input.baseUV);

    float4 fragment = CharacterHairFragment(input, mainTex, lightMap);

    half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
    fragment.a *= 1.0 - saturate(dot(_HeadForward, viewDirWS) * 0.2);

    return fragment;
}

FragmentOutputs CharacterHairGBufferStencilOutPassFragment(Varyings input) : SV_Target
{
    // Sample textures
    half4 mainTex = SampleMainTex(input.baseUV);
    half4 lightMap = SampleLightMap(input.baseUV);

    FragmentOutputs output = (FragmentOutputs)0;
    output.GBuffer0 = half4(mainTex.rgb, 0.0);
    output.GBuffer1 = half4(PackNormal(input.normalWS), input.positionCS.z);
    output.GBuffer2 = CharacterHairFragment(input, mainTex, lightMap);
    
    return output;
}

FragmentOutputs CharacterHairGBufferStencilInPassFragment(Varyings input) : SV_Target
{
    // Sample textures
    half4 mainTex = SampleMainTex(input.baseUV);
    half4 lightMap = SampleLightMap(input.baseUV);
    
    float4 fragment = CharacterHairFragment(input, mainTex, lightMap);

    half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
    fragment.a *= 1.0 - saturate(dot(_HeadForward, viewDirWS) * 0.2);

    FragmentOutputs output = (FragmentOutputs)0;
    output.GBuffer0 = half4(mainTex.rgb, 0.0);
    output.GBuffer1 = half4(PackNormal(input.normalWS), input.positionCS.z);
    output.GBuffer2 = fragment;

    return output;
}

float4 CharacterHairOutlinePassFragment(Varyings input) : SV_Target
{
    #ifndef _ENABLE_OUTLINE
        clip(-1.0);
    #endif
    
    Light light = GetCharacterLight(input.positionWS);
    
    half4 mainTex = SampleMainTex(input.baseUV);

    return float4(GetOutlineColor(mainTex.rgb) * light.color, 1.0);
}

FragmentOutputs CharacterHairGBufferOutlinePassFragment(Varyings input) : SV_Target
{
    #ifndef _ENABLE_OUTLINE
    clip(-1.0);
    #endif

    Light light = GetCharacterLight(input.positionWS);
    
    half4 mainTex = SampleMainTex(input.baseUV);

    FragmentOutputs output = (FragmentOutputs)0;
    output.GBuffer0 = half4(mainTex.rgb, 0.0);
    output.GBuffer1 = half4(PackNormal(input.normalWS), input.positionCS.z);
    output.GBuffer2 = float4(GetOutlineColor(mainTex.rgb) * light.color, 1.0);

    return output;
}

Varyings CharacterDepthOnlyVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

half4 CharacterDepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return 0;
}

PerObjectMotionVectorPassVertexOutput CharacterMotionVectorPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    PerObjectMotionVectorPassVertexInput vIn;

    vIn.positionOS = input.positionOS;
    vIn.positionCS = vertexInput.positionCS;
    vIn.positionOld = input.positionOld;

    return ObjectMotionVectorPassVertex(vIn);
}

PerObjectMotionVectorPassVertexOutput CharacterOutlineMotionVectorPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    float3 smoothNormalWS = GetSmoothNormalWS(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

    float outlineWidth = input.color.a;
    #if defined(_IS_FACE)
    outlineWidth *= lerp(1.0,
        saturate(0.4 - dot(_HeadForward.xz, normalize(GetCameraPositionWS() - positionWS).xz)), step(0.5, input.color.b));
    #endif
    
    positionWS = ExtendOutline(positionWS, smoothNormalWS,
        _OutlineWidth * outlineWidth, _OutlineWidthMin * outlineWidth, _OutlineWidthMax * outlineWidth);
    
    float4 positionCS = TransformWorldToHClip(positionWS);

    PerObjectMotionVectorPassVertexInput vIn;

    vIn.positionOS = input.positionOS;
    vIn.positionCS = positionCS;
    vIn.positionOld = input.positionOld;

    return ObjectMotionVectorPassVertex(vIn);
}

half4 CharacterMotionVectorFragment(PerObjectMotionVectorPassVertexOutput input) : SV_Target
{
    return ObjectMotionVectorPassFragment(input);
}

#endif