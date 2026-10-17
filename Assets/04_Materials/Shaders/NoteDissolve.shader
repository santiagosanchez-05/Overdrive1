Shader "Overdrive/NoteDissolve"
    // Adaptado de VrProject (Custom/NoteDissolve) para Overdrive.
{
    // Disolución con ruido (sin textura externa) + borde brillante en el
    // límite. _DissolveAmount en 0 = visible completo, en 1 = invisible.
    // Animar de 1→0 al aparecer (materializar) y de 0→1 al resolverse.
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 2
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EdgeColor ("Color del borde", Color) = (1, 0.9, 0.6, 1)
        _EdgeWidth ("Ancho del borde brillante", Range(0.01, 0.3)) = 0.08
        _NoiseScale ("Escala del ruido", Range(1, 20)) = 6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
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
                float3 positionOSInterp : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _GlowIntensity;
                float _DissolveAmount;
                float4 _EdgeColor;
                float _EdgeWidth;
                float _NoiseScale;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOSInterp = IN.positionOS.xyz;
                return OUT;
            }

            // Ruido simple basado en hash, no necesita textura.
            float hashNoise(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float n = hashNoise(IN.positionOSInterp * _NoiseScale);

                // Recorta donde el ruido es menor al nivel de disolución actual.
                clip(n - _DissolveAmount);

                half3 baseColor = _Color.rgb * _GlowIntensity;

                // Borde brillante justo en el límite de la disolución.
                float edge = smoothstep(0.0, _EdgeWidth, n - _DissolveAmount);
                half3 finalColor = lerp(_EdgeColor.rgb * _GlowIntensity * 1.5, baseColor, edge);

                return half4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}
