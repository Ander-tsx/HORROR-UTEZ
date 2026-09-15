using System.Collections.Generic;
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
    /// Every surface here is tiled or scaled from a reusable piece in <see cref="UtezKit"/>
    /// — wall, floor, pilaster, window, roof, canopy pillar. Swap a piece's mesh for a
    /// modelled one and every building takes it. The stairs are the one exception, still a
    /// stack of primitives: they are load-bearing for the CharacterController and want a
    /// separate collision mesh before they move to the kit.
    ///
    /// Hollow shells, not solid blocks — the point of this pass is to be able to walk in
    /// and judge whether the interior volume actually fits the planned content.
    /// Regenerate after changing anything in <see cref="UtezDimensions"/> or <see cref="UtezKit"/>.
    /// </summary>
    public static class UtezBuildingBuilder
    {
        private const string RootName = "UTEZ_Buildings";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";

        /// <summary>
        /// Whether upper storeys get a stairwell opening and a flight of steps. Off: the
        /// interiors are being blocked out ground-floor first, so a sealed slab between
        /// levels is what that wants (the upper floor is unreachable without noclip until
        /// vertical circulation is designed). Set true and rebuild to bring the stairs back.
        /// </summary>
        private static readonly bool Stairwells = false;

        /// <summary>The kit prefabs this pass instantiates, loaded once per build.</summary>
        private readonly struct Pieces
        {
            public readonly GameObject Wall, Floor, Pilaster, Window, Roof, Pillar;

            public Pieces(GameObject wall, GameObject floor, GameObject pilaster,
                GameObject window, GameObject roof, GameObject pillar)
            {
                Wall = wall;
                Floor = floor;
                Pilaster = pilaster;
                Window = window;
                Roof = roof;
                Pillar = pillar;
            }
        }

        [MenuItem("HORROR-UTEZ/Build UTEZ Buildings")]
        public static void Build()
        {
            if (!UtezProjectSetup.GuardWorkingScene(RootName))
                return;

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build UTEZ Buildings");

            // Steps are still primitives; concrete keeps them reading like the pad.
            var concrete = LoadMaterial("UTEZ_Concrete");

            // Kit walls take the Kit_* materials, tiled at (1,1) to match the pieces'
            // per-metre UVs. The terrain UTEZ_Wall* materials bake a (6,3) tiling for
            // stretched cubes and turn to noise on a per-metre mesh. Every other piece
            // rides on the material already on its prefab.
            var kitWall = LoadKitMaterial(UtezKit.WallMaterial);
            var kitWallRed = LoadKitMaterial(UtezKit.WallRedMaterial);

            var pieces = new Pieces(
                LoadKit(UtezKit.WallPrefab), LoadKit(UtezKit.FloorPrefab),
                LoadKit(UtezKit.PilasterPrefab), LoadKit(UtezKit.WindowPrefab),
                LoadKit(UtezKit.RoofPrefab), LoadKit(UtezKit.PillarPrefab));

            foreach (var (name, fp) in UtezDimensions.All)
            {
                // CECADEC is the oxblood block in the entrance photo; the rest read pale.
                var wall = name == "CECADEC" ? kitWallRed : kitWall;

                if (fp.HasWalls)
                    BuildShell(root.transform, name, fp, pieces, wall, concrete, EntranceFor(name));
                else
                    BuildCanopy(root.transform, name, fp, pieces);
            }

            foreach (var (name, fp) in UtezDimensions.All)
            {
                var landmark = GameObject.Find($"{RootName}/{name}_Structure/Wall_{fp.Entrance}_Entrance");
                if (landmark != null)
                    HideCoveredGround(landmark.transform);
            }

            if (LoadKit(UtezKit.CecadecInteriorPrefab) != null)
                PlaceCecadecInteriorInOpenScene();
            HideKitPiecesUnderCecadecLandmark();

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[UTEZ] Buildings built at {UtezDimensions.WorldScale}x scale. Root: {RootName}");
        }

        private static void BuildShell(Transform parent, string name, UtezDimensions.Footprint fp,
            Pieces p, Material wallMat, Material stairMat, GameObject entrance)
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
                BuildWall(group, p, side, fp, side == fp.Entrance, wallMat, entrance);
            }

            BuildInteriorFloors(group, p.Floor, fp, size, stairMat);
            BuildReverbZone(group, fp, size, height);
            BuildWindowBands(group, p.Window, fp, size);

            // Roof cap, tiled and sitting on top of the walls.
            UtezKit.TileArea(group, p.Roof, null,
                -size.x * 0.5f, size.x * 0.5f, -size.y * 0.5f, size.y * 0.5f, height, "Roof");
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

        // ---- Walls --------------------------------------------------------------

        /// <summary>
        /// One wall, tiled from <see cref="UtezKit"/> wall pieces storey by storey. The
        /// entrance side splits its ground storey into two jambs and a lintel around the
        /// doorway; every other storey is a solid run. North/South run along X, East/West
        /// along Z, inset so the corners meet without overlapping.
        /// </summary>
        private static void BuildWall(Transform group, Pieces p, UtezDimensions.Side side,
            UtezDimensions.Footprint fp, bool withDoor, Material wallMat, GameObject entrance)
        {
            if (p.Wall == null)
                return;

            Vector2 size = fp.ScaledSize;
            float scale = UtezDimensions.WorldScale;
            float thickness = UtezDimensions.WallThickness;
            float storeyH = UtezDimensions.FloorHeight * scale;

            bool alongX = side is UtezDimensions.Side.North or UtezDimensions.Side.South;
            float sign = side is UtezDimensions.Side.North or UtezDimensions.Side.East ? 1f : -1f;

            float length = alongX ? size.x : size.y - 2f * thickness;
            float offset = (alongX ? size.y : size.x) * 0.5f - thickness * 0.5f;
            float half = length * 0.5f;

            // An along-axis coordinate (centred on 0) plus a base Y -> a local position in
            // the group. East/West pieces are turned 90 deg so their span runs along Z.
            Vector3 Place(float along, float y) => alongX
                ? new Vector3(along, y, sign * offset)
                : new Vector3(sign * offset, y, along);
            Quaternion rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

            // Y scale so one FloorHeight-tall piece fills a storey; Z scale to a wall thick.
            Vector2 crossYZ = new Vector2(storeyH / UtezDimensions.FloorHeight, scale);

            string baseName = $"Wall_{side}";

            // A landmark entrance brings its own, wider doorway, as wide as the corridor behind it.
            float doorW = (entrance != null ? UtezDimensions.LandmarkDoorWidth : UtezDimensions.DoorWidth) * scale;

            for (int floor = 0; floor < fp.Floors; floor++)
            {
                if (!withDoor || floor > 0)
                {
                    UtezKit.TileLinear(group, p.Wall, wallMat, Place, rot, -half, half, storeyH * floor,
                        crossYZ, $"{baseName}_F{floor}");
                    continue;
                }
                BuildDoorway(group, p.Wall, wallMat, side, fp, doorW);
            }

            if (!withDoor)
                return;

            if (entrance != null)
            {
                var (pos, facing) = EntrancePose(fp, side);
                UtezKit.Place(entrance, group, pos, facing, Vector3.one * scale, baseName + "_Entrance");
                if (alongX)
                    CutGlazing(group, p.Wall, baseName);
            }
            else
            {
                BuildEntrancePilasters(group, p.Pilaster, baseName, fp.ScaledHeight,
                    alongX, sign, offset, doorW);
            }
        }

        /// <summary>
        /// Ground-storey jambs and lintel around a doorway <paramref name="doorW"/> wide (world
        /// units), centred on the wall of <paramref name="side"/>. Named Wall_{side}_JambA/_JambB/_Lintel.
        /// </summary>
        private static void BuildDoorway(Transform group, GameObject wallPiece, Material wallMat,
            UtezDimensions.Side side, UtezDimensions.Footprint fp, float doorW)
        {
            Vector2 size = fp.ScaledSize;
            float scale = UtezDimensions.WorldScale;
            float thickness = UtezDimensions.WallThickness;
            float storeyH = UtezDimensions.FloorHeight * scale;
            bool alongX = side is UtezDimensions.Side.North or UtezDimensions.Side.South;
            float sign = side is UtezDimensions.Side.North or UtezDimensions.Side.East ? 1f : -1f;
            float length = alongX ? size.x : size.y - 2f * thickness;
            float offset = (alongX ? size.y : size.x) * 0.5f - thickness * 0.5f;
            float half = length * 0.5f;
            Vector3 Place(float along, float y) => alongX
                ? new Vector3(along, y, sign * offset)
                : new Vector3(sign * offset, y, along);
            Quaternion rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            var crossYZ = new Vector2(storeyH / UtezDimensions.FloorHeight, scale);
            float doorH = UtezDimensions.DoorHeight * scale;
            float jamb = (length - doorW) * 0.5f;
            string baseName = $"Wall_{side}";

            UtezKit.TileLinear(group, wallPiece, wallMat, Place, rot, -half, -half + jamb, 0f,
                crossYZ, $"{baseName}_JambA");
            UtezKit.TileLinear(group, wallPiece, wallMat, Place, rot, half - jamb, half, 0f,
                crossYZ, $"{baseName}_JambB");
            UtezKit.Place(wallPiece, group, Place(0f, doorH), rot,
                new Vector3(doorW / UtezKit.Module, (storeyH - doorH) / UtezDimensions.FloorHeight, scale),
                $"{baseName}_Lintel", wallMat);
        }

        /// <summary>
        /// Replaces the entrance wall's ground-storey jambs and lintel, in the OPEN scene, with
        /// ones cut for the landmark doorway width. Undoable. Returns the pieces removed.
        /// </summary>
        private static int RecutLandmarkDoorway(Transform group, UtezDimensions.Footprint fp)
        {
            string baseName = $"Wall_{fp.Entrance}";
            var old = new List<GameObject>();
            foreach (Transform t in group)
                if (t.name.StartsWith(baseName + "_JambA") || t.name.StartsWith(baseName + "_JambB") ||
                    t.name.StartsWith(baseName + "_Lintel"))
                    old.Add(t.gameObject);
            foreach (var go in old)
                Undo.DestroyObjectImmediate(go);

            var before = new HashSet<Transform>();
            foreach (Transform t in group)
                before.Add(t);
            BuildDoorway(group, LoadKit(UtezKit.WallPrefab), LoadKitMaterial(UtezKit.WallRedMaterial),
                fp.Entrance, fp, UtezDimensions.LandmarkDoorWidth * UtezDimensions.WorldScale);
            foreach (Transform t in group)
                if (!before.Contains(t))
                    Undo.RegisterCreatedObjectUndo(t.gameObject, "Recut CECADEC doorway");
            return old.Count;
        }

        /// <summary>
        /// The bespoke entrance piece for a building, or null for the generic kit pilasters.
        /// Landmark entrances have their origin at the doorway centre on the wall's outer
        /// face, +Z pointing out of the building.
        /// </summary>
        private static GameObject EntranceFor(string buildingName) =>
            buildingName == "CECADEC" ? LoadKit(UtezKit.CecadecNorthPrefab) : null;

        /// <summary>Glazed bay of a landmark entrance, real metres: half-width and height band.</summary>
        private const float GlazingHalfWidthRaw = UtezDimensions.LandmarkDoorWidth * 0.5f;
        private static readonly Vector2 GlazingRaw = new(4.4f, 6.6f);

        /// <summary>
        /// Opens the wall behind a landmark entrance's upper-storey glazing, so the glass
        /// looks into the room instead of onto solid wall. Works on a fresh build and on the
        /// hand-edited scene. Undoable.
        /// </summary>
        private static int CutGlazing(Transform group, GameObject wallPiece, string baseName)
        {
            float scale = UtezDimensions.WorldScale;
            float storeyH = UtezDimensions.FloorHeight * scale;
            float y0 = GlazingRaw.x * scale, y1 = GlazingRaw.y * scale;
            int storey = Mathf.FloorToInt(y0 / storeyH);
            return CutOpening(group, wallPiece, $"{baseName}_F{storey}_", true,
                -GlazingHalfWidthRaw * scale, GlazingHalfWidthRaw * scale, storey * storeyH, storeyH,
                y0, y1, true, "Cut entrance glazing");
        }

        /// <summary>
        /// The glass doors at the east end of CECADEC's cross corridor (the interior piece's
        /// SecondaryDoor), real metres: footprint-local Z range along the east wall, and height.
        /// Must match EAST_DOOR in Tools/blender/gen_cecadec_interior.py (landmark y + 22.5).
        /// </summary>
        private static readonly Vector3 CecadecEastDoorRaw = new(-11.8f, -6.8f, 2.7f);

        /// <summary>Opens CECADEC's east wall (and its fake window band) behind the cross corridor's glass doors.</summary>
        private static int CutCecadecEastDoor(Transform group)
        {
            float scale = UtezDimensions.WorldScale;
            float storeyH = UtezDimensions.FloorHeight * scale;
            float from = CecadecEastDoorRaw.x * scale, to = CecadecEastDoorRaw.y * scale;
            float top = CecadecEastDoorRaw.z * scale;
            int cut = CutOpening(group, LoadKit(UtezKit.WallPrefab), "Wall_East_F0_", false,
                from, to, 0f, storeyH, 0f, top, true, "Cut CECADEC east door");
            var windows = group.Find("Windows");
            if (windows != null)
                cut += CutOpening(windows, LoadKit(UtezKit.WindowPrefab), "Win_E_0_", false,
                    from, to, 0f, storeyH, 0f, top, false, "Cut CECADEC east door");
            return cut;
        }

        /// <summary>
        /// Cuts a rectangular opening into one storey of a tiled run: every child of
        /// <paramref name="parent"/> named <paramref name="prefix"/>* that overlaps [from, to]
        /// along the wall (X for north/south walls, Z for east/west) is replaced by its parts
        /// either side of the hole, plus the sill (yBase..y0) and head (y1..yBase + storeyH)
        /// when <paramref name="keepSillHead"/> — window bands just lose the overlap. Undoable.
        /// </summary>
        private static int CutOpening(Transform parent, GameObject piece, string prefix, bool alongX,
            float from, float to, float yBase, float storeyH, float y0, float y1, bool keepSillHead,
            string undoName)
        {
            if (piece == null)
                return 0;

            var hits = new List<Transform>();
            foreach (Transform t in parent)
            {
                float c = alongX ? t.localPosition.x : t.localPosition.z;
                float half = t.localScale.x * UtezKit.Module * 0.5f;
                if (t.name.StartsWith(prefix) && c + half > from && c - half < to)
                    hits.Add(t);
            }

            foreach (var t in hits)
            {
                float c = alongX ? t.localPosition.x : t.localPosition.z;
                float half = t.localScale.x * UtezKit.Module * 0.5f;
                float a = c - half, b = c + half;
                Vector3 origin = t.localPosition;
                Vector3 scale = t.localScale;
                var rot = t.localRotation;
                string name = t.name;
                var renderer = t.GetComponent<MeshRenderer>();
                var mat = renderer != null ? renderer.sharedMaterial : null;
                Undo.DestroyObjectImmediate(t.gameObject);

                void Piece(float s0, float s1, float y, float scaleY, string suffix)
                {
                    if (s1 - s0 < 0.02f || scaleY * UtezDimensions.FloorHeight < 0.02f)
                        return;
                    float mid = (s0 + s1) * 0.5f;
                    var pos = alongX ? new Vector3(mid, y, origin.z) : new Vector3(origin.x, y, mid);
                    var go = UtezKit.Place(piece, parent, pos, rot,
                        new Vector3((s1 - s0) / UtezKit.Module, scaleY, scale.z), name + suffix, mat);
                    if (go != null)
                        Undo.RegisterCreatedObjectUndo(go, undoName);
                }

                Piece(a, from, origin.y, scale.y, "_A");
                Piece(to, b, origin.y, scale.y, "_B");
                if (!keepSillHead)
                    continue;
                float s0c = Mathf.Max(a, from), s1c = Mathf.Min(b, to);
                Piece(s0c, s1c, yBase, (y0 - yBase) / UtezDimensions.FloorHeight, "_Sill");
                Piece(s0c, s1c, y1, (yBase + storeyH - y1) / UtezDimensions.FloorHeight, "_Head");
            }
            return hits.Count;
        }

        /// <summary>
        /// Clears what the generic builders put where a landmark's plaza now is: hides kit
        /// kerbs, lamp posts and flagpoles inside the footprint of its <c>Ground_*</c>
        /// objects, and trims the building's grass band back to the wall so it can't show
        /// through the low plaza level. Undoable.
        /// </summary>
        private static int HideCoveredGround(Transform landmark)
        {
            Bounds? footprint = null;
            foreach (var f in landmark.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.name.StartsWith("Ground_") || f.sharedMesh == null)
                    continue;
                var mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y,
                        (i & 4) == 0 ? mb.min.z : mb.max.z);
                    var p = landmark.InverseTransformPoint(f.transform.TransformPoint(c));
                    var b = footprint ?? new Bounds(p, Vector3.zero);
                    b.Encapsulate(p);
                    footprint = b;
                }
            }
            if (footprint == null)
                return 0;
            var local = footprint.Value;

            int hidden = 0;
            foreach (var rootName in new[] { "UTEZ_Terrain", "UTEZ_Props" })
            {
                var generic = GameObject.Find(rootName);
                if (generic == null)
                    continue;
                foreach (var t in generic.GetComponentsInChildren<Transform>())
                {
                    if (!(t.name.StartsWith("Kerb_") || t.name.StartsWith("LampPost_") || t.name.StartsWith("Flagpole_")))
                        continue;
                    var p = landmark.InverseTransformPoint(t.position);
                    if (p.x < local.min.x || p.x > local.max.x || p.z < local.min.z || p.z > local.max.z)
                        continue;
                    Undo.RecordObject(t.gameObject, "Hide ground under plaza");
                    t.gameObject.SetActive(false);
                    hidden++;
                }
            }
            return hidden + TrimGrassUnder(landmark);
        }

        /// <summary>
        /// The terrain's grass band is one slab around the whole building. Where a landmark
        /// sits on a wall, cut the slab back to that wall's outer face (the landmark's
        /// origin), along the landmark's outward axis. Returns 1 if a slab was trimmed.
        /// </summary>
        private static int TrimGrassUnder(Transform landmark)
        {
            var terrain = GameObject.Find("UTEZ_Terrain");
            if (terrain == null)
                return 0;
            foreach (var t in terrain.GetComponentsInChildren<Transform>())
            {
                if (t.name != "Grass" || t.parent == null)
                    continue;
                var space = t.parent;
                Vector3 o = space.InverseTransformPoint(landmark.position);
                Vector3 f = space.InverseTransformDirection(landmark.forward);
                int axis = Mathf.Abs(f.z) >= Mathf.Abs(f.x) ? 2 : 0;
                float sign = Mathf.Sign(axis == 2 ? f.z : f.x);

                Vector3 pos = t.localPosition, scale = t.localScale;
                float lo = pos[axis] - scale[axis] * 0.5f, hi = pos[axis] + scale[axis] * 0.5f;
                int other = 2 - axis;
                float oLo = pos[other] - scale[other] * 0.5f, oHi = pos[other] + scale[other] * 0.5f;
                if (o[axis] <= lo || o[axis] >= hi || o[other] <= oLo || o[other] >= oHi)
                    continue;

                if (sign > 0f) hi = o[axis]; else lo = o[axis];
                Undo.RecordObject(t, "Trim grass under plaza");
                pos[axis] = (lo + hi) * 0.5f;
                scale[axis] = hi - lo;
                t.localPosition = pos;
                t.localScale = scale;
                return 1;
            }
            return 0;
        }

        /// <summary>Pose, in the building group, of a landmark entrance on <paramref name="side"/>.</summary>
        private static (Vector3 pos, Quaternion rot) EntrancePose(UtezDimensions.Footprint fp,
            UtezDimensions.Side side)
        {
            Vector2 size = fp.ScaledSize;
            bool alongX = side is UtezDimensions.Side.North or UtezDimensions.Side.South;
            Vector3 outward = side switch
            {
                UtezDimensions.Side.North => Vector3.forward,
                UtezDimensions.Side.South => Vector3.back,
                UtezDimensions.Side.East => Vector3.right,
                _ => Vector3.left,
            };
            float outerFace = (alongX ? size.y : size.x) * 0.5f;
            return (outward * outerFace, Quaternion.LookRotation(outward));
        }

        /// <summary>
        /// Puts the CECADEC north landmark (entrance + plaza) into the OPEN scene without
        /// rebuilding it — for utez.unity, which is hand-edited. Replaces the two kit
        /// pilasters on the entrance wall (or a previous landmark), opens the wall behind
        /// the glazing, and hides generic kerbs/lamps/flagpoles under the plaza. Undoable.
        /// </summary>
        [MenuItem("HORROR-UTEZ/Place CECADEC North In Open Scene")]
        public static void PlaceCecadecEntranceInOpenScene()
        {
            var entrance = LoadKit(UtezKit.CecadecNorthPrefab);
            var group = GameObject.Find($"{RootName}/CECADEC_Structure");
            if (entrance == null || group == null)
            {
                Debug.LogError($"[UTEZ] Needs the entrance prefab (HORROR-UTEZ > Rebuild Building Kit) " +
                               $"and {RootName}/CECADEC_Structure in the open scene.");
                return;
            }

            var fp = UtezDimensions.Cecadec;
            string baseName = $"Wall_{fp.Entrance}";
            foreach (var suffix in new[] { "_PilasterA", "_PilasterB", "_Entrance" })
            {
                var old = group.transform.Find(baseName + suffix);
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);
            }

            int doorway = RecutLandmarkDoorway(group.transform, fp);

            var (pos, rot) = EntrancePose(fp, fp.Entrance);
            var go = UtezKit.Place(entrance, group.transform, pos, rot,
                Vector3.one * UtezDimensions.WorldScale, baseName + "_Entrance");
            Undo.RegisterCreatedObjectUndo(go, "Place CECADEC North");
            int cut = CutGlazing(group.transform, LoadKit(UtezKit.WallPrefab), baseName);
            int hidden = go != null ? HideCoveredGround(go.transform) : 0;
            hidden += HideKitPiecesUnderCecadecLandmark();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log($"[UTEZ] CECADEC north placed: doorway recut ({doorway} pieces replaced), " +
                      $"{cut} wall pieces cut for the glazing, " +
                      $"{hidden} generic kerbs/lamps/flagpoles hidden under the plaza.");
        }

        /// <summary>
        /// Kit pieces the CECADEC north landmark now stands in for: CDS's generic entrance
        /// pilasters (the landmark carries CDS's real south facade) and CECADEC's fake east
        /// window bands (it carries the real east facade). Undoable; returns how many.
        /// </summary>
        private static int HideKitPiecesUnderCecadecLandmark()
        {
            int hidden = 0;
            void Hide(Transform t)
            {
                if (t == null || !t.gameObject.activeSelf)
                    return;
                Undo.RecordObject(t.gameObject, "Hide kit piece under landmark");
                t.gameObject.SetActive(false);
                hidden++;
            }

            var cds = GameObject.Find($"{RootName}/CDS_Structure");
            if (cds != null)
            {
                Hide(cds.transform.Find("Wall_South_PilasterA"));
                Hide(cds.transform.Find("Wall_South_PilasterB"));
            }
            var windows = GameObject.Find($"{RootName}/CECADEC_Structure/Windows");
            if (windows != null)
                foreach (Transform t in windows.transform)
                    if (t.name.StartsWith("Win_E_"))
                        Hide(t);
            return hidden;
        }

        /// <summary>
        /// The interior floor sits just clear of the Kit_Pad footprint (0.05 m thick at
        /// y = 0); level with it, the pad would hide the corridor's tile floor.
        /// </summary>
        private const float InteriorLiftRaw = UtezDimensions.PadThicknessRaw + 0.01f;

        private const string InteriorName = "CECADEC_Interior";

        /// <summary>
        /// Puts the CECADEC ground-floor corridor (walls, ceiling, door and glass openings)
        /// into the OPEN scene, sharing the north entrance's pose: both pieces are authored
        /// around the same origin, the main door threshold. Replaces a previous copy. Undoable.
        /// </summary>
        [MenuItem("HORROR-UTEZ/Place CECADEC Interior In Open Scene")]
        public static void PlaceCecadecInteriorInOpenScene()
        {
            var interior = LoadKit(UtezKit.CecadecInteriorPrefab);
            var group = GameObject.Find($"{RootName}/CECADEC_Structure");
            if (interior == null || group == null)
            {
                Debug.LogError($"[UTEZ] Needs the interior prefab (HORROR-UTEZ > Rebuild Building Kit) " +
                               $"and {RootName}/CECADEC_Structure in the open scene.");
                return;
            }

            var old = group.transform.Find(InteriorName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            var fp = UtezDimensions.Cecadec;
            var (pos, rot) = EntrancePose(fp, fp.Entrance);
            pos += Vector3.up * (InteriorLiftRaw * UtezDimensions.WorldScale);
            var go = UtezKit.Place(interior, group.transform, pos, rot,
                Vector3.one * UtezDimensions.WorldScale, InteriorName);
            Undo.RegisterCreatedObjectUndo(go, "Place CECADEC Interior");
            int cut = CutCecadecEastDoor(group.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log($"[UTEZ] CECADEC interior placed behind the north entrance; {cut} east wall/window " +
                      "pieces cut for the cross corridor's glass doors.");
        }

        /// <summary>
        /// The pale pilasters flanking the entrance, proud of the facade. They are what make
        /// CECADEC recognisable in the photo, so they are structure here rather than a decal.
        /// One <see cref="UtezKit"/> pilaster piece each, scaled to the building height.
        /// </summary>
        private static void BuildEntrancePilasters(Transform group, GameObject pilasterPiece,
            string baseName, float height, bool alongX, float sign, float offset, float doorW)
        {
            if (pilasterPiece == null)
                return;

            float scale = UtezDimensions.WorldScale;
            float proud = UtezDimensions.PilasterProudRaw * scale;
            float pilasterOffset = (doorW + UtezDimensions.PilasterWidthRaw * scale) * 0.5f;

            Quaternion rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            var pscale = new Vector3(scale, height / UtezDimensions.FloorHeight, scale);

            Vector3 Pos(float along) => alongX
                ? new Vector3(along, 0f, sign * (offset + proud))
                : new Vector3(sign * (offset + proud), 0f, along);

            UtezKit.Place(pilasterPiece, group, Pos(-pilasterOffset), rot, pscale, baseName + "_PilasterA");
            UtezKit.Place(pilasterPiece, group, Pos(pilasterOffset), rot, pscale, baseName + "_PilasterB");
        }

        // ---- Interior floors --------------------------------------------------

        /// <summary>
        /// Upper floors, each tiled from <see cref="UtezKit"/> floor pieces with a stairwell
        /// opening left clear and a flight of steps rising into it.
        ///
        /// The slab is laid as four rectangles around the opening rather than one slab with
        /// a hole: a hole needs real mesh work, four rectangles need none, and the player
        /// only ever sees the edges.
        /// </summary>
        private static void BuildInteriorFloors(Transform group, GameObject floorPiece,
            UtezDimensions.Footprint fp, Vector2 size, Material stepMaterial)
        {
            if (fp.Floors < 2 || floorPiece == null)
                return;

            float scale = UtezDimensions.WorldScale;
            float storey = UtezDimensions.FloorHeight * scale;
            float halfX = size.x * 0.5f;
            float halfZ = size.y * 0.5f;

            for (int floor = 1; floor < fp.Floors; floor++)
            {
                float y = storey * floor;
                var slabGroup = new GameObject($"Floor_{floor}").transform;
                slabGroup.SetParent(group, false);

                if (!Stairwells)
                {
                    // Sealed slab, no opening, no steps.
                    UtezKit.TileArea(slabGroup, floorPiece, null, -halfX, halfX, -halfZ, halfZ, y, "Slab");
                    continue;
                }

                // Shared with the prop pass, which has to keep furniture out of the opening.
                Rect well = UtezDimensions.StairwellLocal(fp);
                float wellMinX = well.xMin;
                float wellMaxX = well.xMax;
                float wellMinZ = well.yMin;
                float wellMaxZ = well.yMax;

                // West of the well, east of it, then the two strips north and south.
                UtezKit.TileArea(slabGroup, floorPiece, null, -halfX, wellMinX, -halfZ, halfZ, y, "Slab_W");
                UtezKit.TileArea(slabGroup, floorPiece, null, wellMaxX, halfX, -halfZ, halfZ, y, "Slab_E");
                UtezKit.TileArea(slabGroup, floorPiece, null, wellMinX, wellMaxX, -halfZ, wellMinZ, y, "Slab_S");
                UtezKit.TileArea(slabGroup, floorPiece, null, wellMinX, wellMaxX, wellMaxZ, halfZ, y, "Slab_N");

                BuildStairs(slabGroup, new Vector2(well.center.x, wellMinZ), storey * (floor - 1),
                    storey, well.width, well.height, stepMaterial);
            }
        }

        /// <summary>
        /// A straight flight rising into the stairwell opening. Still a stack of solid
        /// primitives: each step is a block down to the floor, so there is no gap to fall
        /// through and the CharacterController climbs it without a ramp.
        /// </summary>
        private static void BuildStairs(Transform parent, Vector2 footOfStairs, float baseY,
            float rise, float width, float run, Material material)
        {
            // Step count follows the rise so a single step never exceeds the player's
            // CharacterController stepOffset (0.35, unscaled). At WorldScale 1.5 a fixed 14
            // would put each step at 0.43 and the player would be stopped at the first one.
            int steps = Mathf.Max(14, Mathf.CeilToInt(rise / 0.28f));
            float stepRise = rise / steps;
            float stepRun = run / steps;

            var stairs = new GameObject("Stairs").transform;
            stairs.SetParent(parent, false);

            for (int i = 0; i < steps; i++)
            {
                float z = footOfStairs.y + stepRun * (i + 0.5f);
                Slab(stairs, $"Step_{i}",
                    new Vector3(footOfStairs.x, baseY + stepRise * (i + 1) * 0.5f, z),
                    new Vector3(width * 0.7f, stepRise * (i + 1), stepRun), material);
            }
        }

        /// <summary>
        /// Bands of dark glazing at each storey, tiled from the <see cref="UtezKit"/> window
        /// piece. They sit proud of the wall rather than being cut through it: a real
        /// opening needs the wall split, and at PSX resolution a proud dark panel reads as
        /// a window from any distance the player sees it.
        /// </summary>
        private static void BuildWindowBands(Transform group, GameObject windowPiece,
            UtezDimensions.Footprint fp, Vector2 size)
        {
            if (windowPiece == null)
                return;

            float scale = UtezDimensions.WorldScale;
            float storey = UtezDimensions.FloorHeight * scale;
            float thickness = UtezDimensions.WallThickness;
            // Glazing depth, and how far its centre sits from the wall's OUTER face.
            float depth = thickness * 0.5f;
            float faceOffset = depth * 0.4f;
            float inset = 3f * scale;

            // The window piece is WindowBandHeightRaw tall and (WallThicknessRaw/2) deep in
            // real metres; both scale straight with WorldScale.
            var crossYZ = new Vector2(scale, scale);

            float halfX = size.x * 0.5f;
            float halfZ = size.y * 0.5f;

            var windows = new GameObject("Windows").transform;
            windows.SetParent(group, false);

            for (int floor = 0; floor < fp.Floors; floor++)
            {
                float y = storey * floor + storey * 0.55f;

                // The entrance facade stays blank.
                if (fp.Entrance != UtezDimensions.Side.North)
                    UtezKit.TileLinear(windows, windowPiece, null,
                        (along, cy) => new Vector3(along, cy, halfZ - faceOffset), Quaternion.identity,
                        -(halfX - inset), halfX - inset, y, crossYZ, $"Win_N_{floor}");

                if (fp.Entrance != UtezDimensions.Side.South)
                    UtezKit.TileLinear(windows, windowPiece, null,
                        (along, cy) => new Vector3(along, cy, -(halfZ - faceOffset)), Quaternion.identity,
                        -(halfX - inset), halfX - inset, y, crossYZ, $"Win_S_{floor}");

                if (fp.Entrance != UtezDimensions.Side.East)
                    UtezKit.TileLinear(windows, windowPiece, null,
                        (along, cy) => new Vector3(halfX - faceOffset, cy, along),
                        Quaternion.Euler(0f, 90f, 0f),
                        -(halfZ - inset), halfZ - inset, y, crossYZ, $"Win_E_{floor}");

                if (fp.Entrance != UtezDimensions.Side.West)
                    UtezKit.TileLinear(windows, windowPiece, null,
                        (along, cy) => new Vector3(-(halfX - faceOffset), cy, along),
                        Quaternion.Euler(0f, 90f, 0f),
                        -(halfZ - inset), halfZ - inset, y, crossYZ, $"Win_W_{floor}");
            }
        }

        /// <summary>Roof on four corner pillars. No walls: you see and walk straight through.</summary>
        private static void BuildCanopy(Transform parent, string name, UtezDimensions.Footprint fp,
            Pieces p)
        {
            var group = MakeGroup(parent, name, fp);

            Vector2 size = fp.ScaledSize;
            float height = fp.ScaledHeight;
            float scale = UtezDimensions.WorldScale;
            float thickness = UtezDimensions.PillarThickness;

            float x = size.x * 0.5f - thickness * 0.5f;
            float z = size.y * 0.5f - thickness * 0.5f;
            var pscale = new Vector3(scale, height / UtezDimensions.FloorHeight, scale);

            int index = 0;
            foreach (float sx in new[] { -x, x })
            foreach (float sz in new[] { -z, z })
            {
                UtezKit.Place(p.Pillar, group, new Vector3(sx, 0f, sz), Quaternion.identity,
                    pscale, $"Pillar_{index++}");
            }

            UtezKit.TileArea(group, p.Roof, null,
                -size.x * 0.5f, size.x * 0.5f, -size.y * 0.5f, size.y * 0.5f, height, "Roof");
        }

        private static Transform MakeGroup(Transform parent, string name, UtezDimensions.Footprint fp)
        {
            var group = new GameObject($"{name}_Structure").transform;
            group.SetParent(parent, false);
            group.localPosition = new Vector3(fp.ScaledCenter.x, 0f, fp.ScaledCenter.y);
            group.localRotation = Quaternion.Euler(0f, fp.YawDeg, 0f);
            return group;
        }

        /// <summary>A scaled primitive cube. Only the stairs still use this.</summary>
        private static void Slab(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            // Structures never move; static batching is free and occlusion flags let Unity
            // cull whole buildings at once.
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

        private static GameObject LoadKit(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                Debug.LogWarning($"[UTEZ] Missing kit piece '{path}'. Run HORROR-UTEZ > Rebuild Building Kit.");
            return go;
        }

        private static Material LoadKitMaterial(string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                Debug.LogWarning($"[UTEZ] Missing kit material '{path}'. Run HORROR-UTEZ > Rebuild Building Kit.");
            return mat;
        }
    }
}
