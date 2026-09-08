using UnityEngine;

namespace HorrorUtez.World
{
    /// <summary>
    /// Single source of truth for the UTEZ campus layout, in meters.
    /// Mirrors docs/map/utez-campus-reference.md (v2). Only CECADEC is measured;
    /// CDS and roads are proportional estimates from the Google Maps overview.
    ///
    /// World origin (0,0,0) = center of the ~20 m gap between CDS south wall and
    /// CECADEC north wall. +Z = North, +X = East.
    /// </summary>
    public static class UtezDimensions
    {
        /// <summary>Footprint on the ground plane: size on X/Z, position (center) and yaw.</summary>
        public readonly struct Footprint
        {
            public readonly Vector2 Size;   // x = East-West, y = North-South
            public readonly Vector2 Center; // x, z in world space
            public readonly float YawDeg;   // rotation about +Y

            public Footprint(Vector2 size, Vector2 center, float yawDeg)
            {
                Size = size;
                Center = center;
                YawDeg = yawDeg;
            }
        }

        // ---- Concrete explanada ---------------------------------------------
        // Width covers the CDS east wing plus its grass band; nothing may hang off the slab.
        public static readonly Vector2 SlabSize = new(70f, 110f);
        public static readonly Vector2 SlabCenter = new(0f, -12f);

        // ---- Buildings (footprints only, not the buildings themselves) ------
        public static readonly Footprint Cecadec =
            new(size: new Vector2(20f, 45f), center: new Vector2(0f, -32f), yawDeg: -13f);

        public static readonly Footprint CdsBody =
            new(size: new Vector2(32f, 20f), center: new Vector2(0f, 20f), yawDeg: 0f);

        // East wing: sits alongside the body (body spans X -16..+16), protruding south.
        public static readonly Footprint CdsExtension =
            new(size: new Vector2(12f, 12f), center: new Vector2(22f, 10f), yawDeg: 0f);

        // ---- Ground detail -------------------------------------------------
        public const float GrassMargin = 4f;   // grass band width around each building
        public const float KerbHeight = 0.3f;  // stacked-stone border height
        public const float KerbWidth = 0.3f;

        // ---- Surroundings ------------------------------------------------
        public const float DirtMargin = 60f;   // dirt plane extends this far past the slab
        public const float ForestRingWidth = 20f;
        public const float RoadWidth = 7f;

        /// <summary>Full extent of the exterior dirt plane (slab + dirt margin both sides).</summary>
        public static Vector2 DirtSize => SlabSize + 2f * DirtMargin * Vector2.one;
    }
}
