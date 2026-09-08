using UnityEngine;

namespace HorrorUtez.Player
{
    /// <summary>
    /// First-person movement and look, driven by <see cref="PlayerInputReader"/>.
    ///
    /// Speeds are deliberately slow. Horror pacing depends on the player not being able
    /// to outrun tension; a sprint that crosses the explanada in seconds throws it away.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Movement (m/s)")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float runSpeed = 4.5f;
        [Tooltip("How quickly the current speed chases the target speed.")]
        [SerializeField] private float acceleration = 12f;

        [Header("Look")]
        [SerializeField] private float pitchMin = -80f;
        [SerializeField] private float pitchMax = 80f;

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

        /// <summary>Ground speed in m/s. Head bob and, later, footstep audio read this.</summary>
        public float CurrentSpeed => _horizontalVelocity.magnitude;

        public bool IsRunning { get; private set; }

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

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStick;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
