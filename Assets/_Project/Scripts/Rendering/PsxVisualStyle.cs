using UnityEngine;

namespace HorrorUtez.Rendering
{
    /// <summary>The selectable looks. Order is the order the pause menu cycles through.</summary>
    public enum PsxVisualStyle
    {
        /// <summary>
        /// Night lighting with the original crunch: chunky pixels, 5-bit colour with PS1
        /// ordered dither, a touch of CRT. No vertex wobble — it made people seasick.
        /// </summary>
        RetroPixel = 0,

        /// <summary>
        /// The "PS1 render" reference look: cleaner frame, crunchy textures, vertex wobble.
        /// Pretty in a video, tiring to play for long.
        /// </summary>
        Ps1Render = 1,

        /// <summary>
        /// Horror first. Half-light: thick cold fog, crushed desaturated shadows, heavy
        /// vignette and grain, the moon almost gone. The lamps and the flashlight become
        /// islands; everything between them is guesswork.
        /// </summary>
        Penumbra = 2,
        /// <summary>Readable low-poly salvage: modern light, restrained retro texture.</summary>
        Salvage = 3,
    }

    /// <summary>Every number a style sets, in one place.</summary>
    public sealed class PsxStyleData
    {
        public string Label;

        // Screen pass (PsxScreenVolume).
        public float PixelHeight;
        public float ColorLevels;
        public float Dither;
        public float Warp;
        public float Aberration;
        public float Scanlines;
        public float Vignette;
        public float Grain;

        // Surfaces (shader globals read by PsxLit; see PsxLitInput.hlsl).
        public float SnapOverride;      // >0 rows, <0 off, 0 = per material
        public float TexelScale = 1f;
        public float AlbedoLevels;      // >1 forced, <0 off, 0 = per material
        public float AffineOverride;    // >0 forced strength, <0 off, 0 = per material

        // Atmosphere (multipliers over PsxNightPalette, applied by WeatherSystem).
        public float FogDensityScale = 1f;
        public Color FogTint = Color.white;
        public float FogStart = 4f;
        public float FogNoise = 0.05f;
        public float MoonScale = 1f;
        public float AmbientScale = 1f;

        // Grade (URP overrides on the global volume).
        public float PostExposure;
        public float Contrast;
        public float Saturation;
        public Color ColorFilter = Color.white;
        public float Temperature;
        public float Tint;
        public float BloomIntensity;
        public Vector4 Shadows = new(1f, 1f, 1f, 0f);
        public Vector4 Highlights = new(1f, 1f, 1f, 0f);
    }

    public static class PsxStylePresets
    {
        private static readonly PsxStyleData[] All =
        {
            new()
            {
                Label = "Retro pixelado",
                // The original screen settings, minus what was actually a bug (the dither
                // keyed to native pixels). The Bayer pattern now sits in the big pixels,
                // which is what a real PS1 frame looks like.
                PixelHeight = 320f, ColorLevels = 32f, Dither = 0.45f,
                Warp = 0.02f, Aberration = 0.001f, Scanlines = 0.08f, Vignette = 0.28f, Grain = 0.03f,
                SnapOverride = -1f, TexelScale = 0.5f, AlbedoLevels = 0f, AffineOverride = -1f,
                FogDensityScale = 1f, FogTint = Color.white, FogStart = 4f, FogNoise = 0.08f,
                MoonScale = 1f, AmbientScale = 1f,
                PostExposure = 0.3f, Contrast = 12f, Saturation = 5f,
                ColorFilter = new Color(0.93f, 0.94f, 1f), Temperature = -12f, Tint = 8f,
                BloomIntensity = 0.7f,
                Shadows = new Vector4(0.9f, 0.9f, 1.18f, 0f), Highlights = new Vector4(1.04f, 1f, 0.96f, 0f),
            },
            new()
            {
                Label = "PS1 render",
                PixelHeight = 540f, ColorLevels = 64f, Dither = 0.15f,
                Warp = 0f, Aberration = 0.0008f, Scanlines = 0f, Vignette = 0.3f, Grain = 0.025f,
                SnapOverride = 0f, TexelScale = 1f, AlbedoLevels = 0f, AffineOverride = 0f,
                FogDensityScale = 1f, FogTint = Color.white, FogStart = 4f, FogNoise = 0.05f,
                MoonScale = 1f, AmbientScale = 1f,
                PostExposure = 0.35f, Contrast = 14f, Saturation = 10f,
                ColorFilter = new Color(0.93f, 0.94f, 1f), Temperature = -12f, Tint = 8f,
                BloomIntensity = 0.9f,
                Shadows = new Vector4(0.9f, 0.9f, 1.18f, 0f), Highlights = new Vector4(1.04f, 1f, 0.96f, 0f),
            },
            new()
            {
                Label = "Penumbra",
                // Fear is mostly not-seeing. Visibility drops to ~15 m, the shadows go to a
                // sick teal-black and lose their colour, and the grain lives in the dark so
                // the eye keeps finding shapes that are not there. The frame stays readable
                // up close (720p, no wobble) — the dread has to come from what is far away.
                PixelHeight = 720f, ColorLevels = 48f, Dither = 0.25f,
                Warp = 0.012f, Aberration = 0.0016f, Scanlines = 0.03f, Vignette = 0.7f, Grain = 0.075f,
                SnapOverride = -1f, TexelScale = 0.75f, AlbedoLevels = 0f, AffineOverride = -1f,
                FogDensityScale = 2.6f, FogTint = new Color(0.32f, 0.36f, 0.34f), FogStart = 2f, FogNoise = 0.16f,
                MoonScale = 0.3f, AmbientScale = 0.35f,
                PostExposure = -0.2f, Contrast = 24f, Saturation = -38f,
                ColorFilter = new Color(0.86f, 0.93f, 0.92f), Temperature = -18f, Tint = -10f,
                BloomIntensity = 0.55f,
                Shadows = new Vector4(0.86f, 1.0f, 1.02f, -0.04f), Highlights = new Vector4(1.02f, 1f, 0.94f, 0f),
            },
        };

        private static readonly PsxStyleData SalvagePreset = new()
        {
            Label = "Turno nocturno", PixelHeight = 720f, ColorLevels = 64f,
            Dither = .14f, Warp = 0, Aberration = .0005f, Scanlines = .025f,
            Vignette = .3f, Grain = .025f, SnapOverride = -1, TexelScale = .85f,
            AlbedoLevels = 0, AffineOverride = -1, FogDensityScale = .7f,
            FogTint = new Color(.65f,.9f,.82f), FogStart = 7, FogNoise = .04f,
            MoonScale = 1.4f, AmbientScale = 1.5f, PostExposure = .6f,
            Contrast = 9, Saturation = -8, ColorFilter = new Color(.92f,1,.96f),
            Temperature = -6, BloomIntensity = .4f,
        };
        public static int Count => All.Length + 1;

        public static PsxStyleData Get(PsxVisualStyle style) =>
            style == PsxVisualStyle.Salvage ? SalvagePreset : All[Mathf.Clamp((int)style, 0, All.Length - 1)];

        public static PsxStyleData Current => Get(PsxLook.Style);
    }
}
