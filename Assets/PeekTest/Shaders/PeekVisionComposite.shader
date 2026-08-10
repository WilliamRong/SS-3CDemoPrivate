Shader "Hidden/PeekTest/PeekVisionComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 3.5
        #pragma vertex Vert
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        TEXTURE2D_X(_PeekBlurTexture);

        float _PeekBlurOffset;
        float4 _PeekBlurTexelSize;
        float4 _PeekVisionOrigin;
        float4 _PeekVisionDirection;
        float4 _PeekVisionParams;
        float4 _PeekVisionVisualParams;
        float4 _PeekVisionTint;
        float3 _PeekRoomMin;
        float3 _PeekRoomMax;

        float EvaluatePeekCone(float3 worldPosition)
        {
            if (_PeekVisionParams.w < 0.5)
            {
                return 0.0;
            }

            float2 toPoint = worldPosition.xy - _PeekVisionOrigin.xy;
            float pointDistance = length(toPoint);
            float2 direction = normalize(_PeekVisionDirection.xy);
            float alignment = dot(toPoint / max(pointDistance, 0.0001), direction);
            float angleMask = smoothstep(_PeekVisionParams.y, _PeekVisionParams.z, alignment);
            float feather = max(_PeekVisionOrigin.w, 0.0001);
            float distanceMask = 1.0 - smoothstep(_PeekVisionParams.x - feather, _PeekVisionParams.x, pointDistance);
            return angleMask * distanceMask * step(0.0001, pointDistance);
        }

        float EvaluateRoom(float3 worldPosition)
        {
            float3 aboveMin = step(_PeekRoomMin, worldPosition);
            float3 belowMax = step(worldPosition, _PeekRoomMax);
            return aboveMin.x * aboveMin.y * aboveMin.z * belowMax.x * belowMax.y * belowMax.z;
        }
        ENDHLSL

        Pass
        {
            Name "KawaseBlur"
            HLSLPROGRAM
            #pragma fragment FragBlur

            half4 FragBlur(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 texel = _PeekBlurTexelSize.xy * _PeekBlurOffset;
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 0.2h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(1.0, 1.0)) * 0.2h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1.0, 1.0)) * 0.2h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(1.0, -1.0)) * 0.2h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1.0, -1.0)) * 0.2h;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma fragment FragComposite

            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 clearColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 blurredColor = SAMPLE_TEXTURE2D_X(_PeekBlurTexture, sampler_LinearClamp, uv);
                float rawDepth = SampleSceneDepth(uv);
                float3 worldPosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float roomMask = EvaluateRoom(worldPosition);
                float coneMask = EvaluatePeekCone(worldPosition);
                half luminance = dot(blurredColor.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 mutedBlur = lerp(luminance.xxx, blurredColor.rgb, _PeekVisionVisualParams.w);
                half outsideBrightness = lerp(
                    _PeekVisionVisualParams.z,
                    _PeekVisionVisualParams.x,
                    _PeekVisionParams.w);
                half3 outsideColor = mutedBlur * outsideBrightness;
                half3 insideColor = clearColor.rgb * _PeekVisionTint.rgb * _PeekVisionVisualParams.y;
                insideColor += _PeekVisionTint.rgb * 0.025h;
                half edgeHighlight = 4.0h * coneMask * (1.0h - coneMask) * _PeekVisionParams.w;
                half3 styledRoom = lerp(outsideColor, insideColor, coneMask);
                styledRoom += _PeekVisionTint.rgb * edgeHighlight * 0.025h;
                half4 roomColor = half4(styledRoom, clearColor.a);
                return lerp(clearColor, roomColor, roomMask);
            }
            ENDHLSL
        }
    }
}
