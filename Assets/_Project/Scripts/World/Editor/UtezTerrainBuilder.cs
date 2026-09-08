using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Procedurally (re)builds the Phase 0 ground for the UTEZ campus:
    /// concrete explanada, building footprint pads, grass bands, stone kerbs,
    /// an exterior dirt plane and a coarse boundary ring.
    ///
    /// Not a rigid greybox — real, editable geometry driven by
    /// <see cref="UtezDimensions"/>. Change a number there and run this again.
    /// Materials are flat placeholders; PSX materials come later.
    /// </summary>
    public static class UtezTerrainBuilder
    {
        private const string RootName = "UTEZ_Terrain";

        // Real metres; scaled with the rest of the world so the layering never inverts.
        private static float SlabThickness => 0.2f * UtezDimensions.WorldScale;
        private static float PadLift => 0.05f * UtezDimensions.WorldScale;   // pad just above the slab
        private static float GrassLift => 0.03f * UtezDimensions.WorldScale; // grass just above the slab
        private static float DirtDrop => 0.1f * UtezDimensions.WorldScale;   // dirt just below the slab

        [MenuItem("HORROR-UTEZ/Build UTEZ Terrain")]
        public static void Build()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build UTEZ Terrain");

            var concrete = MakeMaterial("UTEZ_Concrete", new Color(0.62f, 0.62f, 0.60f));
            var grass = MakeMaterial("UTEZ_Grass", new Color(0.20f, 0.28f, 0.13f));
            var dirt = MakeMaterial("UTEZ_Dirt", new Color(0.36f, 0.28f, 0.19f));
            var stone = MakeMaterial("UTEZ_Stone", new Color(0.45f, 0.43f, 0.40f));
            var pad = MakeMaterial("UTEZ_Pad", new Color(0.70f, 0.70f, 0.70f));
            var forest = MakeMaterial("UTEZ_ForestMarker", new Color(0.10f, 0.16f, 0.10f));

            // Exterior dirt. Its top face sits DirtDrop below the slab's; sharing a Y with
            // the slab would z-fight and the two would flicker over each other.
            var d = UtezDimensions.DirtSize;
            Slab(root.transform, "Dirt_Ground",
                center: new Vector3(UtezDimensions.SlabCenter.x, -(DirtDrop + SlabThickness * 0.5f), UtezDimensions.SlabCenter.y),
                size: new Vector3(d.x, SlabThickness, d.y), material: dirt);

            // Concrete explanada.
            Slab(root.transform, "Concrete_Slab",
                center: new Vector3(UtezDimensions.SlabCenter.x, -SlabThickness * 0.5f, UtezDimensions.SlabCenter.y),
                size: new Vector3(UtezDimensions.SlabSize.x, SlabThickness, UtezDimensions.SlabSize.y),
                material: concrete);

            foreach (var (name, fp) in UtezDimensions.All)
                BuildBuilding(root.transform, name, fp, grass, stone, pad);

            BuildBoundaryRing(root.transform, forest);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log($"[UTEZ] Terrain built into scene '{scene.name}'. Root: {RootName}");
        }

        /// <summary>Footprint pad, plus a grass band and stone kerb loop when the building has one.</summary>
        private static void BuildBuilding(Transform parent, string name, UtezDimensions.Footprint fp,
            Material grass, Material stone, Material pad)
        {
            var group = new GameObject($"{name}_Group").transform;
            group.SetParent(parent, false);
            group.localPosition = new Vector3(fp.ScaledCenter.x, 0f, fp.ScaledCenter.y);
            group.localRotation = Quaternion.Euler(0f, fp.YawDeg, 0f);

            if (fp.HasGrass)
            {
                var grassSize = fp.ScaledSize + 2f * UtezDimensions.GrassMargin * Vector2.one;
                Slab(group, "Grass",
                    center: new Vector3(0f, GrassLift * 0.5f, 0f),
                    size: new Vector3(grassSize.x, GrassLift, grassSize.y), material: grass);
                BuildKerbLoop(group, grassSize, stone);
            }

            Slab(group, "Pad",
                center: new Vector3(0f, PadLift * 0.5f, 0f),
                size: new Vector3(fp.ScaledSize.x, PadLift, fp.ScaledSize.y), material: pad);
        }

        /// <summary>Four thin boxes forming a rectangle at the outer edge of the grass band.</summary>
        private static void BuildKerbLoop(Transform group, Vector2 outer, Material stone)
        {
            float h = UtezDimensions.KerbHeight;
            float w = UtezDimensions.KerbWidth;
            float hx = outer.x * 0.5f;
            float hz = outer.y * 0.5f;

            Slab(group, "Kerb_North", new Vector3(0f, h * 0.5f, hz), new Vector3(outer.x + w, h, w), stone);
            Slab(group, "Kerb_South", new Vector3(0f, h * 0.5f, -hz), new Vector3(outer.x + w, h, w), stone);
            Slab(group, "Kerb_East", new Vector3(hx, h * 0.5f, 0f), new Vector3(w, h, outer.y - w), stone);
            Slab(group, "Kerb_West", new Vector3(-hx, h * 0.5f, 0f), new Vector3(w, h, outer.y - w), stone);
        }

        /// <summary>
        /// Invisible wall at the dirt edge. The real tree line is scattered geometry from
        /// UtezForestBuilder; these boxes only stop the player walking out of the world.
        /// Renderers are disabled rather than deleted so the boundary stays selectable and
        /// obvious in the editor.
        /// </summary>
        private static void BuildBoundaryRing(Transform parent, Material forest)
        {
            var group = new GameObject("Boundary").transform;
            group.SetParent(parent, false);
            group.localPosition = new Vector3(UtezDimensions.SlabCenter.x, 0f, UtezDimensions.SlabCenter.y);

            var d = UtezDimensions.DirtSize;
            float t = UtezDimensions.ForestRingWidth;
            float h = 8f * UtezDimensions.WorldScale; // tree line height, blocks the view outward
            float hx = d.x * 0.5f;
            float hz = d.y * 0.5f;

            Slab(group, "Boundary_North", new Vector3(0f, h * 0.5f, hz), new Vector3(d.x + t, h, t), forest);
            Slab(group, "Boundary_South", new Vector3(0f, h * 0.5f, -hz), new Vector3(d.x + t, h, t), forest);
            Slab(group, "Boundary_East", new Vector3(hx, h * 0.5f, 0f), new Vector3(t, h, d.y + t), forest);
            Slab(group, "Boundary_West", new Vector3(-hx, h * 0.5f, 0f), new Vector3(t, h, d.y + t), forest);

            foreach (var renderer in group.GetComponentsInChildren<MeshRenderer>())
                renderer.enabled = false;
        }

        /// <summary>A scaled cube (BoxCollider included) parented in local space.</summary>
        private static void Slab(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            // Terrain and structures never move; static batching is free performance and
            // occlusion flags let Unity cull whole buildings at once.
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);
        }

        /// <summary>
        /// Loads a PSX material asset generated by PsxTerrainMaterials. Falls back to a
        /// plain lit material so the terrain still builds if the PSX assets are missing.
        /// </summary>
        private static Material MakeMaterial(string name, Color fallbackColor)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Art/Materials/{name}.mat");
            if (mat != null)
                return mat;

            Debug.LogWarning($"[UTEZ] Missing PSX material '{name}'. Run HORROR-UTEZ > Rebuild PSX Terrain Materials.");
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var fallback = new Material(shader) { name = name };
            fallback.SetColor("_BaseColor", fallbackColor);
            fallback.SetColor("_Color", fallbackColor);
            return fallback;
        }
    }
}
