Shader "RungTramTraSu/VolumetricSunShaft"
{
    Properties
    {
        _BaseColor ("Warm Sun Color", Color) = (1.0, 0.78, 0.42, 0.18)
        _Intensity ("Intensity", Range(0, 3)) = 0.75
        _EdgeSoftness ("Edge Softness", Range(0.5, 8)) = 3.2
        _FadeIn ("Fade In", Range(0.01, 0.5)) = 0.12
        _FadeOut ("Fade Out", Range(0.01, 0.5)) = 0.28
        _NoiseScale ("Noise Scale", Range(0, 16)) = 6.0
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.42
        _DriftSpeed ("Air Drift Speed", Range(-2, 2)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SunShaft"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Intensity;
                float _EdgeSoftness;
                float _FadeIn;
                float _FadeOut;
                float _NoiseScale;
                float _NoiseStrength;
                float _DriftSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float fogFactor : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float side = 1.0 - abs(input.uv.x * 2.0 - 1.0);
                side = pow(saturate(side), _EdgeSoftness);

                float startFade = smoothstep(0.0, max(_FadeIn, 0.001), input.uv.y);
                float endFade = 1.0 - smoothstep(1.0 - max(_FadeOut, 0.001), 1.0, input.uv.y);
                float lengthFade = saturate(startFade * endFade);

                float airPhase = input.uv.y * _NoiseScale + input.color.r * 17.0 + _TimeParameters.y * _DriftSpeed;
                float ribbon = sin(airPhase * 6.2831853 + input.uv.x * 5.0) * 0.5 + 0.5;
                float grain = Hash21(floor(float2(input.uv.x * 16.0, input.uv.y * 48.0) + input.color.r * 31.0));
                float noise = lerp(1.0, saturate(0.62 + ribbon * 0.32 + grain * 0.18), _NoiseStrength);

                float alpha = saturate(_BaseColor.a * _Intensity * input.color.a * side * lengthFade * noise);
                float3 color = _BaseColor.rgb * input.color.rgb * _Intensity;
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
