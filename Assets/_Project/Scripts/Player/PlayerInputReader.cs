using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorUtez.Player
{
    /// <summary>
    /// Single input surface for the player, covering PC and mobile.
    ///
    /// Reads devices directly instead of going through an .inputactions asset: fewer
    /// moving parts while the control scheme is still settling. The trade-off is no
    /// rebinding UI — revisit if remapping becomes a requirement.
    ///
    /// Mobile scheme: the left half of the screen is a virtual stick that anchors
    /// wherever the thumb lands; the right half is drag-to-look. Pushing the stick
    /// near its edge runs, so there is no separate run button to hunt for.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Mouse")]
        [SerializeField] private float mouseSensitivity = 0.12f;

        [Header("Gamepad")]
        [SerializeField] private float gamepadLookSpeed = 180f;

        [Header("Touch")]
        [SerializeField] private float touchLookSensitivity = 0.10f;
        [Tooltip("Virtual stick radius, as a fraction of screen height.")]
        [SerializeField] private float touchStickRadius = 0.12f;
        [Tooltip("Stick deflection above which the player runs.")]
        [SerializeField, Range(0.5f, 1f)] private float touchRunThreshold = 0.8f;

        /// <summary>Desired movement on the ground plane, magnitude 0..1.</summary>
        public Vector2 Move { get; private set; }

        /// <summary>Look delta for this frame, in degrees.</summary>
        public Vector2 Look { get; private set; }

        public bool Run { get; private set; }

        /// <summary>True on the frame the light toggle was pressed.</summary>
        public bool ToggleLight { get; private set; }

        /// <summary>True on the frame jump was pressed.</summary>
        public bool Jump { get; private set; }

        [Tooltip("Right-side touches shorter than this, with no drag, count as a jump.")]
        [SerializeField] private float touchTapSeconds = 0.2f;
        [Tooltip("Pixels of drag allowed before a tap stops counting as a jump.")]
        [SerializeField] private float touchTapSlop = 20f;

        private float _lookStartTime;
        private float _lookDrag;
        private int _lastTouchCount;
        private int _moveTouchId = -1;
        private int _lookTouchId = -1;
        private Vector2 _moveOrigin;

        private void Update()
        {
            Vector2 move = Vector2.zero;
            Vector2 look = Vector2.zero;
            bool run = false;
            bool toggleLight = false;
            bool jump = false;

            ReadKeyboardMouse(ref move, ref look, ref run, ref toggleLight, ref jump);
            ReadGamepad(ref move, ref look, ref run, ref toggleLight, ref jump);
            ReadTouch(ref move, ref look, ref run, ref toggleLight, ref jump);

            Move = Vector2.ClampMagnitude(move, 1f);
            Look = look;
            Run = run;
            ToggleLight = toggleLight;
            Jump = jump;
        }

        private void ReadKeyboardMouse(ref Vector2 move, ref Vector2 look, ref bool run, ref bool toggleLight, ref bool jump)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) run = true;
                if (keyboard.fKey.wasPressedThisFrame) toggleLight = true;
                if (keyboard.spaceKey.wasPressedThisFrame) jump = true;
            }

            var mouse = Mouse.current;
            if (mouse != null)
                look += mouse.delta.ReadValue() * mouseSensitivity;
        }

        private void ReadGamepad(ref Vector2 move, ref Vector2 look, ref bool run, ref bool toggleLight, ref bool jump)
        {
            var pad = Gamepad.current;
            if (pad == null)
                return;

            move += pad.leftStick.ReadValue();
            look += pad.rightStick.ReadValue() * (gamepadLookSpeed * Time.deltaTime);
            if (pad.leftStickButton.isPressed || pad.leftTrigger.ReadValue() > 0.5f)
                run = true;
            if (pad.buttonNorth.wasPressedThisFrame)
                toggleLight = true;
            if (pad.buttonSouth.wasPressedThisFrame)
                jump = true;
        }

        private void ReadTouch(ref Vector2 move, ref Vector2 look, ref bool run, ref bool toggleLight, ref bool jump)
        {
            var screen = Touchscreen.current;
            if (screen == null)
                return;

            // Two fingers down at once toggles the light: no on-screen button to hunt for,
            // and it cannot be confused with a move or look drag.
            int pressed = 0;
            foreach (var t in screen.touches)
                if (t.press.isPressed) pressed++;
            if (pressed >= 2 && _lastTouchCount < 2)
                toggleLight = true;
            _lastTouchCount = pressed;

            float midpoint = Screen.width * 0.5f;
            float radius = Mathf.Max(Screen.height * touchStickRadius, 1f);

            foreach (var touch in screen.touches)
            {
                var phase = touch.phase.ReadValue();
                int id = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();

                if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (position.x < midpoint && _moveTouchId < 0)
                    {
                        _moveTouchId = id;
                        _moveOrigin = position;
                    }
                    else if (position.x >= midpoint && _lookTouchId < 0)
                    {
                        _lookTouchId = id;
                        _lookStartTime = Time.unscaledTime;
                        _lookDrag = 0f;
                    }
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                         phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    if (id == _moveTouchId) _moveTouchId = -1;
                    if (id == _lookTouchId)
                    {
                        // A quick stab that never turned into a drag is a jump, not a look.
                        bool quick = Time.unscaledTime - _lookStartTime < touchTapSeconds;
                        if (quick && _lookDrag < touchTapSlop)
                            jump = true;
                        _lookTouchId = -1;
                    }
                    continue;
                }

                if (id == _moveTouchId)
                {
                    Vector2 stick = Vector2.ClampMagnitude((position - _moveOrigin) / radius, 1f);
                    move += stick;
                    if (stick.magnitude >= touchRunThreshold)
                        run = true;
                }
                else if (id == _lookTouchId)
                {
                    Vector2 delta = touch.delta.ReadValue();
                    look += delta * touchLookSensitivity;
                    _lookDrag += delta.magnitude;
                }
            }
        }
    }
}
