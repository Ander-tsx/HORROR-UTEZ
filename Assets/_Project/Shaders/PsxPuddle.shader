// Rain puddle: a dark film of water that mirrors the scene.
//
// The reflection comes from PsxPlanarReflection, which renders the scene from a camera
// mirrored under the water plane and hands the texture to this renderer. That camera is a normal
// rotation, not a mirror matrix, so its picture arrives flipped left-to-right; the lookup
// undoes it with 1 - u. With no planar camera running, the puddle falls back to the
// reflection probes, which still catches the lamps, just without parallax.
//
// Light sources reflected in real puddles smear into vertical streaks because the surface
// is never flat. A handful of taps stacked along screen-space V fakes that for a few ALU,
// and it is most of what makes the reflection read as wet rather than as a mirror.
Shader "HorrorUtez/PSX/Puddle"
{
    Properties
    {
        _WaterTint ("Water tint", Color) = (0.05, 0.06, 0.09, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        _ReflectionStrength ("Reflection strength", Range(0, 2)) = 1.1
        _Streak ("Vertical streak length", Range(0, 0.08)) = 0.025
        _Distortion ("Ripple distortion", Range(0, 0.05)) = 0.012
        _RippleCell ("Ripple cell size (m)", Float) = 0.35
        _RippleSpeed ("Ripple speed", Float) = 1.3
        _Sparkle ("Raindrop sparkle", Range(0, 4)) = 1.6
        _EdgeNoise ("Edge wobble", Range(0, 0.6)) = 0.28
        _EdgeSoftness ("Edge softness", Range(0, 0.2)) = 0.05
        _Seed ("Shape seed", Float) = 0
        [HideInInspector] _PsxPlanarReflectionOn ("Planar reflection on", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PuddleVertex
            #pragma fragment PuddleFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterTint;
                half _Opacity;
                half _ReflectionStrength;
                half _Streak;
                half _Distortion;
                float _RippleCell;
                float _RippleSpeed;
                half _Sparkle;
                half _EdgeNoise;
                half _EdgeSoftness;
                float _Seed;
                half _PsxPlanarReflectionOn;    // set per renderer by PsxPlanarReflection
            CBUFFER_END

            TEXTURE2D(_PsxPlanarReflectionTex);
            half _PsxWetness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return float2(n, Hash21(p + n * 17.0));
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            /// Expanding rings from raindrops, one per cell, returned as a slope (xy) and
            /// a fresh-impact flash (z) for the sparkle.
            float3 Ripples(float2 p, float time, float rain)
            {
                float2 cell = floor(p);
                float2 slope = 0;
                float flash = 0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 id = cell + float2(x, y);
                    float2 rnd = Hash22(id + _Seed);
                    // Fewer drops land when it is barely raining. A multiplier rather than
                    // `continue`, so the unrolled loop stays branch-free on mobile GPUs.
                    float active = step(rnd.x, rain);

                    float2 centre = id + rnd;
                    float phase = frac(time + rnd.y);
                    float2 delta = p - centre;
                    float dist = length(delta);

                    float radius = phase * 0.9;
                    float ring = saturate(1.0 - abs(dist - radius) * 9.0) * (1.0 - phase) * active;
                    slope += delta / max(dist, 1e-3) * ring;
                    flash += saturate(1.0 - dist * 10.0) * saturate(1.0 - phase * 6.0) * active;
                }

                return float3(slope, flash);
            }

            Varyings PuddleVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                return output;
            }

            half4 PuddleFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Shape: a rounded blob with a slow, low-frequency wobble — how water
                // actually pools. The edge is antialiased over one screen pixel (fwidth) plus
                // a thin soft rim, so it reads round instead of stepping like a staircase.
                // The PSX screen pass still pixelates it with everything else.
                float2 centred = (input.uv - 0.5) * 2.0;
                float wobble = ValueNoise(input.uv * 2.2 + _Seed * 7.13) * 0.75
                             + ValueNoise(input.uv * 4.5 - _Seed * 3.1) * 0.25;
                float dist = length(centred) + (wobble - 0.5) * _EdgeNoise;
                float edgeWidth = fwidth(dist) + _EdgeSoftness;
                half mask = 1.0 - smoothstep(0.8 - edgeWidth, 0.8, dist);
                if (mask <= 0.001)
                    discard;

                float rain = saturate(_PsxWetness);
                float2 worldTexel = floor(input.positionWS.xz * 32.0) / 32.0;
                float3 ripple = Ripples(worldTexel / max(_RippleCell, 0.05), _Time.y * _RippleSpeed, rain);

                float3 normalWS = normalize(float3(-ripple.x * 0.35, 1.0, -ripple.y * 0.35));
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float nov = saturate(dot(normalWS, viewWS));

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 reflection;

                if (_PsxPlanarReflectionOn > 0.5)
                {
                    float2 uv = float2(1.0 - screenUv.x, screenUv.y) + ripple.xy * _Distortion;
                    // Weighted vertical smear: bright lights stretch, dark areas barely move.
                    reflection  = SAMPLE_TEXTURE2D(_PsxPlanarReflectionTex, sampler_LinearClamp, uv).rgb * 0.34;
                    reflection += SAMPLE_TEXTURE2D(_PsxPlanarReflectionTex, sampler_LinearClamp, uv + float2(0, _Streak * 0.5)).rgb * 0.22;
                    reflection += SAMPLE_TEXTURE2D(_PsxPlanarReflectionTex, sampler_LinearClamp, uv - float2(0, _Streak * 0.5)).rgb * 0.22;
                    reflection += SAMPLE_TEXTURE2D(_PsxPlanarReflectionTex, sampler_LinearClamp, uv + float2(0, _Streak)).rgb * 0.11;
                    reflection += SAMPLE_TEXTURE2D(_PsxPlanarReflectionTex, sampler_LinearClamp, uv - float2(0, _Streak)).rgb * 0.11;
                }
                else
                {
                    half3 reflectVector = reflect(-viewWS, normalWS);
                    reflection = GlossyEnvironmentReflection(reflectVector, input.positionWS, 0.08, 1.0, screenUv);
                }

                // Real water reflects ~2% head-on. Exaggerated, as the reference renders do:
                // at standing height the puddle has to read from a few metres away.
                half fresnel = lerp(0.45, 1.0, pow(1.0 - nov, 3.0));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 ambient = SampleSH(normalWS);
                half3 waterBase = _WaterTint.rgb * (ambient + mainLight.color * mainLight.shadowAttenuation * 0.2);

                half3 color = lerp(waterBase, reflection * _ReflectionStrength, fresnel);

                // Fresh impacts: single bright texels, the white specks on wet ground.
                half sparkle = step(0.55, ripple.z) * _Sparkle * rain;
                color += sparkle * (ambient + 0.35);

                return half4(color, _Opacity * mask);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
