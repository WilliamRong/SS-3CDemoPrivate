Shader "PeekTest/PeekZombie"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.65, 0.9, 0.68, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "PeekZombieForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            float4 _PeekVisionOrigin;
            float4 _PeekVisionDirection;
            float4 _PeekVisionParams;
            float4 _PeekVisionVisualParams;
            float4 _PeekVisionTint;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 toPoint = input.positionWS.xy - _PeekVisionOrigin.xy;
                float pointDistance = length(toPoint);
                float alignment = dot(
                    toPoint / max(pointDistance, 0.0001),
                    normalize(_PeekVisionDirection.xy));
                float angleMask = smoothstep(_PeekVisionParams.y, _PeekVisionParams.z, alignment);
                float feather = max(_PeekVisionOrigin.w, 0.0001);
                float distanceMask = 1.0 - smoothstep(_PeekVisionParams.x - feather, _PeekVisionParams.x, pointDistance);
                float visibility = angleMask * distanceMask * _PeekVisionParams.w;
                clip(visibility - 0.01);

                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                Light mainLight = GetMainLight();
                half lighting = saturate(dot(normalize(input.normalWS), mainLight.direction)) * 0.65h + 0.35h;
                baseColor.rgb *= mainLight.color * lighting;
                half3 characterTint = lerp(1.0h.xxx, _PeekVisionTint.rgb, 0.3h);
                baseColor.rgb *= characterTint * min(_PeekVisionVisualParams.y, 1.1h);
                baseColor.a *= saturate(visibility * 2.0);
                return baseColor;
            }
            ENDHLSL
        }
    }
}
