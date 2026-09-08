using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PsxTerrainMaterials = HorrorUtez.Rendering.Editor.PsxTerrainMaterials;

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
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[UTEZ] Scene saved to {ScenePath}");
        }
    }
}
