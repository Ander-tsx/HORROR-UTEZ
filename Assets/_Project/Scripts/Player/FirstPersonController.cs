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

        [Header("Jump")]
        [Tooltip("Peak height in world units. Scaled world, so this is not real metres.")]
        [SerializeField] private float jumpHeight = 1.6f;
        [Tooltip("Grace period after walking off a ledge where a jump still registers.")]
        [SerializeField] private float coyoteTime = 0.12f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -18f;
        [Tooltip("Small downward push so the controller stays glued to slopes and steps.")]
        [SerializeField] private float groundedStick = -2f;

        [SerializeField] private Transform head;

        private CharacterController _controller;
        private PlayerInputReader _input;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;
        private float _lastGroundedTime;

        /// <summary>Ground speed in m/s. Head bob and, later, footstep audio read this.</summary>
        public float CurrentSpeed => _horizontalVelocity.magnitude;

        public bool IsRunning { get; private set; }

        /// <summary>True while the controller is on the ground. Footstep audio reads this.</summary>
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();

            if (head == null && transform.childCount > 0)
                head = transform.GetChild(0);
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            ApplyLook();
            ApplyMovement();
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
            IsRunning = _input.Run && move.sqrMagnitude > 0.01f;

            Vector3 wish = transform.right * move.x + transform.forward * move.y;
            float targetSpeed = IsRunning ? runSpeed : walkSpeed;
            Vector3 targetVelocity = wish * targetSpeed;

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetVelocity, acceleration * Time.deltaTime);

            IsGrounded = _controller.isGrounded;
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
