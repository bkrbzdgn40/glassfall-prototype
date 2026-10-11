Shader "Glassfall/NeonVideoBackground"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.7
        _Brightness ("Brightness", Range(0.5, 1.1)) = 0.86
        _Saturation ("Saturation", Range(0, 1.2)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Opacity;
                float _Brightness;
                float _Saturation;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    saturate(input.uv));
                half luminance = dot(
                    source.rgb,
                    half3(0.2126h, 0.7152h, 0.0722h));
                half3 color = lerp(
                    luminance.xxx,
                    source.rgb,
                    saturate(_Saturation));
                color *= _Tint.rgb * _Brightness;
                return half4(
                    saturate(color),
                    saturate(source.a * _Opacity * _Tint.a));
            }
            ENDHLSL
        }
    }
}
