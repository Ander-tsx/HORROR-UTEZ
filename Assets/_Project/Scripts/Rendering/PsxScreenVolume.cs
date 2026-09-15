using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Volume settings for <see cref="PsxScreenRenderFeature"/>: pixelation, colour depth
    /// with ordered dithering, and the CRT tube pass.
    ///
    /// Every effect here is off at 0, so the whole look can be dialled back to a clean
    /// image without removing the feature — useful for reading debug text or judging
    /// geometry without the crunch in the way.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("HORROR-UTEZ/PSX Screen")]
    public sealed class PsxScreenVolume : VolumeComponent
    {
        [Header("Pixelation")]
        [Tooltip("Internal vertical resolution. 240 is literal PS1 and eats detail; 400 keeps "
             + "the crunch while a prop stays readable. 0 disables pixelation.")]
        public ClampedFloatParameter pixelHeight = new(400f, 0f, 1080f);

        [Header("Colour")]
        [Tooltip("Steps per channel. 32 = 5-bit, the literal PS1 framebuffer, which bands "
                 + "smooth metal into flat blobs. 64 keeps the era without that. 0 or 1 disables.")]
        public ClampedFloatParameter colorLevels = new(64f, 0f, 256f);

        [Tooltip("How hard the Bayer pattern pushes values across quantisation steps.")]
        public ClampedFloatParameter ditherStrength = new(0.55f, 0f, 2f);

        [Header("CRT")]
        [Tooltip("Barrel distortion. Small values only; past ~0.15 it reads as a fisheye.")]
        public ClampedFloatParameter warp = new(0.02f, 0f, 0.3f);

        [Tooltip("Radial channel split. Costs two extra texture samples when above 0.")]
        public ClampedFloatParameter aberration = new(0.001f, 0f, 0.02f);

        public ClampedFloatParameter scanlineStrength = new(0.08f, 0f, 1f);
        public ClampedFloatParameter vignetteStrength = new(0.22f, 0f, 1.5f);
        public ClampedFloatParameter grainStrength = new(0.035f, 0f, 0.5f);

        public bool IsActive() =>
            active && (pixelHeight.value > 0f
                       || colorLevels.value > 1f
                       || warp.value > 0f
                       || aberration.value > 0f
                       || scanlineStrength.value > 0f
                       || vignetteStrength.value > 0f
                       || grainStrength.value > 0f);
    }
}
