using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Counts what the `utez` scene actually costs.
    ///
    /// The mobile budgets in docs/technical/psx-style-guide.md are only worth anything if
    /// something checks them. This reports the totals that matter — renderers, triangles,
    /// realtime lights, colliders, audio sources — and flags the ones over budget, so a
    /// scatter pass that quietly triples the tree count gets caught here rather than on a
    /// phone.
    ///
    /// Scene totals, not per-frame: culling and batching mean the rendered figure is lower.
    /// Treat these as an upper bound.
    /// </summary>
    public static class UtezSceneStats
    {
        // From the mobile-first budgets in the style guide.
        private const int TriangleBudget = 300000;
        private const int RealtimeLightBudget = 24;

        private const string ScenePath = "Assets/_Project/Scenes/utez.unity";

        /// <summary>
        /// Triangles from the submesh descriptors rather than from mesh.triangles.
        ///
        /// Imported props are marked non-readable to keep a second copy of every mesh out
        /// of memory on mobile, and touching mesh.triangles on one throws. Index counts are
        /// metadata and stay available either way.
        /// </summary>
        private static long TriangleCount(Mesh mesh)
        {
            long total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    total += mesh.GetIndexCount(i) / 3;
            }
            return total;
        }

        [MenuItem("HORROR-UTEZ/Report Scene Stats")]
        public static void Report()
        {
            // In batch mode nothing is loaded by default; measuring an empty scene reports
            // a comfortable zero for everything, which is worse than not measuring at all.
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);

            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);

            long triangles = 0;
            int batchingStatic = 0;
            var materials = new HashSet<Material>();

            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    triangles += TriangleCount(filter.sharedMesh);

                foreach (var material in renderer.sharedMaterials)
                    if (material != null)
                        materials.Add(material);

                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) != 0)
                    batchingStatic++;
            }

            int realtimeLights = 0;
            foreach (var light in lights)
                if (light.enabled && light.lightmapBakeType != LightmapBakeType.Baked)
                    realtimeLights++;

            Debug.Log(
                $"[STATS] renderers {renderers.Length} ({batchingStatic} batching-static)\n" +
                $"[STATS] triangles {triangles:N0} (budget {TriangleBudget:N0})\n" +
                $"[STATS] unique materials {materials.Count}\n" +
                $"[STATS] lights {lights.Length}, realtime {realtimeLights} (budget {RealtimeLightBudget})\n" +
                $"[STATS] colliders {colliders.Length}\n" +
                $"[STATS] audio sources {sources.Length}, particle systems {particles.Length}");

            if (triangles > TriangleBudget)
                Debug.LogWarning($"[STATS] OVER TRIANGLE BUDGET by {triangles - TriangleBudget:N0}.");
            if (realtimeLights > RealtimeLightBudget)
                Debug.LogWarning($"[STATS] OVER LIGHT BUDGET by {realtimeLights - RealtimeLightBudget}.");

            int notStatic = renderers.Length - batchingStatic;
            if (notStatic > 200)
                Debug.LogWarning(
                    $"[STATS] {notStatic} renderers are not batching-static. Static batching is the " +
                    "cheapest win available for scenery that never moves.");
        }

        public static void ReportBatch()
        {
            Report();
            EditorApplication.Exit(0);
        }
    }
}
