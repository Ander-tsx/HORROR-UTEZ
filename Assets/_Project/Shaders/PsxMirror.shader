// A working wall mirror: shows what PsxPlanarReflection renders from the mirrored camera.
//
// The reflection texture arrives flipped left-to-right (the reflection camera is a plain
// rotation, see PsxPlanarReflection), so it is read at 1 - u in screen space. A public
// toilet mirror is never clean: a faint film and a few specks
// and streaks that catch the light — the kind of surface you do not want to look into
// for too long at night.
Shader "HorrorUtez/PSX/Mirror"
{
    Properties
    {
        _Tint ("Silvering tint", Color) = (0.86, 0.88, 0.9, 1)
        _Grime ("Grime", Range(0, 1)) = 0.35
        [HideInInspector] _PsxPlanarReflectionOn ("Planar reflection on", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MirrorVertex
            #pragma fragment MirrorFragment
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Grime;
                half _PsxPlanarReflectionOn;    // set per renderer by PsxPlanarReflection
            CBUFFER_END

            TEXTURE2D(_PsxPlanarReflectionTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
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

            Varyings MirrorVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 MirrorFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 reflection;
                if (_PsxPlanarReflectionOn > 0.5)
                {
                    reflection = SAMPLE_TEXTURE2D(_PsxPlanarReflectionTex, sampler_LinearClamp,
                        float2(1.0 - screenUv.x, screenUv.y)).rgb;
                }
                else
                {
                    float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half3 reflectVector = reflect(-viewWS, normalize(input.normalWS));
                    reflection = GlossyEnvironmentReflection(reflectVector, input.positionWS, 0.05, 1.0, screenUv);
                }

                // World-anchored grime, in 1 cm cells so it stays put as you walk past.
                float2 cell = floor(input.positionWS.xz * 100.0 + input.positionWS.yy * 100.0);
                float speck = step(0.985, Hash21(cell));
                float streak = Hash21(floor(float2(input.positionWS.x * 60.0 + input.positionWS.z * 60.0, 0.0)));
                half film = _Grime * (0.12 + 0.08 * streak);

                half3 color = reflection * _Tint.rgb;
                color = lerp(color, half3(0.32, 0.33, 0.3), film);
                color += speck * _Grime * 0.25;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
