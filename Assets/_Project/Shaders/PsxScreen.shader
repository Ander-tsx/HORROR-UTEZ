// The screen-space half of the PSX look: pixelation, colour-depth reduction with
// ordered dithering, and a CRT pass (barrel warp, chromatic aberration, scanlines,
// vignette, grain).
//
// All four live in ONE fragment pass on purpose. As separate render features each would
// cost its own full-screen blit, and this project ships to mobile. They also chain
// naturally: warp the UV, snap it to the low-res grid, sample, crush the colour, then
// overlay the tube artefacts.
//
// Rewritten for URP 17 rather than ported — the originals in docs/skills/URP-PSX-FORKED
// target the built-in pipeline (CGPROGRAM, UnityCG.cginc, sampler2D _MainTex).
Shader "HorrorUtez/PSX/Screen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "PSX Screen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelHeight;      // internal vertical resolution, 0 = off
            float _ColorLevels;      // steps per channel, 0 = off
            float _DitherStrength;
            float _Warp;
            float _Aberration;
            float _ScanlineStrength;
            float _VignetteStrength;
            float _GrainStrength;

            // Classic 4x4 ordered (Bayer) matrix. Ordered dithering, not random noise:
            // the pattern has to be stable frame to frame or it reads as video noise
            // instead of the fixed grain a PS1 puts on a gradient.
            static const float BayerMatrix[16] =
            {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            /// Internal render resolution, aspect-corrected from a target height.
            float2 TargetResolution()
            {
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                return float2(max(_PixelHeight * aspect, 1.0), max(_PixelHeight, 1.0));
            }

            /// Barrel distortion, the bulge of a CRT tube.
            float2 WarpUv(float2 uv)
            {
                if (_Warp <= 0.0)
                    return uv;

                float2 centered = uv * 2.0 - 1.0;
                float bulge = dot(centered, centered) * _Warp;
                return (centered * (1.0 + bulge)) * 0.5 + 0.5;
            }

            /// Snap to the low-res grid, sampling at texel centres so it stays crisp.
            float2 Pixelate(float2 uv, float2 resolution)
            {
                if (_PixelHeight <= 0.0)
                    return uv;
                return (floor(uv * resolution) + 0.5) / resolution;
            }

            half3 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, saturate(uv)).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 rawUv = input.texcoord;
                float2 uv = WarpUv(rawUv);

                // Past the tube edge there is no picture, only the bezel.
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0.0, 0.0, 0.0, 1.0);

                float2 resolution = TargetResolution();
                float2 pixelUv = Pixelate(uv, resolution);

                half3 color;
                if (_Aberration > 0.0)
                {
                    // Split the channels radially: strongest at the edges, none at centre,
                    // which is how a real tube misconverges.
                    float2 offset = (uv - 0.5) * _Aberration;
                    color.r = SampleScene(Pixelate(uv + offset, resolution)).r;
                    color.g = SampleScene(pixelUv).g;
                    color.b = SampleScene(Pixelate(uv - offset, resolution)).b;
                }
                else
                {
                    color = SampleScene(pixelUv);
                }

                // One virtual pixel = one dither cell and one grain cell. Keyed to the native
                // screen instead, the 4x4 Bayer tile shrank to a speck inside every chunky
                // pixel and read as television static over the whole frame.
                float2 virtualPixel = _PixelHeight > 0.0
                    ? floor(uv * resolution)
                    : floor(rawUv * _ScreenParams.xy);

                // Dither BEFORE quantising: adding the threshold then snapping is what turns
                // a hard band edge into an alternating pattern. Quantising first would just
                // add noise on top of the bands.
                if (_ColorLevels > 1.0)
                {
                    float2 pixelCoord = virtualPixel;
                    int index = (int)(fmod(pixelCoord.y, 4.0) * 4.0 + fmod(pixelCoord.x, 4.0));
                    float threshold = (BayerMatrix[index] - 0.5) * _DitherStrength;

                    float levels = _ColorLevels - 1.0;
                    color = saturate(round(color * levels + threshold) / levels);
                }

                if (_ScanlineStrength > 0.0)
                {
                    float scan = 0.5 - 0.5 * cos(uv.y * resolution.y * TWO_PI);
                    color *= 1.0 - _ScanlineStrength * scan;
                }

                if (_VignetteStrength > 0.0)
                {
                    float2 centered = uv * 2.0 - 1.0;
                    color *= saturate(1.0 - _VignetteStrength * dot(centered, centered) * 0.5);
                }

                if (_GrainStrength > 0.0)
                {
                    float grain = Hash21(virtualPixel + frac(_Time.y) * 431.0);
                    color *= 1.0 - _GrainStrength * (grain - 0.5);
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
