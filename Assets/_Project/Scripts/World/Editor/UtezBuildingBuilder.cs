using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Raises the campus structures onto the footprints laid down by
    /// <see cref="UtezTerrainBuilder"/>: four walls and a roof per building, a doorway
    /// cut into whichever wall carries the entrance, and pillars instead of walls for
    /// the covered walkway.
    ///
    /// Hollow shells, not solid blocks — the point of this pass is to be able to walk in
    /// and judge whether the interior volume actually fits the planned content.
    /// Regenerate after changing anything in <see cref="UtezDimensions"/>.
    /// </summary>
    public static class UtezBuildingBuilder
    {
        private const string RootName = "UTEZ_Buildings";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";

        [MenuItem("HORROR-UTEZ/Build UTEZ Buildings")]
        public static void Build()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build UTEZ Buildings");

            var red = LoadMaterial("UTEZ_WallRed");
            var beige = LoadMaterial("UTEZ_WallBeige");
            var roof = LoadMaterial("UTEZ_Roof");

            foreach (var (name, fp) in UtezDimensions.All)
            {
                // CECADEC is the oxblood block in the entrance photo; the rest read pale.
                var wall = name == "CECADEC" ? red : beige;

                if (fp.HasWalls)
                    BuildShell(root.transform, name, fp, wall, roof);
                else
                    BuildCanopy(root.transform, name, fp, beige, roof);
            }

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[UTEZ] Buildings built at {UtezDimensions.WorldScale}x scale. Root: {RootName}");
        }

        private static void BuildShell(Transform parent, string name, UtezDimensions.Footprint fp,
            Material wall, Material roof)
        {
            var group = MakeGroup(parent, name, fp);

            Vector2 size = fp.ScaledSize;
            float height = fp.ScaledHeight;

            foreach (var side in new[]
                     {
                         UtezDimensions.Side.North, UtezDimensions.Side.South,
                         UtezDimensions.Side.East, UtezDimensions.Side.West,
                     })
            {
                BuildWall(group, side, size, height, side == fp.Entrance, wall);
            }

            float roofThickness = UtezDimensions.RoofThickness;
            Slab(group, "Roof",
                new Vector3(0f, height + roofThickness * 0.5f, 0f),
                new Vector3(size.x, roofThickness, size.y), roof);
        }

        /// <summary>
        /// One wall, optionally split into two jambs and a lintel around a doorway.
        /// North/South run along X; East/West run along Z, inset so the corners meet
        /// without overlapping.
        /// </summary>
        private static void BuildWall(Transform group, UtezDimensions.Side side, Vector2 size,
            float height, bool withDoor, Material material)
        {
            float thickness = UtezDimensions.WallThickness;
            bool alongX = side is UtezDimensions.Side.North or UtezDimensions.Side.South;
            float sign = side is UtezDimensions.Side.North or UtezDimensions.Side.East ? 1f : -1f;

            float length = alongX ? size.x : size.y - 2f * thickness;
            float offset = (alongX ? size.y : size.x) * 0.5f - thickness * 0.5f;

            Vector3 Place(float along, float y) => alongX
                ? new Vector3(along, y, sign * offset)
                : new Vector3(sign * offset, y, along);

            Vector3 Extent(float span, float tall) => alongX
                ? new Vector3(span, tall, thickness)
                : new Vector3(thickness, tall, span);

            string name = $"Wall_{side}";

            if (!withDoor)
            {
                Slab(group, name, Place(0f, height * 0.5f), Extent(length, height), material);
                return;
            }

            float doorWidth = UtezDimensions.DoorWidth * UtezDimensions.WorldScale;
            float doorHeight = UtezDimensions.DoorHeight * UtezDimensions.WorldScale;
            float jamb = (length - doorWidth) * 0.5f;

            Slab(group, name + "_JambA",
                Place(-(doorWidth + jamb) * 0.5f, height * 0.5f), Extent(jamb, height), material);
            Slab(group, name + "_JambB",
                Place((doorWidth + jamb) * 0.5f, height * 0.5f), Extent(jamb, height), material);
            Slab(group, name + "_Lintel",
                Place(0f, doorHeight + (height - doorHeight) * 0.5f),
                Extent(doorWidth, height - doorHeight), material);
        }

        /// <summary>Roof on four corner pillars. No walls: you see and walk straight through.</summary>
        private static void BuildCanopy(Transform parent, string name, UtezDimensions.Footprint fp,
            Material pillar, Material roof)
        {
            var group = MakeGroup(parent, name, fp);

            Vector2 size = fp.ScaledSize;
            float height = fp.ScaledHeight;
            float thickness = UtezDimensions.PillarThickness;

            float x = size.x * 0.5f - thickness * 0.5f;
            float z = size.y * 0.5f - thickness * 0.5f;

            int index = 0;
            foreach (float sx in new[] { -x, x })
            foreach (float sz in new[] { -z, z })
            {
                Slab(group, $"Pillar_{index++}", new Vector3(sx, height * 0.5f, sz),
                    new Vector3(thickness, height, thickness), pillar);
            }

            float roofThickness = UtezDimensions.RoofThickness;
            Slab(group, "Roof",
                new Vector3(0f, height + roofThickness * 0.5f, 0f),
                new Vector3(size.x, roofThickness, size.y), roof);
        }

        private static Transform MakeGroup(Transform parent, string name, UtezDimensions.Footprint fp)
        {
            var group = new GameObject($"{name}_Structure").transform;
            group.SetParent(parent, false);
            group.localPosition = new Vector3(fp.ScaledCenter.x, 0f, fp.ScaledCenter.y);
            group.localRotation = Quaternion.Euler(0f, fp.YawDeg, 0f);
            return group;
        }

        private static void Slab(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material LoadMaterial(string name)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
            if (mat != null)
                return mat;

            Debug.LogWarning($"[UTEZ] Missing material '{name}'. Run HORROR-UTEZ > Rebuild PSX Terrain Materials.");
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { name = name };
        }
    }
}
