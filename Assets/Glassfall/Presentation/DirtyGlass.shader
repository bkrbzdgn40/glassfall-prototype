Shader "Glassfall/DirtyGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Dirty Texture", 2D) = "white" {}
        _DirtyTint ("Dirty Tint", Color) = (0.48, 0.39, 0.33, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.22
        _NoiseScale ("Noise Scale", Range(0.01, 20)) = 3.5
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.12
        _EdgeSoftness ("Edge Softness", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DirtyTint;
                float _Opacity;
                float _NoiseScale;
                float _NoiseStrength;
                float _EdgeSoftness;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float lower = lerp(
                    Hash21(cell),
                    Hash21(cell + float2(1.0, 0.0)),
                    local.x);
                float upper = lerp(
                    Hash21(cell + float2(0.0, 1.0)),
                    Hash21(cell + float2(1.0, 1.0)),
                    local.x);
                return lerp(lower, upper, local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.texcoord;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv);
                float noise = ValueNoise(input.uv * _NoiseScale);
                float variation = lerp(
                    1.0 - _NoiseStrength,
                    1.0 + _NoiseStrength,
                    noise);
                float edgeDistance = min(
                    min(input.uv.x, 1.0 - input.uv.x),
                    min(input.uv.y, 1.0 - input.uv.y));
                float edge = smoothstep(
                    0.0,
                    max(_EdgeSoftness, 0.0001),
                    edgeDistance);
                half3 color = saturate(
                    source.rgb * _DirtyTint.rgb * variation);
                half alpha = source.a * _Opacity * edge * input.color.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
