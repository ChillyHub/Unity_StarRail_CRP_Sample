Shader "Hidden/StarRail_CRP/PostProcessing/ToneMapping"
{
    Properties
    {
        _ACESParamA("ACES Param A", Float) = 2.80
        _ACESParamB("ACES Param B", Float) = 0.40
        _ACESParamC("ACES Param C", Float) = 2.10
        _ACESParamD("ACES Param D", Float) = 0.50
        _ACESParamE("ACES Param E", Float) = 1.50
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

            #pragma multi_compile_local_fragment _ _BLOOM
            #pragma multi_compile_local_fragment _ _TONEMAPPING_ACES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BloomParams;
                float _BloomRGBM;

                float _ACESParamA;
                float _ACESParamB;
                float _ACESParamC;
                float _ACESParamD;
                float _ACESParamE;

                float4 _BloomTexture_TexelSize;
            CBUFFER_END

            TEXTURE2D(_BloomTexture);

            #define BloomIntensity          _BloomParams.x
            #define BloomTint               _BloomParams.yzw
            #define BloomRGBM               _BloomRGBM.x

            float3 CustomACESTonemapping(float3 x)
            {
                float3 u = _ACESParamA * x + _ACESParamB;
                float3 v = _ACESParamC * x + _ACESParamD;
                return saturate((x * u) / (x * v + _ACESParamE));
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 color = FragBlit(i, sampler_PointClamp).rgb;

                // Gamma space... Just do the rest of Uber in linear and convert back to sRGB at the end
                #if UNITY_COLORSPACE_GAMMA
                {
                    color = GetSRGBToLinear(color);
                }
                #endif

                #if defined(_BLOOM)
                {
                    float2 uvBloom = i.texcoord;
    
                    #if _BLOOM_HQ && !defined(SHADER_API_GLES)
                    half4 bloom = SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_BloomTexture, sampler_LinearClamp), SCREEN_COORD_REMOVE_SCALEBIAS(uvBloom), _Bloom_Texture_TexelSize.zwxy, (1.0).xx, unity_StereoEyeIndex);
                    #else
                    half4 bloom = SAMPLE_TEXTURE2D_X(_BloomTexture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(uvBloom));
                    #endif
    
                    #if UNITY_COLORSPACE_GAMMA
                    bloom.xyz *= bloom.xyz; // γ to linear
                    #endif
    
                    UNITY_BRANCH
                    if (BloomRGBM > 0)
                    {
                        bloom.xyz = DecodeRGBM(bloom);
                    }
    
                    bloom.xyz *= BloomIntensity;
                    color += bloom.xyz * BloomTint;
                }
                #endif

                #if defined(_TONEMAPPING_ACES)
                    color.rgb = CustomACESTonemapping(color.rgb);
                #endif

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
