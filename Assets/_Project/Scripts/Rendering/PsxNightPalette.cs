using UnityEngine;

namespace HorrorUtez.Rendering
{
    /// <summary>
    /// The night palette in one place. The fog, the moon, the ambient term and the weather
    /// all have to agree on the same blue, or the scene splits into layers that each look
    /// like a different time of night.
    /// </summary>
    public static class PsxNightPalette
    {
        /// <summary>Moonlight: cold, strongly blue, low. It shapes forms; lamps do the lighting.</summary>
        public static readonly Color Moon = new(0.55f, 0.62f, 1f);
        public const float MoonIntensityClear = 0.45f;
        public const float MoonIntensityStorm = 0.14f;

        /// <summary>Ambient sky term. Lifts shadowed faces out of black into readable navy.</summary>
        public static readonly Color AmbientClear = new(0.11f, 0.12f, 0.24f);
        public static readonly Color AmbientStorm = new(0.05f, 0.055f, 0.11f);

        public static readonly Color FogClear = new(0.18f, 0.22f, 0.44f);
        public static readonly Color FogStorm = new(0.12f, 0.14f, 0.28f);
        public const float FogDensityClear = 0.012f;
        public const float FogDensityStorm = 0.04f;

        /// <summary>Sodium street lamp: the warm counterweight to everything above.</summary>
        public static readonly Color Sodium = new(1f, 0.66f, 0.36f);
    }
}
