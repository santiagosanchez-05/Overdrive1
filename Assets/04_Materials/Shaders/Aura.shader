Shader "Overdrive/Aura"
    // Adaptado de VrProject (Custom/Aura) para Overdrive.
{
    // Halo de luz constante, pensado para envolver la luna. Misma idea de
    // dos capas que las luciérnagas (núcleo + halo ancho), pero sin parpadeo:
    // esta nunca se apaga, solo respira muy despacio.
    //
    // Se orienta a cámara en el vertex shader, así que da igual que el objeto
    // padre esté girando (la luna gira sobre su eje) — el halo siempre se ve
    // redondo y de frente.
    //
    // Va sobre un Quad normal de Unity. El tamaño lo da _Size, no la escala
    // del objeto, porque el quad se reconstruye acá adentro.
    Properties
    {
        _Color ("Color", Color) = (0.05, 0.12, 1, 1)
        _GlowIntensity ("Brillo", Range(0, 40)) = 6

        _CoreSharpness ("Qué tan chico es el núcleo", Range(1, 30)) = 4
        _CoreWhiteness ("Cuánto se va a blanco el núcleo", Range(0, 1)) = 0.35
        _HaloFalloff ("Suavidad del halo", Range(0.3, 4)) = 1.6
        _HaloStrength ("Fuerza del halo", Range(0, 2)) = 0.9

        _Size ("Tamaño (unidades de mundo)", Float) = 3

        _PulseSpeed ("Velocidad del latido", Float) = 0.4
        _PulseAmount ("Cuánto respira", Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 offset : TEXCOORD0;
                float glow : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _GlowIntensity;
                float _CoreSharpness;
                float _CoreWhiteness;
                float _HaloFalloff;
                float _HaloStrength;
                float _Size;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // El Quad de Unity va de -0.5 a 0.5: lo llevamos a -1..1.
                float2 corner = IN.positionOS.xy * 2.0;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 camRight = UNITY_MATRIX_V[0].xyz;
                float3 camUp = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = centerWS + (camRight * corner.x + camUp * corner.y) * _Size * 0.5 * pulse;

                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.offset = corner;
                OUT.glow = pulse;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float d = saturate(1.0 - length(IN.offset));

                float core = pow(d, _CoreSharpness);
                float halo = pow(d, _HaloFalloff) * _HaloStrength;
                float shape = core + halo;

                half3 tint = lerp(_Color.rgb, half3(1, 1, 1), core * _CoreWhiteness);
                half3 color = tint * _GlowIntensity * IN.glow * shape;

                return half4(color, saturate(shape));
            }
            ENDHLSL
        }
    }
}
