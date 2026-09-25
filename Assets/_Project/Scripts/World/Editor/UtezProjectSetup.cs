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
    /// auto-generated render settings into _Project/Settings, then regenerates the
    /// blockout scene from <see cref="UtezDimensions"/>.
    ///
    /// It writes the blockout to <c>utez_blockout.unity</c>, every run. The playable scene
    /// <c>utez.unity</c> is authored BY HAND in the editor — it is seeded from the blockout
    /// the first time and never touched again, so stretching, moving and deleting pieces
    /// in the Scene view sticks. To pull fresh geometry after a dimension change, open the
    /// blockout and copy what you need across.
    ///
    /// Safe to re-run: assets are reused, only the blockout scene is rewritten.
    /// Runnable headless via -executeMethod.
    /// </summary>
    public static class UtezProjectSetup
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string UrpAssetPath = SettingsFolder + "/UTEZ_URP.asset";
        private const string UrpRendererPath = SettingsFolder + "/UTEZ_URP_Renderer.asset";

        /// <summary>Regenerated from code on every run. A reference, not the playable scene.</summary>
        private const string BlockoutScenePath = "Assets/_Project/Scenes/utez_blockout.unity";
        /// <summary>Hand-authored, playable, in Build Settings. Seeded once, never overwritten.</summary>
        public const string WorkingScenePath = "Assets/_Project/Scenes/utez.unity";

        [MenuItem("HORROR-UTEZ/Setup Project (URP + utez scene)")]
        public static void RunAll()
        {
            SetupUrp();
            TidyGeneratedSettings();
            PsxTerrainMaterials.EnsureAll();
            UtezKit.EnsureAll();
            UtezPropAssets.EnsureAll();
            PsxRenderingSetup.Setup();
            BuildUtezScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UTEZ] Project setup complete.");
        }

        /// <summary>
        /// A builder that wipes and regenerates a scene root calls this first. Returns true
        /// to proceed. If the OPEN scene is the hand-authored working scene, it asks for
        /// confirmation (auto-yes in batch, where there is no one to ask). During
        /// <see cref="BuildUtezScene"/> the open scene is a fresh unsaved one, so this never
        /// blocks the generator itself.
        /// </summary>
        public static bool GuardWorkingScene(string rootName)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != WorkingScenePath)
                return true;
            if (Application.isBatchMode)
                return true;

            return EditorUtility.DisplayDialog(
                $"Regenerate {rootName}?",
                $"The open scene is {WorkingScenePath}, which is authored by hand. Regenerating " +
                $"destroys its current {rootName} and rebuilds it from code — anything you moved, " +
                "stretched or deleted there is lost.\n\nIterate the generator on utez_blockout.unity instead.",
                "Regenerate anyway", "Cancel");
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
            UtezPropBuilder.Build();
            var sun = ConfigureLighting();
            var player = SpawnPlayer();
            var volume = PsxRenderingSetup.SpawnGlobalVolume();
            UtezWeatherSetup.Build(player, volume, sun);
            SpawnUi();

            EditorSceneManager.SaveScene(scene, BlockoutScenePath);
            AssetDatabase.ImportAsset(BlockoutScenePath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[UTEZ] Blockout regenerated at {BlockoutScenePath}.");

            // The playable scene is hand-authored. Seed it from the blockout once, then
            // leave it alone forever — this is what lets Scene-view edits persist.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkingScenePath) == null)
            {
                AssetDatabase.CopyAsset(BlockoutScenePath, WorkingScenePath);
                Debug.Log($"[UTEZ] Seeded working scene {WorkingScenePath} from the blockout. " +
                          "Edit THIS one by hand; Setup Project will not regenerate it.");
            }
            else
            {
                Debug.Log($"[UTEZ] Working scene {WorkingScenePath} left untouched. To adopt fresh " +
                          "geometry, open the blockout and copy pieces across — or delete the working " +
                          "scene and re-run to reseed it.");
            }

            RegisterInBuildSettings(WorkingScenePath);
        }

        /// <summary>
        /// Drops in the pause menu. It builds its own hierarchy at runtime, so all the scene
        /// needs to carry is the component — no canvas to author, nothing to keep wired.
        /// </summary>
        private static void SpawnUi()
        {
            var go = new GameObject("UI");
            go.AddComponent<HorrorUtez.UI.PauseMenu>();
            Debug.Log("[UTEZ] Pause menu added to the scene.");
        }

        /// <summary>
        /// Ensures <paramref name="path"/> is a live, enabled entry in Build Settings.
        /// Rebuilds its entry rather than trusting a stale one, so a reseeded working scene
        /// (new GUID, same path) is picked up. Without this the play-mode smoke tests have
        /// nothing to load.
        /// </summary>
        private static void RegisterInBuildSettings(string path)
        {
            var kept = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            bool present = false;

            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path == path)
                {
                    present = true;
                    kept.Add(new EditorBuildSettingsScene(path, true));
                }
                else
                {
                    kept.Add(existing);
                }
            }

            if (!present)
                kept.Add(new EditorBuildSettingsScene(path, true));

            EditorBuildSettings.scenes = kept.ToArray();
            Debug.Log($"[UTEZ] Build Settings: {path} registered.");
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

            // Night, per the reel-look palette: a cold low moon and a navy ambient term, so the
            // sodium lamps are what actually light the plaza. UtezReelLook finishes the job
            // (night sky, reflection cubemap, lamp bulbs) on an existing scene.
            sun.transform.rotation = Quaternion.Euler(30f, 160f, 0f);
            sun.color = HorrorUtez.Rendering.PsxNightPalette.Moon;
            sun.intensity = HorrorUtez.Rendering.PsxNightPalette.MoonIntensityClear;
            sun.shadows = LightShadows.Hard;

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = HorrorUtez.Rendering.PsxNightPalette.AmbientClear;

            Debug.Log("[UTEZ] Night lighting configured.");
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
            // The plaza gap runs from the CECADEC north wall (real Z -9.5) to the CDS south
            // wall (real Z +10). Standing 5.3 real metres north of the origin puts the player
            // in open ground facing the CECADEC entrance, and stays right at any WorldScale.
            player.transform.SetPositionAndRotation(
                new Vector3(0f, 2f * UtezDimensions.WorldScale, 5.3f * UtezDimensions.WorldScale),
                Quaternion.Euler(0f, 180f, 0f));

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.02f;

            player.AddComponent<HorrorUtez.Player.PlayerInputReader>();
            var fpc = player.AddComponent<HorrorUtez.Player.FirstPersonController>();

            var stepSource = player.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.spatialBlend = 0f;
            player.AddComponent<HorrorUtez.Player.FootstepAudio>();
            player.AddComponent<HorrorUtez.Player.PauseController>();

            var head = new GameObject("Head");
            head.transform.SetParent(player.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.55f, 0f);

            var camera = head.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            camera.fieldOfView = 65f;
            // Without URP camera data post-processing is off: no tonemapper, no bloom.
            var cameraData = head.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            head.AddComponent<AudioListener>();
            head.AddComponent<HorrorUtez.Player.HeadBob>();

            // Flashlight on its own transform so it can lag the camera instead of being
            // welded to it. Range and angle are in world units, so they follow WorldScale.
            var torch = new GameObject("Flashlight");
            torch.transform.SetParent(head.transform, false);
            var beam = torch.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.96f, 0.88f);
            // URP punctual lights fall off with inverse square, so a corridor tens of units
            // long needs intensity in the hundreds just to put anything on the far wall.
            // The trade is unavoidable: whatever reaches the far end blows out the floor two
            // metres ahead. This value reads as a torch rather than a flare.
            // Intensity must track WorldScale SQUARED — double the world and every surface
            // sits twice as far away, which is a quarter of the light.
            const float torchIntensityAt1x = 155f;
            float lightScale = UtezDimensions.WorldScale * UtezDimensions.WorldScale;
            beam.intensity = torchIntensityAt1x * lightScale;
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
