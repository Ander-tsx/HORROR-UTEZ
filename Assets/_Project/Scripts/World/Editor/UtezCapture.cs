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
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
