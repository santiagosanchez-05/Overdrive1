Shader "Overdrive/LedLine"
{
    // Tira de LEDs de un solo color para un LineRenderer (UV: x a lo largo, y de un borde al otro).
    // Núcleo brillante, casi blanco en el centro, y halo suave hacia los bordes del ancho de la línea.
    // Respira despacio; con _Flicker en 1 parpadea de forma irregular, como una tira con un contacto roto.
    // El parpadeo usa la misma fórmula que Overdrive.Level.LedFlicker (C#), para que las luces reales
    // del cable parpadeen a la vez que la tira.
    Properties
    {
        _Color ("Color", Color) = (0.45, 0.1, 1, 1)
        _Brightness ("Brillo", Range(0, 10)) = 3
        _CoreWidth ("Ancho del núcleo (0 a 1)", Range(0.05, 1)) = 0.3
        _GlowStrength ("Fuerza del halo", Range(0, 2)) = 0.6
        _Breath ("Cuánto respira", Range(0, 0.5)) = 0.12
        _BreathSpeed ("Velocidad de la respiración", Float) = 1.5
        _Flicker ("Parpadeo de tira rota (0 o 1)", Range(0, 1)) = 0
        _Seed ("Semilla", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LedLine"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Brightness;
                float _CoreWidth;
                float _GlowStrength;
                float _Breath;
                float _BreathSpeed;
                float _Flicker;
                float _Seed;
            CBUFFER_END

            // Misma fórmula que LedFlicker.Level en C#: casi siempre encendida, a ratos cae a 0.15.
            float FlickerLevel(float time, float seed)
            {
                float n = frac(sin((floor(time * 10.0) + seed * 13.0) * 91.345) * 47453.5453);
                return n < 0.18 ? 0.15 : 1.0;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float across = abs(IN.uv.y * 2.0 - 1.0); // 0 en el centro, 1 en el borde
                float core = 1.0 - smoothstep(_CoreWidth * 0.5, _CoreWidth, across);
                float glow = exp(-across * across * 3.0) * _GlowStrength;

                float breath = 1.0 + _Breath * sin(_Time.y * _BreathSpeed + _Seed * 6.2831);
                float flicker = lerp(1.0, FlickerLevel(_Time.y, _Seed), _Flicker);

                // El centro tira un poco a blanco, como un LED encendido de verdad.
                half3 tint = lerp(_Color.rgb, half3(1, 1, 1), core * 0.25);
                half3 color = tint * _Brightness * (core + glow) * breath * flicker;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
