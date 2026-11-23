// MURAS — горная речка (URP). Течение вдоль русла, рябь, блики солнца, отражение неба,
// прозрачная у берегов и тёмная в середине, пена по краям и барашки на перекатах.
// UV меша: x — поперёк русла (0..1), y — вдоль течения (в метрах / 4).
Shader "MURAS/RiverWater"
{
    Properties
    {
        _ShallowColor ("Цвет у берега", Color) = (0.36, 0.62, 0.62, 0.55)
        _DeepColor ("Цвет в середине", Color) = (0.07, 0.27, 0.36, 0.88)
        _SkyColor ("Отражение неба", Color) = (0.62, 0.78, 0.95, 1)
        _HorizonColor ("Отражение горизонта", Color) = (0.85, 0.9, 0.95, 1)
        _FoamColor ("Пена", Color) = (0.96, 0.98, 1, 1)
        [Normal][NoScaleOffset] _NormalTex ("Рябь (normal map)", 2D) = "bump" {}
        [NoScaleOffset] _FoamTex ("Шум пены", 2D) = "gray" {}
        _NormalStrength ("Сила ряби", Range(0, 2)) = 0.8
        _Tiling ("Масштаб ряби", Float) = 0.45
        _FlowSpeed ("Скорость течения", Float) = 0.55
        _FoamEdge ("Ширина пены у берега", Range(0, 0.5)) = 0.12
        _FoamAmount ("Барашки на воде", Range(0, 1)) = 0.35
        _Gloss ("Блеск", Range(8, 512)) = 220
        _Specular ("Сила блика", Range(0, 4)) = 1.6
        _WaveHeight ("Высота волн (м)", Float) = 0.025
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 200

        Pass
        {
            Name "RiverForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
            TEXTURE2D(_FoamTex);   SAMPLER(sampler_FoamTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _SkyColor;
                half4 _HorizonColor;
                half4 _FoamColor;
                half _NormalStrength;
                float _Tiling;
                float _FlowSpeed;
                half _FoamEdge;
                half _FoamAmount;
                half _Gloss;
                half _Specular;
                float _WaveHeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float t = _Time.y;
                ws.y += (sin(ws.x * 1.3 + t * 1.7) + sin(ws.z * 0.9 - t * 2.1)) * _WaveHeight;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;
                // UV вдоль русла: x поперёк (≈4 м ширины), y вдоль
                float2 riverUV = float2(i.uv.x * 1.0, i.uv.y) * _Tiling * 4.0;
                float2 uvA = riverUV + float2(0.0, -t);
                float2 uvB = riverUV * 1.7 + float2(0.37, -t * 1.45);

                half3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvA), _NormalStrength);
                half3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvB), _NormalStrength * 0.7);
                half2 tilt = nA.xy + nB.xy;
                half3 N = normalize(half3(tilt.x, 1.0, tilt.y));

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = lerp(0.55, 1.0, mainLight.shadowAttenuation);

                // Глубина: середина тёмная, у берегов светлая и прозрачная
                half edge = saturate(abs(i.uv.x - 0.5) * 2.0);
                half depth = 1.0 - edge * edge;
                half4 body = lerp(_ShallowColor, _DeepColor, depth);

                // Освещение воды
                half ndl = saturate(dot(N, mainLight.direction));
                half3 ambient = SampleSH(N) * 0.6;
                half3 col = body.rgb * (mainLight.color * (0.35 + 0.65 * ndl) * shadow + ambient);

                // Отражение неба (без кубмапы) + Френель
                float3 R = reflect(-V, N);
                half3 sky = lerp(_HorizonColor.rgb, _SkyColor.rgb, saturate(R.y * 1.5));
                half fresnel = pow(1.0 - saturate(dot(N, V)), 4.0);
                col = lerp(col, sky, saturate(fresnel * 0.85 + 0.08));

                // Солнечные блики
                float3 H = normalize(mainLight.direction + V);
                half spec = pow(saturate(dot(N, H)), _Gloss) * _Specular * shadow;
                col += mainLight.color * spec;

                // Пена: у берегов и барашки, плывущие по течению
                half foamNoise = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, riverUV * 0.6 + float2(0.1, -t * 1.2)).r;
                half foamNoise2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, riverUV * 1.9 + float2(0.6, -t * 1.9)).r;
                half edgeFoam = smoothstep(1.0 - _FoamEdge - 0.06, 1.0 - _FoamEdge * 0.2, edge + (foamNoise - 0.5) * 0.25);
                half streaks = smoothstep(1.0 - _FoamAmount * 0.35, 1.0, foamNoise * 0.6 + foamNoise2 * 0.55) * (0.4 + 0.6 * depth);
                half foam = saturate(edgeFoam + streaks);
                col = lerp(col, _FoamColor.rgb * (mainLight.color * 0.8 * shadow + ambient * 0.8), foam);

                half alpha = saturate(body.a + fresnel * 0.35 + foam * 0.8);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
