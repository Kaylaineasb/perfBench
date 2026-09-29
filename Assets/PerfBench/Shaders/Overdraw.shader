// Camada transparente em tela cheia para estressar fillrate (overdraw).
// Loop de ruído controlado por _PB_OverdrawAlu (global).
Shader "PerfBench/Overdraw"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.6, 0.7, 1.0, 0.04)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Overdraw"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PerfBenchNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 seed : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(IN);
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                // uv + distância da camada => cada camada tem um padrão diferente
                float layer = TransformObjectToWorld(float3(0, 0, 0)).z;
                o.seed = float3(IN.uv * 6.0, layer * 3.0 + _Time.y * 0.5);
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float n = PB_StressNoise(IN.seed, (int)_PB_OverdrawAlu);
                return half4(_BaseColor.rgb * (0.5 + n), _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
