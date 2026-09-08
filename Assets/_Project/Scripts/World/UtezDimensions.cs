using UnityEngine;

namespace HorrorUtez.World
{
    /// <summary>
    /// Single source of truth for the UTEZ campus layout.
    /// Mirrors docs/map/utez-campus-reference.md (v3).
    ///
    /// World origin (0,0,0) = center of the gap between the CDS south wall and the
    /// CECADEC north wall. +Z = North, +X = East.
    ///
    /// Every raw value here is the REAL measured one, in real metres. The world is then
    /// built at <see cref="WorldScale"/>. Keeping the two separate means the survey data
    /// stays honest and re-checkable while the playable space gets the extra room that
    /// interiors need for movement, collision and content.
    /// </summary>
    public static class UtezDimensions
    {
        /// <summary>
        /// Global multiplier from real metres to world units.
        ///
        /// At 1.0 the campus is accurate but reads cramped from eye level and leaves too
        /// little interior volume to lay out rooms. Raise this to make the buildings loom;
        /// every derived dimension follows, including heights, so proportions never skew.
        /// </summary>
        public const float WorldScale = 1.5f;

        /// <summary>Real storey height for these institutional blocks, before scaling.</summary>
        public const float FloorHeight = 4f;

        /// <summary>Which wall carries the main entrance. Walls are named by compass point.</summary>
        public enum Side { None, North, South, East, West }

        /// <summary>Real door dimensions in metres, before scaling.</summary>
        public const float DoorWidth = 3f;
        public const float DoorHeight = 3f;

        /// <summary>Footprint on the ground plane, plus the data the building pass needs.</summary>
        public readonly struct Footprint
        {
            /// <summary>REAL size in metres. x = East-West, y = North-South.</summary>
            public readonly Vector2 Size;
            /// <summary>REAL world x/z of the footprint center, in metres.</summary>
            public readonly Vector2 Center;
            /// <summary>Rotation about +Y. Negative = clockwise seen from above, matching the map.</summary>
            public readonly float YawDeg;
            /// <summary>Storey count.</summary>
            public readonly int Floors;
            /// <summary>False for roof-only structures: sightlines pass straight through.</summary>
            public readonly bool HasWalls;
            /// <summary>Whether a grass band + stone kerb rings this footprint.</summary>
            public readonly bool HasGrass;
            /// <summary>Wall carrying the main entrance, cut as a doorway.</summary>
            public readonly Side Entrance;

            public Footprint(Vector2 size, Vector2 center, float yawDeg, int floors,
                bool hasWalls = true, bool hasGrass = true, Side entrance = Side.None)
            {
                Size = size;
                Center = center;
                YawDeg = yawDeg;
                Floors = floors;
                HasWalls = hasWalls;
                HasGrass = hasGrass;
                Entrance = entrance;
            }

            public Vector2 ScaledSize => Size * WorldScale;
            public Vector2 ScaledCenter => Center * WorldScale;
            public float ScaledHeight => Floors * FloorHeight * WorldScale;
        }

        // ---- Concrete explanada (real metres) -------------------------------
        private static readonly Vector2 SlabSizeRaw = new(76f, 110f);
        private static readonly Vector2 SlabCenterRaw = new(0f, -12f);

        public static Vector2 SlabSize => SlabSizeRaw * WorldScale;
        public static Vector2 SlabCenter => SlabCenterRaw * WorldScale;

        // ---- Buildings ------------------------------------------------------

        /// <summary>MEASURED: east wall 44.69 m, south wall 20.59 m.</summary>
        public static readonly Footprint Cecadec =
            new(size: new Vector2(20f, 45f), center: new Vector2(0f, -32f), yawDeg: -13f, floors: 2,
                entrance: Side.North);

        /// <summary>
        /// MEASURED: 30.78 x 22.23 m, closed perimeter 105.67 m. North wall drops ~4.5 deg
        /// toward the east. No grass band — CDS meets the concrete directly.
        /// </summary>
        public static readonly Footprint Cds =
            new(size: new Vector2(31f, 22f), center: new Vector2(0f, 21f), yawDeg: -4.5f, floors: 2,
                hasGrass: false, entrance: Side.South);

        /// <summary>
        /// Covered walkway joining CDS to the auditorium. Roof on pillars, NO walls —
        /// it reads as a building on the map but you see and walk straight through it.
        /// Size confirmed by the user; position estimated from the overview.
        /// </summary>
        public static readonly Footprint Canopy =
            new(size: new Vector2(15f, 7f), center: new Vector2(23f, 19f), yawDeg: -4.5f, floors: 1,
                hasWalls: false, hasGrass: false);

        /// <summary>MEASURED: three-sided run of 49.19 m at a 1:2.1 ratio.</summary>
        public static readonly Footprint Auditorium =
            new(size: new Vector2(10f, 20f), center: new Vector2(25f, 7f), yawDeg: -4.5f, floors: 1,
                hasGrass: false, entrance: Side.North);

        /// <summary>Everything the terrain and building passes iterate over.</summary>
        public static readonly (string Name, Footprint Fp)[] All =
        {
            ("CECADEC", Cecadec),
            ("CDS", Cds),
            ("Canopy", Canopy),
            ("Auditorium", Auditorium),
        };

        // ---- Ground detail (real metres, scaled on access) ------------------
        private const float GrassMarginRaw = 4f;
        private const float KerbHeightRaw = 0.3f;
        private const float KerbWidthRaw = 0.3f;

        public static float GrassMargin => GrassMarginRaw * WorldScale;
        public static float KerbHeight => KerbHeightRaw * WorldScale;
        public static float KerbWidth => KerbWidthRaw * WorldScale;

        // ---- Building detail -------------------------------------------------
        private const float WallThicknessRaw = 0.4f;
        private const float RoofThicknessRaw = 0.5f;
        private const float PillarThicknessRaw = 0.5f;

        public static float WallThickness => WallThicknessRaw * WorldScale;
        public static float RoofThickness => RoofThicknessRaw * WorldScale;
        public static float PillarThickness => PillarThicknessRaw * WorldScale;

        // ---- Surroundings ----------------------------------------------------
        private const float DirtMarginRaw = 60f;
        private const float ForestRingWidthRaw = 20f;
        private const float RoadWidthRaw = 7f;

        public static float DirtMargin => DirtMarginRaw * WorldScale;
        public static float ForestRingWidth => ForestRingWidthRaw * WorldScale;
        public static float RoadWidth => RoadWidthRaw * WorldScale;

        /// <summary>Full extent of the exterior dirt plane (slab + dirt margin both sides).</summary>
        public static Vector2 DirtSize => SlabSize + 2f * DirtMargin * Vector2.one;
    }
}
