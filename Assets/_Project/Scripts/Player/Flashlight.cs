using HorrorUtez.Core;
using UnityEngine;

namespace HorrorUtez.Player
{
    /// <summary>
    /// Head-mounted flashlight.
    ///
    /// Not a convenience: these buildings are windowless concrete at night, so without it
    /// every interior is pure black and unnavigable. It is also the lever the horror
    /// design will lean on — what the cone does NOT reach is where the tension lives.
    ///
    /// The beam lags the camera slightly. A light welded rigidly to the view reads as if
    /// it were painted on the screen; a little lag reads as a held object.
    ///
    /// Exposure: URP spot lights fall off with the inverse square, so one fixed intensity
    /// either blows a wall a metre away to white or leaves a corridor's far end black. The
    /// beam instead measures the nearest surface inside its cone every frame and sets its
    /// intensity so that surface is lit to <see cref="spotBrightness"/>: readable up close,
    /// fading naturally with whatever lies beyond it.
    /// </summary>
    public sealed class Flashlight : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Light beam;
        [SerializeField] private AudioSource source;
        [SerializeField] private bool startOn = true;

        [Tooltip("Degrees per second the beam chases the camera. Lower drags more.")]
        [SerializeField] private float followSpeed = 14f;

        [Header("Exposure")]
        [Tooltip("Irradiance on the nearest lit surface (intensity / distance²). ~1 keeps white plaster readable.")]
        [SerializeField] private float spotBrightness = 1.1f;
        [SerializeField] private float minIntensity = 0.6f;
        [SerializeField] private float maxIntensity = 155f;
        [Tooltip("How fast the exposure settles, per second. Snaps down quickly, eases up.")]
        [SerializeField] private float exposureSpeed = 8f;
        [SerializeField] private LayerMask exposureMask = ~0;

        // Probe directions inside the cone, as (yaw, pitch) fractions of the outer half-angle.
        private static readonly Vector2[] Probes =
        {
            new(0f, 0f), new(0.55f, 0f), new(-0.55f, 0f), new(0f, 0.55f), new(0f, -0.55f),
        };

        private AudioClip _click;

        private void Awake()
        {
            if (input == null)
                input = GetComponentInParent<PlayerInputReader>();
            if (beam == null)
                beam = GetComponent<Light>();

            if (source == null)
                source = GetComponent<AudioSource>();
            _click = ProceduralAudio.CreateClick();

            if (beam != null)
                beam.enabled = startOn;
        }

        private void LateUpdate()
        {
            if (beam == null)
                return;

            if (input != null && input.ToggleLight)
            {
                beam.enabled = !beam.enabled;
                if (source != null && _click != null)
                    source.PlayOneShot(_click, 0.5f);
            }

            // Chase the parent's forward rather than inheriting it outright.
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                transform.parent != null ? transform.parent.rotation : transform.rotation,
                followSpeed * 60f * Time.deltaTime);

            if (beam.enabled)
                Expose();
        }

        private void Expose()
        {
            float halfAngle = beam.spotAngle * 0.5f;
            float nearest = beam.range;
            Vector3 origin = transform.position;
            foreach (var probe in Probes)
            {
                var dir = transform.rotation * Quaternion.Euler(-probe.y * halfAngle, probe.x * halfAngle, 0f) *
                          Vector3.forward;
                // The ray starts inside the player's own capsule, which a raycast from
                // within never reports, so the rig does not dim its own light.
                if (Physics.Raycast(origin, dir, out var hit, beam.range, exposureMask, QueryTriggerInteraction.Ignore))
                    nearest = Mathf.Min(nearest, hit.distance);
            }

            float target = Mathf.Clamp(spotBrightness * nearest * nearest, minIntensity, maxIntensity);
            float speed = target < beam.intensity ? exposureSpeed * 3f : exposureSpeed;
            beam.intensity = Mathf.Lerp(beam.intensity, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        }
    }
}
