using System;
using UnityEngine;

namespace HorrorUtez.Remake
{
    // Procedural animation for the segmented enemy models (Tools/blender/gen_remake_props.py).
    // Rebuilds a joint hierarchy from the rest pose, then drives it every frame:
    // planted feet/hands with two-bone IK and stepping arcs, biped or diagonal quadruped gait,
    // a continuous stand -> crawl posture (so the giant can fold itself under ceilings and doors),
    // a head that tracks its target with nervous twitches, and an arm swipe for attacks.
    public sealed class RemakeBody : MonoBehaviour
    {
        public float Crawl { get; set; }          // 0 standing .. 1 on all fours
        public Vector3 LookTarget { get; set; }
        public bool HasLookTarget { get; set; }
        public float Hunch = 8;                   // standing forward lean, degrees
        public float StepHeight = .25f, StrideScale = 1, StepTime = .32f, Twitch = 0;
        public Action<Vector3, float> Planted;     // world point, loudness 0..1
        public Transform RightHand => limbs[1].end;

        private sealed class Limb
        {
            public Transform upper, lower, end, root;
            public float l1, l2;
            public Vector3 restOffset;            // ground contact in body space (feet) / hand reach
            public Vector3 planted, from, to;
            public float t = 1;                   // 1 = planted
            public bool arm;
        }
        private Transform hips, spine, chest, neck, head;
        private readonly Limb[] limbs = new Limb[4];  // 0 left arm, 1 right arm, 2 left leg, 3 right leg
        private float restHip, legLength, armLength, shoulderOffset, attack, twitchAt, phase;
        private Quaternion twitch = Quaternion.identity;
        private Vector3 lastPosition, velocity;
        private int mask;
        private bool ready;

        public void Setup(Transform model, int groundMask)
        {
            mask = groundMask;
            Transform Find(string n)
            {
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
                return null;
            }
            Transform pelvis = Find("Pelvis");
            if (pelvis == null) return;
            // Joints: empty pivots at each segment's rest position, meshes re-parented under them.
            Transform Joint(string name, Transform parent, Transform mesh)
            {
                var j = new GameObject(name + "_Joint").transform;
                j.SetPositionAndRotation(mesh != null ? mesh.position : parent.position, transform.rotation);
                j.SetParent(parent, true);
                if (mesh != null) mesh.SetParent(j, true);
                return j;
            }
            hips = Joint("Hips", transform, pelvis);
            spine = Joint("Spine", hips, Find("Spine"));
            chest = Joint("Chest", spine, Find("Chest"));
            neck = Joint("Neck", chest, Find("Neck"));
            head = Joint("Head", neck, Find("Head"));
            restHip = hips.localPosition.y;
            string[] sides = { "L", "R" };
            for (int i = 0; i < 2; i++)
            {
                var arm = new Limb { arm = true };
                arm.upper = Joint("UpperArm_" + sides[i], chest, Find("UpperArm_" + sides[i]));
                arm.lower = Joint("LowerArm_" + sides[i], arm.upper, Find("LowerArm_" + sides[i]));
                arm.end = Joint("Hand_" + sides[i], arm.lower, Find("Hand_" + sides[i]));
                arm.l1 = Vector3.Distance(arm.upper.position, arm.lower.position);
                arm.l2 = Vector3.Distance(arm.lower.position, arm.end.position);
                arm.restOffset = transform.InverseTransformPoint(arm.end.position);
                limbs[i] = arm;
                var leg = new Limb();
                leg.upper = Joint("UpperLeg_" + sides[i], hips, Find("UpperLeg_" + sides[i]));
                leg.lower = Joint("LowerLeg_" + sides[i], leg.upper, Find("LowerLeg_" + sides[i]));
                leg.end = Joint("Foot_" + sides[i], leg.lower, Find("Foot_" + sides[i]));
                leg.l1 = Vector3.Distance(leg.upper.position, leg.lower.position);
                leg.l2 = Vector3.Distance(leg.lower.position, leg.end.position);
                Vector3 ankle = transform.InverseTransformPoint(leg.end.position);
                leg.restOffset = new Vector3(ankle.x * 1.15f, 0, 0.05f);
                limbs[2 + i] = leg;
            }
            legLength = limbs[2].l1 + limbs[2].l2;
            armLength = limbs[0].l1 + limbs[0].l2;
            shoulderOffset = chest.InverseTransformPoint(limbs[1].upper.position).y;
            // Props (the caretaker's mop) ride on the right hand.
            Transform mop = Find("Mop");
            if (mop != null) mop.SetParent(limbs[1].end, true);
            foreach (Limb limb in limbs) { limb.planted = limb.from = limb.to = Ground(transform.TransformPoint(limb.restOffset)); limb.t = 1; }
            lastPosition = transform.position;
            ready = true;
        }

        public void Attack() { attack = 1; }

        private Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 3f, mask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(p.x, transform.position.y, p.z);
        }

        private void LateUpdate()
        {
            if (!ready) return;
            float dt = Mathf.Clamp(Time.deltaTime, 0.0001f, .05f);
            velocity = Vector3.Lerp(velocity, (transform.position - lastPosition) / dt, 1 - Mathf.Exp(-8 * dt));
            lastPosition = transform.position;
            Vector3 flat = new Vector3(velocity.x, 0, velocity.z);
            float speed = flat.magnitude;
            Vector3 fwd = transform.forward, right = transform.right;
            float c = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Crawl));
            phase += dt * (1.5f + speed * .9f);

            // Posture: hips drop and the spine folds forward as the body crawls.
            float hipHeight = Mathf.Lerp(restHip * .97f, Mathf.Max(armLength * .62f, restHip * .42f), c);
            float bob = Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * Mathf.Min(speed * .03f, .06f);
            hips.localPosition = new Vector3(0, hipHeight - bob, -c * .1f);
            hips.localRotation = Quaternion.Euler(c * 12, Mathf.Sin(phase * Mathf.PI) * 4 * Mathf.Min(speed, 1), 0);
            float lean = Mathf.Lerp(Hunch, 78, c) + Mathf.Min(speed * 2.5f, 8) * (1 - c);
            spine.localRotation = Quaternion.Euler(lean * .45f, 0, Mathf.Sin(phase * Mathf.PI * .5f) * 2);
            chest.localRotation = Quaternion.Euler(lean * .55f, -Mathf.Sin(phase * Mathf.PI) * 5 * Mathf.Min(speed, 1), 0);
            neck.localRotation = Quaternion.Euler(-lean * .75f, 0, 0);
            AimHead(dt);

            // Gait: biped legs alternate; on all fours the diagonal pairs move together.
            float stride = Mathf.Lerp(.35f, .9f, Mathf.Clamp01(speed / 4)) * StrideScale * (1 + c * .3f);
            Vector3 lead = flat * (StepTime * .9f);
            bool quad = c > .45f;
            for (int i = 0; i < 4; i++)
            {
                Limb limb = limbs[i];
                bool walking = !limb.arm || quad;
                if (!walking) continue;
                Vector3 offset = limb.restOffset;
                if (limb.arm) offset = new Vector3(offset.x * 1.4f, 0, hipHeight * .55f + .35f);
                Vector3 desired = Ground(transform.TransformPoint(new Vector3(offset.x, 0, offset.z)) + lead);
                if (limb.t >= 1)
                {
                    float drift = Vector3.Distance(desired, limb.planted);
                    bool partnerMoving = quad ? limbs[Partner(i)].t < 1 && !SameGroup(i, Partner(i)) : limbs[i < 2 ? 1 - i : 5 - i].t < 1;
                    if ((drift > stride || (speed < .1f && drift > .12f)) && !partnerMoving && GroupFree(i, quad))
                    { limb.from = limb.planted; limb.to = desired; limb.t = 0; }
                }
                else
                {
                    limb.t = Mathf.Min(1, limb.t + dt / Mathf.Max(StepTime * (1.15f - Mathf.Min(speed, 4) * .08f), .12f));
                    float e = Mathf.SmoothStep(0, 1, limb.t);
                    limb.planted = Vector3.Lerp(limb.from, limb.to, e) + Vector3.up * Mathf.Sin(limb.t * Mathf.PI) * StepHeight;
                    if (limb.t >= 1) { limb.planted = limb.to; Planted?.Invoke(limb.to, Mathf.Clamp01(.35f + speed * .15f)); }
                }
            }

            // Legs: two-bone IK to the planted ankle, knees forward; feet flat along the body.
            for (int i = 2; i < 4; i++)
            {
                Limb leg = limbs[i];
                Vector3 ankle = leg.planted + Vector3.up * .06f;
                SolveIK(leg, ankle, fwd + Vector3.up * .2f, fwd);
                leg.end.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            // Arms: hanging/swinging when upright, reaching forward to the ground when crawling.
            for (int i = 0; i < 2; i++)
            {
                Limb arm = limbs[i];
                Vector3 shoulder = arm.upper.position;
                Vector3 target;
                if (quad) target = arm.planted + Vector3.up * .04f;
                else
                {
                    float swing = Mathf.Sin(phase * Mathf.PI + (i == 0 ? 0 : Mathf.PI)) * Mathf.Min(speed * .12f, .45f);
                    target = shoulder - Vector3.up * armLength * .93f + fwd * (swing + .08f + c * .4f) + right * ((i == 0 ? -1 : 1) * .06f);
                    if (!quad) arm.planted = Ground(target);
                }
                if (attack > 0 && i == 1)
                {
                    // Swipe: the right arm whips from high up across the front.
                    float a = 1 - attack;
                    Vector3 high = shoulder + Vector3.up * armLength * .4f + fwd * armLength * .3f + right * .3f;
                    Vector3 low = shoulder + fwd * armLength * .9f - right * armLength * .5f - Vector3.up * armLength * .3f;
                    target = a < .35f ? Vector3.Lerp(target, high, a / .35f) : Vector3.Lerp(high, low, (a - .35f) / .65f);
                }
                SolveIK(arm, target, -fwd + Vector3.down * .3f + right * (i == 0 ? -.4f : .4f), fwd);
                arm.end.rotation = Quaternion.LookRotation(quad ? fwd : (target - arm.lower.position).normalized + fwd * .3f,
                    quad ? Vector3.up : -fwd);
            }
            if (attack > 0) attack = Mathf.Max(0, attack - dt * 2.2f);
        }

        private static int Partner(int i) => i switch { 0 => 3, 3 => 0, 1 => 2, _ => 1 };
        private static bool SameGroup(int a, int b) => Partner(a) == b;
        private bool GroupFree(int i, bool quad)
        {
            if (!quad) return true;
            // Diagonal pairs (LA+RL, RA+LL) step together; wait until the other pair has landed.
            int[] other = i == 0 || i == 3 ? new[] { 1, 2 } : new[] { 0, 3 };
            return limbs[other[0]].t >= 1 && limbs[other[1]].t >= 1;
        }

        private void AimHead(float dt)
        {
            Quaternion look = Quaternion.identity;
            if (HasLookTarget)
            {
                Vector3 d = head.parent.InverseTransformDirection(LookTarget - head.position);
                if (d.sqrMagnitude > .01f)
                {
                    Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
                    look = Quaternion.RotateTowards(Quaternion.identity, want, 75);
                }
            }
            if (Twitch > 0 && Time.time > twitchAt)
            {
                twitchAt = Time.time + UnityEngine.Random.Range(.4f, 2.5f) / Twitch;
                twitch = Quaternion.Euler(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(-35f, 35f));
            }
            twitch = Quaternion.Slerp(twitch, Quaternion.identity, dt * 2);
            head.localRotation = Quaternion.Slerp(head.localRotation, look * twitch, 1 - Mathf.Exp(-10 * dt));
        }

        // Two-bone IK: places the elbow/knee in the plane of `hint`, then aims both segments
        // (segments hang along their local -Y in the rest pose).
        private static void SolveIK(Limb limb, Vector3 target, Vector3 hint, Vector3 forward)
        {
            Vector3 root = limb.upper.position;
            Vector3 toTarget = target - root;
            float d = Mathf.Clamp(toTarget.magnitude, .01f, (limb.l1 + limb.l2) * .999f);
            Vector3 dir = toTarget.normalized;
            float cos = (limb.l1 * limb.l1 + d * d - limb.l2 * limb.l2) / (2 * limb.l1 * d);
            float a = Mathf.Acos(Mathf.Clamp(cos, -1, 1));
            Vector3 bend = Vector3.ProjectOnPlane(hint, dir).normalized;
            if (bend.sqrMagnitude < .001f) bend = Vector3.ProjectOnPlane(forward, dir).normalized;
            Vector3 joint = root + (dir * Mathf.Cos(a) + bend * Mathf.Sin(a)) * limb.l1;
            Vector3 end = root + dir * d;
            limb.upper.rotation = Aim(joint - root, forward);
            limb.lower.rotation = Aim(end - joint, forward);
        }
        private static Quaternion Aim(Vector3 down, Vector3 forward)
        {
            Vector3 up = -down.normalized;
            Vector3 f = Vector3.ProjectOnPlane(forward, up);
            if (f.sqrMagnitude < .0001f) f = Vector3.ProjectOnPlane(Vector3.forward, up);
            return Quaternion.LookRotation(f.normalized, up);
        }
    }
}
