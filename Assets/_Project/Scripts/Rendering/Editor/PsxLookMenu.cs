using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorUtez.Rendering.Editor
{
    /// <summary>
    /// Editor-side switch for the PS1 look, so the Scene and Game views can be worked in
    /// clean without entering play mode and without editing any asset.
    ///
    /// It drives the same <see cref="PsxLook"/> the pause menu drives, and the same
    /// PlayerPrefs entry, so the editor and the game never disagree about the state.
    ///
    /// The material overrides live in MaterialPropertyBlocks, which Unity does not
    /// serialise. They are therefore lost on every domain reload and every scene open,
    /// which is what the hooks below exist to repair.
    /// </summary>
    public static class PsxLookMenu
    {
        private const string MenuPath = "HORROR-UTEZ/PSX Filter";

        [MenuItem(MenuPath, priority = 0)]
        private static void Toggle()
        {
            PsxLook.Toggle();
            PsxLookBinder.ApplyToLoadedScenes(PsxLook.Enabled);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, PsxLook.Enabled);
            return true;
        }

        /// <summary>
        /// Batch entry points, so the documented verification flow can shoot the campus with
        /// the filter on and with it off without anyone clicking a menu.
        /// </summary>
        public static void EnableBatch() => SetBatch(true);

        public static void DisableBatch() => SetBatch(false);

        private static void SetBatch(bool enabled)
        {
            PsxLook.Enabled = enabled;
            PsxLookBinder.ApplyToLoadedScenes(enabled);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Re-applies after a script compile or an editor restart. Deferred because the
        /// scene is not guaranteed to be loaded while static constructors are still running.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.delayCall += ReapplyQuietly;
            EditorSceneManager.sceneOpened += (_, _) => ReapplyQuietly();
        }

        private static void ReapplyQuietly()
        {
            // Nothing to repair when the look is on: "on" is the absence of an override.
            if (PsxLook.Enabled)
                return;

            PsxLookBinder.ApplyToLoadedScenes(false);
        }
    }
}
