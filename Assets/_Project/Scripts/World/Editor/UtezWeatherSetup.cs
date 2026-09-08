using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Builds the scene side of the weather: the rain emitter that rides with the player,
    /// the ambient audio sources, and the <see cref="WeatherSystem"/> that drives them.
    ///
    /// The emitter is parented to the player and simulates in world space, so drops keep
    /// falling straight down while the player moves instead of being dragged sideways.
    /// </summary>
    public static class UtezWeatherSetup
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string TextureFolder = "Assets/_Project/Art/Textures";

        public static void Build(GameObject player, Volume volume, Light sun)
        {
            if (player == null)
            {
                Debug.LogWarning("[WEATHER] No player; skipping weather rig.");
                return;
            }

            var rain = BuildRainEmitter(player.transform);

            var weatherGo = new GameObject("Weather");
            var rainAudio = weatherGo.AddComponent<AudioSource>();
            ConfigureAmbientSource(rainAudio);
            var windAudio = weatherGo.AddComponent<AudioSource>();
            ConfigureAmbientSource(windAudio);

            var weather = weatherGo.AddComponent<WeatherSystem>();

            var so = new SerializedObject(weather);
            so.FindProperty("volume").objectReferenceValue = volume;
            so.FindProperty("sun").objectReferenceValue = sun;
            so.FindProperty("rain").objectReferenceValue = rain;
            so.FindProperty("rainAudio").objectReferenceValue = rainAudio;
            so.FindProperty("windAudio").objectReferenceValue = windAudio;
            so.FindProperty("player").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[WEATHER] Weather rig built.");
        }

        /// <summary>2D ambience: rain and wind are everywhere, not at a point in space.</summary>
        private static void ConfigureAmbientSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }

        private static ParticleSystem BuildRainEmitter(Transform player)
        {
            var go = new GameObject("RainEmitter");
            go.transform.SetParent(player, false);

            float scale = UtezDimensions.WorldScale;
            // Emit from a slab overhead. Wide enough that the player never sees the edge of
            // the rain volume, low enough that drops reach the ground quickly.
            go.transform.localPosition = new Vector3(0f, 22f * scale, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var system = go.AddComponent<ParticleSystem>();
            system.Stop();

            var main = system.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 32f * scale;
            main.startSize = 0.05f * scale;
            main.startColor = new Color(0.70f, 0.76f, 0.86f, 0.30f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 4000;
            // World space, or the whole downpour would slide with the player's steps.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f * scale, 70f * scale, 1f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            // Stretched billboards turn round particles into streaks, which is what reads
            // as falling rain rather than floating dust.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 2.4f;
            renderer.sharedMaterial = EnsureRainMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return system;
        }

        /// <summary>Soft vertical streak on an additive-ish transparent unlit material.</summary>
        private static Material EnsureRainMaterial()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(TextureFolder);

            const string texturePath = TextureFolder + "/UTEZ_RainStreak.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                const int width = 8;
                const int height = 32;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
                {
                    name = "UTEZ_RainStreak",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // Fade to nothing at both ends and at the sides so the quad edge never shows.
                    float u = Mathf.Abs(x / (float)(width - 1) * 2f - 1f);
                    float v = Mathf.Abs(y / (float)(height - 1) * 2f - 1f);
                    float alpha = Mathf.Clamp01((1f - u) * (1f - v * v));
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false);
                AssetDatabase.CreateAsset(texture, texturePath);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }

            const string materialPath = MaterialFolder + "/UTEZ_Rain.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(shader) { name = "UTEZ_Rain" };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);  // transparent
            material.SetFloat("_Blend", 0f);    // alpha
            material.renderQueue = 3000;
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            EditorUtility.SetDirty(material);

            return material;
        }
    }
}
