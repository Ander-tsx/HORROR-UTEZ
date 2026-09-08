// Screen-space depth fog in the URP-PSX spirit, rewritten for URP 17 / Render Graph.
//
// The original (docs/skills/URP-PSX-FORKED) was built for the BUILT-IN pipeline:
// CGPROGRAM, UnityCG.cginc, sampler2D _MainTex. It also folded raw non-linear depth
// straight into the falloff, which only produced fog at all because of reversed-Z, and
// gave parameters no physical meaning.
//
// This version keeps the character that matters — exponential falloff, screen-space
// noise breaking up the gradient, fog colour tinted by ambient — but runs the falloff on
// LINEAR EYE DEPTH. Distances are therefore in metres, which is what makes the fog
// tunable against a campus measured in metres.
//
// Depth arrives through _PsxSceneDepth, bound explicitly by the render feature, NOT via
// the global _CameraDepthTexture: under Render Graph that global was not bound for this
// pass and every pixel read back as the far plane, fogging the whole screen flat.
Shader "HorrorUtez/PSX/Fog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "PSX Fog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Sampled with sampler_PointClamp, already declared by URP's Core.hlsl.
            // Point, not linear: softening the scene before fogging it would undo the
            // crunch the PSX material pass just went to the trouble of creating.
            TEXTURE2D_X(_PsxSceneDepth);

            float4 _FogColor;
            float4 _AmbientColor;
            float _FogDensity;
            float _FogStart;
            float _NoiseScale;
            float _NoiseStrength;
            float _NoiseSpeed;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // Sky and anything past the far plane come back as maximum eye depth, so
                // they fog out completely. That is the point: the horizon disappears and
                // the map has no visible edge.
                float rawDepth = SAMPLE_TEXTURE2D_X(_PsxSceneDepth, sampler_PointClamp, uv).r;
                float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                float distance = max(0.0, eyeDepth - _FogStart);
                float fog = 1.0 - exp2(-_FogDensity * distance);

                float2 noiseUv = uv * _ScreenParams.xy / max(_NoiseScale, 1.0);
                noiseUv += _Time.y * _NoiseSpeed;
                float noise = (ValueNoise(noiseUv) - 0.5) * 2.0;

                fog = saturate(fog + noise * _NoiseStrength);

                half3 fogged = _FogColor.rgb * _AmbientColor.rgb;
                return half4(lerp(sceneColor.rgb, fogged, fog), sceneColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
