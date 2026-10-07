using System.Collections;
using HorrorUtez.Player;
using HorrorUtez.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorUtez.Tests
{
    /// <summary>
    /// Runtime smoke tests for the `utez` scene.
    ///
    /// Everything else in this project is verified in edit mode, where Awake and Update
    /// never run — which means a null reference in any system would go unnoticed until
    /// someone pressed Play. These tests exist to actually execute the scene: they load
    /// it, let it tick, and fail on the first logged error.
    ///
    /// They deliberately assert behaviour rather than structure. Checking that a component
    /// exists only proves the builder ran; checking that weather moves proves it works.
    /// </summary>
    public sealed class UtezSceneSmokeTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Legacy/utez.unity";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            // A few frames so every Awake and the first Updates have run.
            for (int i = 0; i < 5; i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ticks_without_logging_errors()
        {
            // Any Debug.LogError or unhandled exception during this window fails the test.
            yield return new WaitForSeconds(2f);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Player_falls_to_the_ground_and_settles()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player rig missing from the scene.");

            var controller = player.GetComponent<FirstPersonController>();
            Assert.IsNotNull(controller);

            float spawnHeight = player.transform.position.y;

            // Wait on SIMULATED time, not frame count. Batch mode runs frames as fast as it
            // can, so deltaTime shrinks to fractions of a millisecond and a hundred frames
            // advance the physics by almost nothing — which looks exactly like a player that
            // refuses to fall.
            yield return new WaitForSeconds(2f);

            float landed = player.transform.position.y;

            // Assert the contract the game actually relies on, not Unity's raw flag: the
            // player fell, and the controller considers itself grounded. The raw
            // CharacterController.isGrounded is deliberately not used here — it depends on
            // the size of the last Move, which batch mode makes vanishingly small.
            Assert.Less(landed, spawnHeight - 1f,
                $"Player did not fall: spawned at {spawnHeight:F2}, still at {landed:F2}.");
            Assert.Less(landed, 1f, $"Player settled too high at y={landed:F2}; slab top is y=0.");
            Assert.IsTrue(controller.IsGrounded, $"Controller not grounded at y={landed:F2}.");
        }

        [UnityTest]
        public IEnumerator Weather_drifts_and_drives_the_fog()
        {
            var weather = Object.FindFirstObjectByType<WeatherSystem>();
            Assert.IsNotNull(weather, "WeatherSystem missing from the scene.");

            // Force a storm rather than waiting out the random phase timer.
            var field = typeof(WeatherSystem).GetField("_target",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "WeatherSystem._target was renamed; update this test.");
            field.SetValue(weather, 1f);

            float start = weather.RainIntensity;
            yield return new WaitForSeconds(2f);

            Assert.Greater(weather.RainIntensity, start,
                "Rain intensity never rose toward its target.");
        }

        [UnityTest]
        public IEnumerator Flashlight_and_pause_start_in_a_sane_state()
        {
            var torch = GameObject.Find("Flashlight");
            Assert.IsNotNull(torch, "Flashlight missing from the player rig.");
            Assert.IsNotNull(torch.GetComponent<Light>());

            Assert.IsFalse(PauseController.IsPaused, "Game should not start paused.");
            Assert.AreEqual(1f, Time.timeScale, "Time scale should start at 1.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Procedural_audio_clips_are_generated_and_playing()
        {
            var weather = Object.FindFirstObjectByType<WeatherSystem>();
            Assert.IsNotNull(weather);

            var sources = weather.GetComponents<AudioSource>();
            Assert.GreaterOrEqual(sources.Length, 2, "Expected rain and wind sources.");

            foreach (var source in sources)
            {
                if (!source.loop)
                    continue; // one-shot source for thunder

                Assert.IsNotNull(source.clip, "Looping ambience source has no generated clip.");
                Assert.Greater(source.clip.samples, 0, "Generated clip is empty.");
            }

            yield return null;
        }
    }
}
