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
        [Tooltip("Internal vertical resolution. 240 is roughly PS1. 0 disables pixelation.")]
        public ClampedFloatParameter pixelHeight = new(240f, 0f, 1080f);

        [Header("Colour")]
        [Tooltip("Steps per channel. 32 = 5-bit, the PS1 framebuffer. 0 or 1 disables.")]
        public ClampedFloatParameter colorLevels = new(32f, 0f, 256f);

        [Tooltip("How hard the Bayer pattern pushes values across quantisation steps.")]
        public ClampedFloatParameter ditherStrength = new(1f, 0f, 2f);

        [Header("CRT")]
        [Tooltip("Barrel distortion. Small values only; past ~0.15 it reads as a fisheye.")]
        public ClampedFloatParameter warp = new(0.03f, 0f, 0.3f);

        [Tooltip("Radial channel split. Costs two extra texture samples when above 0.")]
        public ClampedFloatParameter aberration = new(0.0015f, 0f, 0.02f);

        public ClampedFloatParameter scanlineStrength = new(0.15f, 0f, 1f);
        public ClampedFloatParameter vignetteStrength = new(0.35f, 0f, 1.5f);
        public ClampedFloatParameter grainStrength = new(0.05f, 0f, 0.5f);

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
