Shader "Overdrive/LightBeam"
    // Adaptado de VrProject (Custom/LightBeam) para Overdrive.
{
    // Haz de luz apuntando hacia arriba: brillante en la base, se desvanece
    // hacia la punta (degradado vertical), y más visible de canto que de
    // frente (Fresnel) — para que se sienta como un rayo de luz suave/
    // volumétrico en vez de un cono sólido de color parejo.
    //
    // Pensado para un Cylinder/Cone default de Unity (pivot en el centro,
    // altura total 2 unidades locales, de Y=-1 a Y=1).
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 2
        _FadeHeight ("Media altura local del haz (1 = Cylinder default)", Float) = 1
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOSInterp : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _GlowIntensity;
                float _FadeHeight;
                float _FresnelPower;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOSInterp = IN.positionOS.xyz;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Degradado vertical: brillante en la base (Y local negativo),
                // se apaga hacia la punta (Y local positivo).
                float t = (IN.positionOSInterp.y + _FadeHeight) / (2.0 * _FadeHeight);
                float verticalFade = saturate(1.0 - t);

                // Fresnel: más transparente mirando de frente, más visible en
                // el borde/canto — da sensación de volumen, no de cono sólido.
                float3 normalWS = normalize(IN.normalWS);
                // abs(): con Cull Off también se ven las caras traseras; sin abs su dot es negativo y brillaban enteras.
                float fresnel = pow(1.0 - abs(dot(normalWS, IN.viewDirWS)), _FresnelPower);

                float alpha = saturate(verticalFade * fresnel);
                half3 glow = _Color.rgb * _GlowIntensity * alpha;

                return half4(glow, alpha);
            }
            ENDHLSL
        }
    }
}
