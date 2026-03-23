Shader "BrickBlast/AimLineFlow"
{
    Properties
    {
        [MainTexture] _MainTex ("Pattern", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1.00, 0.90, 0.18, 0.95)
        _TrailColor ("Trail Color", Color) = (1.00, 0.55, 0.06, 0.28)
        _StripeDensity ("Stripe Density", Float) = 14.0
        _FlowSpeed ("Flow Speed", Float) = 1.85
        _TextureContribution ("Texture Contribution", Range(0.0, 1.0)) = 0.85
        _TextureScrollX ("Texture Scroll X", Float) = 0.9
        _TextureScrollY ("Texture Scroll Y", Float) = 0.0
        _PulseSharpness ("Pulse Sharpness", Range(1.0, 8.0)) = 3.2
        _EdgeSoftness ("Edge Softness", Range(0.5, 8.0)) = 2.2
        _Glow ("Glow", Range(0.0, 2.0)) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half4 _TrailColor;
                float _StripeDensity;
                float _FlowSpeed;
                float _TextureContribution;
                float _TextureScrollX;
                float _TextureScrollY;
                float _PulseSharpness;
                float _EdgeSoftness;
                float _Glow;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float stripeDensity = max(1.0, _StripeDensity);
                float flow = frac((input.uv.x * stripeDensity) - (_Time.y * _FlowSpeed));
                float pulse = 1.0 - abs((flow * 2.0) - 1.0);
                pulse = pow(saturate(pulse), _PulseSharpness);

                float edge = 1.0 - abs((input.uv.y * 2.0) - 1.0);
                edge = pow(saturate(edge), _EdgeSoftness);

                float2 textureUv = TRANSFORM_TEX(input.uv, _MainTex);
                textureUv += float2(_Time.y * _TextureScrollX, _Time.y * _TextureScrollY);
                half4 pattern = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, textureUv);

                half3 stripeColor = lerp(_TrailColor.rgb, _BaseColor.rgb, pulse);
                stripeColor *= lerp(half3(1.0h, 1.0h, 1.0h), pattern.rgb, (half)_TextureContribution);
                half brightness = lerp(0.72h, (half)_Glow, (half)pulse);
                half3 finalColor = stripeColor * input.color.rgb * brightness;
                half finalAlpha = saturate(edge * lerp(_TrailColor.a, _BaseColor.a, pulse) * input.color.a);
                finalAlpha *= lerp(1.0h, pattern.a, (half)_TextureContribution);
                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
