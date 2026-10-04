using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorUtez.Remake.Editor
{
    // Batch-mode probes used while authoring runtime dressing: logs where landmark pieces really are.
    public static class RemakeDiagnostics
    {
        // Lists every renderer/collider around CECADEC's east doorway (interior-local x 7..15, z -21..-30).
        public static void ProbeEastDoor()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/remake.unity");
            Transform interior = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "CECADEC_Interior" && t.gameObject.scene.IsValid());
            if (interior == null) { Debug.Log("[Probe] no interior"); return; }
            Vector3 a = interior.TransformPoint(new Vector3(8.5f, 0, -27.5f)), b = interior.TransformPoint(new Vector3(16, 4.5f, -35.5f));
            var box = new Bounds((a + b) / 2, Vector3.zero); box.Encapsulate(a); box.Encapsulate(b);
            box.Expand(new Vector3(0.2f, 0, 0.2f));
            Debug.Log("[Probe] east door box " + box);
            string Path(Transform t) { string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                if (r.bounds.Intersects(box)) Debug.Log("[Probe] R " + Path(r.transform) + " bounds " + r.bounds.center.ToString("F2") + " size " + r.bounds.size.ToString("F2")
                    + " local " + interior.InverseTransformPoint(r.bounds.center).ToString("F2") + " active " + r.gameObject.activeInHierarchy);
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
                if (c.bounds.Intersects(box)) Debug.Log("[Probe] C " + c.GetType().Name + " " + Path(c.transform) + " bounds " + c.bounds.center.ToString("F2") + " size " + c.bounds.size.ToString("F2")
                    + " local " + interior.InverseTransformPoint(c.bounds.center).ToString("F2") + " enabled " + c.enabled);
        }
        public static void Probe()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/remake.unity");
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Debug.Log("[Probe] ROOT " + root.name);
                foreach (Transform child in root.transform)
                {
                    Debug.Log("[Probe]   " + child.name + " pos " + child.position + " rot " + child.eulerAngles + " scale " + child.lossyScale);
                    foreach (Transform grand in child) Debug.Log("[Probe]     " + grand.name + " pos " + grand.position + " rot " + grand.eulerAngles);
                }
            }
            foreach (string name in new[] { "CECADEC_Interior", "CECADEC_Upper" })
            {
                GameObject interior = GameObject.Find(name) ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == name && g.scene.IsValid());
                if (interior == null) { Debug.Log("[Probe] missing " + name); continue; }
                Transform t = interior.transform;
                Debug.Log("[Probe] " + name + " pos " + t.position + " rot " + t.eulerAngles + " scale " + t.lossyScale);
                foreach (Renderer r in interior.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.gameObject.name;
                    if (!(n.StartsWith("Pillar_") || n.StartsWith("Column_") || n.StartsWith("Room_") || n.StartsWith("Floor_Rooms")
                        || n.StartsWith("Floor_Back") || n.Contains("Front") || n.StartsWith("Aula") || n.StartsWith("CC9")
                        || n.StartsWith("Lab") || n.StartsWith("Room") || n.StartsWith("Lining") || n.StartsWith("Electrical"))) continue;
                    Bounds b = r.bounds;
                    Debug.Log("[Probe]   " + n + " world " + b.center.ToString("F2") + " size " + b.size.ToString("F2")
                        + " local " + t.InverseTransformPoint(b.center).ToString("F2"));
                }
            }
        }
    }
}
