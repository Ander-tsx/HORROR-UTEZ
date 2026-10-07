using System.Collections.Generic;
using UnityEngine;

namespace HorrorUtez.Remake
{
    public enum EnemyKind { Caretaker, Giant, Ulises, Cristian, Derick }

    // Host-simulated enemies with procedural bodies. The caretaker patrols the campus; the giant
    // ("El Rector") walks a fixed route through the whole university, in and out of CECADEC's ground
    // floor, folding into a crawl under ceilings. Sight uses occlusion, a field of view and exposure
    // time; hearing investigates noises, chase remembers only the last visible position. Both recover
    // from getting stuck. Thrown gear stuns both (R.E.P.O.-style).
    public sealed class RemakeEnemy : MonoBehaviour
    {
        private enum Mode { Patrol, Investigate, Chase }
        public EnemyKind Kind { get; private set; }
        public string DisplayName => RemakeBestiary.Names[(int)Kind];
        public bool Recovering => Kind == EnemyKind.Derick && Chasing && (Time.time - burstStarted) % 5f >= 2f;
        public bool Chasing => mode == Mode.Chase && prey != null;
        public bool Stunned => Time.time < stunnedUntil || (!game.Authority && (flags & 2) != 0);
        public int Flags => (Chasing ? 1 : 0) | (Time.time < stunnedUntil ? 2 : 0) | (Time.time < attackUntil ? 4 : 0);
        public float Crawl => crawl;
        public string State => mode + (prey != null ? " " + prey.name : "") + " wp " + patrolIndex;
        private RemakeGame game;
        private CharacterController body;
        private RemakeBody visual;
        private Mode mode;
        private Vector3 spawn, goal, targetPosition, lastKnown, stuckFrom, nudge;
        private float nextThink, nextHit, activeAt, stunnedUntil, attackUntil, pendingHitAt = -1, crawl, nextProbe, targetCrawl;
        private float lastNoise, nextGroan, lastSeen = -99, pauseUntil, investigateUntil, nextRepath, stuckCheckAt, nudgeUntil;
        private readonly List<Vector3> route = new List<Vector3>();
        private Vector3 routeGoal = new Vector3(9999, 0, 0);
        private int routeIndex, flags, patrolIndex, stuckCount;
        private float stuckDistance;
        private StudentState prey, hitTarget;
        private readonly Dictionary<int, float> exposure = new Dictionary<int, float>();
        private AudioSource voice;

        private bool arrived;
        private float burstStarted;
        private void Investigate(Vector3 point, float seconds) { mode = Mode.Investigate; goal = point; investigateUntil = Time.time + seconds; arrived = false; }
        private bool Giant => Kind == EnemyKind.Giant;
        private float StandHeight => Giant ? 3.55f : Kind==EnemyKind.Caretaker ? 1.55f : Kind==EnemyKind.Ulises ? 2.08f : 1.85f;
        private float CrawlHeight => Giant ? 1.45f : 1.2f;
        private float Reach => Giant ? 2.4f : 1.4f;
        private float SightRange => Giant ? 24 : Kind == EnemyKind.Caretaker ? 7 : 18;
        private float SenseRange => Giant ? 2.5f : 2;
        private float Memory => Giant ? 4 : 3;

        public void Setup(RemakeGame owner, EnemyKind kind, Vector3 position, GameObject model)
        {
            game = owner; Kind = kind; spawn = goal = targetPosition = position;
            gameObject.name = DisplayName;
            transform.position = position;
            body = gameObject.AddComponent<CharacterController>();
            body.radius = .25f; body.height = StandHeight; body.center = Vector3.up * (StandHeight / 2);
            body.stepOffset = .35f; body.slopeLimit = 35;
            if (model != null)
            {
                Transform instance = Instantiate(model, transform).transform;
                instance.localPosition = Vector3.zero; instance.localRotation = Quaternion.identity;
                if(Kind==EnemyKind.Caretaker)instance.localScale=Vector3.one*.82f;
                if(Kind==EnemyKind.Derick)instance.localScale=new Vector3(.8f,1.05f,.8f);
                if(Kind==EnemyKind.Cristian)instance.localScale=new Vector3(1.18f,1,1.18f);
                if(Kind==EnemyKind.Ulises)instance.localScale=new Vector3(.83f,1.12f,.83f);
                visual = gameObject.AddComponent<RemakeBody>();
                visual.Setup(instance, game.WorldMask);
                if (Giant) { visual.StrideScale = 1.7f; visual.StepHeight = .42f; visual.StepTime = .5f; visual.Hunch = 30; visual.Twitch = 1.2f; }
                else { visual.Hunch = 6; visual.Twitch = .35f; }
                if (Kind == EnemyKind.Caretaker) { visual.Hunch = 22; visual.Twitch = .7f; }
                if (Kind == EnemyKind.Derick) { instance.localScale = new Vector3(.8f,1.05f,.8f); visual.Hunch = 18; visual.StepTime = .22f; }
                if (Kind == EnemyKind.Cristian) instance.localScale = new Vector3(1.18f,1,1.18f);
                if (Kind == EnemyKind.Ulises) { instance.localScale = new Vector3(.83f,1.12f,.83f); visual.Twitch = 1.5f; }
                Color tint = Kind == EnemyKind.Ulises ? new Color(.35f,.55f,.7f) : Kind == EnemyKind.Cristian ? new Color(.5f,.18f,.12f) : Kind == EnemyKind.Derick ? new Color(.6f,.53f,.22f) : Color.white;
                foreach(Renderer renderer in transform.GetComponentsInChildren<Renderer>()) {
                    var props=new MaterialPropertyBlock(); renderer.GetPropertyBlock(props);props.SetColor("_BaseColor",tint);renderer.SetPropertyBlock(props);
                }
                AddIdentity();
                visual.Planted = (point, loudness) => game.Audio?.Footfall(Kind, point, loudness);
            }
            voice = gameObject.AddComponent<AudioSource>(); voice.spatialBlend = 1; voice.rolloffMode = AudioRolloffMode.Linear;
            voice.maxDistance = Giant ? 32 : 18; voice.loop = true; voice.volume = Giant ? .55f : .25f;
            voice.clip = game.Audio != null ? game.Audio.Clip(Giant ? "giant_breath" : "caretaker_hum") : RemakeSound.Tone("hum", 65, 2, .12f);
            voice.Play();
            WakeIn(Giant ? 25 : 35);
        }
        private void AddIdentity()
        {
            Transform Find(string n){foreach(var t in GetComponentsInChildren<Transform>())if(t.name==n)return t;return transform;}
            void Part(string n,Transform parent,Vector3 p,Vector3 size,Material material,PrimitiveType shape=PrimitiveType.Cube) {
                var go=GameObject.CreatePrimitive(shape);Destroy(go.GetComponent<Collider>());go.name=n;go.layer=9;
                go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            }
            if(Kind==EnemyKind.Ulises) {
                var head=Find("Head_Joint");Part("Visor oscuro / Ulises",head,new Vector3(0,.04f,.17f),new Vector3(.27f,.11f,.05f),game.SleeveMaterial);
                Part("Correa visor",head,new Vector3(0,.04f,0),new Vector3(.3f,.025f,.3f),game.PropMaterial);
            }
            if(Kind==EnemyKind.Cristian) {
                var hips=Find("Hips_Joint");Part("Caja de llaves / Cristian",hips,new Vector3(.32f,-.03f,.1f),new Vector3(.26f,.32f,.17f),game.PropMaterial);
                for(int i=0;i<4;i++)Part("Llave",hips,new Vector3(.28f+i*.035f,-.25f,.18f),new Vector3(.014f,.13f,.025f),game.SignalMaterial);
            }
            if(Kind==EnemyKind.Derick) {
                var chest=Find("Chest_Joint");Part("Dorsal / Derick",chest,new Vector3(0,.03f,.19f),new Vector3(.24f,.18f,.035f),game.SignalMaterial);
                for(int i=-1;i<=1;i+=2)Part("Rodillera",Find(i<0?"LowerLeg_L":"LowerLeg_R"),new Vector3(0,-.07f,.09f),new Vector3(.15f,.2f,.08f),game.SleeveMaterial);
            }
        }
        private void WakeIn(float seconds)
        {
            activeAt = Time.time + seconds;
            mode = Mode.Patrol; prey = null; route.Clear(); routeGoal = new Vector3(9999, 0, 0);
            exposure.Clear();
            patrolIndex = NearestWaypoint();
        }
        public void Hear(Vector3 point, float radius)
        {
            Vector3 ear = transform.position + Vector3.up * .9f;
            Vector3 sound = point + Vector3.up * .3f;
            if (Physics.Linecast(ear, sound, game.WorldMask, QueryTriggerInteraction.Ignore)) radius *= .3f;
            radius *= Kind == EnemyKind.Caretaker ? 1.6f : Kind == EnemyKind.Ulises ? .65f : 1;
            if (Vector3.Distance(transform.position, point) > radius || Time.time - lastNoise < 0.25f || Stunned || mode == Mode.Chase) return;
            Investigate(point, 10); lastNoise = Time.time; nextThink = 0;
        }
        public void Apply(Vector3 position, int state)
        {
            targetPosition = position; body.enabled = false;
            if ((state & 4) != 0 && (flags & 4) == 0) { visual?.Attack(1.05f); game.Audio?.Play(Giant ? "giant_swipe" : "caretaker_swing", transform.position, .8f); }
            if ((state & 1) != 0 && (flags & 1) == 0 && game.Audio != null) game.Audio.Play(Giant ? "giant_scream" : "caretaker_alert", transform.position + Vector3.up * 2, 1);
            flags = state;
        }
        // Diagnostics (capture tour, smoke): place and wake an enemy immediately.
        public void DebugPlace(Vector3 position) { body.enabled = false; transform.position = position; targetPosition = position; body.enabled = game.Authority; route.Clear(); routeGoal = new Vector3(9999, 0, 0); }
        public void Wake() { activeAt = 0; nextThink = 0; }
        public void Rest(float seconds) { activeAt = Time.time + seconds; mode = Mode.Patrol; prey = null; route.Clear(); exposure.Clear(); pendingHitAt = -1; }
        public void Stun(float seconds)
        {
            if (!game.Authority || Time.time < stunnedUntil) return;
            stunnedUntil = Time.time + seconds; pendingHitAt = -1; route.Clear();
            if (prey != null) { Investigate(lastKnown, seconds + 6); prey = null; }
            game.Audio?.Play(Giant ? "giant_hurt" : "caretaker_hurt", transform.position, 1);
            game.Tell("¡" + DisplayName + " quedó aturdido!");
        }
        public void ResetEnemy()
        {
            body.enabled = false; transform.position = spawn; body.enabled = game.Authority;
            goal = targetPosition = spawn; stunnedUntil = 0; pendingHitAt = -1;
            WakeIn(Giant ? 25 : 25);
        }
        public void DaySpawn(Vector3 position) { spawn=position;ResetEnemy(); }
        internal bool DebugDetects(StudentState student) => Sense()==student;

        private void Update()
        {
            if (!game.Started || game.Phase != 1) return;
            ProbeCeiling();
            if (visual != null)
            {
                float slump = Stunned ? (Giant ? .75f : .6f) : 0;
                visual.Crawl = Mathf.Max(crawl, slump);
                Transform look = null; float nearest = 14;
                foreach (StudentState s in game.Students.Values)
                {
                    if (!s.alive) continue;
                    float d = Vector3.Distance(s.position, transform.position);
                    if (d < nearest && Visible(transform.position + Vector3.up * 1.1f, s)) { nearest = d; look = game.Avatar(s.id); }
                }
                visual.HasLookTarget = look != null && !Stunned;
                if (look != null) visual.LookTarget = look.position + Vector3.up * 1.2f;
            }
            if (Giant && Time.time > nextGroan && game.Audio != null)
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
            ResolveHit();
            if (Stunned) { body.Move(Vector3.down * 3 * Time.deltaTime); return; }
            if (Time.time > nextThink) { nextThink = Time.time + .3f; Think(); }
            Steer();
            TryAttack();
        }

        // ------------------------------------------------------------------ perception and decisions
        private void Think()
        {
            game.OpenDoorsNear(transform.position + transform.forward * (Giant ? 1.2f : .8f), Giant ? 2.4f : 1.6f);
            StudentState seen = Sense();
            if (seen != null)
            {
                if (mode != Mode.Chase || prey != seen)
                {
                    burstStarted = Time.time;
                    if (mode != Mode.Chase && game.Audio != null)
                        game.Audio.Play(Giant ? "giant_scream" : "caretaker_alert", transform.position + Vector3.up * 2, 1);
                    Debug.Log("[Remake] " + name + " persigue a " + seen.name);
                }
                mode = Mode.Chase; prey = seen; lastSeen = Time.time; lastKnown = seen.position;
            }
            else if (mode == Mode.Chase && Time.time - lastSeen > Memory)
            {
                Debug.Log("[Remake] " + name + " perdió el rastro de " + (prey != null ? prey.name : "?"));
                Investigate(lastKnown, 8); prey = null;
            }
            if (mode == Mode.Chase && prey != null && (!prey.alive || game.InTruck(prey.position)))
            { Investigate(lastKnown, 5); prey = null; }

            if (mode == Mode.Investigate)
            {
                if (!arrived && Flat(goal - transform.position) < 1.6f) { arrived = true; pauseUntil = Time.time + Random.Range(1.5f, 3f); }
                if (Time.time > investigateUntil || (arrived && Time.time > pauseUntil)) { mode = Mode.Patrol; patrolIndex = NearestWaypoint(); }
            }
            // Patrol never receives a hidden student's position. Only sight and noise provide clues.
            if (mode == Mode.Patrol)
            {
                if (Giant && game.GiantRoute.Count > 0)
                {
                    goal = game.GiantRoute[patrolIndex % game.GiantRoute.Count];
                    if (Flat(goal - transform.position) < 1.8f && Time.time > pauseUntil)
                    {
                        pauseUntil = Time.time + Random.Range(.8f, 2.6f);
                        patrolIndex = (patrolIndex + 1) % game.GiantRoute.Count;
                        if (Random.value < .3f) game.Audio?.Play("giant_groan", transform.position + Vector3.up * 2.5f, .9f);
                    }
                }
                else if (Flat(goal - transform.position) < 1.8f) {
                    goal = game.PatrolPoint(false);
                    if(Kind == EnemyKind.Cristian) {
                        RemakeLoot guarded=null;
                        foreach(var item in game.Loot) if(item.Value>=900 && item.gameObject.activeSelf && !game.CargoContains(item.Bounds) && (guarded==null || Flat(item.transform.position-transform.position)<Flat(guarded.transform.position-transform.position))) guarded=item;
                        if(guarded!=null)goal=guarded.Spawn;
                    }
                }
            }
        }

        private StudentState Sense()
        {
            StudentState best = null; float bestDistance = float.MaxValue;
            Vector3 head = transform.position + Vector3.up * Mathf.Lerp(StandHeight - .25f, CrawlHeight - .2f, crawl);
            Vector3 chest = transform.position + Vector3.up * Mathf.Lerp(StandHeight * .6f, CrawlHeight * .6f, crawl);
            foreach (StudentState s in game.Students.Values)
            {
                if (!s.alive || game.InTruck(s.position) || Mathf.Abs(s.position.y - transform.position.y) > 2.5f) continue;
                float d = Flat(s.position - transform.position);
                bool crouched = s.eye < 1.1f;
                float range = SightRange * (crouched ? .7f : 1);
                if(Kind == EnemyKind.Ulises) range = s.torch ? 28 : 5;
                if(Kind == EnemyKind.Cristian && s.held < 0) range *= .45f;
                Vector3 bearing = s.position - transform.position; bearing.y = 0;
                bool inView = d < SenseRange || Vector3.Angle(transform.forward, bearing) < (crouched ? 45 : 65);
                bool clear = d <= range && inView && (Visible(head, s) || Visible(chest, s));
                exposure.TryGetValue(s.id, out float noticed);
                noticed = clear ? Mathf.Min(2, noticed + .3f) : Mathf.Max(0, noticed - .6f);
                exposure[s.id] = noticed;
                float threshold = crouched ? 1.2f : .6f;
                if (clear && (d < SenseRange || noticed >= threshold || prey == s) && d < bestDistance) { best = s; bestDistance = d; }
            }
            return best;
        }
        private bool Visible(Vector3 from, StudentState s)
        {
            foreach (float h in new[] { Mathf.Min(1.15f, s.eye), Mathf.Min(.45f, s.eye * .6f) })
            {
                Vector3 to = s.position + Vector3.up * h, d = to - from;
                if (!Physics.Raycast(from, d.normalized, d.magnitude, game.WorldMask, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ movement
        private void Steer()
        {
            if (Time.time < pauseUntil && mode != Mode.Chase) { body.Move(Vector3.down * 3 * Time.deltaTime); return; }
            Vector3 target = mode == Mode.Chase && prey != null ? lastKnown : goal;
            bool direct = mode == Mode.Chase && Flat(target - transform.position) < 16 && Clear(transform.position, target);
            Vector3 destination = target;
            if (!direct)
            {
                float repath = mode == Mode.Chase ? .5f : 2.5f;
                if (Time.time > nextRepath || Flat(target - routeGoal) > 1.5f)
                {
                    nextRepath = Time.time + repath; routeGoal = target;
                    game.FindPath(transform.position, target, route); routeIndex = 0;
                }
                while (routeIndex < route.Count && Flat(route[routeIndex] - transform.position) < .6f) routeIndex++;
                if (routeIndex < route.Count) destination = route[routeIndex];
            }
            Vector3 heading = destination - transform.position; heading.y = 0;
            if (Time.time < nudgeUntil) heading = nudge;
            bool attacking = Time.time < attackUntil;
            if (heading.magnitude > .25f && !attacking)
            {
                float speed = mode == Mode.Chase ? RemakeBestiary.Chase(Kind) + Mathf.Min((game.Day - 1) * .08f, .45f)
                    : mode == Mode.Investigate ? (Giant ? 2.2f : 2.3f) : (Giant ? 1.6f : 1.65f);
                if(Recovering) speed = .5f;
                speed *= Mathf.Lerp(1, .72f, crawl);
                body.Move((heading.normalized * speed + Vector3.down * 3) * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), Time.deltaTime * (Giant ? 4 : 7));
                Unstick(heading, target);
            }
            else body.Move(Vector3.down * 3 * Time.deltaTime);
        }
        // Net displacement over a 3 s window: sliding back and forth against an obstacle nets ~0 even though the
        // body moves, while a long detour still nets metres. Escalates: repath, side-step, give up on the goal.
        private void Unstick(Vector3 heading, Vector3 target)
        {
            if (Time.time < stuckCheckAt) return;
            float net = Flat(transform.position - stuckFrom);
            stuckFrom = transform.position; stuckCheckAt = Time.time + 3f;
            if (net > 1.2f || Flat(target - transform.position) < 2) { stuckCount = 0; return; }
            stuckCount++; nextRepath = 0;
            if (stuckCount >= 2)
            {
                nudge = Quaternion.Euler(0, Random.Range(60f, 120f) * (Random.value < .5f ? 1 : -1), 0) * heading.normalized; nudgeUntil = Time.time + 1.1f;
            }
            if (stuckCount >= 3)
            {
                stuckCount = 0;
                Debug.Log("[Remake] " + name + " atascado en " + transform.position.ToString("F1") + ", cambia de objetivo");
                if (mode == Mode.Patrol && Giant && game.GiantRoute.Count > 0) patrolIndex = (patrolIndex + 1) % game.GiantRoute.Count;
                else if (mode == Mode.Investigate) investigateUntil = 0;
            }
        }
        private bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 from = a + Vector3.up * .9f, to = b + Vector3.up * .9f, d = to - from;
            return !Physics.SphereCast(from, .3f, d.normalized, out _, d.magnitude, game.WorldMask, QueryTriggerInteraction.Ignore);
        }
        private int NearestWaypoint()
        {
            if (game == null || game.GiantRoute.Count == 0) return 0;
            int best = 0; float d = float.MaxValue;
            for (int i = 0; i < game.GiantRoute.Count; i++)
            {
                float di = Flat(game.GiantRoute[i] - transform.position);
                if (di < d) { d = di; best = i; }
            }
            return best;
        }
        private static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        // ------------------------------------------------------------------ attacks
        private void ResolveHit()
        {
            if (pendingHitAt <= 0 || Time.time < pendingHitAt) return;
            pendingHitAt = -1;
            if (hitTarget != null && hitTarget.alive && !Stunned && !game.InTruck(hitTarget.position) && Vector3.Distance(hitTarget.position, transform.position) < Reach && Visible(transform.position + Vector3.up * .9f, hitTarget))
            {
                Vector3 away = hitTarget.position - transform.position; away.y = 0;
                away = away.sqrMagnitude > .01f ? away.normalized : transform.forward;
                game.Hurt(hitTarget.id, RemakeBestiary.Damage(Kind), away * (Giant ? 6 : 3) + Vector3.up * (Giant ? 3 : 1.5f));
            }
        }
        private void TryAttack()
        {
            if(Recovering) return;
            if (Time.time < nextHit) return;
            foreach (StudentState student in game.Students.Values)
                if (student.alive && !game.InTruck(student.position) && Vector3.Distance(student.position, transform.position) < Reach && Visible(transform.position + Vector3.up * .9f, student))
                {
                    hitTarget = student; pendingHitAt = Time.time + (Giant ? .75f : .6f);
                    attackUntil = Time.time + 1.05f; nextHit = Time.time + (Giant ? 3.2f : 2.5f);
                    visual?.Attack(1.05f);
                    game.Audio?.Play(Giant ? "giant_swipe" : "caretaker_swing", transform.position, .8f);
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
                // Several samples ahead so a thin door header is caught before the body reaches it.
                float clearance = Clearance(transform.position);
                for (float d = .45f; d <= (Giant ? 2.2f : 1f); d += .45f)
                    clearance = Mathf.Min(clearance, Clearance(transform.position + transform.forward * d));
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
