using System.Collections.Generic;
using UnityEngine;

namespace HorrorUtez.Remake
{
    public enum EnemyKind { Caretaker, Giant }

    // Host-simulated enemies with procedural bodies. The caretaker patrols the campus; the giant
    // ("El Rector") stalks outside and inside CECADEC's ground floor, folding into a crawl to fit
    // under ceilings and through doors. Thrown gear stuns both (R.E.P.O.-style).
    public sealed class RemakeEnemy : MonoBehaviour
    {
        public EnemyKind Kind { get; private set; }
        public bool Chasing => prey != null;
        public bool Stunned => Time.time < stunnedUntil || (!game.Authority && (flags & 2) != 0);
        public int Flags => (Chasing ? 1 : 0) | (Time.time < stunnedUntil ? 2 : 0) | (Time.time < attackUntil ? 4 : 0);
        public float Crawl => crawl;
        private RemakeGame game;
        private CharacterController body;
        private RemakeBody visual;
        private Vector3 spawn, goal, targetPosition;
        private float noiseUntil, nextThink, nextHit, activeAt, stunnedUntil, attackUntil, pendingHitAt = -1, crawl, nextProbe, targetCrawl;
        private float lastNoise, nextGroan;
        private readonly List<Vector3> route = new List<Vector3>();
        private int routeIndex, flags;
        private StudentState prey, hitTarget;
        private AudioSource voice;

        private float StandHeight => Kind == EnemyKind.Giant ? 3.55f : 1.85f;
        private float CrawlHeight => Kind == EnemyKind.Giant ? 1.45f : 1.2f;
        private float Reach => Kind == EnemyKind.Giant ? 2.4f : 1.4f;

        public void Setup(RemakeGame owner, EnemyKind kind, Vector3 position, GameObject model)
        {
            game = owner; Kind = kind; spawn = goal = targetPosition = position;
            transform.position = position;
            body = gameObject.AddComponent<CharacterController>();
            body.radius = kind == EnemyKind.Giant ? .32f : .3f; body.height = StandHeight; body.center = Vector3.up * (StandHeight / 2);
            body.stepOffset = .35f; body.slopeLimit = 50;
            if (model != null)
            {
                Transform instance = Instantiate(model, transform).transform;
                instance.localPosition = Vector3.zero; instance.localRotation = Quaternion.identity;
                visual = gameObject.AddComponent<RemakeBody>();
                visual.Setup(instance, game.WorldMask);
                if (kind == EnemyKind.Giant) { visual.StrideScale = 1.7f; visual.StepHeight = .42f; visual.StepTime = .5f; visual.Hunch = 30; visual.Twitch = 1.2f; }
                else { visual.Hunch = 6; visual.Twitch = .35f; }
                visual.Planted = (point, loudness) => game.Audio?.Footfall(Kind, point, loudness);
            }
            voice = gameObject.AddComponent<AudioSource>(); voice.spatialBlend = 1; voice.rolloffMode = AudioRolloffMode.Linear;
            voice.maxDistance = kind == EnemyKind.Giant ? 32 : 18; voice.loop = true; voice.volume = kind == EnemyKind.Giant ? .55f : .25f;
            voice.clip = game.Audio != null ? game.Audio.Clip(kind == EnemyKind.Giant ? "giant_breath" : "caretaker_hum") : RemakeSound.Tone("hum", 65, 2, .12f);
            voice.Play();
            activeAt = Time.time + (kind == EnemyKind.Giant ? 70 : 35);
        }
        public void Hear(Vector3 point, float radius)
        {
            if (Kind == EnemyKind.Giant) radius *= 1.3f;
            if (Vector3.Distance(transform.position, point) > radius || Time.time - lastNoise < 0.25f || Stunned) return;
            goal = point; noiseUntil = Time.time + 8; nextThink = 0; lastNoise = Time.time;
        }
        public void Apply(Vector3 position, int state)
        {
            targetPosition = position; body.enabled = false;
            if ((state & 4) != 0 && (flags & 4) == 0) { visual?.Attack(); game.Audio?.Play(Kind == EnemyKind.Giant ? "giant_swipe" : "caretaker_swing", transform.position, .8f); }
            flags = state;
        }
        // Diagnostics (capture tour): place and wake an enemy immediately.
        public void DebugPlace(Vector3 position) { body.enabled = false; transform.position = position; targetPosition = position; body.enabled = game.Authority; route.Clear(); }
        public void Wake() { activeAt = 0; nextThink = 0; }
        public void Stun(float seconds)
        {
            if (!game.Authority || Time.time < stunnedUntil) return;
            stunnedUntil = Time.time + seconds; prey = null; pendingHitAt = -1; route.Clear();
            game.Audio?.Play(Kind == EnemyKind.Giant ? "giant_hurt" : "caretaker_hurt", transform.position, 1);
            game.Tell(Kind == EnemyKind.Giant ? "¡El Rector quedó aturdido!" : "¡Aturdiste al velador!");
        }
        public void ResetEnemy()
        {
            body.enabled = false; transform.position = spawn; body.enabled = game.Authority;
            goal = targetPosition = spawn; prey = null; route.Clear(); stunnedUntil = 0; pendingHitAt = -1;
            activeAt = Time.time + (Kind == EnemyKind.Giant ? 60 : 25);
        }
        private void Update()
        {
            if (!game.Started || game.Phase != 1) return;
            ProbeCeiling();
            if (visual != null)
            {
                float slump = Stunned ? (Kind == EnemyKind.Giant ? .75f : .6f) : 0;
                visual.Crawl = Mathf.Max(crawl, slump);
                Transform look = null; float nearest = 14;
                foreach (StudentState s in game.Students.Values)
                {
                    if (!s.alive) continue;
                    float d = Vector3.Distance(s.position, transform.position);
                    if (d < nearest) { nearest = d; look = game.Avatar(s.id); }
                }
                visual.HasLookTarget = look != null && !Stunned;
                if (look != null) visual.LookTarget = look.position + Vector3.up * 1.4f;
            }
            if (Kind == EnemyKind.Giant && Time.time > nextGroan && game.Audio != null)
            { nextGroan = Time.time + Random.Range(7f, 16f); game.Audio.Play("giant_groan", transform.position + Vector3.up * 2.5f, .9f); }
            if (!game.Authority)
            {
                Vector3 delta = targetPosition - transform.position;
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 12);
                if (delta.sqrMagnitude > 0.004f) transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(new Vector3(delta.x, 0, delta.z)), Time.deltaTime * 6);
                return;
            }
            if (Time.time < activeAt) return;
            if (pendingHitAt > 0 && Time.time >= pendingHitAt)
            {
                pendingHitAt = -1;
                if (hitTarget != null && hitTarget.alive && !game.InTruck(hitTarget.position) && Vector3.Distance(hitTarget.position, transform.position) < Reach * 1.25f)
                {
                    Vector3 away = hitTarget.position - transform.position; away.y = 0;
                    away = away.sqrMagnitude > .01f ? away.normalized : transform.forward;
                    if (Kind == EnemyKind.Giant) game.Hurt(hitTarget.id, 45, away * 9 + Vector3.up * 6);
                    else game.Hurt(hitTarget.id, 28, away * 6.5f + Vector3.up * 3.5f);
                }
            }
            if (Stunned) { body.Move(Vector3.down * 3 * Time.deltaTime); return; }
            if (Time.time > nextThink)
            {
                nextThink = Time.time + 0.6f;
                StudentState previous = prey;
                prey = null; float nearest = Kind == EnemyKind.Giant ? 30 : 22;
                Vector3 eye = transform.position + Vector3.up * Mathf.Lerp(StandHeight - .2f, CrawlHeight - .2f, crawl);
                foreach (StudentState student in game.Students.Values)
                {
                    if (!student.alive || game.InTruck(student.position)) continue;
                    float distance = Vector3.Distance(transform.position, student.position);
                    if (distance > nearest || Mathf.Abs(student.position.y - transform.position.y) > 2) continue;
                    Vector3 sight = student.position + Vector3.up - eye;
                    if (Physics.Raycast(eye, sight.normalized, sight.magnitude, game.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    prey = student; nearest = distance;
                }
                if (prey != null && previous == null && game.Audio != null)
                    game.Audio.Play(Kind == EnemyKind.Giant ? "giant_scream" : "caretaker_alert", transform.position + Vector3.up * 2, 1);
                if (prey != null) goal = prey.position;
                else if (Time.time > noiseUntil && (goal - transform.position).sqrMagnitude < 5)
                    goal = game.PatrolPoint(Kind == EnemyKind.Giant);
                game.FindPath(transform.position, goal, route); routeIndex = 0;
            }
            Vector3 destination = routeIndex < route.Count ? route[routeIndex] : goal;
            Vector3 heading = destination - transform.position; heading.y = 0;
            if (heading.magnitude < 0.5f && routeIndex < route.Count) routeIndex++;
            bool attacking = Time.time < attackUntil;
            if (heading.magnitude > 0.25f && !attacking)
            {
                float speed = prey != null
                    ? (Kind == EnemyKind.Giant ? 4.1f : 3.7f) + Mathf.Min(game.Day * 0.12f, 1.3f)
                    : (Kind == EnemyKind.Giant ? 1.45f : 1.65f);
                speed *= Mathf.Lerp(1, .72f, crawl);
                body.Move((heading.normalized * speed + Vector3.down * 3) * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), Time.deltaTime * (Kind == EnemyKind.Giant ? 4 : 7));
            }
            else body.Move(Vector3.down * 3 * Time.deltaTime);
            if (Time.time > nextHit)
                foreach (StudentState student in game.Students.Values)
                    if (student.alive && !game.InTruck(student.position) && Vector3.Distance(student.position, transform.position) < Reach)
                    {
                        hitTarget = student; pendingHitAt = Time.time + (Kind == EnemyKind.Giant ? .4f : .3f);
                        attackUntil = Time.time + .5f; nextHit = Time.time + (Kind == EnemyKind.Giant ? 1.8f : 1.2f);
                        visual?.Attack();
                        game.Audio?.Play(Kind == EnemyKind.Giant ? "giant_swipe" : "caretaker_swing", transform.position, .8f);
                        Vector3 face = student.position - transform.position; face.y = 0;
                        if (face.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(face);
                        break;
                    }
        }
        // Lower ceilings (and doorways just ahead) fold the giant into a crouch or a crawl.
        private void ProbeCeiling()
        {
            if (Time.time > nextProbe)
            {
                nextProbe = Time.time + .12f;
                float clearance = Clearance(transform.position);
                Vector3 ahead = transform.position + transform.forward * (Kind == EnemyKind.Giant ? 1.4f : .8f);
                clearance = Mathf.Min(clearance, Clearance(ahead));
                targetCrawl = Mathf.Clamp01((StandHeight + .15f - clearance) / (StandHeight - CrawlHeight));
            }
            crawl = Mathf.MoveTowards(crawl, targetCrawl, Time.deltaTime * (targetCrawl > crawl ? 2.2f : 1.1f));
            if (body.enabled)
            {
                float h = Mathf.Lerp(StandHeight, CrawlHeight, crawl);
                if (Mathf.Abs(body.height - h) > .02f) { body.height = h; body.center = Vector3.up * (h / 2); }
            }
        }
        private float Clearance(Vector3 p)
        {
            if (Physics.SphereCast(p + Vector3.up * .5f, .3f, Vector3.up, out RaycastHit hit, StandHeight, game.WorldMask, QueryTriggerInteraction.Ignore))
                return hit.distance + .8f;
            return StandHeight + 1;
        }
    }

    public static class RemakeSound
    {
        public static AudioClip Tone(string name, float frequency, float seconds, float volume)
        {
            const int rate = 16000;
            float[] data = new float[Mathf.CeilToInt(rate * seconds)];
            var random = new System.Random(42);
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = name.Contains("hum") ? 1 : Mathf.Exp(-t * 12 / Mathf.Max(seconds, 0.1f));
                data[i] = (Mathf.Sin(t * frequency * Mathf.PI * 2) * 0.7f + ((float)random.NextDouble()*2-1)*0.3f) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
    }
}
