using UnityEngine;

namespace HorrorUtez.World
{
    /// <summary>
    /// Failing sodium lamp.
    ///
    /// Flicker is driven by two independent things: a constant low hum of brightness
    /// noise, and rare hard dropouts where the lamp cuts out entirely for a fraction of a
    /// second. Regular sine flicker reads as a special effect; irregular dropouts read as
    /// broken hardware, which is what makes a corridor feel unmaintained rather than
    /// decorated.
    ///
    /// Each lamp is seeded from its own position, so no two on the same street pulse
    /// together — synchronised lamps are an instant giveaway that they are one script.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LampFlicker : MonoBehaviour
    {
        [SerializeField] private float baseIntensity = 120f;

        [Header("Hum")]
        [Tooltip("How much the steady brightness wobbles, as a fraction of base.")]
        [SerializeField, Range(0f, 0.5f)] private float humDepth = 0.12f;
        [SerializeField] private float humSpeed = 7f;

        [Header("Dropouts")]
        [Tooltip("Chance per second that the lamp cuts out entirely.")]
        [SerializeField, Range(0f, 2f)] private float dropoutsPerSecond = 0.25f;
        [SerializeField] private Vector2 dropoutSeconds = new(0.04f, 0.35f);

        private Light _light;
        private float _seed;
        private float _dropoutUntil;
        private Renderer _bulb;

        private void Awake()
        {
            _light = GetComponent<Light>();
            baseIntensity = _light.intensity;

            // Position-derived so the pattern is stable across reloads but unique per lamp.
            _seed = Mathf.Abs(transform.position.x * 12.9898f + transform.position.z * 78.233f) % 100f;

            // The visible bulb should go dark with the light, or a "broken" lamp still glows.
            _bulb = GetComponentInChildren<Renderer>();
        }

        private void Update()
        {
            bool dropped = Time.time < _dropoutUntil;

            if (!dropped && Random.value < dropoutsPerSecond * Time.deltaTime)
            {
                _dropoutUntil = Time.time + Random.Range(dropoutSeconds.x, dropoutSeconds.y);
                dropped = true;
            }

            if (dropped)
            {
                _light.intensity = 0f;
                if (_bulb != null) _bulb.enabled = false;
                return;
            }

            float hum = Mathf.PerlinNoise(_seed, Time.time * humSpeed);
            _light.intensity = baseIntensity * (1f - humDepth + humDepth * hum * 2f);
            if (_bulb != null) _bulb.enabled = true;
        }
    }
}
