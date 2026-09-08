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
        }
    }
}
