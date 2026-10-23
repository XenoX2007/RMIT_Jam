// Rèm cực quang: hai lớp nhiễu sọc dọc trôi ngang, màu xanh lá ở dưới chuyển tím ở trên, cộng sáng (additive).
Shader "Custom/Aurora"
{
    Properties
    {
        _MainTex ("Streak Noise", 2D) = "white" {}
        _ColorLow ("Low Color", Color) = (0.15, 1, 0.55, 1)
        _ColorHigh ("High Color", Color) = (0.55, 0.3, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Speed ("Drift Speed", Float) = 0.015
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ColorLow;
                float4 _ColorHigh;
                float _Intensity;
                float _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(i.uv.x * 1.3 + t, i.uv.y * 0.35)).r;
                float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(i.uv.x * 2.7 - t * 1.6, i.uv.y * 0.5 + t * 0.4)).r;
                // soft glow everywhere on the curtain + brighter streaks where the two layers overlap
                float bands = 0.22 + smoothstep(0.06, 0.55, a * b * 2.2) * 1.3;

                // soft bottom edge, long fade to the top, fade at both ends of the curtain
                float bottom = smoothstep(0.0, 0.18, i.uv.y);
                float top = 1.0 - smoothstep(0.45, 1.0, i.uv.y);
                float ends = smoothstep(0.0, 0.12, i.uv.x) * (1.0 - smoothstep(0.88, 1.0, i.uv.x));

                float3 col = lerp(_ColorLow.rgb, _ColorHigh.rgb, saturate(i.uv.y * 1.4));
                return half4(col * bands * bottom * top * ends * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
