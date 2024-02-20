Shader "Hidden/StarRail_CRP/SSGI/ScreenSpaceReflection"
{
    Properties
    {
        
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"

            #include "../Utils/HLSL/Depth.hlsl"

            CBUFFER_START(UnityPerMaterial)
                
            CBUFFER_END

            TEXTURE2D(_SSRReflectUVTexture);
            SAMPLER(sampler_SSRReflectUVTexture);
            TEXTURE2D(_AlbedoMetallicTexture);
            SAMPLER(sampler_AlbedoMetallicTexture);
            TEXTURE2D(_PackedNormalSmoothnessTexture);
            SAMPLER(sampler_PackedNormalSmoothnessTexture);
            TEXTURE2D(_ColorPyramidTexture);
            SAMPLER(sampler_ColorPyramidTexture);
            TEXTURE2D(_MotionVectorTexture);
            SAMPLER(sampler_MotionVectorTexture);

            Texture2D<uint2> _StencilTexture;

            //#ifdef _GBUFFER_NORMALS_OCT
            half3 UnpackNormal(half3 pn)
            {
                half2 remappedOctNormalWS = half2(Unpack888ToFloat2(pn));          // values between [ 0, +1]
                half2 octNormalWS = remappedOctNormalWS.xy * half(2.0) - half(1.0);// values between [-1, +1]
                return half3(UnpackNormalOctQuadEncode(octNormalWS));              // values between [-1, +1]
            }
            //#else
            //half3 UnpackNormal(half3 pn)
            //{ return pn; }                                                        // values between [-1, +1]
            //#endif

            // Weight for SSR where Fresnel == 1 (returns value/pdf)
            float GetGGXSampleWeight(float3 V, float3 L, float roughness)
            {
                // Simplification:
                // value = D_GGX / (lambdaVPlusOne + lambdaL);
                // pdf = D_GGX / lambdaVPlusOne;
            
                const float lambdaVPlusOne = Lambda_GGX(roughness, V) + 1.0;
                const float lambdaL = Lambda_GGX(roughness, L);
            
                return lambdaVPlusOne / (lambdaVPlusOne + lambdaL);
            }

            float GetScreenFade(float2 screenUV)
            {
                screenUV = screenUV * 2.0 - 1.0;
                float2 st = 1.0 - smoothstep(0.0, 0.8, abs(screenUV));
                
                return st.x * st.y;
            }

            float3 GetReflectSampleColor(float2 screenUV)
            {
                const float2 offsets[9] = {float2(0, 0), float2(1, 0), float2(0, 1), float2(-1, 0), float2(0, -1),
                    float2(1, 1), float2(-1, 1), float2(1, -1), float2(-1, -1)};
                
                float smooth = LOAD_TEXTURE2D_X(_PackedNormalSmoothnessTexture, int2(screenUV * _ScreenSize.xy)).w;
                float perceptualRough = PerceptualSmoothnessToPerceptualRoughness(smooth);
                float step = lerp(0.0, 10.0, perceptualRough);

                float weightTotal = 0.0;
                float3 colorTotal = 0.0;
                for (int i = 0; i < 9; ++i)
                {
                    float2 sampleCoord = int2(screenUV * _ScreenSize.xy) + offsets[i] * step;
                    float2 motionVec = LOAD_TEXTURE2D_X(_MotionVectorTexture, sampleCoord).xy;
                    float2 hitUV = LOAD_TEXTURE2D_X(_SSRReflectUVTexture, sampleCoord - motionVec * _ScreenSize.xy).xy;
                    int2 hitCoord = int2(hitUV * _ScreenSize.xy);

                    float hitDeviceDepth = LOAD_TEXTURE2D_X(_DepthPyramidTexture, hitCoord).r;
                    float srcDeviceDepth = LOAD_TEXTURE2D_X(_DepthPyramidTexture, sampleCoord).r;
                    float3 hitPointWS = ComputeWorldSpacePosition(hitUV, hitDeviceDepth, UNITY_MATRIX_I_VP);
                    float3 positionWS = ComputeWorldSpacePosition(screenUV, srcDeviceDepth, UNITY_MATRIX_I_VP);

                    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                    float3 reflectDirWS = normalize(hitPointWS - positionWS);

                    half4 normalSmooth = LOAD_TEXTURE2D_X(_PackedNormalSmoothnessTexture, sampleCoord);
                    half4 albedoMetallic = LOAD_TEXTURE2D_X(_AlbedoMetallicTexture, sampleCoord);
                    float3 normalWS = UnpackNormal(normalSmooth.xyz);
                    float smoothness = normalSmooth.w;
                    float3 albedo = albedoMetallic.rgb;
                    float metallic = albedoMetallic.a;
                    
                    float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
                    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
                    float mipLevel = lerp(0.0, 7.0, perceptualRoughness);

                    half3 reflectVector = reflect(-viewDirWS, normalWS);
                    half NoV = saturate(dot(normalWS, viewDirWS));
                    half fresnelTerm = Pow4(1.0 - NoV);

                    half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
                    half reflectivity = half(1.0) - oneMinusReflectivity;
                    half3 brdfSpecular = lerp(kDieletricSpec.rgb, albedo, metallic);

                    BRDFData brdfData = (BRDFData)0;
                    brdfData.perceptualRoughness = perceptualRoughness;
                    brdfData.roughness2 = roughness * roughness;
                    brdfData.specular = brdfSpecular;
                    brdfData.grazingTerm = saturate(smoothness + reflectivity);

                    half3 cubeMapReflect = GlossyEnvironmentReflection(reflectVector, positionWS,
                        brdfData.perceptualRoughness, 1.0, 0.0);
                    float environmentSpecular = EnvironmentBRDFSpecular(brdfData, fresnelTerm);

                    float3 screenReflect = 0.0;
                    float fade = 0.0;
                    float weight = GetGGXSampleWeight(viewDirWS, reflectDirWS, perceptualRoughness);
                    
                    if (!(hitUV.x == 0.0 && hitUV.y == 0.0))
                    {
                        //float2 motionVec = LOAD_TEXTURE2D_X(_MotionVectorTexture, sampleCoord).xy;
                        float2 prevHitUV = hitUV;// - motionVec;
                        screenReflect = SAMPLE_TEXTURE2D_LOD(_ColorPyramidTexture, sampler_ColorPyramidTexture, prevHitUV, mipLevel).rgb;
                        fade = GetScreenFade(prevHitUV);

                        // Disable SSR for negative, infinite and NaN history values.
                        uint3 intCol   = asuint(screenReflect);
                        bool  isPosFin = Max3(intCol.r, intCol.g, intCol.b) < 0x7F800000;
                        screenReflect = isPosFin ? screenReflect : 0;
                    }

                    colorTotal += lerp(cubeMapReflect, screenReflect, fade) * environmentSpecular * weight;
                    weightTotal += weight;
                }

                if (weightTotal < HALF_EPS)
                {
                    return 0.0;
                }
                
                return colorTotal / weightTotal;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 color = FragBlit(i, sampler_PointClamp).rgb;

                uint stencil = GetStencilValue(LOAD_TEXTURE2D_X(_StencilTexture, int2(i.texcoord * _ScreenSize.xy)));
                if ((stencil & 4u) == 0)
                {
                    return float4(color, 1.0);
                }

                float3 reflect = GetReflectSampleColor(i.texcoord);
            
                return float4(color + reflect, 1.0);
            }
            ENDHLSL
        }
    }
}
