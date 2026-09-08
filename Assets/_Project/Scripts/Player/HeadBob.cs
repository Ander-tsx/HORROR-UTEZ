using UnityEngine;

namespace HorrorUtez.Player
{
    /// <summary>
    /// Camera bob while walking and running.
    ///
    /// The vertical figure is traced at twice the horizontal rate, which is what makes
    /// it read as footfalls rather than a swaying boat. Amplitude scales with actual
    /// ground speed, so easing to a stop settles the camera instead of snapping it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class HeadBob : MonoBehaviour
    {
        [SerializeField] private FirstPersonController controller;

        [Header("Walk")]
        [SerializeField] private float walkFrequency = 1.8f;
        [SerializeField] private float walkAmplitude = 0.035f;

        [Header("Run")]
        [SerializeField] private float runFrequency = 2.6f;
        [SerializeField] private float runAmplitude = 0.055f;

        [Tooltip("How quickly the bob fades in and out as speed changes.")]
        [SerializeField] private float blendSpeed = 8f;

        private Vector3 _restPosition;
        private float _phase;
        private float _weight;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            if (controller == null)
                controller = GetComponentInParent<FirstPersonController>();
        }

        private void LateUpdate()
        {
            if (controller == null)
                return;

            bool running = controller.IsRunning;
            float frequency = running ? runFrequency : walkFrequency;
            float amplitude = running ? runAmplitude : walkAmplitude;

            // Normalised against walk speed so a slow drift bobs less than a full stride.
            float speed = controller.CurrentSpeed;
            float target = Mathf.Clamp01(speed / 3.8f);
            _weight = Mathf.MoveTowards(_weight, target, blendSpeed * Time.deltaTime);

            if (_weight <= 0.001f)
            {
                _phase = 0f;
                transform.localPosition = Vector3.Lerp(
                    transform.localPosition, _restPosition, blendSpeed * Time.deltaTime);
                return;
            }

            _phase += Time.deltaTime * frequency * Mathf.PI * 2f * Mathf.Max(speed, 0.1f);

            float horizontal = Mathf.Cos(_phase) * amplitude * _weight;
            float vertical = Mathf.Sin(_phase * 2f) * amplitude * 0.5f * _weight;

            transform.localPosition = _restPosition + new Vector3(horizontal, vertical, 0f);
        }
    }
}
