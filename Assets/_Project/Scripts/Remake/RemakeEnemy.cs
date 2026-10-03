using System.Collections.Generic;
using UnityEngine;

namespace HorrorUtez.Remake
{
    public sealed class RemakeEnemy : MonoBehaviour
    {
        private RemakeGame game;
        private CharacterController body;
        private Vector3 spawn, goal, targetPosition;
        private float noiseUntil, nextThink, nextHit, activeAt;
        private readonly List<Vector3> route = new List<Vector3>();
        private int routeIndex;
        private StudentState prey;
        public bool Chasing => prey != null;
        private Transform visual;
        private float lastNoise;

        public void Setup(RemakeGame owner, Vector3 position, GameObject model)
        {
            game = owner; spawn = goal = targetPosition = position;
            transform.position = position;
            body = gameObject.AddComponent<CharacterController>();
            body.height = 2.3f; body.radius = 0.32f; body.center = new Vector3(0, 1.15f, 0); body.stepOffset = 0.35f;
            if (model != null)
            { visual = Instantiate(model, transform).transform; visual.localRotation=Quaternion.Euler(0,180,0)*visual.localRotation; }
            else visual = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            var source = gameObject.AddComponent<AudioSource>(); source.spatialBlend = 1; source.maxDistance = 18;
            source.rolloffMode = AudioRolloffMode.Linear; source.volume = 0.15f;
            source.clip = RemakeSound.Tone("Caretaker hum", 65, 2, 0.12f); source.loop = true; source.Play();
            activeAt = Time.time + 35;
        }
        public void Hear(Vector3 point, float radius)
        {
            if (Vector3.Distance(transform.position, point) > radius || Time.time - lastNoise < 0.25f) return;
            goal = point; noiseUntil = Time.time + 8; nextThink = 0; lastNoise = Time.time;
        }
        public void Apply(Vector3 position) { targetPosition = position; body.enabled = false; }
        public void ResetEnemy()
        {
            body.enabled = false; transform.position = spawn; body.enabled = game.Authority;
            goal = targetPosition = spawn; prey = null; route.Clear(); activeAt = Time.time + 25;
        }
        private void Update()
        {
            if (!game.Started || game.Phase != 1) return;
            if (!game.Authority)
            {
                Vector3 delta = targetPosition - transform.position;
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 14);
                if (delta.sqrMagnitude > 0.015f) transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(new Vector3(delta.x, 0, delta.z)), Time.deltaTime * 8);
                return;
            }
            if (Time.time < activeAt) return;
            if (Time.time > nextThink)
            {
                nextThink = Time.time + 0.7f;
                prey = null; float nearest = 22;
                foreach (StudentState student in game.Students.Values)
                {
                    if (!student.alive || game.InTruck(student.position)) continue;
                    float distance = Vector3.Distance(transform.position, student.position);
                    if (distance > nearest || Mathf.Abs(student.position.y - transform.position.y) > 2) continue;
                    Vector3 sight = student.position + Vector3.up - (transform.position + Vector3.up * 1.8f);
                    if (Physics.Raycast(transform.position + Vector3.up * 1.8f, sight.normalized, sight.magnitude, game.WorldMask)) continue;
                    prey = student; nearest = distance;
                }
                if (prey != null) goal = prey.position;
                else if (Time.time > noiseUntil && (goal - transform.position).sqrMagnitude < 5)
                {
                    Vector3[] patrol = { new Vector3(-8, 0, 1), new Vector3(6, 0, -8), new Vector3(-12, 0, 18), new Vector3(18, 0, 8) };
                    goal = patrol[Random.Range(0, patrol.Length)];
                }
                game.FindPath(transform.position, goal, route); routeIndex = 0;
            }
            Vector3 destination = routeIndex < route.Count ? route[routeIndex] : goal;
            Vector3 heading = destination - transform.position; heading.y = 0;
            if (heading.magnitude < 0.55f && routeIndex < route.Count) routeIndex++;
            if (heading.magnitude > 0.25f)
            {
                float speed = prey != null ? 3.7f + Mathf.Min(game.Day * 0.12f, 1.3f) : 1.65f;
                body.Move((heading.normalized * speed + Vector3.down * 3) * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), Time.deltaTime * 7);
                if (visual != null) visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * 8) * 0.04f);
            }
            if (Time.time > nextHit)
                foreach (StudentState student in game.Students.Values)
                    if (student.alive && !game.InTruck(student.position) && Vector3.Distance(student.position, transform.position) < 1.4f)
                    { game.Hurt(student.id, 28); nextHit = Time.time + 1.2f; break; }
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
