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
            var glass = LoadMaterial("UTEZ_Glass");

            foreach (var (name, fp) in UtezDimensions.All)
            {
                // CECADEC is the oxblood block in the entrance photo; the rest read pale.
                var wall = name == "CECADEC" ? red : beige;

                if (fp.HasWalls)
                    BuildShell(root.transform, name, fp, wall, beige, roof, glass);
                else
                    BuildCanopy(root.transform, name, fp, beige, roof);
            }

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[UTEZ] Buildings built at {UtezDimensions.WorldScale}x scale. Root: {RootName}");
        }

        private static void BuildShell(Transform parent, string name, UtezDimensions.Footprint fp,
            Material wall, Material trim, Material roof, Material glass)
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
                BuildWall(group, side, size, height, side == fp.Entrance, wall, trim);
            }

            BuildInteriorFloors(group, fp, size, roof, trim);
            BuildReverbZone(group, fp, size, height);
            BuildWindowBands(group, fp, size, glass);

            float roofThickness = UtezDimensions.RoofThickness;
            Slab(group, "Roof",
                new Vector3(0f, height + roofThickness * 0.5f, 0f),
                new Vector3(size.x, roofThickness, size.y), roof);
        }

        /// <summary>
        /// Bare concrete rooms this size ring. A reverb zone sized to the interior means
        /// footsteps and the flashlight click change character the moment you step inside,
        /// which does more to sell "indoors" than any amount of geometry.
        /// </summary>
        private static void BuildReverbZone(Transform group, UtezDimensions.Footprint fp,
            Vector2 size, float height)
        {
            var go = new GameObject("ReverbZone");
            go.transform.SetParent(group, false);
            go.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

            var zone = go.AddComponent<AudioReverbZone>();
            zone.reverbPreset = AudioReverbPreset.Hallway;
            // Min radius fills the building; max gives a short blend at the doorway rather
            // than a hard switch as the player crosses the threshold.
            zone.minDistance = Mathf.Max(size.x, size.y) * 0.5f;
            zone.maxDistance = zone.minDistance + 6f * UtezDimensions.WorldScale;
        }

        /// <summary>
        /// Upper floors, each with a stairwell opening and a flight of steps up to it.
        ///
        /// The slab is built as four rectangles around the opening rather than one slab
        /// with a hole, because a hole needs real mesh work while four boxes need none —
        /// and the player only ever sees the edges.
        /// </summary>
        private static void BuildInteriorFloors(Transform group, UtezDimensions.Footprint fp,
            Vector2 size, Material floorMaterial, Material stepMaterial)
        {
            if (fp.Floors < 2)
                return;

            float scale = UtezDimensions.WorldScale;
            float storey = UtezDimensions.FloorHeight * scale;
            float thickness = 0.35f * scale;

            // Stairwell parked in the north-east corner, clear of the entrance.
            float wellX = 9f * scale;
            float wellZ = 12f * scale;
            float wellCenterX = size.x * 0.5f - wellX * 0.5f - 1f * scale;
            float wellCenterZ = size.y * 0.5f - wellZ * 0.5f - 1f * scale;

            for (int floor = 1; floor < fp.Floors; floor++)
            {
                float y = storey * floor;
                var slabGroup = new GameObject($"Floor_{floor}").transform;
                slabGroup.SetParent(group, false);

                float wellMinX = wellCenterX - wellX * 0.5f;
                float wellMaxX = wellCenterX + wellX * 0.5f;
                float wellMinZ = wellCenterZ - wellZ * 0.5f;
                float wellMaxZ = wellCenterZ + wellZ * 0.5f;

                float halfX = size.x * 0.5f;
                float halfZ = size.y * 0.5f;

                // West of the well, east of it, then the two strips north and south.
                AddFloorPiece(slabGroup, "Slab_W", -halfX, wellMinX, -halfZ, halfZ, y, thickness, floorMaterial);
                AddFloorPiece(slabGroup, "Slab_E", wellMaxX, halfX, -halfZ, halfZ, y, thickness, floorMaterial);
                AddFloorPiece(slabGroup, "Slab_S", wellMinX, wellMaxX, -halfZ, wellMinZ, y, thickness, floorMaterial);
                AddFloorPiece(slabGroup, "Slab_N", wellMinX, wellMaxX, wellMaxZ, halfZ, y, thickness, floorMaterial);

                BuildStairs(slabGroup, new Vector2(wellCenterX, wellMinZ), storey * (floor - 1), storey,
                    wellX, wellZ, stepMaterial);
            }
        }

        private static void AddFloorPiece(Transform parent, string name, float minX, float maxX,
            float minZ, float maxZ, float y, float thickness, Material material)
        {
            float width = maxX - minX;
            float depth = maxZ - minZ;
            if (width <= 0.01f || depth <= 0.01f)
                return;

            Slab(parent, name,
                new Vector3((minX + maxX) * 0.5f, y - thickness * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(width, thickness, depth), material);
        }

        /// <summary>A straight flight rising into the stairwell opening.</summary>
        private static void BuildStairs(Transform parent, Vector2 footOfStairs, float baseY,
            float rise, float width, float run, Material material)
        {
            const int steps = 14;
            float stepRise = rise / steps;
            float stepRun = run / steps;

            var stairs = new GameObject("Stairs").transform;
            stairs.SetParent(parent, false);

            for (int i = 0; i < steps; i++)
            {
                float y = baseY + stepRise * (i + 0.5f);
                float z = footOfStairs.y + stepRun * (i + 0.5f);
                // Each step is a solid block down to the floor, so there is no gap to fall
                // through and the CharacterController climbs it without extra ramp geometry.
                Slab(stairs, $"Step_{i}",
                    new Vector3(footOfStairs.x, baseY + stepRise * (i + 1) * 0.5f, z),
                    new Vector3(width * 0.7f, stepRise * (i + 1), stepRun), material);
            }
        }

        /// <summary>
        /// Bands of dark glazing at each storey.
        ///
        /// These sit proud of the wall rather than being cut through it: a real opening
        /// needs the wall split into pieces, and at PSX resolution an inset dark panel
        /// reads as a window from any distance the player will ever see it from.
        /// </summary>
        private static void BuildWindowBands(Transform group, UtezDimensions.Footprint fp,
            Vector2 size, Material glass)
        {
            float scale = UtezDimensions.WorldScale;
            float storey = UtezDimensions.FloorHeight * scale;
            float thickness = UtezDimensions.WallThickness;
            float bandHeight = 1.6f * scale;
            // Glazing depth, and how far its centre sits from the wall's OUTER face. Offset
            // from the face, not from the wall centre: the wall is thicker than the offset,
            // so measuring from the centre buries the whole band inside the masonry.
            float depth = thickness * 0.5f;
            float faceOffset = depth * 0.4f;

            var windows = new GameObject("Windows").transform;
            windows.SetParent(group, false);

            for (int floor = 0; floor < fp.Floors; floor++)
            {
                float y = storey * floor + storey * 0.55f;

                float inset = 3f * scale;

                // The entrance facade stays blank: CECADEC's north wall in the reference
                // photo is unbroken red apart from the doorway itself.
                if (fp.Entrance != UtezDimensions.Side.North)
                    Slab(windows, $"Win_N_{floor}",
                        new Vector3(0f, y, size.y * 0.5f - faceOffset),
                        new Vector3(size.x - inset * 2f, bandHeight, depth), glass);

                if (fp.Entrance != UtezDimensions.Side.South)
                    Slab(windows, $"Win_S_{floor}",
                        new Vector3(0f, y, -(size.y * 0.5f - faceOffset)),
                        new Vector3(size.x - inset * 2f, bandHeight, depth), glass);

                if (fp.Entrance != UtezDimensions.Side.East)
                    Slab(windows, $"Win_E_{floor}",
                        new Vector3(size.x * 0.5f - faceOffset, y, 0f),
                        new Vector3(depth, bandHeight, size.y - inset * 2f), glass);

                if (fp.Entrance != UtezDimensions.Side.West)
                    Slab(windows, $"Win_W_{floor}",
                        new Vector3(-(size.x * 0.5f - faceOffset), y, 0f),
                        new Vector3(depth, bandHeight, size.y - inset * 2f), glass);
            }

            foreach (var collider in windows.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);
        }

        /// <summary>
        /// One wall, optionally split into two jambs and a lintel around a doorway.
        /// North/South run along X; East/West run along Z, inset so the corners meet
        /// without overlapping.
        /// </summary>
        private static void BuildWall(Transform group, UtezDimensions.Side side, Vector2 size,
            float height, bool withDoor, Material material, Material trim)
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

            // The pale pilasters flanking the entrance are what make CECADEC recognisable
            // in the photo, so they are structure here rather than a later decal.
            float scale = UtezDimensions.WorldScale;
            float pilasterWidth = 1.5f * scale;
            float proud = 0.35f * scale;
            float pilasterOffset = (doorWidth + pilasterWidth) * 0.5f;

            Vector3 Proud(float along) => alongX
                ? new Vector3(along, height * 0.5f, sign * (offset + proud))
                : new Vector3(sign * (offset + proud), height * 0.5f, along);
            Vector3 ProudExtent() => alongX
                ? new Vector3(pilasterWidth, height, thickness + proud)
                : new Vector3(thickness + proud, height, pilasterWidth);

            Slab(group, name + "_PilasterA", Proud(-pilasterOffset), ProudExtent(), trim);
            Slab(group, name + "_PilasterB", Proud(pilasterOffset), ProudExtent(), trim);
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

            // Terrain and structures never move; static batching is free performance and
            // occlusion flags let Unity cull whole buildings at once.
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);
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
