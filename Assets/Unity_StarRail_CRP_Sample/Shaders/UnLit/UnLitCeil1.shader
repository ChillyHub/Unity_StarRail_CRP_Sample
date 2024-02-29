Shader "StarRail_CRP/UnLit/UnLitCeil1"
{
	Properties
	{
		_BaseColor ("Tint Color", Color) = (1,1,1,1)
		_BaseMap ("Main Texture", 2D) = "white" {}
		
		[HDR] _StarColor1 ("Star Color 1", Color) = (1,1,1,1)
		_StarMap1 ("Star Map 1", 2D) = "white" {}
		[HDR] _StarColor2 ("Star Color 2", Color) = (1,1,1,1)
		_StarMap2 ("Star Map 2", 2D) = "white" {}
		[HDR] _EmissColor1 ("Emission Color 1", Color) = (1,1,1,1)
		_EmissMap1 ("Emission Map 1", 2D) = "white" {}
		[HDR] _EmissColor2 ("Emission Color 2", Color) = (1,1,1,1)
		_EmissMap2 ("Emission Map 2", 2D) = "white" {}
		[HDR] _EmissColor3 ("Emission Color 3", Color) = (1,1,1,1)
		_EmissMap3 ("Emission Map 3", 2D) = "white" {}
		
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
        {
	        Name "UnLit Forward Pass"
            Tags{"LightMode" = "UnLitForward"}

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            //#pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex UnLitPassVertex
            #pragma fragment UnLitCeil1ForwardPassFragment

            #include "HLSL/UnLitInput.hlsl"
            #include "HLSL/UnLitPass.hlsl"

            TEXTURE2D(_StarMap1);
            SAMPLER(sampler_StarMap1);
            TEXTURE2D(_StarMap2);
            SAMPLER(sampler_StarMap2);
            TEXTURE2D(_EmissMap1);
            SAMPLER(sampler_EmissMap1);
            TEXTURE2D(_EmissMap2);
            SAMPLER(sampler_EmissMap2);
            TEXTURE2D(_EmissMap3);
            SAMPLER(sampler_EmissMap3);

            CBUFFER_START(UnityPerMaterial2)
			float4 _StarColor1;
            float4 _StarColor2;
            float4 _EmissColor1;
            float4 _EmissColor2;
            float4 _EmissColor3;
			CBUFFER_END

            half4 UnLitCeil1ForwardPassFragment(Varyings input) : SV_Target
			{
				float3 viewDirWS = GetWorldSpaceViewDir(input.positionWS);
				float2 offset = normalize(viewDirWS.xz) * 0.1;
				
			    half4 color = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
				half4 star1 = _StarColor1 * SAMPLE_TEXTURE2D(_StarMap1, sampler_StarMap1, input.uv);
				half4 star2 = _StarColor2 * SAMPLE_TEXTURE2D(_StarMap2, sampler_StarMap2, input.uv + offset);
				half4 emission1 = _EmissColor1 * pow(SAMPLE_TEXTURE2D(_EmissMap1, sampler_EmissMap1, input.uv), 4.0);
				half4 emission2 = _EmissColor2 * SAMPLE_TEXTURE2D(_EmissMap2, sampler_EmissMap2, input.uv);
				half4 emission3 = _EmissColor3 * SAMPLE_TEXTURE2D(_EmissMap3, sampler_EmissMap3, input.uv);

				color += star1 + star2 + emission1 + emission2 + emission3;
			    
			    return color;
			}
            
            ENDHLSL
        }
		Pass
        {
	        Name "UnLit GBuffer Pass"
            Tags{"LightMode" = "UnLitGBuffer"}

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            //#pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex UnLitPassVertex
            #pragma fragment UnLitCeil1GBufferPassFragment

            #include "HLSL/UnLitInput.hlsl"
            #include "HLSL/UnLitPass.hlsl"

            TEXTURE2D(_StarMap1);
            SAMPLER(sampler_StarMap1);
            TEXTURE2D(_StarMap2);
            SAMPLER(sampler_StarMap2);
            TEXTURE2D(_EmissMap1);
            SAMPLER(sampler_EmissMap1);
            TEXTURE2D(_EmissMap2);
            SAMPLER(sampler_EmissMap2);
            TEXTURE2D(_EmissMap3);
            SAMPLER(sampler_EmissMap3);

            CBUFFER_START(UnityPerMaterial2)
			float4 _StarColor1;
            float4 _StarColor2;
            float4 _EmissColor1;
            float4 _EmissColor2;
            float4 _EmissColor3;
			CBUFFER_END

            FragmentOutputs UnLitCeil1GBufferPassFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
			{
			    float3 viewDirWS = GetWorldSpaceViewDir(input.positionWS);
				float2 offset = normalize(viewDirWS.xz) * 0.1;
				
			    half4 color = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
				half4 star1 = _StarColor1 * SAMPLE_TEXTURE2D(_StarMap1, sampler_StarMap1, input.uv);
				half4 star2 = _StarColor2 * SAMPLE_TEXTURE2D(_StarMap2, sampler_StarMap2, input.uv + offset);
				half4 emission1 = _EmissColor1 * pow(SAMPLE_TEXTURE2D(_EmissMap1, sampler_EmissMap1, input.uv), 4.0);
				half4 emission2 = _EmissColor2 * SAMPLE_TEXTURE2D(_EmissMap2, sampler_EmissMap2, input.uv);
				half4 emission3 = _EmissColor3 * SAMPLE_TEXTURE2D(_EmissMap3, sampler_EmissMap3, input.uv);

				color.rgb += star1.rgb + star2.rgb + emission1.rgb + emission2.rgb + emission3.rgb;
			
			    input.normalWS = IS_FRONT_VFACE(face, input.normalWS, -input.normalWS);
			
			    half3 packedNormalWS = PackNormal(input.normalWS);
			
			    FragmentOutputs output;
			    output.GBuffer0 = half4(color.rgb, 0.0);
			    output.GBuffer1 = half4(packedNormalWS, 0.0);      
			    output.GBuffer2 = half4(color);           
			    
			    return output;
			}
            
            ENDHLSL
        }
		Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            //#pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // -------------------------------------
            // Universal Pipeline keywords

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "HLSL/UnLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
		Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            //#pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "HLSL/UnLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            //#pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "HLSL/UnLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
	}
}