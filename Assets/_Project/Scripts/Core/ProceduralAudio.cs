using UnityEngine;

namespace HorrorUtez.Core
{
    /// <summary>
    /// Builds the game's sound effects from filtered noise at runtime.
    ///
    /// Nothing here ships as an audio file. Rain, footsteps and switch clicks are all
    /// broadband noise shaped by an envelope and a filter, which is exactly what they are
    /// acoustically — so synthesising them costs a few milliseconds at load, keeps the
    /// repository free of binaries, and lets every parameter be tuned in code.
    ///
    /// Melodic or voiced audio would NOT work this way; when the game needs those, they
    /// come in as real assets.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// Steady rainfall: broadband hiss with sparse louder droplets on top.
        /// The loop is crossfaded at the seam, otherwise the restart clicks every cycle.
        /// </summary>
        public static AudioClip CreateRainLoop(float seconds = 4f, int seed = 1)
        {
            int count = Mathf.RoundToInt(seconds * SampleRate);
            var data = Noise(count, seed);

            // Two passes: keep the hiss but drop the harshest top end, then remove rumble
            // so the mix leaves room for wind and footsteps underneath.
            LowPass(data, 0.35f);
            HighPass(data, 0.02f);
            Normalise(data, 0.5f);

            AddDroplets(data, seed + 7, densityPerSecond: 55f);
            CrossfadeSeam(data, Mathf.RoundToInt(0.25f * SampleRate));
            Normalise(data, 0.85f);

            return ToClip("RainLoop", data, loop: true);
        }

        /// <summary>Low wind bed. Very dark noise, slow amplitude drift.</summary>
        public static AudioClip CreateWindLoop(float seconds = 8f, int seed = 3)
        {
            int count = Mathf.RoundToInt(seconds * SampleRate);
            var data = Noise(count, seed);

            LowPass(data, 0.006f);
            LowPass(data, 0.006f);
            Normalise(data, 0.7f);

            // Slow swell so it breathes instead of sitting flat.
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                data[i] *= 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 2f * 3f);
            }

            CrossfadeSeam(data, Mathf.RoundToInt(0.5f * SampleRate));
            return ToClip("WindLoop", data, loop: true);
        }

        /// <summary>
        /// One footstep. <paramref name="soft"/> gives the duller, shorter thud of grass
        /// or dirt; hard is the brighter slap of concrete.
        /// </summary>
        public static AudioClip CreateFootstep(int seed, bool soft)
        {
            float seconds = soft ? 0.14f : 0.18f;
            int count = Mathf.RoundToInt(seconds * SampleRate);
            var data = Noise(count, seed);

            LowPass(data, soft ? 0.06f : 0.20f);
            HighPass(data, 0.01f);

            // Sharp attack, fast decay: the shape of an impact rather than a tone.
            float decay = soft ? 26f : 18f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Clamp01(t / 0.004f);
                data[i] *= attack * Mathf.Exp(-decay * t);
            }

            Normalise(data, 0.9f);
            return ToClip($"Footstep{seed}", data, loop: false);
        }

        /// <summary>
        /// Thunder: a bright crack folded into a long, dark rumble.
        ///
        /// The crack alone sounds like a snapped branch and the rumble alone sounds like
        /// traffic; it is the pairing, plus a decay measured in seconds rather than
        /// milliseconds, that reads as a storm.
        /// </summary>
        public static AudioClip CreateThunder(int seed, float seconds = 4.5f)
        {
            int count = Mathf.RoundToInt(seconds * SampleRate);
            var rumble = Noise(count, seed);

            LowPass(rumble, 0.008f);
            LowPass(rumble, 0.008f);
            Normalise(rumble, 1f);

            var crack = Noise(count, seed + 91);
            LowPass(crack, 0.12f);
            HighPass(crack, 0.02f);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;

                // Rumble swells rather than starting at full tilt, then dies away slowly.
                float swell = Mathf.Clamp01(t / 0.35f);
                float rumbleEnvelope = swell * Mathf.Exp(-0.8f * t);

                // The initial crack is short and only present at the very start.
                float crackEnvelope = Mathf.Exp(-9f * t) * Mathf.Clamp01(t / 0.01f);

                rumble[i] = rumble[i] * rumbleEnvelope + crack[i] * crackEnvelope * 0.55f;
            }

            Normalise(rumble, 0.95f);
            return ToClip($"Thunder{seed}", rumble, loop: false);
        }

        /// <summary>Flashlight switch: a tiny double transient, like a real toggle.</summary>
        public static AudioClip CreateClick(int seed = 11)
        {
            int count = Mathf.RoundToInt(0.05f * SampleRate);
            var data = Noise(count, seed);

            HighPass(data, 0.25f);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                // Second, quieter tick a few milliseconds later: the spring returning.
                float envelope = Mathf.Exp(-380f * t) + 0.45f * Mathf.Exp(-320f * Mathf.Abs(t - 0.012f));
                data[i] *= envelope;
            }

            Normalise(data, 0.75f);
            return ToClip("Click", data, loop: false);
        }

        // ---- helpers ---------------------------------------------------------

        private static float[] Noise(int count, int seed)
        {
            var random = new System.Random(seed);
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return data;
        }

        /// <summary>One-pole low pass. Coefficient is roughly cutoff over Nyquist.</summary>
        private static void LowPass(float[] data, float coefficient)
        {
            float y = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                y += coefficient * (data[i] - y);
                data[i] = y;
            }
        }

        /// <summary>One-pole high pass, as the signal minus its low-passed self.</summary>
        private static void HighPass(float[] data, float coefficient)
        {
            float y = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                y += coefficient * (data[i] - y);
                data[i] -= y;
            }
        }

        private static void AddDroplets(float[] data, int seed, float densityPerSecond)
        {
            var random = new System.Random(seed);
            int drops = Mathf.RoundToInt(data.Length / (float)SampleRate * densityPerSecond);
            int tail = Mathf.RoundToInt(0.03f * SampleRate);

            for (int d = 0; d < drops; d++)
            {
                int start = random.Next(0, Mathf.Max(1, data.Length - tail));
                float gain = 0.3f + (float)random.NextDouble() * 0.5f;
                for (int i = 0; i < tail && start + i < data.Length; i++)
                {
                    float t = i / (float)SampleRate;
                    data[start + i] += gain * (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-160f * t);
                }
            }
        }

        /// <summary>Blends the tail into the head so a looping clip has no audible seam.</summary>
        private static void CrossfadeSeam(float[] data, int fade)
        {
            fade = Mathf.Min(fade, data.Length / 4);
            for (int i = 0; i < fade; i++)
            {
                float blend = i / (float)fade;
                int tail = data.Length - fade + i;
                data[i] = Mathf.Lerp(data[tail], data[i], blend);
            }
        }

        private static void Normalise(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(data[i]));

            if (max <= 0.0001f)
                return;

            float gain = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }

        private static AudioClip ToClip(string name, float[] data, bool loop)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, stream: false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
