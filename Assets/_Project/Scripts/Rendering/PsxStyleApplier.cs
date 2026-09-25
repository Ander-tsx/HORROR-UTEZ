using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Puts the selected <see cref="PsxVisualStyle"/> on screen, and swaps it live when the
    /// player picks another one in the pause menu.
    ///
    /// Two halves:
    /// - Surface overrides are shader globals (see PsxLitInput.hlsl), so switching a style
    ///   touches no material and keeps the SRP Batcher happy. Set in edit mode too, so the
    ///   Scene view shows the chosen style.
    /// - Screen and grade values go into the volume profile — in play mode only, and into
    ///   the runtime copy (<c>Volume.profile</c>), so the UTEZ_Volume asset on disk never
    ///   changes. WeatherSystem reads the same copy and scales its fog and light by the
    ///   style's multipliers.
    ///
    /// Lives on the Global Volume object, next to PsxLookBinder.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class PsxStyleApplier : MonoBehaviour
    {
        private static readonly int SnapOverrideId = Shader.PropertyToID("_PsxSnapOverride");
        private static readonly int TexelScaleId = Shader.PropertyToID("_PsxTexelScale");
        private static readonly int AlbedoLevelsId = Shader.PropertyToID("_PsxAlbedoLevels");
        private static readonly int AffineOverrideId = Shader.PropertyToID("_PsxAffineOverride");

        private Volume _volume;

        private void OnEnable()
        {
            _volume = GetComponent<Volume>();
            PsxLook.StyleChanged += OnStyleChanged;
            PsxLook.Changed += OnLookToggled;
            Apply();
        }

        private void OnDisable()
        {
            PsxLook.StyleChanged -= OnStyleChanged;
            PsxLook.Changed -= OnLookToggled;
            SetSurfaceGlobals(null);
        }

        private void OnStyleChanged(PsxVisualStyle _) => Apply();

        private void OnLookToggled(bool _) => Apply();

        public void Apply()
        {
            var style = PsxStylePresets.Current;
            SetSurfaceGlobals(PsxLook.Enabled ? style : null);

            if (Application.isPlaying && _volume != null)
                ApplyToProfile(_volume.profile, style);
        }

        /// <summary>Null clears every override: materials render exactly as authored.</summary>
        private static void SetSurfaceGlobals(PsxStyleData style)
        {
            Shader.SetGlobalFloat(SnapOverrideId, style?.SnapOverride ?? 0f);
            Shader.SetGlobalFloat(TexelScaleId, style?.TexelScale ?? 0f);
            Shader.SetGlobalFloat(AlbedoLevelsId, style?.AlbedoLevels ?? 0f);
            Shader.SetGlobalFloat(AffineOverrideId, style?.AffineOverride ?? 0f);
        }

        private static void ApplyToProfile(VolumeProfile profile, PsxStyleData s)
        {
            if (profile.TryGet<PsxScreenVolume>(out var screen))
            {
                screen.pixelHeight.Override(s.PixelHeight);
                screen.colorLevels.Override(s.ColorLevels);
                screen.ditherStrength.Override(s.Dither);
                screen.warp.Override(s.Warp);
                screen.aberration.Override(s.Aberration);
                screen.scanlineStrength.Override(s.Scanlines);
                screen.vignetteStrength.Override(s.Vignette);
                screen.grainStrength.Override(s.Grain);
            }

            // Density and colour belong to WeatherSystem, which applies the style's scale.
            if (profile.TryGet<PsxFogVolume>(out var fog))
            {
                fog.startDistance.Override(s.FogStart);
                fog.noiseStrength.Override(s.FogNoise);
            }

            if (profile.TryGet<Bloom>(out var bloom))
                bloom.intensity.Override(s.BloomIntensity);

            if (profile.TryGet<ColorAdjustments>(out var adjustments))
            {
                adjustments.postExposure.Override(s.PostExposure);
                adjustments.contrast.Override(s.Contrast);
                adjustments.saturation.Override(s.Saturation);
                adjustments.colorFilter.Override(s.ColorFilter);
            }

            if (profile.TryGet<WhiteBalance>(out var whiteBalance))
            {
                whiteBalance.temperature.Override(s.Temperature);
                whiteBalance.tint.Override(s.Tint);
            }

            if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh))
            {
                smh.shadows.Override(s.Shadows);
                smh.highlights.Override(s.Highlights);
            }
        }
    }
}
