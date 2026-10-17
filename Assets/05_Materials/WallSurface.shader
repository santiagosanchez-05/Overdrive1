Shader "Overdrive/Wall Surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Wall Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Top Color", Color) = (0.22, 0.29, 0.27, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.15
        [HideInInspector] _Metallic("Metallic", Float) = 0
        [HideInInspector] _SpecColor("Specular", Color) = (0.2, 0.2, 0.2, 1)
        [HideInInspector] _BumpScale("Normal Scale", Float) = 1
        [HideInInspector] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        [HideInInspector] _Cutoff("Alpha Cutoff", Float) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "DisableBatching" = "True" }
        Pass
        {
            Name "WallForward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment WallFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog

            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            half4 WallFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Walls are unit cubes scaled in the scene. Anchor the skirting
                // to the bottom on every side, independently of the cube UVs.
                float3 positionOS = TransformWorldToObject(input.positionWS);
                float wallHeight = length(TransformObjectToWorldDir(float3(0, 1, 0), false));
                float textureWidth = wallHeight * _BaseMap_TexelSize.z / _BaseMap_TexelSize.w;
                float horizontal = abs(input.normalWS.x) > abs(input.normalWS.z)
                    ? input.positionWS.z : input.positionWS.x;
                float2 wallUV = float2(horizontal / max(textureWidth, 0.001), saturate(positionOS.y + 0.5));

                SurfaceData surface;
                InitializeStandardLitSurfaceData(wallUV, surface);
                surface.albedo = abs(input.normalWS.y) > 0.5
                    ? _BaseColor.rgb : SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, wallUV).rgb;
                surface.alpha = 1;

                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                InitializeBakedGIData(input, lighting);
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
