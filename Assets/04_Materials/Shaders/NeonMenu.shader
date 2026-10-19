Shader "Overdrive/UI/Neon Atmosphere"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Strength("Neon Strength", Range(0,1)) = 0.8
        _Artwork("Color Existing Artwork", Float) = 0
        _NeonTime("Unscaled Time", Float) = 1
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect;
            float _Strength, _Artwork, _NeonTime;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _NeonTime * .13;
                float3 pink = float3(1.0, .035, .32);
                float3 cyan = float3(.025, .77, 1.0);
                float blend = smoothstep(.2, .88, uv.x + .035 * sin(t));
                float3 neon = lerp(pink, cyan, blend);
                float2 a = (uv - float2(.08, .32 + .045*sin(t))) * float2(1.1, .7);
                float2 b = (uv - float2(.92, .7 + .035*cos(t))) * float2(1.1, .75);
                float glow = exp(-dot(a,a)*7.0) + exp(-dot(b,b)*8.0);
                float breath = .94 + .06*sin(t*1.7);
                fixed4 c;
                if (_Artwork > .5)
                {
                    c = tex2D(_MainTex, uv) + _TextureSampleAdd;
                    float light = dot(c.rgb, float3(.2126,.7152,.0722));
                    float edge = saturate(abs(uv.x-.5)*2.4);
                    float highlight = smoothstep(.25,.8,light) * (.24 + .66*edge);
                    c.rgb = lerp(c.rgb, neon * light * 1.15, highlight * _Strength);
                    c.rgb += neon * glow * .055 * _Strength * breath * (1.0-light);
                }
                else
                {
                    c.rgb = neon * .8;
                    c.a = saturate(glow * .32 * _Strength * breath);
                }
                c *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - .001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
