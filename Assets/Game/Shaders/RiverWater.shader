// MURAS — горная речка (URP). Течение вдоль русла, рябь, блики солнца/луны, искры на ряби,
// отражение настоящего неба (цвета берутся из MURAS/Sky под время суток), прозрачная на мелководье,
// пена по краям, барашки на перекатах и пена вокруг камней (по глубине сцены).
// UV меша: x — поперёк русла (0..1), y — вдоль течения (в метрах / 4).
Shader "MURAS/RiverWater"
{
    Properties
    {
        _ShallowColor ("Цвет у берега", Color) = (0.36, 0.62, 0.62, 0.55)
        _DeepColor ("Цвет в середине", Color) = (0.07, 0.27, 0.36, 0.88)
        _SkyColor ("Отражение неба (если нет MURAS/Sky)", Color) = (0.62, 0.78, 0.95, 1)
        _HorizonColor ("Отражение горизонта (если нет MURAS/Sky)", Color) = (0.85, 0.9, 0.95, 1)
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
        _RockFoam ("Пена у камней (м)", Range(0, 1)) = 0.09
        _Sparkle ("Искры на ряби", Range(0, 4)) = 0.9
        _Clarity ("Прозрачность на мелководье (м)", Range(0.05, 3)) = 0.7
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                half _RockFoam;
                half _Sparkle;
                half _Clarity;
            CBUFFER_END

            // цвета неба от MURAS/Sky (a = 1, если заданы)
            half4 _MurasSkyZenith;
            half4 _MurasSkyHorizon;

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
                float4 screenPos : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float t = _Time.y;
                ws.y += (sin(ws.x * 1.3 + t * 1.7) + sin(ws.z * 0.9 - t * 2.1)) * _WaveHeight;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;
                float2 riverUV = float2(i.uv.x * 1.0, i.uv.y) * _Tiling * 4.0;
                float2 uvA = riverUV + float2(0.0, -t);
                float2 uvB = riverUV * 1.7 + float2(0.37, -t * 1.45);
                float2 uvC = riverUV * 3.3 + float2(0.71, -t * 2.2);

                half3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvA), _NormalStrength);
                half3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvB), _NormalStrength * 0.7);
                half3 nC = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvC), _NormalStrength * 0.45);
                half2 tilt = (nA.xy + nB.xy + nC.xy) * 0.75;
                half3 N = normalize(half3(tilt.x, 1.0, tilt.y));
                // для отражения — более гладкая нормаль, иначе вся рябь пятнами отражает яркий горизонт
                half3 Nr = normalize(half3(tilt.x * 0.3, 1.0, tilt.y * 0.3));

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = lerp(0.55, 1.0, mainLight.shadowAttenuation);

                // ---- глубина воды по буферу глубины: мелко → прозрачно и пена у камней/берега
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float waterEye = i.screenPos.w;
                float thick = max(0.0, sceneEye - waterEye);

                half edge = saturate(abs(i.uv.x - 0.5) * 2.0);
                half depth = (1.0 - edge * edge) * saturate(thick / max(_Clarity, 0.01));
                half4 body = lerp(_ShallowColor, _DeepColor, depth);

                half ndl = saturate(dot(N, mainLight.direction));
                half3 ambient = SampleSH(N) * 0.6;
                half3 col = body.rgb * (mainLight.color * (0.35 + 0.65 * ndl) * shadow + ambient);

                // ---- отражение неба: цвета MURAS/Sky (меняются со временем суток) или запасные из материала
                half3 skyTop = _MurasSkyZenith.a > 0.5 ? _MurasSkyZenith.rgb : _SkyColor.rgb;
                half3 skyHor = _MurasSkyHorizon.a > 0.5 ? _MurasSkyHorizon.rgb : _HorizonColor.rgb;
                float3 R = reflect(-V, Nr);
                half3 sky = lerp(skyHor, skyTop, saturate(R.y * 1.5)) * 0.9;
                half fresnel = pow(1.0 - saturate(dot(Nr, V)), 5.0);
                col = lerp(col, sky, saturate(fresnel * 0.6 + 0.06));

                // ---- блики солнца (ночью — луны) и искры на гребешках ряби
                float3 H = normalize(mainLight.direction + V);
                half nh = saturate(dot(N, H));
                half spec = pow(nh, _Gloss) * _Specular * shadow;
                half glintNoise = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uvC * 2.3 + float2(t * 0.3, 0)).r;
                half sparkle = pow(nh, _Gloss * 0.3) * step(0.86, glintNoise) * _Sparkle * shadow;
                col += mainLight.color * (spec + sparkle);

                // ---- пена: у берегов, барашки по течению и кольца вокруг камней
                half foamNoise = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, riverUV * 0.6 + float2(0.1, -t * 1.2)).r;
                half foamNoise2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, riverUV * 1.9 + float2(0.6, -t * 1.9)).r;
                half edgeFoam = smoothstep(1.0 - _FoamEdge - 0.06, 1.0 - _FoamEdge * 0.2, edge + (foamNoise - 0.5) * 0.25);
                half streaks = smoothstep(1.0 - _FoamAmount * 0.35, 1.0, foamNoise * 0.6 + foamNoise2 * 0.55) * (0.4 + 0.6 * depth);
                half rock = 1.0 - saturate(thick / max(_RockFoam, 0.01));
                // только узкая кайма там, где дно/камень почти у поверхности (а не вся мелкая речка)
                half rockFoam = smoothstep(0.6, 0.92, rock + (foamNoise2 - 0.5) * 0.35) * step(0.001, _RockFoam) * 0.85;
                half foam = saturate(edgeFoam + streaks + rockFoam);
                col = lerp(col, _FoamColor.rgb * (mainLight.color * 0.8 * shadow + ambient * 0.8), foam);

                half alpha = saturate(body.a * lerp(0.35, 1.0, saturate(thick / max(_Clarity, 0.01))) + fresnel * 0.2 + foam * 0.8);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
