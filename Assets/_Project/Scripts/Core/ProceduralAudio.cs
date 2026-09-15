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
    ///
    /// Three rules learned the hard way, after the first pass came out sounding like sheet
    /// metal:
    ///
    /// 1. **White noise is not a sound, it is static.** It carries as much energy at 15 kHz
    ///    as at 200 Hz, and nothing in the physical world does that. Every bed here starts
    ///    from pink or brown noise, whose falling spectrum is what rain, wind and cloth
    ///    actually have.
    /// 2. **An impact without a low end reads as metal.** What makes a footstep sound like a
    ///    body landing is a damped low-frequency thump under the noise. Filtered noise on its
    ///    own, however well enveloped, is a hiss.
    /// 3. **Sparse transients read as tapping.** Fifty droplets a second are heard as fifty
    ///    separate ticks; a thousand fuse into rainfall. If individual events are audible,
    ///    the answer is more of them and quieter, not fewer and softer.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// Steady rainfall: a pink hiss bed with a dense wash of tiny impacts on top.
        ///
        /// The impacts are deliberately far too many to count. That is the whole trick —
        /// real rain is thousands of overlapping drops per second, and any density low
        /// enough to pick individual ones out sounds like water dripping into a bucket.
        /// </summary>
        public static AudioClip CreateRainLoop(float seconds = 4f, int seed = 1)
        {
            int count = Mathf.RoundToInt(seconds * SampleRate);
            int fade = Mathf.RoundToInt(0.25f * SampleRate);
            var data = PinkNoise(count + fade, seed);

            // Rain has almost nothing below a few hundred hertz. Leaving it in muddies the
            // mix and steals the room the thunder rumble needs.
            Biquad.HighPass(450f, 0.7f).Process(data);
            Biquad.HighPass(250f, 0.7f).Process(data);
            // Roll the top off hard. Rain heard from under a roof or through a window has
            // almost nothing above 6 kHz, and every kilohertz left up there is what made the
            // first version sound like frying metal rather than water.
            Biquad.LowPass(6500f, 0.7f).Process(data);
            ScaleRms(data, 0.075f);

            AddImpacts(data, seed + 7, densityPerSecond: 900f,
                minGain: 0.04f, maxGain: 0.16f, decay: 650f, lowPassHz: 3500f);

            data = CloseLoop(data, count, fade);
            ScaleRms(data, 0.12f);
            return ToClip("RainLoop", data, loop: true);
        }

        /// <summary>Low wind bed. Brown noise, slow amplitude drift, nothing above a whisper.</summary>
        public static AudioClip CreateWindLoop(float seconds = 8f, int seed = 3)
        {
            int count = Mathf.RoundToInt(seconds * SampleRate);
            int fade = Mathf.RoundToInt(0.5f * SampleRate);
            var data = BrownNoise(count + fade, seed);

            Biquad.LowPass(320f, 0.7f).Process(data);
            Biquad.HighPass(60f, 0.7f).Process(data);
            ScaleRms(data, 0.16f);

            // Slow swell so it breathes instead of sitting flat. Three cycles across the
            // loop, which at eight seconds is slower than a breath and reads as weather.
            int total = data.Length;
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)count;
                data[i] *= 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 2f * 3f);
            }

            data = CloseLoop(data, count, fade);
            return ToClip("WindLoop", data, loop: true);
        }

        /// <summary>
        /// One footstep. <paramref name="soft"/> gives the duller, shorter press of grass
        /// or dirt; hard is the brighter slap of concrete.
        ///
        /// Three layers, because a real footfall is three events inside 100 ms: the heel
        /// hitting (a low thump), the sole loading (a mid-range body) and the grit under it
        /// (a thin scuff). Drop the thump and it stops being a person walking.
        /// </summary>
        public static AudioClip CreateFootstep(int seed, bool soft)
        {
            float seconds = soft ? 0.16f : 0.20f;
            int count = Mathf.RoundToInt(seconds * SampleRate);
            var data = new float[count];
            var random = new System.Random(seed);

            // 1. Heel thump. The frequency wanders per step so a bank of four does not
            // sound like the same foot four times.
            // Balance measured, not guessed: these three gains put the spectral centroid of
            // a concrete step between 550 and 1100 Hz across the bank, which is where a real
            // one sits. The first attempt was at 1173 Hz — all hiss, no body, hence the tin
            // sound. Overcorrecting to a pure thump lands at 187 Hz and turns into a kick drum.
            float thumpHz = soft ? Range(random, 72f, 96f) : Range(random, 96f, 132f);
            float thumpGain = soft ? 0.26f : 0.32f;
            AddDampedSine(data, thumpHz, decay: soft ? 58f : 44f, gain: thumpGain);
            AddDampedSine(data, thumpHz * 1.9f, decay: 90f, gain: thumpGain * 0.24f);

            // 2. Body: pink noise through a broad band, which is the shoe and the floor
            // resonating together.
            var body = PinkNoise(count, seed + 11);
            Biquad.BandPass(soft ? Range(random, 260f, 380f) : Range(random, 520f, 900f), 0.8f).Process(body);
            // Band-passed pink noise loses a lot of level, so the gain here looks large next
            // to the thump's and is not.
            Envelope(body, attackSeconds: 0.0015f, decay: soft ? 70f : 52f, gain: soft ? 1.10f : 1.55f);
            Mix(data, body);

            // 3. Scuff: the only genuinely bright layer, and kept small on purpose. Push it
            // and the step turns back into the tin sound this replaced.
            var scuff = Noise(count, seed + 23);
            Biquad.HighPass(soft ? 2200f : 3200f, 0.7f).Process(scuff);
            if (soft)
                Biquad.LowPass(5200f, 0.7f).Process(scuff);
            Envelope(scuff, attackSeconds: 0.0004f, decay: soft ? 150f : 110f, gain: soft ? 0.30f : 0.45f);
            Mix(data, scuff);

            // Per-step level variation instead of normalising every clip to the same peak.
            // Identical peaks are what make a footstep bank sound like a drum machine.
            Gain(data, Range(random, 0.82f, 1f));
            Clip(data);
            return ToClip($"Footstep{seed}", data, loop: false);
        }

        /// <summary>
        /// Landing after a jump: the same anatomy as a footstep with the weight of a whole
        /// body behind it — lower, longer, and with a short tail as the knees absorb it.
        /// </summary>
        public static AudioClip CreateLanding(int seed = 301)
        {
            const float seconds = 0.38f;
            int count = Mathf.RoundToInt(seconds * SampleRate);
            var data = new float[count];
            var random = new System.Random(seed);

            float thumpHz = Range(random, 58f, 74f);
            AddDampedSine(data, thumpHz, decay: 17f, gain: 0.55f);
            AddDampedSine(data, thumpHz * 2.4f, decay: 42f, gain: 0.13f);

            var body = PinkNoise(count, seed + 11);
            Biquad.BandPass(380f, 0.7f).Process(body);
            Envelope(body, attackSeconds: 0.002f, decay: 26f, gain: 1.40f);
            Mix(data, body);

            var scuff = Noise(count, seed + 23);
            Biquad.HighPass(2600f, 0.7f).Process(scuff);
            Envelope(scuff, attackSeconds: 0.0005f, decay: 90f, gain: 0.40f);
            Mix(data, scuff);

            Clip(data);
            return ToClip("Landing", data, loop: false);
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
            var rumble = BrownNoise(count, seed);

            Biquad.LowPass(120f, 0.7f).Process(rumble);
            Biquad.LowPass(180f, 0.7f).Process(rumble);
            ScaleRms(rumble, 0.3f);

            var crack = PinkNoise(count, seed + 91);
            Biquad.BandPass(1400f, 0.5f).Process(crack);
            Biquad.HighPass(300f, 0.7f).Process(crack);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;

                // Rumble swells rather than starting at full tilt, then dies away slowly.
                float swell = Mathf.Clamp01(t / 0.35f);
                float rumbleEnvelope = swell * Mathf.Exp(-0.8f * t);

                // The initial crack is short and only present at the very start.
                float crackEnvelope = Mathf.Exp(-9f * t) * Mathf.Clamp01(t / 0.01f);

                rumble[i] = rumble[i] * rumbleEnvelope * 3.2f + crack[i] * crackEnvelope * 0.5f;
            }

            Normalise(rumble, 0.92f);
            return ToClip($"Thunder{seed}", rumble, loop: false);
        }

        /// <summary>Flashlight switch: a tiny double transient, like a real toggle.</summary>
        public static AudioClip CreateClick(int seed = 11)
        {
            int count = Mathf.RoundToInt(0.05f * SampleRate);
            var data = Noise(count, seed);

            Biquad.BandPass(2600f, 1.1f).Process(data);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                // Second, quieter tick a few milliseconds later: the spring returning.
                float envelope = Mathf.Exp(-380f * t) + 0.45f * Mathf.Exp(-320f * Mathf.Abs(t - 0.012f));
                data[i] *= envelope;
            }

            Normalise(data, 0.6f);
            return ToClip("Click", data, loop: false);
        }

        // ---- noise sources ---------------------------------------------------

        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);

        /// <summary>Flat spectrum. Only useful as raw material for a filter.</summary>
        private static float[] Noise(int count, int seed)
        {
            var random = new System.Random(seed);
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return data;
        }

        /// <summary>
        /// Pink noise, -3 dB per octave. Paul Kellet's economy filter: six one-pole sections
        /// whose sum tracks 1/f closely enough for sound effects and costs almost nothing.
        ///
        /// This is the default bed for anything natural. Rain, cloth and grit all have
        /// falling spectra; white noise has none, which is why it reads as static.
        /// </summary>
        private static float[] PinkNoise(int count, int seed)
        {
            var random = new System.Random(seed);
            var data = new float[count];
            float b0 = 0f, b1 = 0f, b2 = 0f, b3 = 0f, b4 = 0f, b5 = 0f, b6 = 0f;

            for (int i = 0; i < count; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                b0 = 0.99886f * b0 + white * 0.0555179f;
                b1 = 0.99332f * b1 + white * 0.0750759f;
                b2 = 0.96900f * b2 + white * 0.1538520f;
                b3 = 0.86650f * b3 + white * 0.3104856f;
                b4 = 0.55000f * b4 + white * 0.5329522f;
                b5 = -0.7616f * b5 - white * 0.0168980f;
                data[i] = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f) * 0.11f;
                b6 = white * 0.115926f;
            }

            return data;
        }

        /// <summary>Brown noise, -6 dB per octave: leaky integration of white. Wind and rumble.</summary>
        private static float[] BrownNoise(int count, int seed)
        {
            var random = new System.Random(seed);
            var data = new float[count];
            float last = 0f;

            for (int i = 0; i < count; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                // The leak keeps the integrator from wandering off into DC.
                last = (last + 0.02f * white) * 0.998f;
                data[i] = last * 12f;
            }

            return data;
        }

        // ---- filtering -------------------------------------------------------

        /// <summary>
        /// A second-order RBJ biquad. One-pole filters were not enough: their 6 dB/octave
        /// slope leaves most of the top end in place, which is how "low-passed" noise still
        /// managed to sound bright and thin.
        /// </summary>
        private readonly struct Biquad
        {
            private readonly float _b0, _b1, _b2, _a1, _a2;

            private Biquad(float b0, float b1, float b2, float a0, float a1, float a2)
            {
                _b0 = b0 / a0;
                _b1 = b1 / a0;
                _b2 = b2 / a0;
                _a1 = a1 / a0;
                _a2 = a2 / a0;
            }

            public static Biquad LowPass(float hz, float q)
            {
                Coefficients(hz, q, out float w0, out float cos, out float alpha);
                float b1 = 1f - cos;
                return new Biquad(b1 * 0.5f, b1, b1 * 0.5f, 1f + alpha, -2f * cos, 1f - alpha);
            }

            public static Biquad HighPass(float hz, float q)
            {
                Coefficients(hz, q, out float w0, out float cos, out float alpha);
                float b0 = (1f + cos) * 0.5f;
                return new Biquad(b0, -(1f + cos), b0, 1f + alpha, -2f * cos, 1f - alpha);
            }

            /// <summary>Constant 0 dB peak gain, so Q widens the band without changing level.</summary>
            public static Biquad BandPass(float hz, float q)
            {
                Coefficients(hz, q, out float w0, out float cos, out float alpha);
                return new Biquad(alpha, 0f, -alpha, 1f + alpha, -2f * cos, 1f - alpha);
            }

            private static void Coefficients(float hz, float q, out float w0, out float cos, out float alpha)
            {
                // Never let a cutoff reach Nyquist: the coefficients blow up there.
                hz = Mathf.Clamp(hz, 10f, SampleRate * 0.45f);
                w0 = 2f * Mathf.PI * hz / SampleRate;
                cos = Mathf.Cos(w0);
                alpha = Mathf.Sin(w0) / (2f * Mathf.Max(q, 0.05f));
            }

            public void Process(float[] data)
            {
                float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    float x0 = data[i];
                    float y0 = _b0 * x0 + _b1 * x1 + _b2 * x2 - _a1 * y1 - _a2 * y2;
                    x2 = x1; x1 = x0;
                    y2 = y1; y1 = y0;
                    data[i] = y0;
                }
            }
        }

        // ---- shaping ---------------------------------------------------------

        /// <summary>A damped sine: one resonant mode of something being struck.</summary>
        private static void AddDampedSine(float[] data, float hz, float decay, float gain)
        {
            float step = 2f * Mathf.PI * hz / SampleRate;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                data[i] += Mathf.Sin(step * i) * Mathf.Exp(-decay * t) * gain;
            }
        }

        /// <summary>Percussive envelope: near-instant attack, exponential decay.</summary>
        private static void Envelope(float[] data, float attackSeconds, float decay, float gain)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float attack = attackSeconds <= 0f ? 1f : Mathf.Clamp01(t / attackSeconds);
                data[i] *= attack * Mathf.Exp(-decay * t) * gain;
            }
        }

        private static void Mix(float[] into, float[] source)
        {
            int n = Mathf.Min(into.Length, source.Length);
            for (int i = 0; i < n; i++)
                into[i] += source[i];
        }

        private static void Gain(float[] data, float gain)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }

        /// <summary>
        /// A dense wash of short impacts. Density is the parameter that matters: below a few
        /// hundred a second they are heard as individual taps.
        /// </summary>
        private static void AddImpacts(float[] data, int seed, float densityPerSecond,
            float minGain, float maxGain, float decay, float lowPassHz)
        {
            var random = new System.Random(seed);
            int events = Mathf.RoundToInt(data.Length / (float)SampleRate * densityPerSecond);
            int tail = Mathf.RoundToInt(0.012f * SampleRate);
            var scratch = new float[tail];

            for (int e = 0; e < events; e++)
            {
                int start = random.Next(0, Mathf.Max(1, data.Length - tail));
                float gain = Range(random, minGain, maxGain);

                for (int i = 0; i < tail; i++)
                {
                    float t = i / (float)SampleRate;
                    scratch[i] = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-decay * t);
                }

                // Each drop is a small resonant splash, not a click. Filtering per event is
                // what stops the wash sounding like static with a tremolo on it.
                Biquad.LowPass(Range(random, lowPassHz * 0.5f, lowPassHz), 0.9f).Process(scratch);

                for (int i = 0; i < tail; i++)
                    data[start + i] += scratch[i] * gain;
            }
        }

        /// <summary>
        /// Crossfades the overtail into the head and returns exactly <paramref name="count"/>
        /// samples.
        ///
        /// The generator is asked for count + fade samples for this reason: blending inside a
        /// buffer that then plays to its end leaves the seam exactly where it was. The extra
        /// tail has to be consumed and discarded.
        /// </summary>
        private static float[] CloseLoop(float[] data, int count, int fade)
        {
            fade = Mathf.Min(fade, count / 2);
            var result = new float[count];
            System.Array.Copy(data, result, count);

            for (int i = 0; i < fade; i++)
            {
                float blend = i / (float)fade;
                result[i] = result[i] * blend + data[count + i] * (1f - blend);
            }

            return result;
        }

        /// <summary>
        /// Scales to a target RMS rather than a target peak.
        ///
        /// Peak normalising a noise bed is meaningless: the peak is one stray sample, so two
        /// clips normalised the same way can differ by 10 dB in how loud they actually sound.
        /// RMS tracks loudness.
        /// </summary>
        private static void ScaleRms(float[] data, float target)
        {
            double sum = 0.0;
            for (int i = 0; i < data.Length; i++)
                sum += data[i] * (double)data[i];

            float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, data.Length)));
            if (rms <= 0.0001f)
                return;

            Gain(data, target / rms);
        }

        private static void Normalise(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(data[i]));

            if (max <= 0.0001f)
                return;

            Gain(data, peak / max);
        }

        /// <summary>Hard limit, so summed layers cannot wrap around into digital distortion.</summary>
        private static void Clip(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = Mathf.Clamp(data[i], -1f, 1f);
        }

        private static AudioClip ToClip(string name, float[] data, bool loop)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, stream: false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
