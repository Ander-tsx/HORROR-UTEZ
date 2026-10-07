using UnityEngine;

namespace HorrorUtez.Remake
{
    // Failing fluorescent: flickers, cuts out at random and browns out when El Rector is close.
    [RequireComponent(typeof(Light))]
    public sealed class RemakeFlickerLight : MonoBehaviour
    {
        public float BaseIntensity = 2.4f;
        public float Seed;
        [Tooltip("Plays the fluorescent hum loop at this light.")]
        public bool Hum = true;

        private Light lamp;
        private RemakeGame game;

        private void Start()
        {
            lamp = GetComponent<Light>();
            game = FindAnyObjectByType<RemakeGame>();
            if (Hum && game != null) game.Audio?.Loop("amb_fluoro", transform.position, .25f, 7);
        }

        private void Update()
        {
            if (game == null) return;
            RemakeEnemy giant = null;
            foreach (RemakeEnemy e in game.Enemies) if (e.Kind == EnemyKind.Giant) giant = e;
            float n = Mathf.PerlinNoise(Time.time * 7 + Seed, Seed);
            bool cut = Mathf.PerlinNoise(Time.time * .7f + Seed * 3, 1) > .72f && n > .45f;
            // The Rector's presence browns out the tubes around it.
            float near = giant != null ? Mathf.Clamp01(1 - Vector3.Distance(giant.transform.position, transform.position) / 12) : 0;
            if (near > 0 && Mathf.PerlinNoise(Time.time * 18 + Seed, 7) < near) cut = true;
            if (near > .3f && Random.value < .004f) game.Audio?.Play("spark", transform.position, .6f, 14);
            lamp.intensity = cut ? BaseIntensity * .03f : BaseIntensity * (.75f + n * .35f) * (1 - near * .5f);
        }
    }
}
