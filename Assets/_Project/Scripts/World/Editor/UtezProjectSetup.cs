using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PsxTerrainMaterials = HorrorUtez.Rendering.Editor.PsxTerrainMaterials;
using PsxRenderingSetup = HorrorUtez.Rendering.Editor.PsxRenderingSetup;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// One-shot project setup: creates and assigns the URP pipeline asset, tidies the
    /// auto-generated render settings into _Project/Settings, then creates the `utez`
    /// scene and fills it with the campus terrain.
    ///
    /// Safe to re-run: existing assets are reused, the scene is rebuilt from
    /// <see cref="UtezDimensions"/>. Runnable headless via -executeMethod.
    /// </summary>
    public static class UtezProjectSetup
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string UrpAssetPath = SettingsFolder + "/UTEZ_URP.asset";
        private const string UrpRendererPath = SettingsFolder + "/UTEZ_URP_Renderer.asset";
        private const string ScenePath = "Assets/_Project/Scenes/utez.unity";

        [MenuItem("HORROR-UTEZ/Setup Project (URP + utez scene)")]
        public static void RunAll()
        {
            SetupUrp();
            TidyGeneratedSettings();
            PsxTerrainMaterials.EnsureAll();
            PsxRenderingSetup.Setup();
            BuildUtezScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UTEZ] Project setup complete.");
        }

        /// <summary>Entry point for batch mode; exits non-zero so the shell sees failures.</summary>
        public static void RunAllBatch()
        {
            try
            {
                RunAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UTEZ] Setup failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        private static UniversalRenderPipelineAsset SetupUrp()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);

                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, UrpAssetPath);
                Debug.Log($"[UTEZ] Created {UrpAssetPath}");
            }

            // The PSX fog and post-process render features sample both of these.
            urp.supportsCameraDepthTexture = true;
            urp.supportsCameraOpaqueTexture = true;

            // Additional-light shadows are off in a freshly created URP asset. The flashlight
            // is an additional light, and a torch that casts no shadow is just an ambient
            // boost — what it fails to reach is where the horror lives.
            var urpSo = new SerializedObject(urp);
            var shadowProp = urpSo.FindProperty("m_AdditionalLightShadowsSupported");
            if (shadowProp != null)
                shadowProp.boolValue = true;
            urpSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;

            int original = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(original, false);

            AssetDatabase.SaveAssets();
            Debug.Log("[UTEZ] URP assigned to Graphics + all Quality levels.");
            return urp;
        }

        /// <summary>Unity drops these at the Assets root on first URP init; keep the tree tidy.</summary>
        private static void TidyGeneratedSettings()
        {
            MoveIfAtRoot("UniversalRenderPipelineGlobalSettings.asset");
            MoveIfAtRoot("DefaultVolumeProfile.asset");
        }

        private static void MoveIfAtRoot(string fileName)
        {
            string from = "Assets/" + fileName;
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(from) == null)
                return;

            string error = AssetDatabase.MoveAsset(from, SettingsFolder + "/" + fileName);
            if (string.IsNullOrEmpty(error))
                Debug.Log($"[UTEZ] Moved {fileName} to {SettingsFolder}");
            else
                Debug.LogWarning($"[UTEZ] Could not move {fileName}: {error}");
        }

        private static void BuildUtezScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            UtezTerrainBuilder.Build();
            UtezBuildingBuilder.Build();
            UtezForestBuilder.Build();
            var sun = ConfigureLighting();
            var player = SpawnPlayer();
            var volume = PsxRenderingSetup.SpawnGlobalVolume();
            UtezWeatherSetup.Build(player, volume, sun);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[UTEZ] Scene saved to {ScenePath}");
        }

        /// <summary>
        /// Dusk lighting aimed so the facades actually read.
        ///
        /// Unity's default directional light leaves every north-facing wall unlit, and with
        /// no ambient term those faces render pure black — the CECADEC entrance, the one
        /// facade we have a photo of, was a silhouette. This rakes the light from the north
        /// and lifts ambient just enough that shadowed surfaces keep their colour.
        /// </summary>
        private static Light ConfigureLighting()
        {
            Light sun = null;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }

            if (sun == null)
            {
                var go = new GameObject("Directional Light");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.transform.rotation = Quaternion.Euler(30f, 160f, 0f);
            sun.color = new Color(0.72f, 0.78f, 0.95f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.22f, 0.28f);

            Debug.Log("[UTEZ] Dusk lighting configured.");
            return sun;
        }

        /// <summary>
        /// Drops a playable first-person rig into the plaza, facing south toward the
        /// CECADEC entrance. Replaces the default scene camera so pressing Play just works.
        /// </summary>
        private static GameObject SpawnPlayer()
        {
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.CompareTag("MainCamera"))
                    UnityEngine.Object.DestroyImmediate(cam.gameObject);

            var player = new GameObject("Player");
            // World units, already scaled. The plaza gap runs roughly Z -14 .. +16 at
            // WorldScale 1.5, so this stands in open ground facing the CECADEC entrance.
            player.transform.SetPositionAndRotation(
                new Vector3(0f, 2f, 8f), Quaternion.Euler(0f, 180f, 0f));

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.02f;

            player.AddComponent<HorrorUtez.Player.PlayerInputReader>();
            var fpc = player.AddComponent<HorrorUtez.Player.FirstPersonController>();

            var stepSource = player.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.spatialBlend = 0f;
            player.AddComponent<HorrorUtez.Player.FootstepAudio>();

            var head = new GameObject("Head");
            head.transform.SetParent(player.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            var camera = head.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            camera.fieldOfView = 65f;
            head.AddComponent<AudioListener>();
            head.AddComponent<HorrorUtez.Player.HeadBob>();

            // Flashlight on its own transform so it can lag the camera instead of being
            // welded to it. Range and angle are in world units, so they follow WorldScale.
            var torch = new GameObject("Flashlight");
            torch.transform.SetParent(head.transform, false);
            var beam = torch.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.96f, 0.88f);
            // URP punctual lights fall off with inverse square. The far wall of CECADEC is
            // ~34 units away, so reaching it at all needs intensity in the hundreds:
            // 34^2 is roughly 1150, and anything under that arrives as black.
            // Compromise: inverse-square means whatever reaches the far wall blows out the
            // floor two metres ahead. 900 lit the wall but burned the near pool to white;
            // this trades some reach for a beam that reads as a torch rather than a flare.
            beam.intensity = 350f;
            beam.range = 40f * UtezDimensions.WorldScale;
            beam.spotAngle = 46f;
            beam.innerSpotAngle = 18f;
            beam.shadows = LightShadows.Soft;
            var torchSource = torch.AddComponent<AudioSource>();
            torchSource.playOnAwake = false;
            torchSource.spatialBlend = 0f;
            torch.AddComponent<HorrorUtez.Player.Flashlight>();

            // Wire the serialized head reference without exposing a public setter.
            var so = new SerializedObject(fpc);
            so.FindProperty("head").objectReferenceValue = head.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[UTEZ] Player rig spawned in the plaza.");
            return player;
        }
    }
}
