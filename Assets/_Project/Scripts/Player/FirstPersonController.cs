using UnityEngine;

namespace HorrorUtez.Player
{
    /// <summary>
    /// First-person movement and look, driven by <see cref="PlayerInputReader"/>.
    ///
    /// The player is NOT slowed down to create tension — that job belongs to the enemies,
    /// which are meant to be much faster. Exploration should feel unrestricted.
    /// Speeds are also sized for a world built at UtezDimensions.WorldScale.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Movement (units/s)")]
        [SerializeField] private float walkSpeed = 3.8f;
        [SerializeField] private float runSpeed = 7.5f;
        [Tooltip("How quickly the current speed chases the target speed.")]
        [SerializeField] private float acceleration = 12f;

        [Header("Look")]
        [SerializeField] private float pitchMin = -80f;
        [SerializeField] private float pitchMax = 80f;

        [Header("Crouch")]
        [SerializeField] private float crouchHeightFactor = 0.55f;
        [SerializeField] private float crouchSpeedFactor = 0.45f;
        [SerializeField] private float crouchLerpSpeed = 9f;

        [Header("Jump")]
        [Tooltip("Peak height in world units. Scaled world, so this is not real metres.")]
        [SerializeField] private float jumpHeight = 1.6f;
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
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;
        private float _lastGroundedTime;
        private float _standHeight;
        private Vector3 _standHeadLocal;
        private float _crouchBlend;

        /// <summary>Ground speed in m/s. Head bob and, later, footstep audio read this.</summary>
        public float CurrentSpeed => _horizontalVelocity.magnitude;

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

            _standHeight = _controller.height;
            if (head != null)
                _standHeadLocal = head.localPosition;
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
            float targetSpeed = IsRunning ? runSpeed : walkSpeed;
            targetSpeed *= Mathf.Lerp(1f, crouchSpeedFactor, _crouchBlend);
            Vector3 targetVelocity = wish * targetSpeed;

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetVelocity, acceleration * Time.deltaTime);

            // CharacterController.isGrounded only reports what the LAST Move collided with,
            // so at high frame rates the downward stick shrinks below skin width and it
            // starts reporting airborne while the player is plainly standing still. An
            // explicit probe is frame-rate independent; the flag is kept as a cheap OR.
            IsGrounded = _controller.isGrounded || ProbeGround();
            if (IsGrounded)
                _lastGroundedTime = Time.time;

            if (IsGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStick;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            // Coyote time: jumping the instant you step off an edge is a near-universal
            // player intent, and refusing it reads as the controller being unresponsive.
            bool canJump = Time.time - _lastGroundedTime <= coyoteTime;
            if (_input.Jump && canJump)
            {
                _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                _lastGroundedTime = float.NegativeInfinity;
            }

            Vector3 velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
