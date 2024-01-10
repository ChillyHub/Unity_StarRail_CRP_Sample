Shader "StarRail_CRP/Charater/CharacterEyesShadow"
{
    Properties
    {
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
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+19" }

        Pass
        {
            Name "Character Eyes Shadow GBuffer"
            Tags { "Lightmode"="CharacterGBuffer" "Queue"="Geometry+19" }

            Cull back
            
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            
            // ColorMask 0 0
            // ColorMask 0 1
            // ColorMask RGBA 2
            
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

            #pragma vertex CharacterCommonPassVertex
            #pragma fragment CharacterEyesShadowGBufferPassFragment

            #include "HLSL/CharacterInput.hlsl"
            #include "HLSL/CharacterFunction.hlsl"
            #include "HLSL/CharacterPass.hlsl"

            ENDHLSL
        }
    }
}
