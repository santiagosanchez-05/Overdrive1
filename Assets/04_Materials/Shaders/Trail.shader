Shader "Overdrive/Trail"
    // Adaptado de VrProject (Custom/Trail) para Overdrive.
{
    // Estela de la baqueta. Aditivo, sin textura.
    //
    // Lee el COLOR DEL VÉRTICE, que es lo que un Trail Renderer usa para
    // pasarle su degradado a lo largo del rastro. Sin eso, la cola no se
    // desvanece: termina de golpe. (Por eso no se puede reusar StarGlow acá,
    // que ignora el color del vértice y fuerza el alfa a 1.)
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.54, 1)
        _GlowIntensity ("Brillo", Range(0, 20)) = 4
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
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _GlowIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float alpha = IN.color.a;
                half3 glow = _Color.rgb * IN.color.rgb * _GlowIntensity * alpha;
                return half4(glow, alpha);
            }
            ENDHLSL
        }
    }
}
