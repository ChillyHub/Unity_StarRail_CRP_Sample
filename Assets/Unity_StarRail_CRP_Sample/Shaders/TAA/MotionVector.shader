Shader "Hidden/StarRail_CRP/TAA/MotionVector" 
{
	SubShader
    {
        Pass
        {
            Name "Camera Motion Vectors"

            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma multi_compile_fragment _ _FOVEATED_RENDERING_NON_UNIFORM_RASTER
            #pragma never_use_dxc metal

            //#pragma exclude_renderers d3d11_9x
            #pragma target 3.5

            #pragma vertex CameraMotionVectorVertex
            #pragma fragment CameraMotionVectorFragment

            #include "HLSL/MotionVectorPass.hlsl"

            ENDHLSL
        }
    }
}