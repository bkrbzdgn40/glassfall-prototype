Shader "Glassfall/VideoBackgroundBlur"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _BlurRadius ("Blur Radius (Pixels)", Range(0, 12)) = 8
        _BlurMix ("Blur Mix", Range(0, 1)) = 0.98
        _Brightness ("Brightness", Range(0.5, 1.1)) = 1
        _Saturation ("Saturation", Range(0, 1.2)) = 1
        _TargetAspect ("Target Aspect", Float) = 1.7777778
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
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Opacity;
                float _BlurRadius;
                float _BlurMix;
                float _Brightness;
                float _Saturation;
                float _TargetAspect;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float2 FitOutsideUv(float2 uv)
            {
                float textureWidth = max(_MainTex_TexelSize.z, 1.0);
                float textureHeight = max(_MainTex_TexelSize.w, 1.0);
                float sourceAspect = textureWidth / textureHeight;
                float targetAspect = max(_TargetAspect, 0.01);
                float2 scale = float2(1.0, 1.0);

                if (sourceAspect > targetAspect)
                    scale.x = targetAspect / sourceAspect;
                else
                    scale.y = sourceAspect / targetAspect;

                return saturate((uv - 0.5) * scale + 0.5);
            }

            half4 SampleVideo(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = FitOutsideUv(input.uv);
                float2 texel = _MainTex_TexelSize.xy * _BlurRadius;

                half4 center = SampleVideo(uv);
                float2 halfTexel = texel * 0.5;

                // 13-tap kernel. The first phase can be deliberately heavy without
                // turning the pixel-art background into a single smeared sample ring.
                half4 blurred = center * 0.16h;
                blurred += SampleVideo(uv + float2(texel.x, 0.0)) * 0.08h;
                blurred += SampleVideo(uv - float2(texel.x, 0.0)) * 0.08h;
                blurred += SampleVideo(uv + float2(0.0, texel.y)) * 0.08h;
                blurred += SampleVideo(uv - float2(0.0, texel.y)) * 0.08h;
                blurred += SampleVideo(uv + texel) * 0.05h;
                blurred += SampleVideo(uv - texel) * 0.05h;
                blurred += SampleVideo(uv + float2(texel.x, -texel.y)) * 0.05h;
                blurred += SampleVideo(uv + float2(-texel.x, texel.y)) * 0.05h;
                blurred += SampleVideo(uv + float2(halfTexel.x, 0.0)) * 0.08h;
                blurred += SampleVideo(uv - float2(halfTexel.x, 0.0)) * 0.08h;
                blurred += SampleVideo(uv + float2(0.0, halfTexel.y)) * 0.08h;
                blurred += SampleVideo(uv - float2(0.0, halfTexel.y)) * 0.08h;

                half3 softened = lerp(center.rgb, blurred.rgb, saturate(_BlurMix));
                half luminance = dot(softened, half3(0.2126h, 0.7152h, 0.0722h));
                softened = lerp(luminance.xxx, softened, _Saturation);
                softened *= _Tint.rgb * _Brightness;

                return half4(saturate(softened), saturate(_Opacity * _Tint.a));
            }
            ENDHLSL
        }
    }
}
