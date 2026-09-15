using HorrorUtez.Core;
using HorrorUtez.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorUtez.World
{
    /// <summary>
    /// Drifting rain that drags the rest of the atmosphere with it.
    ///
    /// One value — <see cref="RainIntensity"/> — drives fog density and colour, ambient
    /// light, sun strength, particle emission and the audio mix. Wiring them to a single
    /// source is what keeps a downpour from looking like clear weather with sprites in
    /// front of it, and it is why heavy rain makes the flashlight genuinely necessary
    /// rather than merely decorative.
    ///
    /// The target wanders on its own and is squared before use, which biases the campus
    /// toward calm: a storm that is always happening stops being an event.
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Volume volume;
        [SerializeField] private Light sun;
        [SerializeField] private ParticleSystem rain;
        [SerializeField] private AudioSource rainAudio;
        [SerializeField] private AudioSource windAudio;
        [Tooltip("Followed by the rain emitter, and used for the indoor check.")]
        [SerializeField] private Transform player;

        [Header("Timing")]
        [Tooltip("Shortest time a weather phase holds before a new target is picked.")]
        [SerializeField] private float minPhaseSeconds = 25f;
        [SerializeField] private float maxPhaseSeconds = 90f;
        [Tooltip("Intensity units per second. Low values make weather turn slowly.")]
        [SerializeField] private float changeRate = 0.06f;

        [Header("Fog")]
        [SerializeField] private float clearFogDensity = 0.008f;
        [SerializeField] private float stormFogDensity = 0.045f;
        [SerializeField] private Color clearFogColor = new(0.42f, 0.46f, 0.52f);
        [SerializeField] private Color stormFogColor = new(0.30f, 0.33f, 0.38f);

        [Header("Light")]
        [SerializeField] private float clearSunIntensity = 1.1f;
        [SerializeField] private float stormSunIntensity = 0.25f;
        [SerializeField] private Color clearAmbient = new(0.20f, 0.22f, 0.28f);
        [SerializeField] private Color stormAmbient = new(0.06f, 0.07f, 0.10f);

        [Header("Rain")]
        [SerializeField] private float maxEmissionRate = 2600f;
        [SerializeField] private float maxRainVolume = 0.45f;

        /// <summary>0 = dry, 1 = downpour. Other systems read this rather than guessing.</summary>
        public float RainIntensity { get; private set; }

        /// <summary>True once the rain is heavy enough to be worth reacting to.</summary>
        public bool IsRaining => RainIntensity > 0.08f;

        /// <summary>True while something solid is directly overhead.</summary>
        public bool IsSheltered { get; private set; }

        private PsxFogVolume _fog;
        private float _target;
        private float _nextChangeTime;

        private void Awake()
        {
            if (volume != null && volume.profile != null)
                volume.profile.TryGet(out _fog);

            if (_fog == null)
                Debug.LogWarning("[WEATHER] No PsxFogVolume on the volume profile; fog will not react.");

            // Clips are synthesised rather than shipped as files; see ProceduralAudio.
            if (rainAudio != null)
            {
                rainAudio.clip = ProceduralAudio.CreateRainLoop();
                rainAudio.loop = true;
                rainAudio.volume = 0f;
                rainAudio.Play();
            }

            if (windAudio != null)
            {
                windAudio.clip = ProceduralAudio.CreateWindLoop();
                windAudio.loop = true;
                windAudio.volume = 0.12f;
                windAudio.Play();
            }

            // Start dry so the first storm is something the player watches arrive.
            RainIntensity = 0f;
            PickNewTarget();
            Apply();
        }

        private void Update()
        {
            if (Time.time >= _nextChangeTime)
                PickNewTarget();

            RainIntensity = Mathf.MoveTowards(RainIntensity, _target, changeRate * Time.deltaTime);
            Apply();
        }

        private void PickNewTarget()
        {
            // Squared: uniform targets would leave the campus soaked most of the time.
            float roll = Random.value;
            _target = roll * roll;
            _nextChangeTime = Time.time + Random.Range(minPhaseSeconds, maxPhaseSeconds);
        }

        private void Apply()
        {
            float t = RainIntensity;

            // Rain must not fall through roofs, and it must sound muffled once you are
            // under one. A single upward ray is far cheaper than particle collision and
            // gives both for free.
            IsSheltered = player != null &&
                          Physics.Raycast(player.position + Vector3.up * 0.5f, Vector3.up, 60f);
            float outdoor = IsSheltered ? 0f : 1f;

            if (_fog != null)
            {
                _fog.density.overrideState = true;
                _fog.density.value = Mathf.Lerp(clearFogDensity, stormFogDensity, t);
                _fog.fogColor.overrideState = true;
                _fog.fogColor.value = Color.Lerp(clearFogColor, stormFogColor, t);
            }

            if (sun != null)
                sun.intensity = Mathf.Lerp(clearSunIntensity, stormSunIntensity, t);

            RenderSettings.ambientLight = Color.Lerp(clearAmbient, stormAmbient, t);

            if (rain != null)
            {
                var emission = rain.emission;
                emission.rateOverTime = maxEmissionRate * t * outdoor;

                bool shouldPlay = t > 0.01f && !IsSheltered;
                if (shouldPlay && !rain.isPlaying) rain.Play();
                else if (!shouldPlay && rain.isPlaying) rain.Stop();
            }

            if (rainAudio != null)
                rainAudio.volume = maxRainVolume * t * (IsSheltered ? 0.35f : 1f);

            if (windAudio != null)
                windAudio.volume = 0.12f + 0.35f * t;
        }
    }
}
