Shader "StarRail_CRP/Charater/CharacterFace"
{
    Properties
    {
        [Header(Textures)][Space]
        _MainTex("Color Map", 2D) = "black" {}
        _FaceMap("Face Map", 2D) = "black" {}
        _FaceExpressionMap("Face Expression Map", 2D) = "balck" {}
        _CoolRampMap("Cool Ramp Map", 2D) = "black" {}
        _WarmRampMap("Warm Ramp Map", 2D) = "black" {}
        
        [Toggle(_USE_NORMAL_MAP)] _UseNormalMapToggle("Use Normal Map", Float) = 0
        _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 4)) = 1
        
        [Header(GI)][Space]
        [Toggle(_ENABLE_GI)] _EnableGIToggle("Enable GI", Float) = 1
        _GI_Intensity("GI Intensity", Range(0, 2)) = 1
        _GI_Flatten("GI Flatten", Range(0, 1)) = 0
        _GI_UseMainColor("GI Use Main Color", Range(0, 1)) = 1
        
        [Header(Additional Light)][Space]
        [Toggle(_ENABLE_ADDITIONAL_LIGHT)] _EnableAddLightToggle("Enable Addition Light", Float) = 1
        
        [Header(Diffuse)][Space]
        [Toggle(_ENABLE_DIFFUSE)] _EnableDiffuseToggle("Enable Diffuse", Float) = 1
        _ShadowRamp("Shadow Ramp", Range(0.001, 1.0)) = 0.1
        _ShadowOffset("Shadow Offset", Range(-1.0, 1.0)) = 0.0
        _ShadowBoost("Shadow Boost", Range(0.0, 1.0)) = 1.0
        
        [Header(Specular)][Space]
        [Toggle(_ENABLE_SPECULAR)] _EnableSpecularToggle("Enable Specular", Float) = 1
        [Toggle(_ANISOTROPY_SPECULAR)] _AnisotropySpecularToggle("Is Anisotropy Specular", Float) = 0
        _SpecularColor("Spwcular Color", Color) = (1, 1, 1, 1)
        _SpecularShininess("Specular Shininess", Range(0.1, 100)) = 10
        _SpecularRoughness("Specular Roughness", Range(0, 1)) = 0.1
        _SpecularIntensity("Specualr Intensity", Range(0, 10)) = 1
        
        [Header(Emission)][Space]
        [Toggle(_ENABLE_EMISSION)] _EnableEmissionToggle("Enable Emission", Float) = 1
        _EmissionIntensity("Emission Intensity", Range(0, 4)) = 1
        _EmissionThreshold("Emission Threshold", Range(0, 1)) = 1
        
        [Header(Rim)][Space]
        [Toggle(_ENABLE_RIM)] _EnableRimToggle("Enable Rim", Float) = 1
        [KeywordEnum(Disable, Multiply, Overlay)] _CustomRimVarEnum("Custom Rim Var State", Float) = 0
        _RimColor0("Rim Color 0", Color) = (1, 1, 1, 1)
        _RimWidth0("Rim Width 0", Float) = 1
        _RimColor1("Rim Color 1", Color) = (1, 1, 1, 1)
        _RimWidth1("Rim Width 1", Float) = 1
        _RimColor2("Rim Color 2", Color) = (1, 1, 1, 1)
        _RimWidth2("Rim Width 2", Float) = 1
        _RimColor3("Rim Color 3", Color) = (1, 1, 1, 1)
        _RimWidth3("Rim Width 3", Float) = 1
        _RimColor4("Rim Color 4", Color) = (1, 1, 1, 1)
        _RimWidth4("Rim Width 4", Float) = 1
        _RimColor5("Rim Color 5", Color) = (1, 1, 1, 1)
        _RimWidth5("Rim Width 5", Float) = 1
        _RimColor6("Rim Color 6", Color) = (1, 1, 1, 1)
        _RimWidth6("Rim Width 6", Float) = 1
        _RimColor7("Rim Color 7", Color) = (1, 1, 1, 1)
        _RimWidth7("Rim Width 7", Float) = 1
        [Space]
        _RimIntensity("Rim Intensity", Range(0, 10)) = 1
        
        [Header(Expression)][Space]
        _CheckIntensity("Check Intensity", Range(0.0, 2.0)) = 0.0
        _ShyIntensity("Shy Intensity", Range(0.0, 2.0)) = 0.0
        _ShadowIntensity("Shadow Intensity", Range(0.0, 1.0)) = 0.0
        
        [Header(Outline)][Space]
        [Toggle(_ENABLE_OUTLINE)] _EnableOutlineToggle("Enable Outline", Float) = 1
        [KeywordEnum(Normal, Tangent, UV2)] _OutlineNormalChannel("Outline Normal Channel", Float) = 0
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("OutlineWidth (WS)(m)", Range(0, 0.01)) = 0.001
        _OutlineWidthMin("Outline Width Min (SS)(pixel)", Range(0, 10)) = 0
        _OutlineWidthMax("Outline Width Max (SS)(pixel)", Range(0, 30)) = 10
        
        [Header(Bloom)][Space]
        _BloomIntensity("Bloom Intensity", Range(0.0, 6.0)) = 1.0
        
        [Header(Fresnel)][Space]
        _FresnelColor("Fresnel Color", Color) = (0, 0, 0, 0)
        _FresnelIntensity("Fresnel Intensity", Range(0, 10)) = 1
        
        [Header(Dissolve)][Space]
        _EnableDissolveToggle("Enable Dissolve", Float) = 0
        _DissolveMap("Dissolve Map", 2D) = "white" {}
        // TODO: Other dissolve properties ...
        
        [Header(Shader State)][Space]
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadowsToggle ("Receive Shadows", Float) = 1
        _StencilRef ("Stencil Ref", Float) = 16
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOP ("Stencil Op", Float) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        _RenderingMode ("Rendering Mode", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        
        [HideInInspector][Toggle(_IS_FACE)] _IS_FACE_TOGGLE("Is Face", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }

        Pass
        {
            Name "Character Face GBuffer"
            Tags { "Lightmode"="CharacterGBuffer" "Queue"="Geometry+10" }
            
            Cull [_CullMode]
            
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            
            Stencil {
                Ref 32
                ReadMask 0
                WriteMask 32
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }
            
            HLSLPROGRAM

            #pragma target 4.5

            #pragma shader_feature _USE_NORMAL_MAP
            #pragma shader_feature _CUSTOM_RAMP_MAPPING
            #pragma shader_feature _ENABLE_GI
            #pragma shader_feature _ENABLE_ADDITIONAL_LIGHT
            #pragma shader_feature _ENABLE_DIFFUSE
            #pragma shader_feature _ENABLE_SPECULAR
            #pragma shader_feature _ANISOTROPY_SPECULAR
            #pragma shader_feature _ENABLE_EMISSION
            #pragma shader_feature _ENABLE_RIM
            #pragma shader_feature _ENABLE_OUTLINE
            #pragma shader_feature _WITH_STOCKING
            #pragma shader_feature _RECEIVE_SHADOWS
            
            #pragma shader_feature _CUSTOMSPECULARVARENUM_DISABLE _CUSTOMSPECULARVARENUM_MULTIPLY _CUSTOMSPECULARVARENUM_OVERLAY
            #pragma shader_feature _CUSTOMRIMVARENUM_DISABLE _CUSTOMRIMVARENUM_MULTIPLY _CUSTOMRIMVARENUM_OVERLAY
            #pragma shader_feature _OUTLINENORMALCHANNEL_NORMAL _OUTLINENORMALCHANNEL_TANGENT _OUTLINENORMALCHANNEL_UV2
            #pragma shader_feature _CUSTOMOUTLINEVARENUM_DISABLE _CUSTOMOUTLINEVARENUM_MULTIPLY _CUSTOMOUTLINEVARENUM_OVERLAY

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #pragma vertex CharacterCommonPassVertex
            #pragma fragment CharacterFaceGBufferPassFragment

            #include "HLSL/CharacterInput.hlsl"
            #include "HLSL/CharacterFunction.hlsl"
            #include "HLSL/CharacterPass.hlsl"

            ENDHLSL
        }
        Pass
        {
            Name "Character Face GBuffer Outline"
            Tags { "Lightmode"="CharacterOutline" "Queue"="Geometry+10" }
            
            Cull Front
            
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            
            Stencil {
                Ref 32
                ReadMask 0
                WriteMask 32
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }
            
            HLSLPROGRAM

            #pragma shader_feature _IS_FACE
            #pragma shader_feature _ENABLE_OUTLINE
            #pragma shader_feature _OUTLINENORMALCHANNEL_NORMAL _OUTLINENORMALCHANNEL_TANGENT _OUTLINENORMALCHANNEL_UV2

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #pragma vertex CharacterOutlinePassVertex
            #pragma fragment CharacterFaceGBufferOutlinePassFragment

            #include "HLSL/CharacterInput.hlsl"
            #include "HLSL/CharacterFunction.hlsl"
            #include "HLSL/CharacterPass.hlsl"

            ENDHLSL
        }
        
        Pass
        {
            Name "Character Face GBuffer Stencil Mask"
            Tags { "Lightmode"="CharacterGBuffer2" "Queue"="Geometry+15" }
            
            Cull [_CullMode]
            
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest LEqual
            
            ColorMask 0 0
            ColorMask 0 1
            ColorMask 0 2
            
            Stencil {
                Ref 33
                ReadMask 0
                WriteMask 47
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }
            
            HLSLPROGRAM

            #pragma target 4.5

            #pragma shader_feature _USE_NORMAL_MAP
            #pragma shader_feature _CUSTOM_RAMP_MAPPING
            #pragma shader_feature _ENABLE_GI
            #pragma shader_feature _ENABLE_ADDITIONAL_LIGHT
            #pragma shader_feature _ENABLE_DIFFUSE
            #pragma shader_feature _ENABLE_SPECULAR
            #pragma shader_feature _ANISOTROPY_SPECULAR
            #pragma shader_feature _ENABLE_EMISSION
            #pragma shader_feature _ENABLE_RIM
            #pragma shader_feature _ENABLE_OUTLINE
            #pragma shader_feature _WITH_STOCKING
            #pragma shader_feature _RECEIVE_SHADOWS
            
            #pragma shader_feature _CUSTOMSPECULARVARENUM_DISABLE _CUSTOMSPECULARVARENUM_MULTIPLY _CUSTOMSPECULARVARENUM_OVERLAY
            #pragma shader_feature _CUSTOMRIMVARENUM_DISABLE _CUSTOMRIMVARENUM_MULTIPLY _CUSTOMRIMVARENUM_OVERLAY
            #pragma shader_feature _OUTLINENORMALCHANNEL_NORMAL _OUTLINENORMALCHANNEL_TANGENT _OUTLINENORMALCHANNEL_UV2
            #pragma shader_feature _CUSTOMOUTLINEVARENUM_DISABLE _CUSTOMOUTLINEVARENUM_MULTIPLY _CUSTOMOUTLINEVARENUM_OVERLAY

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #pragma vertex CharacterCommonPassVertex
            #pragma fragment CharacterFaceGBufferStencilPassFragment

            #include "HLSL/CharacterInput.hlsl"
            #include "HLSL/CharacterFunction.hlsl"
            #include "HLSL/CharacterPass.hlsl"

            ENDHLSL
        }
        Pass
        {
            Name "Character Shadow Caster"
            Tags { "Lightmode"="CharacterShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_CullMode]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
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

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}
        
            ZWrite On
            ColorMask 0
            Cull Off
        
            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5
        
            #pragma vertex CharacterDepthOnlyVertex
            #pragma fragment CharacterDepthOnlyFragment
        
            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
        
            #include "HLSL/CharacterInput.hlsl"
            #include "HLSL/CharacterFunction.hlsl"
            #include "HLSL/CharacterPass.hlsl"
            ENDHLSL
        }
    }
}
