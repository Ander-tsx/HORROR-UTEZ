using System.Collections.Generic;
using UnityEngine;

namespace HorrorUtez.Remake
{
    // Music, ambience and one-shots. Clips are synthesized offline by Tools/audio/gen_remake_audio.py
    // into Resources/Audio. Music crossfades between exploration and chase from the danger around
    // the local student; a heartbeat rises as enemies close in; random distant scares keep the
    // campus restless. Every peer runs this locally from replicated state.
    public sealed class RemakeAudio : MonoBehaviour
    {
        private RemakeGame game;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private AudioSource explore, chase, wind, heart, breath, sting;
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private float danger, nextScare, nextBeat;
        private int lastPhase = -1;

        public void Setup(RemakeGame owner)
        {
            game = owner;
            explore = Source2D("music_explore", true, 0);
            chase = Source2D("music_chase", true, 0);
            wind = Source2D("amb_wind", true, .18f);
            heart = Source2D(null, false, .8f);
            breath = Source2D("breath_tired", true, 0);
            sting = Source2D(null, false, .9f);
            explore.Play(); chase.Play(); wind.Play(); breath.Play();
            nextScare = Time.time + Random.Range(30f, 50f);
        }

        public AudioClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!clips.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip == null) clip = RemakeSound.Tone(name, 180, .3f, .3f);
                clips[name] = clip;
            }
            return clip;
        }

        private AudioSource Source2D(string clip, bool loop, float volume)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = Clip(clip); source.loop = loop; source.volume = volume; source.spatialBlend = 0; source.playOnAwake = false;
            return source;
        }

        public void Play2D(string name, float volume = 1) { AudioClip clip = Clip(name); if (clip != null) sting.PlayOneShot(clip, volume); }

        public void Play(string name, Vector3 position, float volume, float maxDistance = 30)
        {
            AudioClip clip = Clip(name);
            if (clip == null) return;
            AudioSource source = pool.Find(s => s != null && !s.isPlaying);
            if (source == null)
            {
                if (pool.Count >= 24) { source = pool[0]; pool.RemoveAt(0); pool.Add(source); }
                else
                {
                    var go = new GameObject("Sonido"); go.transform.SetParent(transform, false);
                    source = go.AddComponent<AudioSource>(); source.spatialBlend = 1; source.rolloffMode = AudioRolloffMode.Linear;
                    source.dopplerLevel = 0; pool.Add(source);
                }
            }
            source.transform.position = position; source.maxDistance = maxDistance; source.minDistance = 1.5f;
            source.pitch = Random.Range(.93f, 1.07f); source.clip = clip; source.volume = volume; source.Play();
        }

        public AudioSource Loop(string name, Vector3 position, float volume, float maxDistance)
        {
            var go = new GameObject("Bucle " + name); go.transform.SetParent(transform, false); go.transform.position = position;
            var source = go.AddComponent<AudioSource>(); source.clip = Clip(name); source.loop = true; source.volume = volume;
            source.spatialBlend = 1; source.rolloffMode = AudioRolloffMode.Linear; source.maxDistance = maxDistance; source.minDistance = 1;
            source.time = Random.Range(0, Mathf.Max(.01f, source.clip.length - .01f)); source.Play();
            return source;
        }

        public void Footfall(EnemyKind kind, Vector3 point, float loudness)
        {
            if (kind == EnemyKind.Giant)
            {
                Play("giant_step", point, .55f + loudness * .45f, 45);
                if (game.Player != null && game.Player.View != null)
                {
                    float d = Vector3.Distance(game.Player.transform.position, point);
                    if (d < 22) game.Player.Shake(Mathf.Lerp(1.6f, .1f, d / 22), .25f);
                }
            }
            else Play("step_caretaker", point, .35f + loudness * .3f, 16);
        }

        private void Update()
        {
            if (game == null) return;
            if (game.Phase != lastPhase)
            {
                if (game.Phase == 3) Play2D("music_extract", .8f);
                if (game.Phase == 4) Play2D("music_fail", .8f);
                lastPhase = game.Phase;
            }
            bool playing = game.Started && (game.Phase == 1 || game.Phase == 2) && game.Player != null;
            float threat = 0, nearest = 99;
            if (playing)
            {
                Vector3 me = game.Player.transform.position;
                foreach (RemakeEnemy enemy in game.Enemies)
                {
                    float d = Vector3.Distance(enemy.transform.position, me);
                    nearest = Mathf.Min(nearest, d);
                    bool hunting = (enemy.Flags & 1) != 0 || enemy.Chasing;
                    if (hunting && d < 32) threat = Mathf.Max(threat, 1);
                    else if (d < 14) threat = Mathf.Max(threat, .45f * (1 - d / 14));
                }
            }
            danger = Mathf.MoveTowards(danger, threat, Time.deltaTime * (threat > danger ? 1.2f : .25f));
            float master = playing ? 1 : (game.Started ? .35f : .55f);
            explore.volume = Mathf.Lerp(explore.volume, master * .55f * (1 - danger), Time.deltaTime * 2);
            chase.volume = Mathf.Lerp(chase.volume, master * .7f * danger, Time.deltaTime * 3);
            wind.volume = playing ? .16f : .08f;
            float stamina = game.Player != null ? game.Player.Stamina : 100;
            breath.volume = Mathf.Lerp(breath.volume, playing && stamina < 30 ? (30 - stamina) / 30 * .6f : 0, Time.deltaTime * 3);
            if (playing && nearest < 16 && Time.time > nextBeat)
            {
                heart.PlayOneShot(Clip("heartbeat"), Mathf.Lerp(.9f, .2f, nearest / 16));
                nextBeat = Time.time + Mathf.Lerp(.42f, 1.1f, nearest / 16);
            }
            if (playing && Time.time > nextScare)
            {
                nextScare = Time.time + Random.Range(35f, 80f);
                string[] scares = { "whisper", "distant_scream", "metal_bang", "door_creak", "static_burst" };
                Vector3 dir = Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward;
                Play(scares[Random.Range(0, scares.Length)], game.Player.transform.position + dir * Random.Range(9f, 22f) + Vector3.up, .7f, 40);
            }
        }
    }
}
