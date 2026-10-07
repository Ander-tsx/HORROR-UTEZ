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
        public void Follow(Transform target) { player = target; }
        public float NightLightScale { get; set; } = 1;
        public float FogScale { get; set; } = 1;
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
        [SerializeField] private float clearFogDensity = PsxNightPalette.FogDensityClear;
        [SerializeField] private float stormFogDensity = PsxNightPalette.FogDensityStorm;
        [SerializeField] private Color clearFogColor = PsxNightPalette.FogClear;
        [SerializeField] private Color stormFogColor = PsxNightPalette.FogStorm;

        [Header("Light")]
        [SerializeField] private float clearSunIntensity = PsxNightPalette.MoonIntensityClear;
        [SerializeField] private float stormSunIntensity = PsxNightPalette.MoonIntensityStorm;
        [SerializeField] private Color clearAmbient = PsxNightPalette.AmbientClear;
        [SerializeField] private Color stormAmbient = PsxNightPalette.AmbientStorm;

        [Header("Rain")]
        [Tooltip("Rain never drops below this. 0 lets the campus dry out completely.")]
        [SerializeField, Range(0f, 1f)] private float rainFloor;
        [SerializeField] private float maxEmissionRate = 2600f;
        [SerializeField] private float maxRainVolume = 0.45f;

        [Header("Surface wetness")]
        [Tooltip("How fast the ground soaks up once rain starts, per second.")]
        [SerializeField] private float soakRate = 0.12f;
        [Tooltip("How fast it dries after the rain stops, per second. Slow on purpose: " +
                 "a campus that is still shining after a storm is half the atmosphere.")]
        [SerializeField] private float dryRate = 0.008f;

        /// <summary>0 = dry, 1 = downpour. Other systems read this rather than guessing.</summary>
        public float RainIntensity { get; private set; }

        /// <summary>True once the rain is heavy enough to be worth reacting to.</summary>
        public bool IsRaining => RainIntensity > 0.08f;

        /// <summary>True while something solid is directly overhead.</summary>
        public bool IsSheltered { get; private set; }

        /// <summary>0 = dry ground, 1 = soaked. Lags the rain both ways.</summary>
        public float SurfaceWetness { get; private set; }

        private static readonly int WetnessId = Shader.PropertyToID("_PsxWetness");

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

            // Start at the floor so the first storm is something the player watches arrive.
            // The ground starts as wet as that rain would have left it.
            RainIntensity = rainFloor;
            SurfaceWetness = rainFloor;
            PickNewTarget();
            Apply();
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(WetnessId, 0f);
        }

        private void Update()
        {
            if (Time.time >= _nextChangeTime)
                PickNewTarget();

            RainIntensity = Mathf.MoveTowards(RainIntensity, _target, changeRate * Time.deltaTime);

            float rate = RainIntensity > SurfaceWetness ? soakRate : dryRate;
            SurfaceWetness = Mathf.MoveTowards(SurfaceWetness, RainIntensity, rate * Time.deltaTime);

            Apply();
        }

        private void PickNewTarget()
        {
            // Squared: uniform targets would leave the campus soaked most of the time.
            float roll = Random.value;
            _target = Mathf.Lerp(rainFloor, 1f, roll * roll);
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

            // The visual style (pause menu) scales the atmosphere: Penumbra thickens the fog
            // and all but removes the moon. Read every frame so a switch lands immediately.
            var style = PsxStylePresets.Current;

            if (_fog != null)
            {
                _fog.density.overrideState = true;
                _fog.density.value = Mathf.Lerp(clearFogDensity, stormFogDensity, t) * style.FogDensityScale * FogScale;
                _fog.fogColor.overrideState = true;
                _fog.fogColor.value = Color.Lerp(clearFogColor, stormFogColor, t) * style.FogTint;
            }

            if (sun != null)
                sun.intensity = Mathf.Lerp(clearSunIntensity, stormSunIntensity, t) * style.MoonScale * NightLightScale;

            // Surfaces now take their ambient from the spherical-harmonics probe (PsxLit is a
            // full URP lit shader), and editing ambientLight alone does not rebuild that probe
            // at runtime. Write both, so a storm actually darkens the shadowed faces.
            Color ambient = Color.Lerp(clearAmbient, stormAmbient, t) * style.AmbientScale * NightLightScale;
            RenderSettings.ambientLight = ambient;
            // Mostly flat, plus a lobe from straight up: faces open to the sky catch more of
            // the night blue than the undersides of eaves, which keeps forms readable.
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambient * 0.75f);
            probe.AddDirectionalLight(Vector3.up, ambient * 0.5f, 1f);
            RenderSettings.ambientProbe = probe;

            Shader.SetGlobalFloat(WetnessId, SurfaceWetness);

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
