using HorrorUtez.Core;
using UnityEngine;

namespace HorrorUtez.World
{
    /// <summary>
    /// Lightning strikes during heavy rain, with thunder arriving late.
    ///
    /// Two details do most of the work here. The flash is a burst of two or three uneven
    /// pulses, not a single fade — a real strike flickers, and a smooth ramp reads as
    /// someone turning on a lamp. And the thunder is delayed by a distance, then attenuated
    /// by that same distance, so a near strike cracks almost instantly and a far one
    /// mutters seconds later. Simultaneous flash and bang would collapse the sense of space.
    ///
    /// Frequency and probability are tied to <see cref="WeatherSystem.RainIntensity"/>, so
    /// storms build rather than switching on.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class LightningSystem : MonoBehaviour
    {
        [SerializeField] private WeatherSystem weather;
        [Tooltip("Directional light used only for the flash. Kept dark between strikes.")]
        [SerializeField] private Light flash;
        [SerializeField] private AudioSource thunder;

        [Header("Trigger")]
        [Tooltip("Rain intensity below which no lightning happens at all.")]
        [SerializeField] private float minimumRain = 0.35f;
        [SerializeField] private float minIntervalSeconds = 6f;
        [SerializeField] private float maxIntervalSeconds = 40f;

        [Header("Flash")]
        [SerializeField] private float flashIntensity = 4.5f;
        [SerializeField] private Color flashColor = new(0.85f, 0.90f, 1f);

        [Header("Thunder")]
        [Tooltip("Seconds of delay per unit of 'distance'. Sound is slow; light is not.")]
        [SerializeField] private Vector2 delayRange = new(0.4f, 6f);

        private AudioClip[] _thunderClips;
        private float _nextStrikeTime;
        private float _flashLevel;

        private void Awake()
        {
            if (thunder == null)
                thunder = GetComponent<AudioSource>();

            _thunderClips = new[]
            {
                ProceduralAudio.CreateThunder(401),
                ProceduralAudio.CreateThunder(402, 5.5f),
                ProceduralAudio.CreateThunder(403, 3.5f),
            };

            if (flash != null)
            {
                flash.type = LightType.Directional;
                flash.color = flashColor;
                flash.intensity = 0f;
                flash.shadows = LightShadows.None;
            }

            ScheduleNext();
        }

        private void Update()
        {
            DecayFlash();

            if (weather == null || weather.RainIntensity < minimumRain)
                return;

            if (Time.time < _nextStrikeTime)
                return;

            Strike();
            ScheduleNext();
        }

        private void ScheduleNext()
        {
            // Heavier rain means strikes crowd together.
            float storm = weather != null ? Mathf.InverseLerp(minimumRain, 1f, weather.RainIntensity) : 0f;
            float interval = Mathf.Lerp(maxIntervalSeconds, minIntervalSeconds, storm);
            _nextStrikeTime = Time.time + Random.Range(interval * 0.5f, interval * 1.5f);
        }

        private void Strike()
        {
            // Strike from a random bearing so the storm is not always on one side.
            if (flash != null)
                flash.transform.rotation = Quaternion.Euler(Random.Range(20f, 60f), Random.Range(0f, 360f), 0f);

            StopAllCoroutines();
            StartCoroutine(FlashBurst());

            float distance = Random.value;
            float delay = Mathf.Lerp(delayRange.x, delayRange.y, distance);
            // Far strikes are quieter as well as later.
            float volume = Mathf.Lerp(0.9f, 0.25f, distance);
            StartCoroutine(PlayThunder(delay, volume));
        }

        /// <summary>Two or three uneven pulses. A single smooth fade reads as a lamp.</summary>
        private System.Collections.IEnumerator FlashBurst()
        {
            int pulses = Random.Range(2, 4);
            for (int i = 0; i < pulses; i++)
            {
                _flashLevel = flashIntensity * Random.Range(0.55f, 1f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.09f));
                _flashLevel = 0f;
                yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
            }

            _flashLevel = flashIntensity;
        }

        private System.Collections.IEnumerator PlayThunder(float delay, float volume)
        {
            yield return new WaitForSeconds(delay);

            if (thunder != null && _thunderClips.Length > 0)
            {
                thunder.pitch = Random.Range(0.85f, 1.1f);
                thunder.PlayOneShot(_thunderClips[Random.Range(0, _thunderClips.Length)], volume);
            }
        }

        private void DecayFlash()
        {
            if (flash == null)
                return;

            // Fast falloff so the afterglow never lingers into normal lighting.
            _flashLevel = Mathf.MoveTowards(_flashLevel, 0f, flashIntensity * 6f * Time.deltaTime);
            flash.intensity = _flashLevel;
            flash.enabled = _flashLevel > 0.01f;
        }
    }
}
