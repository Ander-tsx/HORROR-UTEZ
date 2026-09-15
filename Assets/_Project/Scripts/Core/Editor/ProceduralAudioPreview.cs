using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HorrorUtez.Core.Editor
{
    /// <summary>
    /// Writes every procedurally generated clip to Logs/audio as a 16-bit WAV.
    ///
    /// Audio cannot be tuned by reading the code that makes it. Every parameter in
    /// <see cref="ProceduralAudio"/> was guessed until these files existed and someone
    /// listened to them — which is how the first footsteps shipped sounding like sheet
    /// metal despite the synthesis looking perfectly reasonable on the page.
    ///
    /// Logs/ is gitignored, so nothing here ends up in the repository.
    /// </summary>
    public static class ProceduralAudioPreview
    {
        [MenuItem("HORROR-UTEZ/Export Audio Previews")]
        public static void Export()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "audio");
            Directory.CreateDirectory(dir);

            Write(dir, ProceduralAudio.CreateRainLoop());
            Write(dir, ProceduralAudio.CreateWindLoop());
            Write(dir, ProceduralAudio.CreateThunder(41));
            Write(dir, ProceduralAudio.CreateClick());
            Write(dir, ProceduralAudio.CreateLanding());

            // The banks, back to back in one file: a footstep is judged by how a run of them
            // sounds, not by one hit. Four identical-sounding steps are the actual failure
            // mode here, and a single exported clip cannot show it.
            Write(dir, "Footsteps_Hard", Sequence(0.42f, false, 101, 102, 103, 104));
            Write(dir, "Footsteps_Soft", Sequence(0.46f, true, 201, 202, 203));

            Debug.Log($"[AUDIO] Previews written to {dir}");
        }

        public static void ExportBatch()
        {
            try
            {
                Export();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AUDIO] Preview export failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Lays several one-shots out in time so a cadence can be heard.</summary>
        private static float[] Sequence(float spacing, bool soft, params int[] seeds)
        {
            int step = Mathf.RoundToInt(spacing * ProceduralAudio.SampleRate);
            // Twice through the bank, so repetition has a chance to become obvious.
            int repeats = 2;
            var output = new float[step * seeds.Length * repeats + ProceduralAudio.SampleRate];

            int cursor = 0;
            for (int r = 0; r < repeats; r++)
            {
                foreach (int seed in seeds)
                {
                    var clip = ProceduralAudio.CreateFootstep(seed, soft);
                    var samples = new float[clip.samples];
                    clip.GetData(samples, 0);

                    for (int i = 0; i < samples.Length && cursor + i < output.Length; i++)
                        output[cursor + i] += samples[i];

                    cursor += step;
                }
            }

            return output;
        }

        private static void Write(string dir, AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            Write(dir, clip.name, samples);
        }

        private static void Write(string dir, string name, float[] samples)
        {
            string path = Path.Combine(dir, name + ".wav");
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            int sampleRate = ProceduralAudio.SampleRate;
            int dataBytes = samples.Length * 2;

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);              // PCM header size
            writer.Write((short)1);        // PCM
            writer.Write((short)1);        // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);  // byte rate
            writer.Write((short)2);        // block align
            writer.Write((short)16);       // bits per sample
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);

            foreach (float sample in samples)
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
        }
    }
}
