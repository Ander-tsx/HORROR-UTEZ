using System;
using UnityEngine;

namespace HorrorUtez.Remake
{
    // Layered first-person camera with the feel of R.E.P.O.'s camera stack:
    // smoothed aim -> footstep bob -> turn/strafe tilt -> jump/land kick + shake, plus sprint/scare FOV.
    public sealed class RemakeCameraRig
    {
        public Transform Aim { get; }
        public Camera View { get; }
        public float BaseFov = 72;
        public Action<float> Step;
        private readonly Transform bob, tilt, kick;
        private float bobPhase, bobActive, side = 1, fov, zoom, zoomTimer, shakeAmount, shakeTime, shakeDuration;
        private float tiltRoll, tiltPitch, lastYaw, lastPitch;
        private Vector3 kickPos, kickPosVelocity, kickRot, kickRotVelocity;

        public RemakeCameraRig(Transform eyes)
        {
            Aim = Child("Aim", eyes); bob = Child("Bob", Aim); tilt = Child("Tilt", bob); kick = Child("Kick", tilt);
            View = kick.gameObject.AddComponent<Camera>(); fov = BaseFov;
        }
        private static Transform Child(string name, Transform parent)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }

        public void Jump() { kickPosVelocity += Vector3.down * .5f; kickRotVelocity.x += 18; }
        public void Land(float strength)
        {
            kickPosVelocity += Vector3.down * (1.1f + strength * 2.2f); kickRotVelocity.x += 35 + strength * 70;
            Shake(.8f + strength * 2.5f, .22f + strength * .2f);
        }
        public void Hit(float strength) { kickRotVelocity += new Vector3(-30, UnityEngine.Random.Range(-40f, 40f), 50) * strength; Shake(3 * strength, .4f); }
        public void Shake(float degrees, float seconds)
        {
            if (degrees < shakeAmount * (shakeTime / Mathf.Max(shakeDuration, .01f))) return;
            shakeAmount = degrees; shakeTime = shakeDuration = seconds;
        }
        public void Zoom(float degrees, float seconds) { zoom = degrees; zoomTimer = seconds; }

        public void Tick(float yaw, float pitch, bool grounded, float speed, bool sprinting, float sprintLerp, bool crouching, float strafe, float dt)
        {
            dt = Mathf.Min(dt, .05f);
            if (dt <= 0) return;
            // Aim: exponential smoothing towards the input rotation, like R.E.P.O.'s camera smoothing setting.
            Aim.rotation = Quaternion.Slerp(Aim.rotation, Quaternion.Euler(pitch, yaw, 0), 1 - Mathf.Exp(-30 * dt));

            // Bob: one dip per footstep, side sway alternates feet. Footstep sounds are driven by the cycle.
            bool moving = grounded && speed > .4f;
            bobActive = Mathf.MoveTowards(bobActive, moving ? 1 : 0, dt * (moving ? 3 : 4));
            if (bobActive > .01f)
            {
                float previous = bobPhase;
                bobPhase += (.75f + speed * .33f) * dt;
                if (Mathf.Floor(bobPhase) != Mathf.Floor(previous))
                {
                    side = -side;
                    if (moving) Step?.Invoke(sprinting ? 1 : crouching ? .35f : .7f);
                }
            }
            float f = bobPhase % 1, amplitude = sprinting ? .055f : crouching ? .02f : .035f;
            float up = (.5f - (1 + Mathf.Cos(f * Mathf.PI * 2)) * .5f) * amplitude, sway = side * Mathf.Sin(f * Mathf.PI);
            bob.localPosition = new Vector3(sway * .018f, up, 0) * bobActive;
            bob.localRotation = Quaternion.Euler(0, sway * .35f * bobActive, sway * .8f * bobActive);

            // Tilt: lean into turns and strafes.
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt, pitchRate = Mathf.DeltaAngle(lastPitch, pitch) / dt;
            lastYaw = yaw; lastPitch = pitch;
            float k = 1 - Mathf.Exp(-6 * dt), crouchScale = crouching ? .5f : 1;
            tiltRoll = Mathf.Lerp(tiltRoll, Mathf.Clamp(-yawRate * .008f - strafe * 1.8f, -6, 6) * crouchScale, k);
            tiltPitch = Mathf.Lerp(tiltPitch, Mathf.Clamp(-pitchRate * .004f, -3, 3) * crouchScale, k);
            tilt.localRotation = Quaternion.Euler(tiltPitch, 0, tiltRoll);

            // Kick: damped springs for jump/land/hit impulses, then noise shake on top.
            kickPosVelocity += (-kickPos * 150 - kickPosVelocity * 14) * dt; kickPos += kickPosVelocity * dt;
            kickRotVelocity += (-kickRot * 150 - kickRotVelocity * 14) * dt; kickRot += kickRotVelocity * dt;
            Vector3 shake = Vector3.zero;
            if (shakeTime > 0)
            {
                shakeTime -= dt;
                float a = shakeAmount * Mathf.Clamp01(shakeTime / shakeDuration), t = Time.time * 32;
                shake = new Vector3(Mathf.PerlinNoise(t, 1) - .5f, Mathf.PerlinNoise(2, t) - .5f, Mathf.PerlinNoise(t, 3) - .5f) * 2 * a;
            }
            kick.localPosition = kickPos;
            kick.localRotation = Quaternion.Euler(kickRot + shake);

            // FOV widens with the sprint ramp and snaps in on a scare.
            if (zoomTimer > 0) zoomTimer -= dt; else zoom = 0;
            fov = Mathf.Lerp(fov, BaseFov + (sprinting ? 9 * sprintLerp : 0) + zoom, 1 - Mathf.Exp(-(zoom != 0 ? 9 : 4) * dt));
            View.fieldOfView = fov;
        }
    }
}
