Shader "Hidden/StarRail_CRP/TAA/TemporalAA" 
{
	SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        
        LOD 100
        ZTest Always 
        ZWrite Off 
        Blend Off 
        Cull Off
        
        Pass
        {
            Name "Temporal AA"

            Cull Off
            ZWrite On

            HLSLPROGRAM
            //#pragma exclude_renderers gles
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL
            #pragma multi_compile _ TAA_YCOCG

            #pragma vertex Vert
            #pragma fragment TemporalAAFragment

            #include "HLSL/TemporalAAPass.hlsl"

            ENDHLSL
        }
    }
}