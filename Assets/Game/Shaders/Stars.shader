// MURAS — звёзды ночного неба: яркие точки, без тумана, проявляются через _Alpha (0 днём → 1 ночью).
Shader "MURAS/Stars"
{
    Properties
    {
        _Color ("Цвет", Color) = (1, 1, 1, 1)
        _Alpha ("Видимость", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                half glow = saturate(1.0 - dot(d, d));
                glow *= glow;
                half twinkle = 0.8 + 0.2 * sin(_Time.y * (2.0 + i.color.a * 5.0) + i.color.a * 40.0);
                return half4(_Color.rgb * i.color.rgb, glow * _Alpha * twinkle);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
