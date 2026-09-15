using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Renders orthographic top-down references of the `utez` scene to Logs/ (gitignored).
    /// Used to eyeball the campus layout against the Google Maps overview without
    /// opening the editor. Needs a graphics device, so run batch WITHOUT -nographics.
    /// </summary>
    public static class UtezCapture
    {
        private const string ScenePath = "Assets/_Project/Scenes/utez.unity";
        private const int Resolution = 1400;

        [MenuItem("HORROR-UTEZ/Capture Top-Down References")]
        public static void CaptureAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);

            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "capture");
            Directory.CreateDirectory(dir);

            // Wide: everything including the dirt plane and the tree line.
            Capture(Path.Combine(dir, "utez_top_wide.png"), orthoSize: 125f);
            // Close: the explanada and the two building footprints.
            Capture(Path.Combine(dir, "utez_top_close.png"), orthoSize: 60f);

            // Through the player's own camera, so this is literally what the player sees
            // on pressing Play. Affine warping and pixelation only read in perspective.
            CaptureFromPlayer(Path.Combine(dir, "utez_eye_plaza.png"));

            // Inside CECADEC: the shot that shows whether the interior volume is usable at
            // the current WorldScale. Must go through the player rig, not a free camera —
            // the flashlight hangs off the rig, and without it the interior is pure black.
            var cecadec = UtezDimensions.Cecadec;
            CaptureFromPlayer(Path.Combine(dir, "utez_interior_cecadec.png"),
                new Vector3(cecadec.ScaledCenter.x, 0f, cecadec.ScaledCenter.y),
                cecadec.YawDeg);

            // The reference bench, three metres from where the player spawns. This is the
            // shot that answers "is the PSX filter eating the model" — it has to be taken at
            // the distance a prop is actually looked at, not from across the plaza.
            // Free camera, not the player rig: this shot exists to judge the models and the
            // filter, so it must not also depend on the torch reaching them or on the ground
            // probe landing right.
            CapturePerspective(Path.Combine(dir, "utez_props_plaza.png"),
                new Vector3(0f, 1.1f, 5.0f) * UtezDimensions.WorldScale, 180f);

            // Looking out from the campus edge: the shot that shows the tree line.
            Vector2 slabHalf = UtezDimensions.SlabSize * 0.5f;
            CaptureFromPlayer(Path.Combine(dir, "utez_treeline.png"),
                new Vector3(-slabHalf.x + 6f, 0f, UtezDimensions.SlabCenter.y), 270f);

            // Rain never starts on its own here: WeatherSystem.Awake does not run in edit
            // mode, so the emitter sits idle. Force it, simulate, shoot, restore.
            CaptureRain(Path.Combine(dir, "utez_rain.png"));

            Debug.Log($"[UTEZ] Captures written to {dir}");
        }

        public static void CaptureAllBatch()
        {
            try
            {
                CaptureAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UTEZ] Capture failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Drives the rain emitter by hand so the weather can be verified without entering
        /// play mode, then puts it back exactly as it was.
        /// </summary>
        private static void CaptureRain(string path)
        {
            var emitterGo = GameObject.Find("RainEmitter");
            if (emitterGo == null)
            {
                Debug.LogWarning("[UTEZ] No RainEmitter in the scene; skipping rain capture.");
                return;
            }

            var system = emitterGo.GetComponent<ParticleSystem>();
            if (system == null)
                return;

            var emission = system.emission;
            float previousRate = emission.rateOverTime.constant;
            emission.rateOverTime = 2600f;

            // Simulate forward so the volume is full of drops rather than a thin first frame.
            system.Simulate(3f, withChildren: true, restart: true);

            CaptureFromPlayer(path);

            system.Clear();
            emission.rateOverTime = previousRate;
        }

        /// <summary>Free-standing perspective shot at a given position and heading.</summary>
        private static void CapturePerspective(string path, Vector3 position, float yaw)
        {
            var go = new GameObject("UtezCaptureCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            RenderToFile(cam, path);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Renders through the player rig's own camera. The rig has not fallen to the
        /// ground yet (no physics step in edit mode), so drop it onto the terrain first
        /// to get a real eye-level view instead of one floating above the slab.
        /// </summary>
        private static void CaptureFromPlayer(string path, Vector3? moveTo = null, float? yaw = null)
        {
            var player = GameObject.Find("Player");
            var cam = player != null ? player.GetComponentInChildren<Camera>() : null;
            if (cam == null)
            {
                Debug.LogWarning("[UTEZ] No Player camera in the scene; skipping eye-level capture.");
                return;
            }

            Vector3 original = player.transform.position;
            Quaternion originalRotation = player.transform.rotation;

            if (moveTo.HasValue)
                player.transform.position = moveTo.Value;
            if (yaw.HasValue)
                player.transform.rotation = Quaternion.Euler(0f, yaw.Value, 0f);

            // No physics step runs in edit mode, so drop the rig onto the ground manually.
            //
            // The rig's own CharacterController has to come out of the way first. A ray
            // starting five metres up and pointing down enters that capsule from OUTSIDE,
            // so it reports a hit on the top of the player's own head — and every eye-level
            // capture came out 1.8 m too high, which is why props on the floor kept falling
            // out of the bottom of frame.
            var ownCollider = player.GetComponent<Collider>();
            bool colliderWasEnabled = ownCollider != null && ownCollider.enabled;
            if (ownCollider != null)
                ownCollider.enabled = false;

            if (Physics.Raycast(player.transform.position + Vector3.up * 5f, Vector3.down,
                    out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
                player.transform.position = hit.point;

            if (ownCollider != null)
                ownCollider.enabled = colliderWasEnabled;

            var previousClear = cam.clearFlags;
            var previousColor = cam.backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);

            RenderToFile(cam, path);

            cam.clearFlags = previousClear;
            cam.backgroundColor = previousColor;
            player.transform.position = original;
            player.transform.rotation = originalRotation;
        }

        private static void Capture(string path, float orthoSize)
        {
            var go = new GameObject("UtezCaptureCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);

            // Centered on the slab, looking straight down.
            go.transform.position = new Vector3(UtezDimensions.SlabCenter.x, 300f, UtezDimensions.SlabCenter.y);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            RenderToFile(cam, path);
            Object.DestroyImmediate(go);
        }

        private static void RenderToFile(Camera cam, string path)
        {
            var rt = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // In batch mode the first URP frame comes back unlit/flat; warm up, then keep the second.
            cam.Render();
            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
