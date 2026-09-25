using UnityEngine;
using HorrorUtez.Core;
using HorrorUtez.World;

namespace HorrorUtez.Player
{
    /// <summary>
    /// First-person movement and look, driven by <see cref="PlayerInputReader"/>.
    ///
    /// The player is NOT slowed down to create tension — that job belongs to the enemies,
    /// which are meant to be much faster. Exploration should feel unrestricted.
    ///
    /// The rig is human-sized (1.8 unit capsule) and is NOT multiplied by
    /// UtezDimensions.WorldScale, so the values below are authored in real metres and real
    /// m/s (WorldScale 1). Raising WorldScale would otherwise shrink the player relative to
    /// the campus, so traversal — speeds, acceleration, jump height, gravity — is
    /// multiplied by <see cref="_scale"/> at runtime. Look, crouch factors and timings are
    /// scale-free and stay as authored.
    /// </summary>
    // Early Awake: the body heights are applied here, and HeadBob caches the head's rest
    // position in its own Awake — it has to see the new eye height, not the scene's.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Movement (units/s)")]
        [Tooltip("Brisk human walk. One unit is one metre at WorldScale 1.")]
        [SerializeField] private float walkSpeed = 2.6f;
        [Tooltip("Hard run. Fast enough to cross the explanada without it feeling like a chore.")]
        [SerializeField] private float runSpeed = 5.2f;
        [Tooltip("How quickly the current speed chases the target speed.")]
        [SerializeField] private float acceleration = 12f;

        [Header("Body")]
        [Tooltip("Standing capsule height in metres. Applied on Awake, so it wins over " +
                 "whatever the CharacterController in the scene says.")]
        [SerializeField] private float standingHeight = 1.7f;
        [Tooltip("Camera height above the feet. A little under the capsule top, where eyes are. " +
                 "Lower eyes make the buildings loom and the plaza feel bigger.")]
        [SerializeField] private float eyeHeight = 1.575f;

        [Header("Look")]
        [SerializeField] private float pitchMin = -80f;
        [SerializeField] private float pitchMax = 80f;

        [Header("Crouch")]
        [SerializeField] private float crouchHeightFactor = 0.55f;
        [SerializeField] private float crouchSpeedFactor = 0.45f;
        [SerializeField] private float crouchLerpSpeed = 9f;

        [Header("Jump")]
        [Tooltip("Peak height in world units — real metres at WorldScale 1. Well above the "
                 + "0.3 m kerbs, which the CharacterController step offset already handles.")]
        [SerializeField] private float jumpHeight = 1f;
        [Tooltip("Grace period after walking off a ledge where a jump still registers.")]
        [SerializeField] private float coyoteTime = 0.12f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -18f;
        [Tooltip("Small downward push so the controller stays glued to slopes and steps.")]
        [SerializeField] private float groundedStick = -2f;
        [Tooltip("How far below the feet the ground probe reaches.")]
        [SerializeField] private float groundProbeDistance = 0.2f;

        [SerializeField] private Transform head;

        private CharacterController _controller;
        private PlayerInputReader _input;
        private Camera _camera;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;
        private float _lastGroundedTime;
        private float _standHeight;
        private Vector3 _standHeadLocal;
        private float _crouchBlend;

        /// <summary>
        /// World-unit multiplier from <see cref="UtezDimensions.WorldScale"/>. The rig is
        /// deliberately not scaled, so traversal distances and speeds are multiplied here
        /// instead: crossing the campus keeps the same feel whatever the campus is scaled
        /// to, and the jump arc keeps the same airtime because gravity scales with it.
        /// </summary>
        private float _scale = 1f;

        /// <summary>Ground speed in m/s. Head bob and, later, footstep audio read this.</summary>
        public float CurrentSpeed => _horizontalVelocity.magnitude;

        /// <summary>World-space ground velocity (m/s). The body animator reads it in local space.</summary>
        public Vector3 Velocity => _horizontalVelocity;

        /// <summary>Up/down speed (m/s); positive while rising from a jump.</summary>
        public float VerticalVelocity => _verticalVelocity;

        /// <summary>Walk and run speeds, so animation can match its playback rate to them.</summary>
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;

        public bool IsRunning { get; private set; }

        /// <summary>True while the controller is on the ground. Footstep audio reads this.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>0 = standing, 1 = fully crouched. Head bob scales with this too.</summary>
        public float CrouchBlend => _crouchBlend;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();

            if (head == null && transform.childCount > 0)
                head = transform.GetChild(0);

            _controller.height = standingHeight;
            _controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            if (head != null)
            {
                var local = head.localPosition;
                local.y = eyeHeight;
                head.localPosition = local;
            }

            _camera = head != null ? head.GetComponentInChildren<Camera>() : null;

            _standHeight = _controller.height;
            if (head != null)
                _standHeadLocal = head.localPosition;

            _scale = UtezDimensions.WorldScale;
        }

        private void OnEnable()
        {
            GameSettings.Changed += ApplyFieldOfView;
            ApplyFieldOfView();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= ApplyFieldOfView;
        }

        private void ApplyFieldOfView()
        {
            if (_camera != null)
                _camera.fieldOfView = GameSettings.FieldOfView;
        }

        private void Update()
        {
            // Mouse delta is not affected by timeScale, so a paused player would still be
            // able to spin the camera. Cursor ownership lives in PauseController.
            if (PauseController.IsPaused)
                return;

            ApplyLook();
            ApplyCrouch();
            ApplyMovement();
        }

        /// <summary>
        /// Shrinks the capsule and drops the head. Standing back up is refused while
        /// something is directly overhead, otherwise the player pops through ceilings and
        /// through the underside of the stairs.
        /// </summary>
        private void ApplyCrouch()
        {
            bool wantsCrouch = _input.Crouch;

            if (!wantsCrouch && _crouchBlend > 0.01f && Blocked())
                wantsCrouch = true;

            _crouchBlend = Mathf.MoveTowards(_crouchBlend, wantsCrouch ? 1f : 0f,
                crouchLerpSpeed * Time.deltaTime);

            float height = Mathf.Lerp(_standHeight, _standHeight * crouchHeightFactor, _crouchBlend);
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (head != null)
            {
                var target = _standHeadLocal;
                target.y = Mathf.Lerp(_standHeadLocal.y, _standHeadLocal.y * crouchHeightFactor, _crouchBlend);
                head.localPosition = target;
            }
        }

        /// <summary>
        /// Downward ray from inside the capsule. Rays started inside a collider do not
        /// report it (they exit through a backface), so this never hits the player.
        /// </summary>
        private bool ProbeGround()
        {
            const float originHeight = 0.4f;
            return Physics.Raycast(
                transform.position + Vector3.up * originHeight,
                Vector3.down,
                originHeight + groundProbeDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
        }

        private bool Blocked()
        {
            float radius = _controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (_controller.height - radius);
            float distance = _standHeight - _controller.height + 0.05f;
            return Physics.SphereCast(origin, radius, Vector3.up, out _, distance);
        }

        private void ApplyLook()
        {
            Vector2 look = _input.Look;

            // Yaw turns the body so movement follows the camera; pitch stays on the head.
            transform.Rotate(Vector3.up, look.x, Space.Self);

            _pitch = Mathf.Clamp(_pitch - look.y, pitchMin, pitchMax);
            if (head != null)
                head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void ApplyMovement()
        {
            Vector2 move = _input.Move;
            IsRunning = _input.Run && move.sqrMagnitude > 0.01f && _crouchBlend < 0.5f;

            Vector3 wish = transform.right * move.x + transform.forward * move.y;
            float targetSpeed = (IsRunning ? runSpeed : walkSpeed) * _scale;
            targetSpeed *= Mathf.Lerp(1f, crouchSpeedFactor, _crouchBlend);
            Vector3 targetVelocity = wish * targetSpeed;

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetVelocity, acceleration * _scale * Time.deltaTime);

            // CharacterController.isGrounded only reports what the LAST Move collided with,
            // so at high frame rates the downward stick shrinks below skin width and it
            // starts reporting airborne while the player is plainly standing still. An
            // explicit probe is frame-rate independent; the flag is kept as a cheap OR.
            IsGrounded = _controller.isGrounded || ProbeGround();
            if (IsGrounded)
                _lastGroundedTime = Time.time;

            if (IsGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStick * _scale;
            else
                _verticalVelocity += gravity * _scale * Time.deltaTime;

            // Coyote time: jumping the instant you step off an edge is a near-universal
            // player intent, and refusing it reads as the controller being unresponsive.
            bool canJump = Time.time - _lastGroundedTime <= coyoteTime;
            if (_input.Jump && canJump)
            {
                // Both jumpHeight and gravity are scaled at integration time, so the launch
                // speed carries one _scale and the airtime is unchanged.
                _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity) * _scale;
                _lastGroundedTime = float.NegativeInfinity;
            }

            Vector3 velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
