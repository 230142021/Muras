// MURAS — небо: градиент зенит→горизонт под время суток, горизонт в цвет тумана (горы плавно тонут в дымке),
// диск солнца со свечением, ореол на закате, луна с сиянием и мерцающие звёзды. Параметры задаёт MurasSky (GraphicsPolish.cs).
Shader "MURAS/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.22, 0.45, 0.85, 1)
        _HorizonColor ("Horizon", Color) = (0.73, 0.82, 0.93, 1)
        _GroundColor ("Below horizon", Color) = (0.35, 0.38, 0.40, 1)
        _SunDir ("Sun direction", Vector) = (0, 0.5, 0.8, 0)
        [HDR] _SunColor ("Sun color", Color) = (6, 5.6, 5, 1)
        _SunSize ("Sun size (rad)", Float) = 0.035
        _SunAlpha ("Sun visible", Float) = 1
        [HDR] _SunsetColor ("Sunset halo", Color) = (1.6, 0.7, 0.35, 1)
        _Sunset ("Sunset amount", Range(0, 1)) = 0
        _MoonDir ("Moon direction", Vector) = (0, 0.6, -0.8, 0)
        [HDR] _MoonColor ("Moon color", Color) = (2.2, 2.3, 2.6, 1)
        _MoonAlpha ("Moon visible", Float) = 0
        _StarAlpha ("Stars", Float) = 0
        _HazeHeight ("Horizon haze", Float) = 0.12
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor;
                float4 _SunDir; half4 _SunColor; float _SunSize; float _SunAlpha;
                half4 _SunsetColor; float _Sunset;
                float4 _MoonDir; half4 _MoonColor; float _MoonAlpha;
                float _StarAlpha; float _HazeHeight;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash3(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // процедурные звёзды: одна звезда на ячейку сетки направлений, яркость и мерцание из хеша
            half Stars(float3 dir)
            {
                float3 p = dir * 210.0;
                float3 cell = floor(p);
                float h = Hash3(cell);
                if (h < 0.965) return 0;
                float3 starPos = cell + 0.5 + (float3(Hash3(cell + 1.7), Hash3(cell + 3.1), Hash3(cell + 5.3)) - 0.5) * 0.7;
                float d = length(p - starPos);
                float size = lerp(0.10, 0.24, frac(h * 37.0));
                float core = saturate(1.0 - d / size);
                float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.5 + frac(h * 91.0) * 3.0) + h * 60.0);
                float bright = lerp(0.6, 2.4, pow(frac(h * 13.0), 3.0));
                return core * core * bright * twinkle;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float h = dir.y;

                // градиент неба: у горизонта — цвет тумана, выше — зенит
                float up = saturate(h);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, 0.45));
                // под горизонтом — та же дымка, переходящая в тёмную землю
                col = h < 0 ? lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-h * 4.0)) : col;
                // плотная полоса дымки у самого горизонта
                half haze = exp(-abs(h) / max(_HazeHeight, 0.01));
                col = lerp(col, _HorizonColor.rgb * 1.04, haze * 0.6);

                // солнце: диск + мягкое сияние + ореол заката вдоль горизонта
                float3 sd = normalize(_SunDir.xyz);
                float sdot = dot(dir, sd);
                float ang = acos(clamp(sdot, -1.0, 1.0));
                half disk = smoothstep(_SunSize * 1.15, _SunSize * 0.85, ang);
                half glow = pow(saturate(sdot), 12.0) * 0.35 + pow(saturate(sdot), 180.0) * 0.9;
                col += _SunColor.rgb * (disk * 1.0 + glow * 0.18) * _SunAlpha * step(-0.02, h);
                half halo = pow(saturate(sdot * 0.5 + 0.5), 4.0) * exp(-abs(h) * 5.0);
                col += _SunsetColor.rgb * halo * _Sunset;

                // луна: диск с лёгким узором и сияние
                float3 md = normalize(_MoonDir.xyz);
                float mdot = dot(dir, md);
                float mang = acos(clamp(mdot, -1.0, 1.0));
                half mdisk = smoothstep(0.034, 0.028, mang);
                half pattern = 0.82 + 0.18 * Hash3(floor(dir * 900.0));
                half mglow = pow(saturate(mdot), 60.0) * 0.25 + pow(saturate(mdot), 600.0) * 0.6;
                col += _MoonColor.rgb * (mdisk * pattern + mglow * 0.3) * _MoonAlpha * step(-0.01, h);

                // звёзды (тускнеют к горизонту и не светят сквозь луну)
                half starMask = saturate(h * 6.0) * (1.0 - mdisk);
                col += Stars(dir) * _StarAlpha * starMask * half3(0.95, 0.97, 1.0);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
