using UnityEngine;

namespace HorrorUtez.World
{
    /// <summary>
    /// An interior fluorescent fitting that is on, but tired.
    ///
    /// Healthy tubes burn steady with a faint hum and, every half-minute or so, stutter: a
    /// quick burst of on/off flashes, the starter struggling. About one tube in five is
    /// failing: dimmer, and stuttering every few seconds. The fitting's glowing panel goes
    /// dark with the light, or a "dead" tube would still read as lit.
    ///
    /// Each light seeds its own random stream from its position, so the pattern is stable
    /// between runs but no two fittings flicker together.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class FluorescentFlicker : MonoBehaviour
    {
        [Tooltip("Share of tubes that are failing: stutter often and burn dimmer.")]
        [SerializeField, Range(0f, 1f)] private float failingShare = 0.2f;
        [Tooltip("Seconds between stutters for a healthy tube.")]
        [SerializeField] private Vector2 healthyGap = new(18f, 55f);
        [Tooltip("Seconds between stutters for a failing tube.")]
        [SerializeField] private Vector2 failingGap = new(1.5f, 6f);
        [Tooltip("Brightness wobble of a lit tube, as a fraction of its intensity.")]
        [SerializeField, Range(0f, 0.2f)] private float hum = 0.035f;
        [Tooltip("Glowing panels within this horizontal distance belong to this light.")]
        [SerializeField] private float glowRadius = 1.6f;

        private const string GlowPrefix = "Ceil_TrofferGlow_";

        private Light _light;
        private Renderer[] _glows;
        private System.Random _rng;
        private float _base;
        private float _seed;
        private bool _failing;
        private bool _on = true;
        private float _nextBurst;
        private float _burstEnd;
        private float _nextToggle;

        private void Awake()
        {
            _light = GetComponent<Light>();
            Vector3 p = transform.position;
            _seed = Mathf.Abs(p.x * 12.9898f + p.z * 78.233f) % 100f;
            _rng = new System.Random(Mathf.RoundToInt(_seed * 1000f));

            _failing = Rand() < failingShare;
            // Tubes age unevenly: a little brightness spread, the failing ones clearly dimmer.
            _base = _light.intensity * Mathf.Lerp(0.85f, 1.1f, Rand()) * (_failing ? 0.6f : 1f);
            _light.intensity = _base;
            _nextBurst = Time.time + Gap();

            var glows = new System.Collections.Generic.List<Renderer>();
            var scope = transform.parent != null ? transform.parent : transform;
            foreach (var r in scope.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith(GlowPrefix))
                    continue;
                Vector3 d = r.bounds.center - p;
                d.y = 0f;
                if (d.magnitude <= glowRadius * transform.lossyScale.x)
                    glows.Add(r);
            }
            _glows = glows.ToArray();
        }

        private float Rand() => (float)_rng.NextDouble();

        private float Gap()
        {
            var g = _failing ? failingGap : healthyGap;
            return Mathf.Lerp(g.x, g.y, Rand());
        }

        private void Update()
        {
            float t = Time.time;
            if (t >= _nextBurst && t >= _burstEnd)
            {
                _burstEnd = t + Mathf.Lerp(0.25f, _failing ? 1.4f : 0.7f, Rand());
                _nextBurst = _burstEnd + Gap();
                _nextToggle = t;
            }

            bool on;
            if (t < _burstEnd)
            {
                if (t >= _nextToggle)
                {
                    _on = !_on;
                    _nextToggle = t + Mathf.Lerp(0.02f, 0.13f, Rand());
                }
                on = _on;
            }
            else
            {
                on = _on = true;
            }

            float wobble = 1f - hum + hum * 2f * Mathf.PerlinNoise(_seed, t * 9f);
            _light.intensity = on ? _base * wobble : 0f;
            foreach (var g in _glows)
                g.enabled = on;
        }
    }
}
