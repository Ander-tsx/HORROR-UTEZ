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
            float scale = HorrorUtez.World.UtezDimensions.WorldScale;
            fog.density.overrideState = true;
            fog.density.value = 0.018f / scale;
            fog.startDistance.overrideState = true;
            fog.startDistance.value = 3.3f * scale;
            fog.fogColor.overrideState = true;
            fog.fogColor.value = new Color(0.42f, 0.46f, 0.52f);
            fog.ambientColor.overrideState = true;
            fog.ambientColor.value = new Color(0.30f, 0.32f, 0.38f);
            fog.noiseScale.overrideState = true;
            fog.noiseScale.value = 45f;
            fog.noiseStrength.overrideState = true;
            fog.noiseStrength.value = 0.12f;
            fog.noiseSpeed.overrideState = true;
            fog.noiseSpeed.value = 0.02f;

            EditorUtility.SetDirty(fog);

            if (!profile.TryGet<PsxScreenVolume>(out var screen))
            {
                screen = profile.Add<PsxScreenVolume>(overrides: true);
                AssetDatabase.AddObjectToAsset(screen, profile);
            }

            // PS1 framebuffer: 240p internal, 5 bits per channel. The CRT settings stay
            // restrained — heavy warp and scanlines are exhausting over a whole session.
            // Retuned after the first pass ate the props: at a literal 240p with a 5-bit
            // framebuffer, a chromed bench three metres away came out as one grey blob with
            // no edges left. The look has to survive being pointed at an actual model, so the
            // internal resolution and the colour depth both came up. Everything else is
            // seasoning and was pulled back with them.
            screen.pixelHeight.overrideState = true;
            screen.pixelHeight.value = 400f;
            screen.colorLevels.overrideState = true;
            screen.colorLevels.value = 64f;
            screen.ditherStrength.overrideState = true;
            screen.ditherStrength.value = 0.55f;
            screen.warp.overrideState = true;
            screen.warp.value = 0.02f;
            screen.aberration.overrideState = true;
            screen.aberration.value = 0.001f;
            screen.scanlineStrength.overrideState = true;
            screen.scanlineStrength.value = 0.08f;
            screen.vignetteStrength.overrideState = true;
            screen.vignetteStrength.value = 0.22f;
            screen.grainStrength.overrideState = true;
            screen.grainStrength.value = 0.035f;

            EnsureTonemapping(profile);

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

            Debug.Log("[PSX] Global Volume added to the scene.");
            return volume;
        }
    }
}
