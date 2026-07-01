Shader "RungTramTraSu/RealtimeGrassInteractiveWind"
{
    Properties
    {
        _BaseMap("Grass Plate", 2D) = "white" {}
        _BaseColor("Tint", Color) = (0.58, 0.68, 0.42, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.36
        _WindStrength("Wind Strength", Range(0, 1)) = 0.18
        _WindSpeed("Wind Speed", Range(0, 5)) = 1.1
        _WindScale("Wind Scale", Range(0.01, 2)) = 0.22
        _Flutter("Blade Flutter", Range(0, 1)) = 0.12
        _BladeHeight("Blade Height", Range(0.1, 3)) = 1.25
        _InteractionStrength("Interaction Strength", Range(0, 2)) = 0.85
        _FlattenStrength("Flatten Strength", Range(0, 2)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _WindStrength;
                half _WindSpeed;
                half _WindScale;
                half _Flutter;
                half _BladeHeight;
                half _InteractionStrength;
                half _FlattenStrength;
            CBUFFER_END

            float4 _GrassWindDirection;
            float4 _GrassWindParams;
            float4 _GrassInteractor0;
            float4 _GrassInteractor1;
            float4 _GrassInteractor0Data;
            float4 _GrassInteractor1Data;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float2 SafeNormalize2(float2 v)
            {
                float lenSq = max(dot(v, v), 0.0001);
                return v * rsqrt(lenSq);
            }

            float2 ApplyInteractor(float3 worldPos, float tipWeight, float4 interactor, float4 data)
            {
                float radius = max(interactor.w, 0.001);
                float2 toBlade = worldPos.xz - interactor.xz;
                float dist = length(toBlade);
                float influence = saturate(1.0 - dist / radius);
                influence = influence * influence * (3.0 - 2.0 * influence);

                float dynamicBoost = lerp(0.72, 1.35, saturate(data.z));
                float push = data.x * _InteractionStrength * dynamicBoost;
                float2 direction = SafeNormalize2(toBlade);
                return direction * influence * tipWeight * push;
            }

            float ApplyFlatten(float3 worldPos, float tipWeight, float4 interactor, float4 data)
            {
                float radius = max(interactor.w, 0.001);
                float dist = distance(worldPos.xz, interactor.xz);
                float influence = saturate(1.0 - dist / radius);
                influence = influence * influence * (3.0 - 2.0 * influence);

                float dynamicBoost = lerp(0.65, 1.25, saturate(data.z));
                return influence * tipWeight * data.y * _FlattenStrength * dynamicBoost;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float tip = saturate(input.positionOS.y / max(_BladeHeight, 0.001));
                tip = tip * tip;

                float2 windDir = SafeNormalize2(_GrassWindDirection.xy);
                float windStrength = max(_GrassWindParams.x, _WindStrength);
                float windSpeed = max(_GrassWindParams.y, _WindSpeed);
                float windScale = max(_GrassWindParams.z, _WindScale);
                float flutterStrength = max(_GrassWindParams.w, _Flutter);

                float phase = dot(positionWS.xz, float2(0.73, 1.31)) * windScale + _Time.y * windSpeed;
                float wave = sin(phase) * 0.65 + sin(phase * 1.73 + positionWS.x * 0.37) * 0.35;
                float flutter = sin(phase * 3.2 + input.uv.y * 5.0) * flutterStrength;
                float2 windOffset = (windDir * wave + float2(-windDir.y, windDir.x) * flutter) * windStrength * tip;

                float2 interactionOffset = 0;
                interactionOffset += ApplyInteractor(positionWS, tip, _GrassInteractor0, _GrassInteractor0Data);
                interactionOffset += ApplyInteractor(positionWS, tip, _GrassInteractor1, _GrassInteractor1Data);

                float flatten = 0;
                flatten += ApplyFlatten(positionWS, tip, _GrassInteractor0, _GrassInteractor0Data);
                flatten += ApplyFlatten(positionWS, tip, _GrassInteractor1, _GrassInteractor1Data);

                positionWS.xz += windOffset + interactionOffset;
                positionWS.y -= flatten;

                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalize(normalWS);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a - _Cutoff);

                half3 albedo = tex.rgb * _BaseColor.rgb;
                Light mainLight = GetMainLight();
                half ndotl = saturate(abs(dot(normalize(input.normalWS), mainLight.direction)));
                half3 ambient = SampleSH(normalize(input.normalWS)) * 0.55;
                half3 color = albedo * (ambient + mainLight.color * (0.35 + ndotl * 0.65));
                color = MixFog(color, input.fogFactor);
                return half4(color, tex.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
