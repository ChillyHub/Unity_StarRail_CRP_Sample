Shader "StarRail_CRP/Decal/DecalObject"
{
	Properties
	{
		_BaseTex("Base Texture", 2D) = "white" {}
		_BaseColor("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)
		// _NormalTex("Normal Texture", 2D) = "bump" {}
		// _NormalBlend("Normal Blend", Range(0, 1)) = 0.5
		_Metallic("Metallic", Range(0, 1)) = 0.0
		_Smoothness("Smoothness", Range(0, 1)) = 0.5
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{
			Name "DecalGBuffer"
			Tags { "Lightmode"="DecalGBuffer" }
			
			Cull Front
			ZTest Greater
			ZWrite Off
            
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
			
			ColorMask RGB 0
			ColorMask 0 1
			ColorMask 0 2
			
			HLSLPROGRAM

            #pragma target 4.5
			
			#pragma multi_compile_instancing

			#pragma vertex DecalObjectVertex
			#pragma fragment DecalObjectFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

			#include "../Deferred/HLSL/CRPGBuffer.hlsl"
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

			TEXTURE2D(_BaseTex);
			SAMPLER(sampler_BaseTex);

			UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
				UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
				UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
				UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
				UNITY_DEFINE_INSTANCED_PROP(float4x4, _NormalToWorld)
			UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

			Varyings DecalObjectVertex(Attributes input)
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

			FragmentOutputs DecalObjectFragment(Varyings input)
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				
				float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
				float depth = SampleDepth(screenUV);

				float3 posWS = ComputeWorldSpacePosition(screenUV, DeviceDepth(depth), UNITY_MATRIX_I_VP);
				float3 posOS = TransformWorldToObject(posWS);
				float2 sampleUV = saturate(posOS.xz * 0.999 + 0.5);

				float4 albedo = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, sampleUV);
				albedo.a = lerp(0.0, albedo.a, step(0.0, dot(input.normalWS, GetWorldSpaceViewDir(input.positionWS))));
				albedo.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor.rgb);
				
				FragmentOutputs output;
				output.GBuffer0 = albedo;
				output.GBuffer1 = float4(0, 0, 0, 0);
				output.GBuffer2 = float4(0, 0, 0, 0);
				return output;
			}

			ENDHLSL
		}
	}
}