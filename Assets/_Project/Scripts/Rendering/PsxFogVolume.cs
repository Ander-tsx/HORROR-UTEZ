using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Volume settings for <see cref="PsxFogRenderFeature"/>.
    ///
    /// Distances are in metres, so these read directly against the campus dimensions in
    /// docs/map/utez-campus-reference.md: the CDS-to-CECADEC gap is 20 m, the whole
    /// explanada is 110 m deep.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("HORROR-UTEZ/PSX Fog")]
    public sealed class PsxFogVolume : VolumeComponent
    {
        [Tooltip("Fog thickness. 0 disables the effect entirely.")]
        public ClampedFloatParameter density = new(0.02f, 0f, 0.5f);

        [Tooltip("Metres of clear air in front of the camera before fog starts building.")]
        public ClampedFloatParameter startDistance = new(4f, 0f, 60f);

        public ColorParameter fogColor = new(new Color(0.55f, 0.58f, 0.62f), hdr: false, showAlpha: false, showEyeDropper: true);

        [Tooltip("Multiplies the fog colour. Drop it dark for night; this is the main night/day lever.")]
        public ColorParameter ambientColor = new(new Color(0.35f, 0.37f, 0.42f), hdr: false, showAlpha: false, showEyeDropper: true);

        [Header("Noise")]
        [Tooltip("Screen-space size of the noise cells, in pixels. Larger = broader drifts.")]
        public ClampedFloatParameter noiseScale = new(90f, 1f, 400f);

        [Tooltip("How much the noise pushes the fog around. Keep low; this is texture, not weather.")]
        public ClampedFloatParameter noiseStrength = new(0.06f, 0f, 0.5f);

        public ClampedFloatParameter noiseSpeed = new(0.02f, 0f, 1f);

        public bool IsActive() => active && density.value > 0f;
    }
}
