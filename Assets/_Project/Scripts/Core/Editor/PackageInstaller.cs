using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace HorrorUtez.Core.Editor
{
    /// <summary>
    /// Installs packages through UPM so the editor picks the version compatible with
    /// itself. Never hand-write a version into Packages/manifest.json: pinning old
    /// versions is what dropped this project into Safe Mode three times (see
    /// docs/technical/unity-setup.md).
    ///
    /// Batch entry points deliberately do NOT pair with -quit: UPM requests only
    /// complete while the editor keeps pumping EditorApplication.update.
    /// </summary>
    public static class PackageInstaller
    {
        private static AddRequest _request;
        private static string _packageId;

        [MenuItem("HORROR-UTEZ/Install Input System")]
        public static void InstallInputSystem() => Install("com.unity.inputsystem");

        public static void InstallInputSystemBatch()
        {
            Install("com.unity.inputsystem");
            EditorApplication.update += PollBatch;
        }

        private static void Install(string packageId)
        {
            _packageId = packageId;
            _request = Client.Add(packageId);
            Debug.Log($"[PKG] Requesting {packageId} (version chosen by the editor)");
        }

        private static void PollBatch()
        {
            if (_request == null || !_request.IsCompleted)
                return;

            EditorApplication.update -= PollBatch;

            if (_request.Status == StatusCode.Success)
            {
                Debug.Log($"[PKG] Installed {_request.Result.name}@{_request.Result.version}");
                EnableNewInputSystem();
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[PKG] Failed to install {_packageId}: {_request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Switches Active Input Handling to the new Input System only.
        /// The setting lives in ProjectSettings.asset as "activeInputHandler":
        /// 0 = legacy, 1 = new, 2 = both.
        /// </summary>
        private static void EnableNewInputSystem()
        {
            const string path = "ProjectSettings/ProjectSettings.asset";
            var settings = AssetDatabase.LoadAllAssetsAtPath(path);
            if (settings == null || settings.Length == 0)
            {
                Debug.LogWarning("[PKG] Could not open ProjectSettings.asset to set activeInputHandler.");
                return;
            }

            var so = new SerializedObject(settings[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null)
            {
                Debug.LogWarning("[PKG] activeInputHandler property not found; set it manually in Player Settings.");
                return;
            }

            prop.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[PKG] Active Input Handling set to Input System Package (New).");
        }
    }
}
