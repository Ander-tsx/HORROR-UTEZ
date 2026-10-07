using UnityEngine;

namespace HorrorUtez.Remake
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RemakeLoot : MonoBehaviour
    {
        public int Id, Kind, BaseValue, Value;
        public string Label;
        public Rigidbody Body { get; private set; }
        public Vector3 Spawn, Size;
        public Quaternion SpawnRotation;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private float safeUntil, nextImpact;
        private RemakeGame game;
        public const int CartKind = 6, CredentialKind = 9;
        public int Owner { get; private set; } = -1;
        private static readonly Vector3 Hidden = new Vector3(0, -200, 0);
        private bool grabbedOnce;
        public string Trait => Kind >= 10 ? RemakeLootCatalog.Traits[Kind-10] : Kind == CredentialKind ? "Reanimación" : Body.mass >= 25 ? "Pesado / cooperativo" : "Equipo frágil";
        public float GripEffort(StudentState student) {
            int holders=0;foreach(var s in game.Students.Values)if(s.alive && s.held==Id)holders++;
            return Mathf.Clamp01(Body.mass/(Mathf.Max(1,holders)*(21+student.strength*7f)));
        }
        private float Fragile => Kind >= 10 ? (Kind == 12 ? 100 : Kind == 11 || Kind == 16 ? 25 : 70) : Fragility[Kind];
        private float Durable => Kind >= 10 ? (Kind == 11 || Kind == 16 ? 85 : Kind == 12 ? 25 : 65) : Durability[Kind];
        public Bounds Bounds => GetComponent<BoxCollider>().bounds;

        public void Setup(RemakeGame owner, int id, int kind, string label, int value, float mass, Vector3 size)
        {
            game = owner; Id = id; Kind = kind; Label = label; BaseValue = Value = value; Size = size;
            Body = GetComponent<Rigidbody>();
            Body.mass = mass;
            Body.linearDamping = 0.2f;
            Body.angularDamping = 1.4f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Spawn = transform.position; SpawnRotation = transform.rotation;
            targetPosition = Spawn; targetRotation = SpawnRotation;
            safeUntil = Time.time + 3;
        }
        public void SetAuthority(bool authority)
        {
            Body.isKinematic = !authority;
            Body.collisionDetectionMode = authority ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
        }
        public void Apply(LootState state)
        {
            if (Kind == CredentialKind)
            {
                Owner = state.owner; bool shown = Owner >= 0; if (gameObject.activeSelf != shown) gameObject.SetActive(shown);
                if (shown && game.Students.TryGetValue(Owner, out StudentState who)) Label = "Credencial de " + who.name;
                targetPosition = state.position; targetRotation = state.rotation;
                if (Vector3.Distance(transform.position, targetPosition) > 8) transform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }
            // Clients mirror value loss popups and destruction from the host's snapshots.
            if (state.value < Value && game.Started) game.ValuePopup(transform.position, Value - state.value, state.value == 0 && BaseValue > 0);
            if (BaseValue > 0) gameObject.SetActive(state.value > 0);
            Value = state.value; targetPosition = state.position; targetRotation = state.rotation;
            if (Vector3.Distance(transform.position, targetPosition) > 8)
                transform.SetPositionAndRotation(targetPosition, targetRotation);
        }
        private void Update()
        {
            if (game == null || game.Authority || !game.Started) return;
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 18),
                Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 18));
        }
        private void FixedUpdate()
        {
            if (game == null || !game.Authority || game.Phase != 1) return;
            // Grip physics follow R.E.P.O.'s PhysGrabObject model: a damped spring
            // pulls the exact grab point, so objects hang and swing from where they were grabbed, and a torque keeps
            // their camera-relative orientation. Each student contributes a capped lift, so heavy gear needs a team.
            bool held = false;
            foreach (StudentState student in game.Students.Values)
            {
                if (!student.alive || student.held != Id) continue;
                Quaternion cam = Quaternion.Euler(student.pitch, student.yaw, 0);
                Vector3 eyes = student.position + Vector3.up * student.eye;
                Vector3 aim = cam * Vector3.forward;
                float reach = student.reach;
                if (Physics.Raycast(eyes, aim, out RaycastHit hit, reach, game.WorldMask, QueryTriggerInteraction.Ignore))
                    reach = Mathf.Max(0.5f, hit.distance - 0.2f);
                Vector3 puller = eyes + aim * reach;
                Vector3 grabPoint = transform.TransformPoint(student.grabLocal);
                if (Vector3.Distance(puller, grabPoint) > 3.2f) { student.held = -1; continue; }
                held = true;
                float lift = 9.81f * 1.4f * (15 + student.strength * 5) / Mathf.Max(Body.mass, 0.5f);
                Vector3 accel = (puller - grabPoint) * 85 - Body.GetPointVelocity(grabPoint) * 13;
                Body.AddForceAtPosition(Vector3.ClampMagnitude(accel, lift) * Body.mass, grabPoint);
                Quaternion delta = cam * student.hold * Quaternion.Inverse(Body.rotation);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180) angle -= 360;
                if (float.IsFinite(axis.x))
                {
                    Vector3 spin = axis * (angle * Mathf.Deg2Rad * 40) - Body.angularVelocity * 9;
                    Body.AddTorque(Vector3.ClampMagnitude(spin, 6 + lift * 1.5f), ForceMode.Acceleration);
                }
            }
            if (held) Body.angularVelocity *= 0.92f;
            // Like R.E.P.O., the first pickup is briefly indestructible so yanking gear off a shelf is forgiving.
            if (held && !grabbedOnce) { grabbedOnce = true; safeUntil = Mathf.Max(safeUntil, Time.time + 0.5f); }
            if (Body.position.y < -8)
            {
                Body.position = Spawn; Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
                safeUntil = Time.time + 2;
            }
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (game == null || !game.Authority || game.Phase != 1 || Time.time < safeUntil || Time.time < nextImpact) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 1.6f) return;
            nextImpact = Time.time + 0.25f;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            game.Noise(transform.position, Mathf.Clamp(speed * 3, 5, 32));
            game.PlayImpact(point, Mathf.Clamp01(speed / 9), (Kind >= 10 || Metallic[Kind]) ? "impact_metal" : "impact_plastic");
            // Thrown gear stuns enemies (R.E.P.O.): heavier and faster hits stun longer; the giant shrugs off light ones.
            RemakeEnemy enemy = collision.collider.GetComponentInParent<RemakeEnemy>();
            if (enemy != null)
            {
                float hit = speed * Mathf.Sqrt(Body.mass);
                float needed = enemy.Kind == EnemyKind.Giant ? 16 : 7;
                if (hit >= needed) enemy.Stun(Mathf.Clamp(hit / needed * (enemy.Kind == EnemyKind.Giant ? 1.6f : 2.5f), 1.5f, 6));
            }
            // Damage tiers follow R.E.P.O.'s impact detector: fragility scales the hit,
            // durability scales the loss (1/5/10% of the original value).
            if (BaseValue <= 0 || Value <= 0) return;
            float force = speed * Fragile / 100f;
            float tier = force >= 6.5f ? 0.1f : force >= 4.2f ? 0.05f : force >= 2.2f ? 0.01f : 0;
            if (tier == 0) return;
            float share = tier * (1 + 9 * (100 - Durable) / 100f);
            int lost = Mathf.Clamp(Mathf.RoundToInt(BaseValue * share * Random.Range(0.9f, 1.1f)), 1, Value);
            Value -= lost;
            if (Value < BaseValue * 0.15f) Shatter(); else { game.ValuePopup(point, lost); game.PlayImpact(point, .6f, "value_lost"); }
        }
        // Per kind: laptop, projector, microscope, workstation, UPS, printer, cart, oscilloscope, network switch.
        private static readonly float[] Fragility = { 90, 80, 100, 60, 40, 55, 0, 85, 30, 0 };
        private static readonly float[] Durability = { 45, 55, 30, 65, 85, 70, 100, 40, 90, 100 };
        private static readonly bool[] Metallic = { false, false, false, true, true, false, true, true, true, false };
        private void Shatter()
        {
            int lost = Value; Value = 0;
            foreach (StudentState student in game.Students.Values) if (student.held == Id) student.held = -1;
            game.ValuePopup(transform.position, lost, true);
            game.Noise(transform.position, 15); game.PlayImpact(transform.position, 1, "glass_break");
            gameObject.SetActive(false);
        }
        // ID card of a fallen student: appears where they fell, glowing, until it reaches the truck or the day ends.
        public void Drop(int owner, string name, Vector3 point)
        {
            Owner = owner; Label = "Credencial de " + name; gameObject.SetActive(true);
            Body.position = point; transform.position = point; Body.rotation = Quaternion.identity;
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.up * 2; Body.angularVelocity = Random.insideUnitSphere * 4; }
            targetPosition = point; safeUntil = Time.time + 1;
        }
        public void Hide()
        {
            Owner = -1; transform.position = Hidden; Body.position = Hidden; targetPosition = Hidden;
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            gameObject.SetActive(false);
        }
        public void ResetLoot()
        {
            gameObject.SetActive(true); Value = BaseValue; grabbedOnce = false;
            Body.position = Spawn; Body.rotation = SpawnRotation;
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            targetPosition = Spawn; targetRotation = SpawnRotation; safeUntil = Time.time + 3;
        }
    }
}
