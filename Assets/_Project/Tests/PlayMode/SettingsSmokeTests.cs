using System.Collections;
using HorrorUtez.Core;
using HorrorUtez.Player;
using HorrorUtez.Rendering;
using HorrorUtez.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorUtez.Tests
{
    /// <summary>
    /// Runtime tests for the pause menu and the settings behind it.
    ///
    /// Same rule as the scene smoke tests: assert what a setting DOES, not that a field
    /// holds a number. A settings screen whose switches are wired to nothing looks correct
    /// in every inspector and is worthless.
    ///
    /// Every test restores what it touched. These settings live in PlayerPrefs, which is
    /// per machine and shared with the editor — a test that leaves the PSX filter on would
    /// silently change how the next person's editor looks.
    /// </summary>
    public sealed class SettingsSmokeTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/utez.unity";

        private bool _psxWasEnabled;
        private float _volumeWas;
        private int _frameCapWas;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _psxWasEnabled = PsxLook.Enabled;
            _volumeWas = GameSettings.MasterVolume;
            _frameCapWas = GameSettings.FrameCapIndex;

            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            for (int i = 0; i < 5; i++)
                yield return null;
        }

        [TearDown]
        public void Restore()
        {
            PsxLook.Enabled = _psxWasEnabled;
            GameSettings.MasterVolume = _volumeWas;
            GameSettings.FrameCapIndex = _frameCapWas;
        }

        [UnityTest]
        public IEnumerator Psx_filter_defaults_to_off_and_reaches_the_materials()
        {
            Assert.IsFalse(PsxLook.Enabled,
                "The PSX filter must ship off by default; it is what makes the editor hard to work in.");

            var renderer = FindPsxRenderer();
            Assert.IsNotNull(renderer, "No renderer in the scene uses a PSX shader.");

            // Off is expressed as a property block overriding the material's own switches.
            Assert.IsTrue(renderer.HasPropertyBlock(),
                "Filter is off but no override reached the renderer.");

            PsxLook.Enabled = true;
            yield return null;

            Assert.IsFalse(renderer.HasPropertyBlock(),
                "Turning the filter on must hand the renderer back to its material.");

            PsxLook.Enabled = false;
            yield return null;

            Assert.IsTrue(renderer.HasPropertyBlock(),
                "Turning the filter back off must restore the override.");
        }

        [UnityTest]
        public IEnumerator Pause_menu_shows_itself_only_while_paused()
        {
            var menu = Object.FindFirstObjectByType<PauseMenu>();
            Assert.IsNotNull(menu, "No pause menu in the scene.");

            var canvas = menu.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "The pause menu never built its canvas.");
            Assert.IsFalse(canvas.enabled, "The menu should be hidden while the game runs.");

            // Drive it through the pause authority rather than the menu, which is the path
            // the Escape key takes.
            typeof(PauseController)
                .GetMethod("SetPaused", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { true });
            yield return null;

            Assert.IsTrue(canvas.enabled, "Pausing did not open the menu.");

            PauseController.Resume();
            yield return null;

            Assert.IsFalse(canvas.enabled, "Resuming did not close the menu.");
            Assert.AreEqual(1f, Time.timeScale, "Resuming left the game frozen.");
        }

        [UnityTest]
        public IEnumerator Volume_and_frame_cap_reach_Unity()
        {
            GameSettings.MasterVolume = 0.42f;
            yield return null;
            Assert.AreEqual(0.42f, AudioListener.volume, 0.001f,
                "Master volume never reached the AudioListener.");

            GameSettings.FrameCapIndex = 0; // 30 FPS
            yield return null;
            Assert.AreEqual(30, Application.targetFrameRate, "Frame cap never reached Unity.");
            Assert.AreEqual(0, QualitySettings.vSyncCount,
                "vSync overrides targetFrameRate, so the cap would do nothing.");

            GameSettings.FrameCapIndex = System.Array.IndexOf(GameSettings.FrameCaps, 0);
            yield return null;
            Assert.AreEqual(-1, Application.targetFrameRate,
                "\"Sin límite\" must hand Unity -1, not 0.");
        }

        private static Renderer FindPsxRenderer()
        {
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var material = renderer.sharedMaterial;
                if (material != null && material.shader != null &&
                    material.shader.name.StartsWith("Shader Graphs/URP_PSX"))
                    return renderer;
            }

            return null;
        }
    }
}
