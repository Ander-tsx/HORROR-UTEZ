using HorrorUtez.Core;
using UnityEngine;

namespace HorrorUtez.Player
{
    /// <summary>
    /// Footsteps triggered by distance travelled, not by a timer.
    ///
    /// Distance-based means the cadence follows the actual gait for free: walking, running
    /// and easing to a halt all sound right without separate cases. A timer would drift out
    /// of step the moment speed changed.
    ///
    /// The surface under the player is sampled per step, so grass sounds duller than the
    /// concrete explanada.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class FootstepAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource source;

        [Tooltip("World units between footfalls. Scaled world, so not real metres.")]
        [SerializeField] private float strideLength = 1.5f;

        [SerializeField] private float volume = 0.35f;
        [SerializeField] private Vector2 pitchRange = new(0.88f, 1.12f);

        private FirstPersonController _controller;
        private AudioClip[] _hard;
        private AudioClip[] _soft;
        private AudioClip _land;
        private float _distance;
        private bool _wasGrounded = true;

        private void Awake()
        {
            _controller = GetComponent<FirstPersonController>();
            if (source == null)
                source = GetComponent<AudioSource>();

            // Several variants because a single repeated sample is the fastest way to make
            // footsteps sound like a machine rather than a person.
            _hard = new[]
            {
                ProceduralAudio.CreateFootstep(101, soft: false),
                ProceduralAudio.CreateFootstep(102, soft: false),
                ProceduralAudio.CreateFootstep(103, soft: false),
                ProceduralAudio.CreateFootstep(104, soft: false),
            };
            _soft = new[]
            {
                ProceduralAudio.CreateFootstep(201, soft: true),
                ProceduralAudio.CreateFootstep(202, soft: true),
                ProceduralAudio.CreateFootstep(203, soft: true),
            };
            _land = ProceduralAudio.CreateFootstep(301, soft: false);
        }

        private void Update()
        {
            if (source == null || _controller == null)
                return;

            bool grounded = _controller.IsGrounded;

            if (grounded && !_wasGrounded)
            {
                source.pitch = 0.8f;
                source.PlayOneShot(_land, volume * 1.4f);
                _distance = 0f;
            }
            _wasGrounded = grounded;

            if (!grounded)
                return;

            _distance += _controller.CurrentSpeed * Time.deltaTime;
            // Shorter stride when crouched, so the cadence stays believable at low speed.
            float stride = strideLength * Mathf.Lerp(1f, 0.65f, _controller.CrouchBlend);
            if (_distance < stride)
                return;

            _distance -= stride;
            PlayStep();
        }

        private void PlayStep()
        {
            var bank = IsSoftGround() ? _soft : _hard;
            var clip = bank[Random.Range(0, bank.Length)];

            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            // Running lands harder than strolling; crouching is the point of crouching.
            float gain = volume * (_controller.IsRunning ? 1.3f : 1f);
            gain *= Mathf.Lerp(1f, 0.25f, _controller.CrouchBlend);
            source.PlayOneShot(clip, gain);
        }

        /// <summary>Grass, dirt and the forest floor read as soft; concrete does not.</summary>
        private bool IsSoftGround()
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var hit, 3f))
                return false;

            string name = hit.collider.gameObject.name;
            return name.Contains("Grass") || name.Contains("Dirt") || name.Contains("Boundary");
        }
    }
}
