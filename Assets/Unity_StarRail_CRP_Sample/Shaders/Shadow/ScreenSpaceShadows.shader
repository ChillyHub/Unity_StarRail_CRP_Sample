Shader "Hidden/StarRail_CRP/Shadow/ScreenSpaceShadows"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE

        #include "HLSL/ScreenSpaceShadowsPass.hlsl"

        ENDHLSL

        Pass
        {
            Name "CRP Directional Screen Space Shadows"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma multi_compile _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #pragma vertex   Vert
            #pragma fragment DirectionalShadowFragment
            ENDHLSL
        }
        Pass
        {
            Name "CRP Additional Shadow Stencil Volume"
            
            ZTest LEQual
            ZWrite Off
            ZClip false
            Cull Off
            ColorMask 0

            Stencil 
            {
                Ref 0
                ReadMask 2
                WriteMask 2
                CompFront Equal
                PassFront Keep
                ZFailFront Invert
                CompBack Equal
                PassBack Keep
                ZFailBack Invert
            }

            HLSLPROGRAM
            #pragma multi_compile _SPOT
            
            #pragma vertex   VolumeVertex
            #pragma fragment WhiteFragment
            ENDHLSL
        }
        Pass
        {
            Name "CRP Additional Screen Space Shadows"
            
            ZTest GEqual
            ZWrite Off
            ZClip false
            Cull Front
            Blend One One, Zero One
            BlendOp Min, Add

            Stencil 
            {
                Ref 2
                ReadMask 2
                WriteMask 2
                Comp Equal
                Pass Zero
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma multi_compile _POINT _SPOT
            #pragma multi_compile_fragment _UNLIT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED

            #pragma vertex   VolumeVertex
            #pragma fragment AdditionalShadowFragment
            ENDHLSL
        }
        Pass
        {
            Name "CRP Character Shadow Stencil Volume"
            
            ZTest LEQual
            ZWrite Off
            ZClip false
            Cull Off
            ColorMask 0

            Stencil 
            {
                Ref 32
                ReadMask 96
                WriteMask 2
                CompFront NotEqual
                PassFront Keep
                ZFailFront Invert
                CompBack NotEqual
                PassBack Keep
                ZFailBack Invert
            }

            HLSLPROGRAM
            #pragma vertex   VolumeVertex
            #pragma fragment WhiteFragment
            ENDHLSL
        }
        Pass
        {
            Name "CRP Character Screen Space Shadows"
            
            ZTest GEqual
            ZWrite Off
            ZClip false
            Cull Front
            Blend One One, Zero One
            BlendOp Min, Add

            Stencil 
            {
                Ref 2
                ReadMask 2
                WriteMask 2
                Comp Equal
                Pass Zero
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma multi_compile_fragment _ _CHARACTER_SHADOWS_SOFT

            #pragma vertex   VolumeVertex
            #pragma fragment CharacterShadowFragment
            ENDHLSL
        }
    }
}
