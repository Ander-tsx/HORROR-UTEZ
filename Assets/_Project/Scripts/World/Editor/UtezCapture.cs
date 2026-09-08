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
            if (Physics.Raycast(player.transform.position + Vector3.up * 5f, Vector3.down, out var hit, 50f))
                player.transform.position = hit.point;

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
