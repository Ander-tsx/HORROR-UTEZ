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
    /// </summary>
    public sealed class Flashlight : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Light beam;
        [SerializeField] private bool startOn = true;

        [Tooltip("Degrees per second the beam chases the camera. Lower drags more.")]
        [SerializeField] private float followSpeed = 14f;

        private void Awake()
        {
            if (input == null)
                input = GetComponentInParent<PlayerInputReader>();
            if (beam == null)
                beam = GetComponent<Light>();

            if (beam != null)
                beam.enabled = startOn;
        }

        private void LateUpdate()
        {
            if (beam == null)
                return;

            if (input != null && input.ToggleLight)
                beam.enabled = !beam.enabled;

            // Chase the parent's forward rather than inheriting it outright.
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                transform.parent != null ? transform.parent.rotation : transform.rotation,
                followSpeed * 60f * Time.deltaTime);
        }
    }
}
