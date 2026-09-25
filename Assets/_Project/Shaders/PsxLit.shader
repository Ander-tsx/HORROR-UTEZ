// PS1 surface on top of URP's full physically based lighting.
//
// Replaces the vendored URP-PSX graphs, which pushed ALL lighting through Emission with a
// black BaseColor. That cut every surface off from URP's GI: no light probes, no
// lightmaps, no reflection probes, so nothing could ever look wet, glossy or lit by bounce
// light. Here the PS1 part is only what the hardware actually did to geometry and
// textures — snapped vertices, affine UVs, point-sampled low-res texels — and the light
// itself is modern, which is what makes a crunchy scene read as beautiful instead of flat.
//
// Property names are the vendored graphs' auto-generated references on purpose: a material
// switched from the graph to this shader keeps every value it had.
Shader "HorrorUtez/PSX/Lit"
{
    Properties
    {
        [MainTexture][NoScaleOffset] Texture2D_4450AB74 ("Albedo", 2D) = "white" {}
        Vector2_8044833E ("Tiling", Vector) = (1, 1, 0, 0)
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)

        [Header(Lighting)]
        [ToggleUI] Boolean_8BBF99CD ("Lit", Float) = 1
        Vector1_6F288C5B ("Smoothness", Range(0, 1)) = 0.2
        [ToggleUI] Boolean_7165A49C ("Metal-like specular", Float) = 0
        Color_2E5415DE ("Specular colour", Color) = (0.8, 0.8, 0.8, 1)
        [ToggleUI] _Wettable ("Gets wet in rain", Float) = 0

        [Header(Emission)]
        [NoScaleOffset] _EmissionMap ("Emission map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission colour", Color) = (0, 0, 0, 0)

        [Header(PS1)]
        [ToggleUI] Boolean_43476D73 ("Vertex snapping", Float) = 1
        Vector1_B2CC132F ("Snap grid (virtual rows)", Float) = 320
        [ToggleUI] _PsxSnapSlide ("Snap slides along surface (off for foliage)", Float) = 1
        [ToggleUI] Boolean_85059BBE ("Affine texture warp", Float) = 0
        Vector1_B9994903 ("Affine strength", Range(0, 1)) = 0.3
        [ToggleUI] Boolean_193028D2 ("Texel snapping", Float) = 1
        Vector1_21C41D02 ("Texels per repeat", Float) = 256
        [ToggleUI] Boolean_3F1A8DAB ("Albedo colour depth", Float) = 0
        Vector1_E8746023 ("Levels per channel", Float) = 32
        [HideInInspector] Boolean_D258FF8E ("Camera clipping (unused)", Float) = 0

        [Header(Transparency)]
        _Alpha_Clipping ("Alpha clip threshold", Range(0, 1)) = 0.5
        _Alpha_Multiplier ("Alpha multiplier", Range(0, 1)) = 1

        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _SrcBlendAlpha ("__srcA", Float) = 1
        [HideInInspector] _DstBlendAlpha ("__dstA", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PsxLitVertex
            #pragma fragment PsxLitFragment

            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #pragma multi_compile_instancing

            // Reflectance comes from a specular colour, not a metallic slider: that is what
            // the vendored graphs exposed, so migrated materials keep their meaning.
            #define _SPECULAR_SETUP 1

            #include "PsxLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS       : POSITION;
                float3 normalOS         : NORMAL;
                float2 texcoord         : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv               : TEXCOORD0;
                float3 positionWS       : TEXCOORD1;
                float3 normalWS         : TEXCOORD2;
                float3 affineUv         : TEXCOORD3;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half4 fogFactorAndVertexLight : TEXCOORD4;
            #else
                half fogFactor          : TEXCOORD4;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord      : TEXCOORD5;
            #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 6);
                float4 positionCS       : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings PsxLitVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                float4 positionCS = PsxSnapVertex(vertexInput.positionWS, normalInput.normalWS);

                output.uv = PsxTiledUv(input.texcoord);
                output.affineUv = PsxAffinePack(output.uv, positionCS);
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;

                half fogFactor = ComputeFogFactor(positionCS.z);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
            #else
                output.fogFactor = fogFactor;
            #endif

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
            #endif

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz,
                    GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

                output.positionCS = positionCS;
                return output;
            }

            half4 PsxLitFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = PsxResolveUv(input.uv, input.affineUv);
                half4 albedoAlpha = PsxSampleAlbedo(uv);
                PsxAlphaClip(albedoAlpha.a);

                half3 emission = 0;
            #if defined(_EMISSION)
                emission = SAMPLE_TEXTURE2D(_EmissionMap, samplerTexture2D_4450AB74, uv).rgb * _EmissionColor.rgb;
            #endif

            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half fogFactor = input.fogFactorAndVertexLight.x;
            #else
                half fogFactor = input.fogFactor;
            #endif
                float fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), fogFactor);

                // Unlit: signs, globes, screens. Full brightness, still fogged.
                if (Boolean_8BBF99CD < 0.5)
                {
                    half3 unlitColor = MixFog(albedoAlpha.rgb + emission, fogCoord);
                    return half4(unlitColor, OutputAlpha(albedoAlpha.a, IsSurfaceTypeTransparent(_Surface)));
                }

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedoAlpha.rgb;
                surface.alpha = albedoAlpha.a;
                surface.metallic = 0;
                surface.specular = Boolean_7165A49C > 0.5 ? Color_2E5415DE.rgb : kDielectricSpec.rgb;
                surface.smoothness = Vector1_6F288C5B;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.emission = emission;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                PsxApplyWetness(inputData.normalWS, surface.albedo, surface.smoothness);

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif

                inputData.fogCoord = fogCoord;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
            #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PsxShadowVertex
            #pragma fragment PsxShadowFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "PsxLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Not snapped: a shadow map is not a screen, and snapping to a grid sized for
            // the camera would only add acne.
            Varyings PsxShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                output.uv = PsxTiledUv(input.texcoord);
                return output;
            }

            half4 PsxShadowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                PsxAlphaClip(PsxSampleAlbedo(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PsxDepthVertex
            #pragma fragment PsxDepthFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "PsxLitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Snapped exactly like the forward pass, or the depth prepass and the colour
            // pass disagree about where every vertex is and the surface z-fights itself.
            Varyings PsxDepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.uv = PsxTiledUv(input.texcoord);
                output.positionCS = PsxSnapVertex(TransformObjectToWorld(input.positionOS.xyz),
                    TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half PsxDepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                PsxAlphaClip(PsxSampleAlbedo(input.uv).a);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PsxDepthNormalsVertex
            #pragma fragment PsxDepthNormalsFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            #include "PsxLitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings PsxDepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.uv = PsxTiledUv(input.texcoord);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = PsxSnapVertex(TransformObjectToWorld(input.positionOS.xyz), output.normalWS);
                return output;
            }

            half4 PsxDepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                PsxAlphaClip(PsxSampleAlbedo(input.uv).a);

            #if defined(_GBUFFER_NORMALS_OCT)
                float3 normalWS = normalize(input.normalWS);
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #endif
            }
            ENDHLSL
        }

        // Only used when baking lightmaps: tells the lightmapper each surface's colour and
        // how much light it gives off, so emissive signs and globes bounce onto the walls.
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PsxMetaVertex
            #pragma fragment PsxMetaFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "PsxLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uv2        : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            #ifdef EDITOR_VISUALIZATION
                float2 VizUV      : TEXCOORD1;
                float4 LightCoord : TEXCOORD2;
            #endif
            };

            Varyings PsxMetaVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.uv = PsxTiledUv(input.uv0);
            #ifdef EDITOR_VISUALIZATION
                UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.VizUV, output.LightCoord);
            #endif
                return output;
            }

            half4 PsxMetaFragment(Varyings input) : SV_Target
            {
                half4 albedoAlpha = PsxSampleAlbedo(input.uv);
                PsxAlphaClip(albedoAlpha.a);

                UnityMetaInput meta = (UnityMetaInput)0;
                meta.Albedo = albedoAlpha.rgb;
                meta.Emission = Boolean_8BBF99CD < 0.5 ? albedoAlpha.rgb : half3(0, 0, 0);
            #if defined(_EMISSION)
                meta.Emission += SAMPLE_TEXTURE2D(_EmissionMap, samplerTexture2D_4450AB74, input.uv).rgb * _EmissionColor.rgb;
            #endif
            #ifdef EDITOR_VISUALIZATION
                meta.VizUV = input.VizUV;
                meta.LightCoord = input.LightCoord;
            #endif
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
