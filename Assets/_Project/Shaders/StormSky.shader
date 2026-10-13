// Night storm skybox for the always-raining campus.
//
// Low, heavy clouds drift over a sky that fades into the fog colour at the horizon, so the
// buildings melt into it with no seam (WeatherSystem feeds the live fog colour through
// _StormSkyHorizon). Two cloud decks move at different speeds for depth. The campus lights
// throw a faint sick glow onto the cloud bellies near the horizon.
//
// Lightning: LightningSystem drives _StormFlash / _StormFlashDir, lighting the clouds from the
// side the strike came from. Between strikes, distant sheet lightning flickers silently inside
// the clouds now and then, timed by hashing time cells so it needs no script.
Shader "HorrorUtez/StormSky"
{
    Properties
    {
        _HorizonFallback ("Horizon (if no weather)", Color) = (0.06, 0.07, 0.12, 1)
        _ZenithTint ("Zenith tint", Color) = (0.18, 0.20, 0.28, 1)
        _CloudLight ("Cloud light", Color) = (0.62, 0.66, 0.78, 1)
        _CloudDark ("Cloud dark", Color) = (0.05, 0.055, 0.075, 1)
        _GlowColor ("Town glow on clouds", Color) = (0.30, 0.24, 0.14, 1)
        _FlashColor ("Lightning colour", Color) = (0.75, 0.82, 1.0, 1)
        _CloudScale ("Cloud scale", Float) = 0.8
        _Cover ("Cloud cover", Range(0, 1)) = 0.5
        _Wind ("Wind (xy deck 1, zw deck 2)", Vector) = (0.010, 0.004, 0.022, 0.009)
        _SheetLightning ("Sheet lightning", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonFallback, _ZenithTint, _CloudLight, _CloudDark, _GlowColor, _FlashColor;
                float _CloudScale, _Cover, _SheetLightning;
                float4 _Wind;
            CBUFFER_END

            // Globals from the weather scripts.
            float4 _StormSkyHorizon;   // rgb = fog colour as rendered, a = 1 once set
            float _StormFlash;         // 0..1 lightning level
            float3 _StormFlashDir;     // towards the strike

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                const float2x2 turn = float2x2(1.6, 1.2, -1.2, 1.6);
                for (int i = 0; i < 5; i++) { v += a * Noise(p); p = mul(turn, p); a *= 0.5; }
                return v;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dir);
                float h = dir.y;
                half3 horizon = _StormSkyHorizon.a > 0.5 ? _StormSkyHorizon.rgb : _HorizonFallback.rgb;
                half3 zenith = horizon * _ZenithTint.rgb;
                half3 sky = lerp(horizon, zenith, smoothstep(0.0, 0.55, h));

                // Cloud decks projected on flat planes overhead: they bunch up towards the horizon like real ones.
                float up = max(h, 0.0) + 0.09;
                float2 plane = dir.xz / up * _CloudScale;
                float t = _Time.y;
                float deck1 = Fbm(plane + t * _Wind.xy * 10.0);
                float deck2 = Fbm(plane * 2.3 + float2(7.1, 3.7) + t * _Wind.zw * 10.0);
                float density = saturate((deck1 * 0.7 + deck2 * 0.45 - (1.0 - _Cover)) * 2.2);
                // A slightly shifted sample darkens the far side of each lump, giving the decks volume.
                float shade = saturate((Fbm(plane + t * _Wind.xy * 10.0 + 0.12) - deck1) * 6.0 + 0.5);
                half3 cloud = lerp(_CloudDark.rgb, _CloudLight.rgb, shade * 0.6 + deck2 * 0.4) * (0.6 + horizon * 2.0);

                // Light from the campus below catches the cloud bellies low on the horizon.
                float low = pow(saturate(1.0 - h * 2.5), 3.0);
                cloud += _GlowColor.rgb * low * density * 0.35;

                // Lightning lights the clouds from the strike's side; thin gaps barely catch it.
                float facing = pow(saturate(dot(dir, normalize(_StormFlashDir + float3(0, 0.001, 0))) * 0.5 + 0.5), 4.0);
                half3 flash = _FlashColor.rgb * _StormFlash * (0.15 + density * 1.4) * (0.35 + facing * 1.6);

                // Silent sheet lightning: every ~2.7 s a time cell may pick a bearing and flicker deep in the clouds.
                float cell = floor(t / 2.7);
                float roll = Hash21(float2(cell, 17.0));
                float local = frac(t / 2.7) * 2.7;
                float bearing = Hash21(float2(cell, 3.0)) * 6.2832;
                float3 sheetDir = normalize(float3(cos(bearing), 0.18, sin(bearing)));
                float flicker = step(roll, 0.35 * _SheetLightning) * saturate(1.0 - local * 2.2)
                              * (0.55 + 0.45 * step(0.5, frac(local * 13.0)));
                float sheetMask = pow(saturate(dot(dir, sheetDir)), 18.0) * low;
                flash += _FlashColor.rgb * flicker * sheetMask * density * 0.8;

                half3 col = lerp(sky, cloud, density) + flash;
                // The horizon stays pure fog colour so the map edge never shows.
                col = lerp(horizon, col, smoothstep(0.0, 0.22, h));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
