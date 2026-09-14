Shader "Custom/PlayerGroundRing_URP"
{
    Properties
    {
        [HDR]_RingColor ("Ring Color", Color) = (0.2,1,0.9,1)
        _InnerRadius ("Inner Radius", Range(0,1)) = 0.33
        _OuterRadius ("Outer Radius", Range(0,1)) = 0.48
        _Softness ("Softness", Range(0.001,0.2)) = 0.03
        _PulseSpeed ("Pulse Speed", Float) = 2.5
        _PulseAmount ("Pulse Amount", Range(0,0.3)) = 0.06
        _Opacity ("Opacity", Range(0,1)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _RingColor;
                half _InnerRadius;
                half _OuterRadius;
                half _Softness;
                half _PulseSpeed;
                half _PulseAmount;
                half _Opacity;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv - float2(0.5, 0.5);
                float dist = length(uv);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                float distPulsed = dist / max(0.0001, pulse);

                float inner = smoothstep(_InnerRadius, _InnerRadius + _Softness, distPulsed);
                float outer = 1.0 - smoothstep(_OuterRadius - _Softness, _OuterRadius, distPulsed);
                float ring = saturate(inner * outer);

                half alpha = ring * _Opacity * _RingColor.a;
                return half4(_RingColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}