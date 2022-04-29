// 天啦噜，写完 renderfeature，又要来写 defer 的 shader
// 原版还是个 build-in 的，得升级为 URP 规范，饶了我吧

// 作者的多光源可以分为两部分：
// 1、绘制多边形光源
// 2、查LUT，在CommandBuffer里，绘制光源与其他物体之间的高光效果。叠上去。
// （妙不可言）
// 倘若第二步不做的话，就只有一个干巴巴的多边形光源。甚至它都不是四边形

// 重点居然是这个 shader，草，URP怎么写 defer …容我去学学
// 草啊！URP的 defer 出来得太晚了！属于是大难产的东西，我什么时候直接去学虚幻得了，不会真的会有公司把自己的产品命运压在一个不稳定的玩意儿上吧？
// URP version 12.0 以上才有 defer ，别人的实现例子好难找————此时我有几个选择
// 1：给旧版 URP 写一个 defer ，再不济写一个 MRT（为了一个 shader，写一个新的管线是吧）  https://catlikecoding.com/unity/tutorials/rendering/part-13/
// 2：用版本较高的 URP ，升级现有的 defer shader（这个痛苦程度低一些）
// 3：用版本较低的 URP ，换成 forward （shader 重写）
// 什么通常魔法 “苦涩的选择” ……

// 不是，URP 的摄影机怎么不能把 render path 改成 deferred 了？太坑！

Shader "Hidden/AreaLight" 
{
    Properties
    {
        //_BaseMap ("基础贴图", 2D) = "white" { }
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"  
    //拿深度

    #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Deferred.hlsl"
    //#include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"

    //#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

    //#include "UnityDeferredLibrary.cginc"
    //#include "UnityPBSLighting.cginc"

    #define AREA_LIGHT_ENABLE_DIFFUSE 1

    #include "Assets/LTC/AreaLight.hlsl"
    ENDHLSL 

    SubShader 
    {
        Tags { "Queue" = "Geometry-1" "RenderPipeline" = "UniversalPipeline"} 

        HLSLINCLUDE
    
        //TEXTURE2D_X_FLOAT(_CameraDepthTexture);
        //SAMPLER(sampler_CameraDepthTexture);
        //声明深度图

        //TEXTURE2D_X(_CameraDepthTexture);
        TEXTURE2D_X_HALF(_GBuffer0);
        TEXTURE2D_X_HALF(_GBuffer1);
        TEXTURE2D_X_HALF(_GBuffer2);

        SamplerState my_point_clamp_sampler;
        float4x4 _ScreenToWorld[2];

        float3 _LightPos;
        half3 _LightColor;

        //o.scrPos = ComputeScreenPos(vertexInput.positionCS);

        // void DeferredCalculateLightParams (Varyings i, out float3 outWorldPos, out float2 outUV)
        // {
            //     i.ray = i.ray * (_ProjectionParams.z / i.ray.z);
            //     float2 uv = i.uv.xy / i.uv.w;
            
            //     // read depth and reconstruct world position
            //     float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenPos).r;
            //     depth = Linear01Depth(depth, _ZBufferParams);
            //     float4 vpos = float4(i.ray * depth,1);
            //     float3 wpos = mul (unity_CameraToWorld, vpos).xyz;

            //     outWorldPos = wpos;
            //     outUV = uv;
        // }

        float4 CalculateLightDeferred (Varyings i)
        {
            float3 worldPos = i.positionCS;
            float2 uv;

            // DeferredCalculateLightParams (i, worldPos, uv);

            float2 screen_uv = (i.screenUV.xy / i.screenUV.z);

            // float  depth    = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, my_point_clamp_sampler, screen_uv, 0).x; 
            float  depth    = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv), 0).r;

            float4 gbuffer0 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, my_point_clamp_sampler, screen_uv, 0);
            float4 gbuffer1 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer1, my_point_clamp_sampler, screen_uv, 0);
            float4 gbuffer2 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer2, my_point_clamp_sampler, screen_uv, 0);

            // float4 gbuffer2 = tex2D (_CameraGBufferTexture2, uv);

            // RT0, ARGB32 format: Diffuse color (RGB), occlusion (A).
            // RT1, ARGB32 format: Specular color(RGB), roughness (A).
            // RT2, ARGB2101010 format: World space normal (RGB), unused (A).
            // RT3, ARGB2101010 (non-HDR) or ARGBHalf (HDR) format: Emission + lighting + lightmaps + reflection probes buffer.
            // RT4, ARGB32 format: Light occlusion values (RGBA). （用于SM）
            // Depth + Stencil buffer.

            uint materialFlags = UnpackMaterialFlags(gbuffer0.a);
            bool materialReceiveShadowsOff = (materialFlags & kMaterialFlagReceiveShadowsOff) != 0;

            float3 baseColor = gbuffer0.rgb;
            float3 specColor = gbuffer1.rgb;

            float oneMinusRoughness = gbuffer1.a;
            float3 normalWorld = gbuffer2.rgb * 2 - 1;

            normalWorld = normalize(normalWorld);
            
            return CalculateLight (worldPos, baseColor, specColor, oneMinusRoughness, normalWorld, _LightPos.xyz, _LightColor.xyz).rgbb;
        }
        ENDHLSL

        // no shadows
        Pass
        {
            ZWrite Off
            Blend One One
            Cull Front
            ZTest Always
            
            HLSLPROGRAM 
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers nomrt

            float4 frag (Varyings i) : SV_Target
            {
                return CalculateLightDeferred(i);

                //return float4(0, 0, 0, 1);
            }
            ENDHLSL
        }

    }
    Fallback Off
}
