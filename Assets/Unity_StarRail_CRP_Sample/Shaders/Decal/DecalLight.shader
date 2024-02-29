Shader "StarRail_CRP/Decal/DecalLight"
{
	Properties
	{
		_LightMap("Light Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{
			Name "DecalScreenSpaceShadow"
			Tags { "Lightmode"="DecalScreenSpaceShadow" }
			
			Cull Front
			ZTest Greater
			ZWrite Off
            
            Blend One One, Zero One
            BlendOp Max, Add
			
			ColorMask RGB
			
			HLSLPROGRAM

            #pragma target 4.5
			
			#pragma multi_compile_instancing

			#pragma vertex DecalScreenSpaceShadowVertex
			#pragma fragment DecalScreenSpaceShadowFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			
			#include "../Utils/HLSL/Depth.hlsl"

			struct Attributes
			{
				float3 positionOS : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionOS : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float3 normalWS : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			TEXTURE2D(_LightMap);
			SAMPLER(sampler_LightMap);

			Varyings DecalScreenSpaceShadowVertex(Attributes input)
			{
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				
				output.positionCS = TransformObjectToHClip(input.positionOS);
				output.positionOS = input.positionOS;
				output.positionWS = TransformObjectToWorld(input.positionOS);
				output.normalWS = TransformObjectToWorldDir(float3(0.0, 1.0, 0.0));
				return output;
			}

			half4 DecalScreenSpaceShadowFragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				
				float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
				float depth = SampleDepth(screenUV);

				float3 posWS = ComputeWorldSpacePosition(screenUV, DeviceDepth(depth), UNITY_MATRIX_I_VP);
				float3 posOS = TransformWorldToObject(posWS);
				float2 sampleUV = saturate(posOS.xz * 0.999 + 0.5);

				float4 albedo = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, sampleUV);
				float inBox = step(-0.5, posOS.y) * step(posOS.y, 0.5);

				float mask = albedo.r * inBox;
				
				return mask;
			}

			ENDHLSL
		}
		Pass
		{
			Name "DecalStencilLighting"
			Tags { "Lightmode"="DecalStencilLighting" }
			
			Cull Front
			ZTest Greater
			ZWrite Off
            
            Blend One One, Zero One
            BlendOp Add, Add
			
			ColorMask RGB
			
			Stencil 
			{
                Ref 64
                ReadMask 96
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }
			
			HLSLPROGRAM

            #pragma target 4.5
			
			#pragma multi_compile_instancing

			#pragma multi_compile_fragment _DEFERRED_STENCIL
            #pragma multi_compile _POINT _SPOT
            #pragma multi_compile_fragment _SCENE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _CRP_ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

			#pragma vertex DecalScreenSpaceShadowVertex
			#pragma fragment DecalScreenSpaceShadowFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			
			#include "HLSL/Decal.hlsl"

			struct Attributes
			{
				float3 positionOS : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionOS : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float3 normalWS : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			TEXTURE2D(_LightMap);
			SAMPLER(sampler_LightMap);

			TEXTURE2D_X_HALF(_CustomGBuffer0);
			TEXTURE2D_X_HALF(_CustomGBuffer1);
			TEXTURE2D_X_HALF(_CustomGBuffer2);

			#if _RENDER_PASS_ENABLED
			
			    #define GBUFFER0 0
			    #define GBUFFER1 1
			    #define GBUFFER2 2
			    #define GBUFFER3 3
			
			    FRAMEBUFFER_INPUT_HALF(GBUFFER0);
			    FRAMEBUFFER_INPUT_HALF(GBUFFER1);
			    FRAMEBUFFER_INPUT_HALF(GBUFFER2);
			    FRAMEBUFFER_INPUT_FLOAT(GBUFFER3);
			#else
			    #ifdef GBUFFER_OPTIONAL_SLOT_1
			    TEXTURE2D_X_HALF(_GBuffer4);
			    #endif
			#endif

			UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
				UNITY_DEFINE_INSTANCED_PROP(float4, _DecalLightColor)
			UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

			Varyings DecalScreenSpaceShadowVertex(Attributes input)
			{
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				
				output.positionCS = TransformObjectToHClip(input.positionOS);
				output.positionOS = input.positionOS;
				output.positionWS = TransformObjectToWorld(input.positionOS);
				output.normalWS = TransformObjectToWorldDir(float3(0.0, 1.0, 0.0), true);
				return output;
			}

			half4 DecalScreenSpaceShadowFragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				
				float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
				float depth = SampleDepth(screenUV);

				float3 posWS = ComputeWorldSpacePosition(screenUV, DeviceDepth(depth), UNITY_MATRIX_I_VP);
				float3 posOS = TransformWorldToObject(posWS);
				float2 sampleUV = saturate(posOS.xz * 0.999 + 0.5);
				
				#if defined(_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
        		float2 undistorted_screen_uv = screenUV;
        		screenUV = input.positionCS.xy * _ScreenSize.zw;
        		#endif
        		
        		#if _RENDER_PASS_ENABLED
        		float d        = LOAD_FRAMEBUFFER_INPUT(GBUFFER3, input.positionCS.xy).x;
        		half4 gbuffer0 = LOAD_FRAMEBUFFER_INPUT(GBUFFER0, input.positionCS.xy);
        		half4 gbuffer1 = LOAD_FRAMEBUFFER_INPUT(GBUFFER1, input.positionCS.xy);
        		//half4 gbuffer2 = LOAD_FRAMEBUFFER_INPUT(GBUFFER2, input.positionCS.xy);
        		#else
        		// Using SAMPLE_TEXTURE2D is faster than using LOAD_TEXTURE2D on iOS platforms (5% faster shader).
        		// Possible reason: HLSLcc upcasts Load() operation to float, which doesn't happen for Sample()?
        		float d        = SampleDepth(screenUV); // raw depth value has UNITY_REVERSED_Z applied on most platforms.
        		half4 gbuffer0 = SAMPLE_TEXTURE2D_X_LOD(_CustomGBuffer0, sampler_PointClamp, screenUV, 0);
        		half4 gbuffer1 = SAMPLE_TEXTURE2D_X_LOD(_CustomGBuffer1, sampler_PointClamp, screenUV, 0);
        		#endif
        		half4 shadowMask = 1.0;
		
        		half3 color = (0.0).xxx;
        		half alpha = 1.0;
		
        		#if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
        		    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(screenUV);
        		    light.color *= aoFactor.directAmbientOcclusion;
        		    #if defined(_DIRECTIONAL) && defined(_DEFERRED_FIRST_LIGHT)
        		    // What we want is really to apply the mininum occlusion value between the baked occlusion from surfaceDataOcclusion and real-time occlusion from SSAO.
        		    // But we already applied the baked occlusion during gbuffer pass, so we have to cancel it out here.
        		    // We must also avoid divide-by-0 that the reciprocal can generate.
        		    half occlusion = aoFactor.indirectAmbientOcclusion < surfaceDataOcclusion ? aoFactor.indirectAmbientOcclusion * rcp(surfaceDataOcclusion) : 1.0;
        		    alpha = occlusion;
        		    #endif
        		#endif
		
        		InputData inputData = InputDataFromGBufferAndWorldPosition(gbuffer1, posWS.xyz);

				float4 albedo = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, sampleUV);
				albedo.a = lerp(0.0, albedo.a, step(0.0, dot(input.normalWS, inputData.normalWS)));
				float inBox = step(-0.5, posOS.y) * step(posOS.y, 0.5);

				float3 mask = albedo.rgb * albedo.a * inBox;

				float4 lightColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DecalLightColor);
				Light light = GetDecalLight(input.normalWS, mask, screenUV, lightColor);
		
        		bool specularOff = false;
				color = SceneLighting(inputData, gbuffer0, gbuffer1, light, specularOff);
		
        		return half4(color, alpha);
			}

			ENDHLSL
		}
	}
}
