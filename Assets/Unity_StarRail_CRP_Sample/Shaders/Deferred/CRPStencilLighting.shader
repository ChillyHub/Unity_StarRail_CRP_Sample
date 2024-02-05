Shader "Hidden/StarRail_CRP/Deferred/CRPStencilLighting"
{
    Properties {

    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    #include "HLSL/CRPDeferred.hlsl"
    
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

    #include "../Utils/HLSL/Depth.hlsl"
    
    #include "../Scene/HLSL/SceneLighting.hlsl"
    #include "../Scene/HLSL/SceneShadow.hlsl"
    #include "../SSS/HLSL/SSSLighting.hlsl"

    struct Attributes
    {
        float4 positionOS : POSITION;
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 screenUV : TEXCOORD1;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };

    #if defined(_SPOT)
    float4 _SpotLightScale;
    float4 _SpotLightBias;
    float4 _SpotLightGuard;
    #endif

    Varyings Vertex(Attributes input)
    {
        Varyings output = (Varyings)0;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 positionOS = input.positionOS.xyz;

        #if defined(_SPOT)
        // Spot lights have an outer angle than can be up to 180 degrees, in which case the shape
        // becomes a capped hemisphere. There is no affine transforms to handle the particular cone shape,
        // so instead we will adjust the vertices positions in the vertex shader to get the tighest fit.
        [flatten] if (any(positionOS.xyz))
        {
            // The hemisphere becomes the rounded cap of the cone.
            positionOS.xyz = _SpotLightBias.xyz + _SpotLightScale.xyz * positionOS.xyz;
            positionOS.xyz = normalize(positionOS.xyz) * _SpotLightScale.w;
            // Slightly inflate the geometry to fit the analytic cone shape.
            // We want the outer rim to be expanded along xy axis only, while the rounded cap is extended along all axis.
            positionOS.xyz = (positionOS.xyz - float3(0, 0, _SpotLightGuard.w)) * _SpotLightGuard.xyz + float3(0, 0, _SpotLightGuard.w);
        }
        #endif

        #if defined(_DIRECTIONAL) || defined(_FOG) || defined(_CLEAR_STENCIL_PARTIAL) || (defined(_SSAO_ONLY) && defined(_SCREEN_SPACE_OCCLUSION))
            // Full screen render using a large triangle.
            output.positionCS = float4(positionOS.xy, UNITY_RAW_FAR_CLIP_VALUE, 1.0); // Force triangle to be on zfar
        #elif defined(_SSAO_ONLY) && !defined(_SCREEN_SPACE_OCCLUSION)
            // Deferred renderer does not know whether there is a SSAO feature or not at the C# scripting level.
            // However, this is known at the shader level because of the shader keyword SSAO feature enables.
            // If the keyword was not enabled, discard the SSAO_only pass by rendering the geometry outside the screen.
            output.positionCS = float4(positionOS.xy, -2, 1.0); // Force triangle to be discarded
        #else
            // Light shape geometry is projected as normal.
            VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS.xyz);
            output.positionCS = vertexInput.positionCS;
        #endif

        output.screenUV = output.positionCS.xyw;
        #if UNITY_UV_STARTS_AT_TOP
        output.screenUV.xy = output.screenUV.xy * float2(0.5, -0.5) + 0.5 * output.screenUV.z;
        #else
        output.screenUV.xy = output.screenUV.xy * 0.5 + 0.5 * output.screenUV.z;
        #endif

        return output;
    }

    TEXTURE2D_X(_CameraDepthTexture);
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

    #if defined(GBUFFER_OPTIONAL_SLOT_2) && _RENDER_PASS_ENABLED
    TEXTURE2D_X_HALF(_GBuffer5);
    #elif defined(GBUFFER_OPTIONAL_SLOT_2)
    TEXTURE2D_X(_GBuffer5);
    #endif

    float4x4 _ScreenToWorld[2];
    SamplerState my_point_clamp_sampler;

    float3 _LightPosWS;
    half3 _LightColor;
    half4 _LightAttenuation; // .xy are used by DistanceAttenuation - .zw are used by AngleAttenuation *for SpotLights)
    half3 _LightDirection;   // directional/spotLights support
    half4 _LightOcclusionProbInfo;
    int _LightFlags;
    int _ShadowLightIndex;
    uint _LightLayerMask;
    int _CookieLightIndex;

    half4 FragWhite(Varyings input) : SV_Target
    {
        return half4(1.0, 1.0, 1.0, 1.0);
    }

    Light GetStencilLight(float3 posWS, float2 screen_uv, half4 shadowMask)
    {
        Light unityLight;
        
        uint lightLayerMask = _LightLayerMask;

        #if defined(_CRP_ADDITIONAL_LIGHT_SHADOWS)
            bool materialReceiveShadowsOff = false;
        #else
            bool materialReceiveShadowsOff = true;
        #endif

        float4 shadowCoord = float4(screen_uv, 0.0, 1.0);
        #if defined(_DIRECTIONAL)
            #if defined(_DEFERRED_MAIN_LIGHT)
                unityLight = GetMainLight();
                // unity_LightData.z is set per mesh for forward renderer, we cannot cull lights in this fashion with deferred renderer.
                unityLight.distanceAttenuation = 1.0;
                unityLight.shadowAttenuation = SampleScreenSpaceShadowmap(shadowCoord);;
            #else
                unityLight.distanceAttenuation = 1.0;
                unityLight.direction = _LightDirection;
                unityLight.distanceAttenuation = 1.0;
                unityLight.shadowAttenuation = 1.0;
                unityLight.color = _LightColor.rgb;
                unityLight.layerMask = lightLayerMask;
                unityLight.shadowAttenuation = SampleScreenSpaceShadowmap(shadowCoord);;
            #endif
        #else
            PunctualLightData light;
            light.posWS = _LightPosWS;
            light.radius2 = 0.0; //  only used by tile-lights.
            light.color = float4(_LightColor, 0.0);
            light.attenuation = _LightAttenuation;
            light.spotDirection = _LightDirection;
            light.occlusionProbeInfo = _LightOcclusionProbInfo;
            light.flags = _LightFlags;
            light.layerMask = lightLayerMask;
            unityLight = UnityLightFromPunctualLightDataAndWorldSpacePosition2(light, posWS.xyz, shadowCoord, materialReceiveShadowsOff);
        #endif

        return unityLight;
        
        //#if defined(_DIRECTIONAL)
        //    #if defined(_DEFERRED_MAIN_LIGHT)
        //        unityLight = GetMainLight();
        //        // unity_LightData.z is set per mesh for forward renderer, we cannot cull lights in this fashion with deferred renderer.
        //        unityLight.distanceAttenuation = 1.0;
//
        //        if (!materialReceiveShadowsOff)
        //        {
        //            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        //                float4 shadowCoord = float4(screen_uv, 0.0, 1.0);
        //            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        //                float4 shadowCoord = TransformWorldToShadowCoord(posWS.xyz);
        //            #else
        //                float4 shadowCoord = float4(0, 0, 0, 0);
        //            #endif
//
        //            #if !defined(_MAIN_LIGHT_SHADOWS_SCREEN) && defined(_SURFACE_TYPE_TRANSPARENT) && (defined(_SCENE) || defined(_SSS))
        //                unityLight.shadowAttenuation = CustomMainLightShadow(shadowCoord, posWS.xyz, shadowMask, _MainLightOcclusionProbes);
        //            #else
        //                unityLight.shadowAttenuation = MainLightShadow(shadowCoord, posWS.xyz, shadowMask, _MainLightOcclusionProbes);
        //            #endif
        //        }
//
        //        #if defined(_LIGHT_COOKIES)
        //            real3 cookieColor = SampleMainLightCookie(posWS);
        //            unityLight.color *= float4(cookieColor, 1);
        //        #endif
        //    #else
        //        unityLight.direction = _LightDirection;
        //        unityLight.distanceAttenuation = 1.0;
        //        unityLight.shadowAttenuation = 1.0;
        //        unityLight.color = _LightColor.rgb;
        //        unityLight.layerMask = lightLayerMask;
//
        //        if (!materialReceiveShadowsOff)
        //        {
        //            #if defined(_ADDITIONAL_LIGHT_SHADOWS)
        //                unityLight.shadowAttenuation = AdditionalLightShadow(_ShadowLightIndex, posWS.xyz, _LightDirection, shadowMask, _LightOcclusionProbInfo);
        //            #endif
        //        }
        //    #endif
        //#else
        //    PunctualLightData light;
        //    light.posWS = _LightPosWS;
        //    light.radius2 = 0.0; //  only used by tile-lights.
        //    light.color = float4(_LightColor, 0.0);
        //    light.attenuation = _LightAttenuation;
        //    light.spotDirection = _LightDirection;
        //    light.occlusionProbeInfo = _LightOcclusionProbInfo;
        //    light.flags = _LightFlags;
        //    light.layerMask = lightLayerMask;
        //    unityLight = UnityLightFromPunctualLightDataAndWorldSpacePosition(light, posWS.xyz, shadowMask, _ShadowLightIndex, materialReceiveShadowsOff);
//
        //    #ifdef _LIGHT_COOKIES
        //        // Enable/disable is done toggling the keyword _LIGHT_COOKIES, but we could do a "static if" instead if required.
        //        // if(_CookieLightIndex >= 0)
        //        {
        //            float4 cookieUvRect = GetLightCookieAtlasUVRect(_CookieLightIndex);
        //            float4x4 worldToLight = GetLightCookieWorldToLightMatrix(_CookieLightIndex);
        //            float2 cookieUv = float2(0,0);
        //            #if defined(_SPOT)
        //                cookieUv = ComputeLightCookieUVSpot(worldToLight, posWS, cookieUvRect);
        //            #endif
        //            #if defined(_POINT)
        //                cookieUv = ComputeLightCookieUVPoint(worldToLight, posWS, cookieUvRect);
        //            #endif
        //            half4 cookieColor = SampleAdditionalLightsCookieAtlasTexture(cookieUv);
        //            cookieColor = half4(IsAdditionalLightsCookieAtlasTextureRGBFormat() ? cookieColor.rgb
        //                                : IsAdditionalLightsCookieAtlasTextureAlphaFormat() ? cookieColor.aaa
        //                                : cookieColor.rrr, 1);
        //            unityLight.color *= cookieColor;
        //        }
        //    #endif
        //#endif
        //return unityLight;
    }

    half4 DeferredShading(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 screen_uv = (input.screenUV.xy / input.screenUV.z);

        #if defined(_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
        float2 undistorted_screen_uv = screen_uv;
        screen_uv = input.positionCS.xy * _ScreenSize.zw;
        #endif
        
        #if _RENDER_PASS_ENABLED
        float d        = LOAD_FRAMEBUFFER_INPUT(GBUFFER3, input.positionCS.xy).x;
        half4 gbuffer0 = LOAD_FRAMEBUFFER_INPUT(GBUFFER0, input.positionCS.xy);
        half4 gbuffer1 = LOAD_FRAMEBUFFER_INPUT(GBUFFER1, input.positionCS.xy);
        //half4 gbuffer2 = LOAD_FRAMEBUFFER_INPUT(GBUFFER2, input.positionCS.xy);
        #else
        // Using SAMPLE_TEXTURE2D is faster than using LOAD_TEXTURE2D on iOS platforms (5% faster shader).
        // Possible reason: HLSLcc upcasts Load() operation to float, which doesn't happen for Sample()?
        float d        = SampleDepth(screen_uv); // raw depth value has UNITY_REVERSED_Z applied on most platforms.
        half4 gbuffer0 = SAMPLE_TEXTURE2D_X_LOD(_CustomGBuffer0, my_point_clamp_sampler, screen_uv, 0);
        half4 gbuffer1 = SAMPLE_TEXTURE2D_X_LOD(_CustomGBuffer1, my_point_clamp_sampler, screen_uv, 0);
        #endif
        #if defined(_DEFERRED_MIXED_LIGHTING)
        half4 shadowMask = SAMPLE_TEXTURE2D_X_LOD(MERGE_NAME(_, GBUFFER_SHADOWMASK), my_point_clamp_sampler, screen_uv, 0);
        #else
        half4 shadowMask = 1.0;
        #endif

        // return half4(gbuffer0.rgb, 1.0);

        //#ifdef _LIGHT_LAYERS
        //float4 renderingLayers = SAMPLE_TEXTURE2D_X_LOD(MERGE_NAME(_, GBUFFER_LIGHT_LAYERS), my_point_clamp_sampler, screen_uv, 0);
        //uint meshRenderingLayers = uint(renderingLayers.r * 255.5);
        //#else
        //uint meshRenderingLayers = DEFAULT_LIGHT_LAYERS;
        //#endif

        half surfaceDataOcclusion = 1.0;
        // uint materialFlags = UnpackMaterialFlags(gbuffer0.a);

        half3 color = (0.0).xxx;
        half alpha = 1.0;

        #if defined(_DEFERRED_MIXED_LIGHTING)
        // If both lights and geometry are static, then no realtime lighting to perform for this combination.
        [branch] if ((_LightFlags & materialFlags) == kMaterialFlagSubtractiveMixedLighting)
            return half4(color, alpha); // Cannot discard because stencil must be updated.
        #endif

        #if defined(USING_STEREO_MATRICES)
        int eyeIndex = unity_StereoEyeIndex;
        #else
        int eyeIndex = 0;
        #endif
        float4 posWS = mul(_ScreenToWorld[eyeIndex], float4(input.positionCS.xy, d, 1.0));
        posWS.xyz *= rcp(posWS.w);

        Light unityLight = GetStencilLight(posWS.xyz, screen_uv, shadowMask);

        //#ifdef _LIGHT_LAYERS
        //float4 renderingLayers = SAMPLE_TEXTURE2D_X_LOD(MERGE_NAME(_, GBUFFER_LIGHT_LAYERS), my_point_clamp_sampler, screen_uv, 0);
        //uint meshRenderingLayers = DecodeMeshRenderingLayer(renderingLayers.r);
        //[branch] if (!IsMatchingLightLayer(unityLight.layerMask, meshRenderingLayers))
        //    return half4(color, alpha); // Cannot discard because stencil must be updated.
        //#endif

        #if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
            AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(screen_uv);
            unityLight.color *= aoFactor.directAmbientOcclusion;
            #if defined(_DIRECTIONAL) && defined(_DEFERRED_FIRST_LIGHT)
            // What we want is really to apply the mininum occlusion value between the baked occlusion from surfaceDataOcclusion and real-time occlusion from SSAO.
            // But we already applied the baked occlusion during gbuffer pass, so we have to cancel it out here.
            // We must also avoid divide-by-0 that the reciprocal can generate.
            half occlusion = aoFactor.indirectAmbientOcclusion < surfaceDataOcclusion ? aoFactor.indirectAmbientOcclusion * rcp(surfaceDataOcclusion) : 1.0;
            alpha = occlusion;
            #endif
        #endif

        InputData inputData = InputDataFromGBufferAndWorldPosition(gbuffer1, posWS.xyz);

        bool specularOff = false;
        //specularOff = true;
        #if defined(_POINT)
            //specularOff = true;
        #endif
        
        #if defined(_UNLIT)
        #elif defined(_CHARACTER)
        #elif defined(_SCENE)
            color = SceneLighting(inputData, gbuffer0, gbuffer1, unityLight, specularOff);
        #elif defined(_SSS)
            color = SSSLighting(inputData, gbuffer0, gbuffer1, unityLight, specularOff);
        #endif

        return half4(color, alpha);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}

        // 0 - Stencil pass
        Pass
        {
            Name "Stencil Volume"

            ZTest LEQual
            ZWrite Off
            ZClip false
            Cull Off
            ColorMask 0

            Stencil {
                Ref 0
                ReadMask 96
                WriteMask 16
                CompFront NotEqual
                PassFront Keep
                ZFailFront Invert
                CompBack NotEqual
                PassBack Keep
                ZFailBack Invert
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_vertex _ _SPOT

            #pragma vertex Vertex
            #pragma fragment FragWhite

            ENDHLSL
        }

        // 1 - UnLit Directional Light
        Pass
        {
            Name "UnLit Directional Light"

            ZTest NotEqual
            ZWrite Off
            Cull Off
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add

            Stencil {
                Ref 0
                ReadMask 96
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_fragment _DEFERRED_STENCIL
            #pragma multi_compile _DIRECTIONAL
            #pragma multi_compile_fragment _UNLIT
            //#pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile_fragment _ _DEFERRED_MAIN_LIGHT
            //#pragma multi_compile_fragment _ _DEFERRED_FIRST_LIGHT
            //#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            //#pragma multi_compile_fragment _ _SHADOWS_SOFT
            //#pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            //#pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            //#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            //#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            //#pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            //#pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma vertex Vertex
            #pragma fragment DeferredShading

            ENDHLSL
        }

        // 2 - Character Directional Light
        Pass
        {
            Name "Character Directional Light"

            ZTest NotEqual
            ZWrite Off
            Cull Off
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add

            Stencil {
                Ref 32
                ReadMask 96
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_fragment _DEFERRED_STENCIL
            #pragma multi_compile _DIRECTIONAL
            #pragma multi_compile_fragment _CHARACTER
            //#pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile_fragment _ _DEFERRED_MAIN_LIGHT
            //#pragma multi_compile_fragment _ _DEFERRED_FIRST_LIGHT
            //#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            //#pragma multi_compile_fragment _ _SHADOWS_SOFT
            //#pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            //#pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            //#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            //#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            //#pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            //#pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma vertex Vertex
            #pragma fragment DeferredShading

            ENDHLSL
        }
        
        // 3 - Scene Directional Light
        Pass
        {
            Name "Scene Directional Light"

            ZTest NotEqual
            ZWrite Off
            Cull Off
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add

            Stencil {
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

            #pragma multi_compile_fragment _DEFERRED_STENCIL
            #pragma multi_compile _DIRECTIONAL
            #pragma multi_compile_fragment _SCENE
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _DEFERRED_MAIN_LIGHT
            #pragma multi_compile_fragment _ _DEFERRED_FIRST_LIGHT
            //#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _CRP_ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma vertex Vertex
            #pragma fragment DeferredShading

            ENDHLSL
        }
        
        // 4 - SSS Directional Light
        Pass
        {
            Name "SSS Directional Light"

            ZTest NotEqual
            ZWrite Off
            Cull Off
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add

            Stencil {
                Ref 96
                ReadMask 96
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_fragment _DEFERRED_STENCIL
            #pragma multi_compile _DIRECTIONAL
            #pragma multi_compile_fragment _SSS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _DEFERRED_MAIN_LIGHT
            #pragma multi_compile_fragment _ _DEFERRED_FIRST_LIGHT
            //#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _CRP_ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma vertex Vertex
            #pragma fragment DeferredShading

            ENDHLSL
        }
        
        // 5 - UnLit Additional Light
        Pass
        {
            Name "UnLit Additional Light"

            ZTest GEqual
            ZWrite Off
            ZClip false
            Cull Front
            Blend One One, Zero One
            BlendOp Add, Add

            Stencil {
                Ref 16
                ReadMask 112
                WriteMask 16
                Comp Equal
                Pass Zero
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_fragment _DEFERRED_STENCIL
            #pragma multi_compile _POINT _SPOT
            #pragma multi_compile_fragment _UNLIT
            //#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            //#pragma multi_compile_fragment _ _SHADOWS_SOFT
            //#pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            //#pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            //#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            //#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            //#pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            //#pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma vertex Vertex
            #pragma fragment DeferredShading

            ENDHLSL
        }
        
        // 6 - Character Additional Light
        Pass
        {
            Name "Character Additional Light"

            ZTest GEqual
            ZWrite Off
            ZClip false
            Cull Front
            Blend One One, Zero One
            BlendOp Add, Add

            Stencil {
                Ref 48
                ReadMask 112
                WriteMask 16
                Comp Equal
                Pass Zero
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_fragment _DEFERRED_STENCIL
            #pragma multi_compile _POINT _SPOT
            #pragma multi_compile_fragment _CHARACTER
            //#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            //#pragma multi_compile_fragment _ _SHADOWS_SOFT
            //#pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            //#pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            //#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            //#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            //#pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            //#pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma vertex Vertex
            #pragma fragment DeferredShading

            ENDHLSL
        }
        
        // 7 - Scene Additional Light
        Pass
        {
            Name "Scene Additional Light"

            ZTest GEqual
            ZWrite Off
            ZClip false
            Cull Front
            Blend One One, Zero One
            BlendOp Add, Add

            Stencil {
                Ref 80
                ReadMask 112
                WriteMask 16
                Comp Equal
                Pass Zero
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_fragment _DEFERRED_STENCIL
            #pragma multi_compile _POINT _SPOT
            #pragma multi_compile_fragment _SCENE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            //#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _CRP_ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma vertex Vertex
            #pragma fragment DeferredShading

            ENDHLSL
        }
        
        // 8 - SSS Additional Light
        Pass
        {
            Name "SSS Additional Light"

            ZTest GEqual
            ZWrite Off
            ZClip false
            Cull Front
            Blend One One, Zero One
            BlendOp Add, Add

            Stencil {
                Ref 112
                ReadMask 112
                WriteMask 16
                Comp Equal
                Pass Zero
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_fragment _DEFERRED_STENCIL
            #pragma multi_compile _POINT _SPOT
            #pragma multi_compile_fragment _SSS
            //#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _CRP_ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma vertex Vertex
            #pragma fragment DeferredShading

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
