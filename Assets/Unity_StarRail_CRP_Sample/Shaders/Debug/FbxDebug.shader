Shader "StarRail_CRP/Debug/FbxDebug"
{
	Properties
	{
		[KeywordEnum(OutlineColor, OutlineWidth, UV1, UV2, NormalOS, TangentOS, SmoothNormalTS)] _DebugShow("Debug Show", Float) = 0
	}
	SubShader
	{
		Pass
		{
			Name "FBX Debug Pass"
			
			Cull Off
			
			HLSLPROGRAM

			#pragma target 4.5

			#pragma multi_compile _ _DEBUGSHOW_OUTLINECOLOR _DEBUGSHOW_OUTLINEWIDTH _DEBUGSHOW_UV1 _DEBUGSHOW_UV2 _DEBUGSHOW_NORMALOS _DEBUGSHOW_TANGENTOS _DEBUGSHOW_SMOOTHNORMALTS

			#pragma vertex DebugVert
			#pragma fragment DebugFrag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
    			float4 color : COLOR;
    			float3 normalOS : NORMAL;
    			float4 tangentOS : TANGENT;
    			float2 baseUV : TEXCOORD0;
    			float2 addUV : TEXCOORD1;
    			float2 packSmoothNormal : TEXCOORD2;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
    			float4 color : TEXCOORD0;
				float4 uv : TEXCOORD1;
				float3 normalOS : TEXCOORD2;
				float3 tangentOS : TEXCCOR3;
				float3 smoothNormalTS : TEXCOORD4;
			};

			Varyings DebugVert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				output.uv = float4(input.baseUV, input.addUV);
				output.normalOS = normalize(input.normalOS);
				output.tangentOS = normalize(input.tangentOS.xyz);
				output.smoothNormalTS = UnpackNormalOctQuadEncode(input.packSmoothNormal);

				return output;
			}

			half4 DebugFrag(Varyings input) : SV_Target
			{
				half3 res = input.color.rgb;
				#if _DEBUGSHOW_OUTLINEWIDTH
					res = input.color.a;
				#elif _DEBUGSHOW_UV1
					res = half3(input.uv.xy, 0.0);
				#elif _DEBUGSHOW_UV2
					res = half3(input.uv.zw, 0.0);
				#elif _DEBUGSHOW_NORMALOS
					res = input.normalOS * 0.5 + 0.5;
				#elif _DEBUGSHOW_TANGENTOS
					res = input.tangentOS * 0.5 + 0.5;
				#elif _DEBUGSHOW_SMOOTHNORMALTS
					res = input.smoothNormalTS;
				#endif

				return half4(res, 1.0);
			}

			ENDHLSL
		}
	}
}