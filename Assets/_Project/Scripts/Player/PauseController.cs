using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorUtez.Player
{
    /// <summary>
    /// Escape pauses the game and gives the mouse back.
    ///
    /// Without this the cursor is locked for the whole session with no way out but
    /// stopping play, which makes the build painful to test and impossible to alt-tab out
    /// of. Owning the cursor here rather than in the controller keeps a single authority
    /// over it, instead of two components fighting over lock state.
    /// </summary>
    public sealed class PauseController : MonoBehaviour
    {
        /// <summary>Read by the controller so a paused player cannot look around.</summary>
        public static bool IsPaused { get; private set; }

        /// <summary>
        /// Raised with the new state whenever the game pauses or resumes.
        ///
        /// The menu listens to this rather than the other way round: pausing has to keep
        /// working whether or not any UI exists, and a controller that reaches into a
        /// canvas is a controller that breaks the moment the canvas is not there.
        /// </summary>
        public static event Action<bool> PauseChanged;

        /// <summary>Lets the menu's resume button close the menu without duplicating state.</summary>
        public static void Resume() => SetPaused(false);

        private void OnEnable()
        {
            SetPaused(false);
        }

        private void OnDisable()
        {
            // Never leave the editor with a captured cursor after exiting play mode.
            IsPaused = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetPaused(!IsPaused);

            var pad = Gamepad.current;
            if (pad != null && pad.startButton.wasPressedThisFrame)
                SetPaused(!IsPaused);
        }

        private static void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
            PauseChanged?.Invoke(paused);
        }
    }
}
