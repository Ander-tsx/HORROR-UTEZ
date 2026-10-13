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
        // Also re-samples the roofs: by the time a player exists, objects built at runtime (the truck) are in place.
        public void Follow(Transform target) { player = target; if (rain != null) BuildRoofMap(); }
        public float NightLightScale { get; set; } = 1;
        public float FogScale { get; set; } = 1;
        // Scales rain and wind volume; the remake mutes the synthetic weather noise on its main menu.
        public float AudioScale { get; set; } = 1;
        [Header("Scene references")]
        [SerializeField] private Volume volume;
        [SerializeField] private Light sun;
        [SerializeField] private ParticleSystem rain;
        [SerializeField] private AudioSource rainAudio;
        [Tooltip("Recorded rain loop. Empty falls back to the synthesised one.")]
        [SerializeField] private AudioClip rainClip;
        [Tooltip("Muffles the rain while the player is under a roof.")]
        [SerializeField] private AudioLowPassFilter rainMuffle;
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
        [Tooltip("Height above the player the drops are emitted from.")]
        [SerializeField] private float emitterHeight = 14f;
        [Tooltip("What stops a drop: roofs, ground, trees. Leave out players and loose props.")]
        [SerializeField] private LayerMask rainBlockers = ~0;
        [Tooltip("Size in metres of a cell of the roof height map that ends the drops.")]
        [SerializeField] private float roofCell = 0.5f;

        [Header("Sky")]
        [Tooltip("How much of a skybox shows through the fog (0 = fully fogged, the default for scenes without one).")]
        [SerializeField, Range(0f, 1f)] private float skyClear;

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

        private static readonly int SkyHorizonId = Shader.PropertyToID("_StormSkyHorizon");
        private static readonly int SkyClearId = Shader.PropertyToID("_PsxSkyClear");
        private static readonly int WetnessId = Shader.PropertyToID("_PsxWetness");

        private PsxFogVolume _fog;
        private float _target;
        private float _nextChangeTime;
        private float _shelter;
        // Height of the first surface seen from the sky in each cell, sampled once at start: a drop below it has hit
        // a roof or the ground. Exact where particle collision leaks, so it rains past the windows but never indoors.
        private float[] _roofs;
        private Vector2 _roofMin;
        private int _roofWidth, _roofDepth;
        private ParticleSystem.Particle[] _drops;

        /// <summary>0 = out in the open, 1 = under a roof; eased over about half a second.</summary>
        public float Shelter => _shelter;

        private void Awake()
        {
            if (volume != null && volume.profile != null)
                volume.profile.TryGet(out _fog);

            if (_fog == null)
                Debug.LogWarning("[WEATHER] No PsxFogVolume on the volume profile; fog will not react.");

            // Clips are synthesised rather than shipped as files; see ProceduralAudio.
            if (rainAudio != null)
            {
                rainAudio.clip = rainClip != null ? rainClip : ProceduralAudio.CreateRainLoop();
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

        private void Start()
        {
            if (rain != null)
                BuildRoofMap();
        }

        private void BuildRoofMap()
        {
            // The static map's extent, from its solid colliders. Loot and players spawn later and are masked out.
            bool any = false;
            var area = new Bounds();
            foreach (var c in FindObjectsByType<Collider>())
            {
                if (c.isTrigger || (rainBlockers.value & (1 << c.gameObject.layer)) == 0 || c.bounds.size.x > 400f || c.bounds.size.z > 400f)
                    continue;
                if (any) area.Encapsulate(c.bounds); else { area = c.bounds; any = true; }
            }
            if (!any)
                return;

            _roofMin = new Vector2(area.min.x, area.min.z);
            _roofWidth = Mathf.CeilToInt(area.size.x / roofCell) + 1;
            _roofDepth = Mathf.CeilToInt(area.size.z / roofCell) + 1;
            _roofs = new float[_roofWidth * _roofDepth];
            float top = area.max.y + 1f, depth = area.size.y + 2f;
            for (int z = 0; z < _roofDepth; z++)
            for (int x = 0; x < _roofWidth; x++)
            {
                var from = new Vector3(_roofMin.x + x * roofCell, top, _roofMin.y + z * roofCell);
                _roofs[z * _roofWidth + x] = Physics.Raycast(from, Vector3.down, out RaycastHit hit, depth, rainBlockers, QueryTriggerInteraction.Ignore)
                    ? hit.point.y : float.NegativeInfinity;
            }
            _drops = new ParticleSystem.Particle[rain.main.maxParticles];
        }

        private void LateUpdate()
        {
            if (rain == null || _roofs == null || rain.particleCount == 0)
                return;
            int count = rain.GetParticles(_drops);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = _drops[i].position;
                int x = Mathf.RoundToInt((p.x - _roofMin.x) / roofCell), z = Mathf.RoundToInt((p.z - _roofMin.y) / roofCell);
                if (x >= 0 && z >= 0 && x < _roofWidth && z < _roofDepth && p.y < _roofs[z * _roofWidth + x])
                    _drops[i].remainingLifetime = 0f;
            }
            rain.SetParticles(_drops, count);
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(WetnessId, 0f);
            Shader.SetGlobalFloat(SkyClearId, 0f);
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
            // Eased so walking through a door fades the rain instead of cutting it.
            _shelter = Mathf.MoveTowards(_shelter, outdoor > 0 ? 0f : 1f, Time.deltaTime * 1.5f);

            // The visual style (pause menu) scales the atmosphere: Penumbra thickens the fog
            // and all but removes the moon. Read every frame so a switch lands immediately.
            var style = PsxStylePresets.Current;

            if (_fog != null)
            {
                _fog.density.overrideState = true;
                _fog.density.value = Mathf.Lerp(clearFogDensity, stormFogDensity, t) * style.FogDensityScale * FogScale;
                _fog.fogColor.overrideState = true;
                _fog.fogColor.value = Color.Lerp(clearFogColor, stormFogColor, t) * style.FogTint;
                // The storm sky fades into exactly the colour the fog pass paints, so the horizon has no seam.
                Color fogged = _fog.fogColor.value * _fog.ambientColor.value;
                fogged.a = 1f;
                Shader.SetGlobalColor(SkyHorizonId, fogged);
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
            Shader.SetGlobalFloat(SkyClearId, skyClear);

            if (rain != null)
            {
                // The emitter is a scene object; it hovers over the player so the drops are always around them.
                if (player != null)
                    rain.transform.position = player.position + Vector3.up * emitterHeight;
                // The roof map ends drops at roofs, so it keeps raining outside while the player is indoors.
                // Without one (no colliders yet) fall back to stopping the rain under a roof.
                bool roofed = _roofs != null;
                var emission = rain.emission;
                emission.rateOverTime = maxEmissionRate * t * (roofed ? 1f : outdoor);

                bool shouldPlay = t > 0.01f && (roofed || !IsSheltered);
                if (shouldPlay && !rain.isPlaying) rain.Play();
                else if (!shouldPlay && rain.isPlaying) rain.Stop();
            }

            if (rainAudio != null)
                rainAudio.volume = maxRainVolume * t * Mathf.Lerp(1f, 0.45f, _shelter) * AudioScale;
            if (rainMuffle != null)
                rainMuffle.cutoffFrequency = Mathf.Lerp(22000f, 700f, Mathf.Sqrt(_shelter));

            if (windAudio != null)
                windAudio.volume = (0.12f + 0.35f * t) * AudioScale;
        }
    }
}
