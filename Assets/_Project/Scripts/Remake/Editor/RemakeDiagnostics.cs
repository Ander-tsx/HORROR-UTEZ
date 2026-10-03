using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorUtez.Remake.Editor
{
    // Batch-mode probes used while authoring runtime dressing: logs where landmark pieces really are.
    public static class RemakeDiagnostics
    {
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
