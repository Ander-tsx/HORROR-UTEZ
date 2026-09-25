using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorUtez.Rendering.Editor
{
    /// <summary>
    /// Registers the PSX render features on the project's URP renderer and builds the
    /// volume profile that drives them.
    ///
    /// Adding a renderer feature from code is not just a list insert: ScriptableRendererData
    /// keeps a parallel m_RendererFeatureMap of local file ids, and a feature whose id is
    /// missing from that map gets dropped on reload without an error. This mirrors what
    /// URP's own ScriptableRendererDataEditor.AddComponent does.
    /// </summary>
    public static class PsxRenderingSetup
    {
        private const string RendererPath = "Assets/_Project/Settings/UTEZ_URP_Renderer.asset";
        private const string ProfilePath = "Assets/_Project/Settings/UTEZ_Volume.asset";

        [MenuItem("HORROR-UTEZ/Setup PSX Rendering")]
        public static void Setup()
        {
            // Order matters: fog first, then the screen crunch, so the fog gets pixelated
            // and scanned along with the scene instead of sitting cleanly on top of it.
            RegisterFeature<PsxFogRenderFeature>("PSX Fog");
            RegisterFeature<PsxScreenRenderFeature>("PSX Screen");
            EnsureVolumeProfile();
        }

        private static void RegisterFeature<T>(string displayName) where T : ScriptableRendererFeature
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[PSX] Renderer data not found at {RendererPath}");
                return;
            }

            foreach (var existing in rendererData.rendererFeatures)
            {
                if (existing is T)
                {
                    Debug.Log($"[PSX] {displayName} already registered on the renderer.");
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = displayName;
            feature.hideFlags |= HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.SaveAssets();

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var so = new SerializedObject(rendererData);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            // Keep the id map the same length as the feature list, or URP discards the feature.
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PSX] Registered {displayName} on {RendererPath}");
        }

        private static void EnsureVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                Debug.Log($"[PSX] Created volume profile {ProfilePath}");
            }

            if (!profile.TryGet<PsxFogVolume>(out var fog))
            {
                fog = profile.Add<PsxFogVolume>(overrides: true);
                AssetDatabase.AddObjectToAsset(fog, profile);
            }

            // Night-time campus: thin enough to see across the 20 m gap between buildings,
            // thick enough that the tree line and the map edge dissolve.
            // Density is per world unit, so it has to be divided by WorldScale to keep the
            // same real-world visibility when the campus is rebuilt at a different scale.
            // Colour: deep blue-violet, not grey. Grey fog over a night scene reads as smog;
            // the reference renders sink every distance into the same saturated night blue
            // the moonlight uses, so the fog and the light agree about what time it is.
            float scale = HorrorUtez.World.UtezDimensions.WorldScale;
            fog.density.overrideState = true;
            fog.density.value = 0.02f / scale;
            fog.startDistance.overrideState = true;
            fog.startDistance.value = 4f * scale;
            fog.fogColor.overrideState = true;
            fog.fogColor.value = PsxNightPalette.FogClear;
            fog.ambientColor.overrideState = true;
            fog.ambientColor.value = new Color(0.62f, 0.62f, 0.72f);
            fog.noiseScale.overrideState = true;
            fog.noiseScale.value = 90f;
            fog.noiseStrength.overrideState = true;
            fog.noiseStrength.value = 0.05f;
            fog.noiseSpeed.overrideState = true;
            fog.noiseSpeed.value = 0.02f;

            EditorUtility.SetDirty(fog);

            if (!profile.TryGet<PsxScreenVolume>(out var screen))
            {
                screen = profile.Add<PsxScreenVolume>(overrides: true);
                AssetDatabase.AddObjectToAsset(screen, profile);
            }

            // Second retune, against the "PS1 render" reference look: crunchy textures and
            // snapping geometry, but a clean, readable frame. At 400p with the dither keyed to
            // native pixels the whole screen read as static; the style lives in the surfaces
            // (PsxLit) and the light, so the screen pass is now a light touch — no tube warp,
            // no scanlines, dither only where a gradient bands.
            screen.pixelHeight.overrideState = true;
            screen.pixelHeight.value = 540f;
            screen.colorLevels.overrideState = true;
            screen.colorLevels.value = 64f;
            screen.ditherStrength.overrideState = true;
            screen.ditherStrength.value = 0.15f;
            screen.warp.overrideState = true;
            screen.warp.value = 0f;
            screen.aberration.overrideState = true;
            screen.aberration.value = 0.0008f;
            screen.scanlineStrength.overrideState = true;
            screen.scanlineStrength.value = 0f;
            screen.vignetteStrength.overrideState = true;
            screen.vignetteStrength.value = 0.3f;
            screen.grainStrength.overrideState = true;
            screen.grainStrength.value = 0.025f;

            EnsureTonemapping(profile);
            EnsureGrading(profile);

            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds the scene-side global Volume that applies the profile.</summary>
        /// <summary>
        /// A tonemapper on the global volume.
        ///
        /// Without one, URP clips at 1.0: anything the torch lights from closer than about
        /// five metres saturates to flat white, which is why every prop photographed near
        /// the player came out as a featureless cutout. That reads as "the PSX filter
        /// destroyed the model" and it is not the filter at all — the detail is gone before
        /// the filter ever runs.
        ///
        /// Neutral rather than ACES: ACES pushes contrast and shifts hue, which fights a
        /// look already built on crushed colour depth. Neutral just rolls the highlights off.
        /// </summary>
        private static void EnsureTonemapping(VolumeProfile profile)
        {
            if (!profile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(overrides: true);
                AssetDatabase.AddObjectToAsset(tonemapping, profile);
            }

            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;
        }

        /// <summary>
        /// Bloom and grading for the night look.
        ///
        /// Bloom is what makes a lamp, a lit window or a sign read as a light source rather
        /// than a bright texture — every practical in the reference renders haloes. The
        /// threshold sits just under 1 so only emissive surfaces and hot spots bloom, never
        /// a pale wall under the torch.
        ///
        /// The grade pushes shadows toward blue-violet and leaves highlights warm, so the
        /// sodium lamps and the moonlit ground pull apart instead of meeting in grey.
        /// </summary>
        private static void EnsureGrading(VolumeProfile profile)
        {
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.94f, 0.96f));
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Half);

            var adjustments = GetOrAdd<ColorAdjustments>(profile);
            adjustments.postExposure.Override(0.35f);
            adjustments.contrast.Override(14f);
            adjustments.colorFilter.Override(new Color(0.93f, 0.94f, 1f));
            adjustments.saturation.Override(10f);

            var whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.temperature.Override(-12f);
            whiteBalance.tint.Override(8f);

            var smh = GetOrAdd<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.9f, 0.9f, 1.18f, 0f));
            smh.midtones.Override(new Vector4(0.98f, 0.97f, 1.04f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1f, 0.96f, 0f));

            foreach (var component in new VolumeComponent[] { bloom, adjustments, whiteBalance, smh })
                EditorUtility.SetDirty(component);
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(overrides: false);
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        public static Volume SpawnGlobalVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogWarning("[PSX] No volume profile; run Setup PSX Rendering first.");
                return null;
            }

            var go = new GameObject("Global Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            // The render features read PsxLook on their own; this is what carries the switch
            // into the materials, which the features cannot reach.
            go.AddComponent<PsxLookBinder>();
            go.AddComponent<PsxStyleApplier>();

            Debug.Log("[PSX] Global Volume added to the scene.");
            return volume;
        }
    }
}
