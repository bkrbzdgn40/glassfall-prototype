Shader "Glassfall/WetnessTrail"
{
    Properties
    {
        [PerRendererData] _MainTex ("Wetness Texture", 2D) = "white" {}
        _WetTint ("Wet Tint", Color) = (0.16, 0.42, 0.62, 1)
        _HighlightColor ("Highlight Color", Color) = (0.72, 0.90, 0.98, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.38
        _HighlightStrength ("Highlight Strength", Range(0, 2)) = 0.18
        _ResponseExponent ("Response Exponent", Range(0.01, 2)) = 0.92
        _FadedThreshold ("Faded Threshold", Range(0, 1)) = 0.30
        _FreshThreshold ("Fresh Threshold", Range(0, 1)) = 0.65
        _FadedOpacityMultiplier ("Faded Opacity Multiplier", Range(0, 1)) = 0.24
        _AgingOpacityMultiplier ("Aging Opacity Multiplier", Range(0, 1)) = 0.42
        _FreshHighlightMultiplier ("Fresh Highlight Multiplier", Range(0, 2)) = 1.10
        _NeighborBlend ("Neighbor Blend", Range(0, 1)) = 0.46
        _EdgeSoftness ("Edge Softness", Range(0.5, 4)) = 1.55
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
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _WetTint;
                float4 _HighlightColor;
                float _Opacity;
                float _HighlightStrength;
                float _ResponseExponent;
                float _FadedThreshold;
                float _FreshThreshold;
                float _FadedOpacityMultiplier;
                float _AgingOpacityMultiplier;
                float _FreshHighlightMultiplier;
                float _NeighborBlend;
                float _EdgeSoftness;
            CBUFFER_END

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
                float2 texel = _MainTex_TexelSize.xy;
                half centerWetness = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv).a;
                half upWetness = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv + float2(0.0, texel.y)).a;
                half downWetness = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv - float2(0.0, texel.y)).a;
                half leftWetness = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv - float2(texel.x, 0.0)).a;
                half rightWetness = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv + float2(texel.x, 0.0)).a;

                half neighborAverage = (
                    centerWetness +
                    upWetness +
                    downWetness +
                    leftWetness +
                    rightWetness) * 0.2h;
                half sourceWetness = lerp(
                    centerWetness,
                    neighborAverage,
                    saturate((half)_NeighborBlend));

                half visibleWetness = pow(
                    saturate(sourceWetness),
                    max(_ResponseExponent, 0.01));
                half fadedThreshold = max(
                    (half)_FadedThreshold,
                    0.001h);
                half freshThreshold = clamp(
                    (half)_FreshThreshold,
                    fadedThreshold,
                    0.999h);

                half fadedTransition = smoothstep(
                    0.0h,
                    fadedThreshold,
                    sourceWetness);
                half freshTransition = smoothstep(
                    freshThreshold,
                    1.0h,
                    sourceWetness);
                half agingTransition = smoothstep(
                    fadedThreshold,
                    freshThreshold,
                    sourceWetness);

                half lifecycleOpacity = lerp(
                    (half)_FadedOpacityMultiplier,
                    (half)_AgingOpacityMultiplier,
                    fadedTransition);
                lifecycleOpacity = lerp(
                    lifecycleOpacity,
                    1.0h,
                    agingTransition);

                half lifecycleHighlight = lerp(
                    0.22h,
                    0.52h,
                    fadedTransition);
                lifecycleHighlight = lerp(
                    lifecycleHighlight,
                    (half)_FreshHighlightMultiplier,
                    freshTransition);

                half highlight = pow(visibleWetness, 1.35h) *
                    _HighlightStrength *
                    lifecycleHighlight;
                half3 color = saturate(
                    _WetTint.rgb + _HighlightColor.rgb * highlight);

                half edgeAlpha = smoothstep(
                    0.0h,
                    0.12h * max((half)_EdgeSoftness, 0.5h),
                    visibleWetness);
                half alpha = visibleWetness *
                    edgeAlpha *
                    _Opacity *
                    lifecycleOpacity *
                    input.color.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
