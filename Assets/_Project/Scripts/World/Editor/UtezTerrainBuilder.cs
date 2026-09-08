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
        private const float SlabThickness = 0.2f;
        private const float PadLift = 0.05f;   // pad sits just above the slab
        private const float GrassLift = 0.03f; // grass just above the slab
        private const float DirtDrop = 0.1f;   // dirt just below the slab

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

            // Exterior dirt, well below the slab so it reads as the base ground.
            var d = UtezDimensions.DirtSize;
            Slab(root.transform, "Dirt_Ground",
                center: new Vector3(UtezDimensions.SlabCenter.x, -DirtDrop, UtezDimensions.SlabCenter.y),
                size: new Vector3(d.x, SlabThickness, d.y), material: dirt);

            // Concrete explanada.
            Slab(root.transform, "Concrete_Slab",
                center: new Vector3(UtezDimensions.SlabCenter.x, -SlabThickness * 0.5f, UtezDimensions.SlabCenter.y),
                size: new Vector3(UtezDimensions.SlabSize.x, SlabThickness, UtezDimensions.SlabSize.y),
                material: concrete);

            BuildBuilding(root.transform, "CECADEC", UtezDimensions.Cecadec, grass, stone, pad);
            BuildBuilding(root.transform, "CDS_Body", UtezDimensions.CdsBody, grass, stone, pad);
            BuildBuilding(root.transform, "CDS_Extension", UtezDimensions.CdsExtension, grass, stone, pad);

            BuildBoundaryRing(root.transform, forest);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log($"[UTEZ] Terrain built into scene '{scene.name}'. Root: {RootName}");
        }

        /// <summary>Grass band + stone kerb loop + footprint pad, grouped and yawed.</summary>
        private static void BuildBuilding(Transform parent, string name, UtezDimensions.Footprint fp,
            Material grass, Material stone, Material pad)
        {
            var group = new GameObject($"{name}_Group").transform;
            group.SetParent(parent, false);
            group.localPosition = new Vector3(fp.Center.x, 0f, fp.Center.y);
            group.localRotation = Quaternion.Euler(0f, fp.YawDeg, 0f);

            float m = UtezDimensions.GrassMargin;
            var grassSize = fp.Size + 2f * m * Vector2.one;

            Slab(group, "Grass",
                center: new Vector3(0f, GrassLift * 0.5f, 0f),
                size: new Vector3(grassSize.x, GrassLift, grassSize.y), material: grass);

            Slab(group, "Pad",
                center: new Vector3(0f, PadLift * 0.5f, 0f),
                size: new Vector3(fp.Size.x, PadLift, fp.Size.y), material: pad);

            BuildKerbLoop(group, grassSize, stone);
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

        /// <summary>Coarse tree-line ring at the dirt edge: a visible + collidable map boundary.</summary>
        private static void BuildBoundaryRing(Transform parent, Material forest)
        {
            var group = new GameObject("Boundary").transform;
            group.SetParent(parent, false);
            group.localPosition = new Vector3(UtezDimensions.SlabCenter.x, 0f, UtezDimensions.SlabCenter.y);

            var d = UtezDimensions.DirtSize;
            float t = UtezDimensions.ForestRingWidth;
            float h = 8f; // tree-line height, blocks the view outward
            float hx = d.x * 0.5f;
            float hz = d.y * 0.5f;

            Slab(group, "Treeline_North", new Vector3(0f, h * 0.5f, hz), new Vector3(d.x + t, h, t), forest);
            Slab(group, "Treeline_South", new Vector3(0f, h * 0.5f, -hz), new Vector3(d.x + t, h, t), forest);
            Slab(group, "Treeline_East", new Vector3(hx, h * 0.5f, 0f), new Vector3(t, h, d.y + t), forest);
            Slab(group, "Treeline_West", new Vector3(-hx, h * 0.5f, 0f), new Vector3(t, h, d.y + t), forest);
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
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NothingStatic);
        }

        private static Material MakeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color); // Standard fallback
            return mat;
        }
    }
}
