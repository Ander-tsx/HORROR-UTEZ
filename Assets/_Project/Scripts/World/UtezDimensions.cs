using UnityEngine;

namespace HorrorUtez.World
{
    /// <summary>
    /// Single source of truth for the UTEZ campus layout, in meters.
    /// Mirrors docs/map/utez-campus-reference.md (v3).
    ///
    /// World origin (0,0,0) = center of the ~20 m gap between the CDS south wall and
    /// the CECADEC north wall. +Z = North, +X = East.
    /// </summary>
    public static class UtezDimensions
    {
        /// <summary>Footprint on the ground plane, plus the data the building pass will need.</summary>
        public readonly struct Footprint
        {
            /// <summary>x = East-West extent, y = North-South extent.</summary>
            public readonly Vector2 Size;
            /// <summary>World x/z of the footprint center.</summary>
            public readonly Vector2 Center;
            /// <summary>Rotation about +Y. Negative = clockwise seen from above, matching the map.</summary>
            public readonly float YawDeg;
            /// <summary>Storey count, for the building pass.</summary>
            public readonly int Floors;
            /// <summary>False for roof-only structures with no walls: sightlines pass straight through.</summary>
            public readonly bool HasWalls;
            /// <summary>Whether a grass band + stone kerb rings this footprint.</summary>
            public readonly bool HasGrass;

            public Footprint(Vector2 size, Vector2 center, float yawDeg, int floors,
                bool hasWalls = true, bool hasGrass = true)
            {
                Size = size;
                Center = center;
                YawDeg = yawDeg;
                Floors = floors;
                HasWalls = hasWalls;
                HasGrass = hasGrass;
            }
        }

        // ---- Concrete explanada ---------------------------------------------
        // Sized to contain every footprint plus its grass band; nothing may hang off it.
        public static readonly Vector2 SlabSize = new(76f, 110f);
        public static readonly Vector2 SlabCenter = new(0f, -12f);

        // ---- Buildings (footprints only; the structures come later) ---------

        /// <summary>MEASURED: closed perimeter 105.67 m, walls 30.78 (E-W) x 22.23 (N-S).</summary>
        public static readonly Footprint Cecadec =
            new(size: new Vector2(20f, 45f), center: new Vector2(0f, -32f), yawDeg: -13f, floors: 2);

        /// <summary>MEASURED: 30.78 x 22.23 m. North wall drops ~4.5 deg toward the east.</summary>
        public static readonly Footprint Cds =
            new(size: new Vector2(31f, 22f), center: new Vector2(0f, 21f), yawDeg: -4.5f, floors: 2);

        /// <summary>
        /// Covered walkway joining CDS to the auditorium. Roof on pillars, NO walls —
        /// it reads as a building on the map but you can see and walk straight through it.
        /// Estimated from the overview, not measured.
        /// </summary>
        public static readonly Footprint Canopy =
            new(size: new Vector2(15f, 7f), center: new Vector2(23f, 19f), yawDeg: -4.5f, floors: 1,
                hasWalls: false, hasGrass: false);

        /// <summary>MEASURED: three-sided run of 49.19 m at a 1:2.1 ratio.</summary>
        public static readonly Footprint Auditorium =
            new(size: new Vector2(10f, 20f), center: new Vector2(25f, 7f), yawDeg: -4.5f, floors: 1,
                hasGrass: false);

        /// <summary>Everything the terrain pass lays down a pad for.</summary>
        public static readonly (string Name, Footprint Fp)[] All =
        {
            ("CECADEC", Cecadec),
            ("CDS", Cds),
            ("Canopy", Canopy),
            ("Auditorium", Auditorium),
        };

        // ---- Ground detail -------------------------------------------------
        public const float GrassMargin = 4f;   // grass band width around a building
        public const float KerbHeight = 0.3f;  // stacked-stone border height
        public const float KerbWidth = 0.3f;

        // ---- Surroundings ------------------------------------------------
        public const float DirtMargin = 60f;   // dirt plane extends this far past the slab
        public const float ForestRingWidth = 20f;
        public const float RoadWidth = 7f;

        /// <summary>Full extent of the exterior dirt plane (slab + dirt margin on both sides).</summary>
        public static Vector2 DirtSize => SlabSize + 2f * DirtMargin * Vector2.one;
    }
}
