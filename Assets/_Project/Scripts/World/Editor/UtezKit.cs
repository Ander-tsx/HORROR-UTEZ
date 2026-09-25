using System.IO;
using UnityEditor;
using UnityEngine;
using HorrorUtez.Rendering;
using HorrorUtez.Rendering.Editor;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// The reusable building blocks the campus is assembled from. Every greybox surface an
    /// artist might want to remodel is one of these: a <b>.blend file</b> in
    /// <c>Assets/_Project/Art/Environment/Kit/</c> holding the geometry, a PSX material,
    /// and a prefab that binds the two. <see cref="UtezBuildingBuilder"/> and
    /// <see cref="UtezTerrainBuilder"/> tile or scale the prefabs to fill the campus.
    ///
    /// Open <c>Kit_Wall_4m.blend</c> (or any other piece) directly in Blender, edit, save.
    /// Unity re-imports on focus and every placed instance updates — no builder change, the
    /// prefab, material and collider stay. Keep the piece at its real-metre size (the box
    /// this generated: see <c>Tools/blender/gen_kit_pieces.py</c>), long axis on X, pivot
    /// as authored, one material slot. Per-metre UVs (1 UV unit = 1 real metre) keep a
    /// Tiling (1,1) texture repeating once per metre and continuous across a tiled run.
    ///
    /// This class does NOT generate the .blend files (that would clobber your edits) — the
    /// bootstrap script does, once. It (re)writes only the materials and the prefabs, and
    /// points each prefab at the mesh inside its .blend. Safe to re-run any time.
    /// </summary>
    public static class UtezKit
    {
        /// <summary>Native run length of a tiled piece, in real metres. The tiling module.</summary>
        public const float Module = 4f;

        /// <summary>World length of one full module piece at the current WorldScale.</summary>
        public static float NativeModule => Module * UtezDimensions.WorldScale;

        private const string MeshFolder = "Assets/_Project/Art/Environment/Kit";
        private const string TextureFolder = "Assets/_Project/Art/Textures/Kit";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Kit";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Kit";

        // ---- Prefab paths (what the builders load) ---------------------------
        public const string WallPrefab = PrefabFolder + "/Kit_Wall_4m.prefab";
        public const string FloorPrefab = PrefabFolder + "/Kit_Floor_4m.prefab";
        public const string PilasterPrefab = PrefabFolder + "/Kit_Pilaster.prefab";
        public const string WindowPrefab = PrefabFolder + "/Kit_Window.prefab";
        public const string RoofPrefab = PrefabFolder + "/Kit_Roof.prefab";
        public const string PillarPrefab = PrefabFolder + "/Kit_Pillar.prefab";
        public const string KerbPrefab = PrefabFolder + "/Kit_Kerb.prefab";
        public const string PadPrefab = PrefabFolder + "/Kit_Pad.prefab";

        // ---- Landmarks: bespoke multi-material pieces, one per place ------------
        private const string LandmarkFolder = "Assets/_Project/Art/Environment/Landmarks";
        private const string LandmarkPrefabFolder = "Assets/_Project/Prefabs/Landmarks";

        /// <summary>
        /// CECADEC north: entrance (pilasters, glazed bay, hinged doors) plus the plaza in
        /// front of it — planters, trees, lamps, flagpoles, ramp, generator. Replaces the
        /// kit pilasters on that wall.
        /// </summary>
        public const string CecadecNorthPrefab = LandmarkPrefabFolder + "/CECADEC_North.prefab";

        /// <summary>
        /// CECADEC ground floor: the straight corridor, the cross corridor with its glass
        /// doors and stairs, every room front, fittings and signage. Room interiors are empty.
        /// Shares CECADEC_North's origin and pose; placed by UtezBuildingBuilder.
        /// </summary>
        public const string CecadecInteriorPrefab = LandmarkPrefabFolder + "/CecadecInterior.prefab";

        /// <summary>
        /// CECADEC above the ground floor: the intermediate floor between storeys (an empty
        /// concrete crawl level, 1.04 m clear) and the upper floor's corridor, rooms fronts,
        /// stairwell head and glazed screens. Shares CECADEC_North's origin and pose.
        /// </summary>
        public const string CecadecUpperPrefab = LandmarkPrefabFolder + "/CecadecUpper.prefab";

        private enum Surface { Opaque, Cutout, Transparent, Emissive }

        /// <summary>
        /// Materials only landmarks use. Name = the Blender slot name; map in
        /// <see cref="TextureFolder"/>; Tiling = 1 / metres the map covers. Must match
        /// MATERIALS in Tools/blender/gen_cecadec_north.py.
        /// </summary>
        private static readonly (string Name, string Map, float U, float V, Surface Kind)[] LandmarkMaterials =
        {
            ("Kit_Glass_Clear", "T_Glass", 1f, 1f, Surface.Transparent),
            ("Kit_Apron", "T_Apron", 0.5f, 0.5f, Surface.Opaque),
            ("Kit_Slab", "T_Slab", 0.25f, 0.25f, Surface.Opaque),
            ("Kit_Aggregate", "T_Aggregate", 1f / 3f, 1f / 3f, Surface.Opaque),
            ("Kit_Paint_Green", "T_Paint_Green", 1f, 1f, Surface.Opaque),
            ("Kit_Paint_Blue", "T_Paint_Blue", 1f, 1f, Surface.Opaque),
            ("Kit_Paint_Yellow", "T_Paint_Yellow", 1f, 1f, Surface.Opaque),
            ("Kit_Grass", "T_Grass", 0.5f, 0.5f, Surface.Opaque),
            ("Kit_Soil", "T_Soil", 0.5f, 0.5f, Surface.Opaque),
            ("Kit_Stone", "T_Stone", 0.5f, 1f, Surface.Opaque),
            ("Kit_Rock", "T_Rock", 0.5f, 0.5f, Surface.Opaque),
            ("Kit_Bark", "T_Bark", 1f, 0.5f, Surface.Opaque),
            ("Kit_Foliage", "T_Foliage", 1f, 1f, Surface.Cutout),
            // Hazelnut bush pack (tree_bed hillside trees), photo-sourced instead of procedural.
            ("Kit_Bark_Hazelnut", "T_Bark_Hazelnut", 1f, 1f, Surface.Opaque),
            ("Kit_Foliage_Hazelnut", "T_Foliage_Hazelnut", 1f, 1f, Surface.Cutout),
            // Interior (CECADEC ground-floor corridor); must match MAPS in
            // Tools/blender/gen_interior_textures.py. The sign atlas carries its own UVs.
            ("Kit_Tile_Floor", "T_Tile_Floor", 1f / 3.2f, 1f / 3.2f, Surface.Opaque),
            ("Kit_Tile_Border", "T_Tile_Border", 1f / 3.2f, 1f / 3.2f, Surface.Opaque),
            ("Kit_Ceiling", "T_Ceiling", 1f / 2.44f, 1f / 2.44f, Surface.Opaque),
            ("Kit_Wall_White", "T_Wall_White", 0.5f, 0.5f, Surface.Opaque),
            ("Kit_Wood_Door", "T_Wood_Door", 1f, 1f, Surface.Opaque),
            ("Kit_Wood_Header", "T_Wood_Header", 0.5f, 0.5f, Surface.Opaque),
            ("Kit_Glass_Frosted", "T_Glass_Frosted", 1f, 1f, Surface.Transparent),
            ("Kit_Glass_Tinted", "T_Glass_Tinted", 1f, 1f, Surface.Transparent),
            ("Kit_Panel_Grey", "T_Panel_Grey", 1f, 1f, Surface.Opaque),
            ("Kit_Metal_Grey", "T_Metal_Grey", 1f, 1f, Surface.Opaque),
            ("Kit_Chrome", "T_Chrome", 2f, 2f, Surface.Opaque),
            ("Kit_Vinyl_Blue", "T_Vinyl_Blue", 2f, 2f, Surface.Opaque),
            ("Kit_Perforated", "T_Perforated", 4f, 4f, Surface.Opaque),
            ("Kit_Plastic_Grey", "T_Plastic_Grey", 1f, 1f, Surface.Opaque),
            ("Kit_Nosing", "T_Nosing", 2f, 2f, Surface.Opaque),
            ("Kit_Light", "T_Light", 1f, 1f, Surface.Emissive),
            ("Kit_Signs", "T_Signs", 1f, 1f, Surface.Opaque),
            // Toilets, waiting tables and the intermediate floor (2026-09-16); must match
            // MAPS in gen_interior_textures.py.
            ("Kit_Tile_Wall", "T_Tile_Wall", 0.5f, 0.5f, Surface.Opaque),
            ("Kit_Tile_Counter", "T_Tile_Counter", 1f / 1.6f, 1f / 1.6f, Surface.Opaque),
            ("Kit_Porcelain", "T_Porcelain", 1f, 1f, Surface.Opaque),
            ("Kit_Paint_Grey", "T_Paint_Grey", 1f, 1f, Surface.Opaque),
            ("Kit_Mirror", "T_Mirror", 1f, 1f, Surface.Opaque),
            ("Kit_Wood_Desk", "T_Wood_Desk", 1f, 1f, Surface.Opaque),
            ("Kit_Concrete", "T_Concrete", 0.5f, 0.5f, Surface.Opaque),
            // CDS south facade and CECADEC east facade (photos 33-35), in CECADEC_North.
            ("Kit_Canvas_Red", "T_Canvas_Red", 0.5f, 0.5f, Surface.Opaque),
            ("Kit_Palm", "T_Palm", 1f, 1f, Surface.Cutout),
            ("Kit_PalmTrunk", "T_PalmTrunk", 1f, 1f, Surface.Opaque),
            ("Kit_Letters", "T_Letters", 1f, 1f, Surface.Cutout),
            ("Kit_Generator", "T_Generator", 0.25f, 0.5f, Surface.Opaque),
            ("Kit_Metal_Red", "T_Metal_Red", 1f, 1f, Surface.Opaque),
            ("Kit_Metal_White", "T_Metal_White", 1f, 1f, Surface.Opaque),
            ("Kit_Globe", "T_Globe", 1f, 1f, Surface.Emissive),
        };

        /// <summary>Opacity of the see-through landmark materials; anything unlisted is clear glass.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, float> TransparentAlpha = new()
        {
            ["Kit_Glass_Frosted"] = 0.88f,
            ["Kit_Glass_Tinted"] = 0.72f,
        };

        /// <summary>
        /// Kit materials, all authored at Tiling (1,1) to match the pieces' per-metre UVs.
        /// The builder assigns the wall ones per building; the rest ride on the prefab.
        /// The terrain UTEZ_* materials are NOT used here — they bake a Tiling tuned for
        /// stretched primitives and turn to noise on a per-metre mesh.
        /// </summary>
        public const string WallMaterial = MaterialFolder + "/Kit_Wall.mat";
        public const string WallRedMaterial = MaterialFolder + "/Kit_Wall_Red.mat";

        private enum Joints { None, Horizontal, Grid }

        // ---- Generation ----------------------------------------------------

        [MenuItem("HORROR-UTEZ/Rebuild Building Kit")]
        public static void EnsureAll()
        {
            Directory.CreateDirectory(TextureFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            var shader = PsxMaterialDefaults.LitShader;
            if (shader == null)
            {
                Debug.LogError($"[KIT] Shader not found: {PsxShaderProperties.Lit}. " +
                               "Has Assets/_Project/Shaders/PsxLit.shader imported without errors?");
                return;
            }

            // Materials — Tiling (1,1), one per surface family.
            var wallMat = WriteMaterial("Kit_Wall", shader,
                new Color(0.70f, 0.66f, 0.58f), new Color(0.58f, 0.54f, 0.46f), Joints.Horizontal);
            WriteMaterial("Kit_Wall_Red", shader,
                new Color(0.42f, 0.10f, 0.09f), new Color(0.32f, 0.07f, 0.06f), Joints.Horizontal,
                new Authored("T_Wall_Red", 0.25f));
            var floorMat = WriteMaterial("Kit_Floor", shader,
                new Color(0.55f, 0.55f, 0.54f), new Color(0.44f, 0.44f, 0.43f), Joints.Grid);
            var trimMat = WriteMaterial("Kit_Trim", shader,
                new Color(0.74f, 0.70f, 0.60f), new Color(0.60f, 0.56f, 0.47f), Joints.Horizontal,
                new Authored("T_Stucco", 1f));
            var kerbMat = WriteMaterial("Kit_Kerb", shader,
                new Color(0.45f, 0.43f, 0.40f), new Color(0.29f, 0.28f, 0.26f), Joints.Grid);
            var padMat = WriteMaterial("Kit_Pad", shader,
                new Color(0.68f, 0.68f, 0.67f), new Color(0.56f, 0.56f, 0.55f), Joints.Grid);
            var roofMat = WriteMaterial("Kit_Roof", shader,
                new Color(0.29f, 0.28f, 0.27f), new Color(0.22f, 0.21f, 0.20f), Joints.None);
            var glassMat = WriteMaterial("Kit_Glass", shader,
                new Color(0.05f, 0.06f, 0.08f), new Color(0.13f, 0.15f, 0.19f), Joints.None,
                new Authored("T_Glass", 1f));
            // Door and window frames on the landmark pieces; no kit piece uses it.
            WriteMaterial("Kit_Frame", shader,
                new Color(0.16f, 0.16f, 0.17f), new Color(0.12f, 0.12f, 0.13f), Joints.None,
                new Authored("T_Frame", 1f));

            // Prefabs — bind each .blend's mesh to its material and a primitive collider.
            int built = 0;
            built += BuildPrefab(WallPrefab, "Kit_Wall_4m", wallMat, collider: true);
            built += BuildPrefab(FloorPrefab, "Kit_Floor_4m", floorMat, collider: true);
            built += BuildPrefab(PilasterPrefab, "Kit_Pilaster", trimMat, collider: true);
            built += BuildPrefab(WindowPrefab, "Kit_Window", glassMat, collider: false);
            built += BuildPrefab(RoofPrefab, "Kit_Roof", roofMat, collider: true);
            built += BuildPrefab(PillarPrefab, "Kit_Pillar", trimMat, collider: true);
            built += BuildPrefab(KerbPrefab, "Kit_Kerb", kerbMat, collider: true);
            built += BuildPrefab(PadPrefab, "Kit_Pad", padMat, collider: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[KIT] {built}/8 prefabs written to {PrefabFolder}; meshes from the .blend " +
                      $"files in {MeshFolder}.");

            foreach (var (name, map, u, v, kind) in LandmarkMaterials)
            {
                var mat = WriteMaterial(name, shader,
                    new Color(0.5f, 0.5f, 0.5f), new Color(0.45f, 0.45f, 0.45f), Joints.None,
                    new Authored(map, u, v));
                switch (kind)
                {
                    case Surface.Transparent:
                        PsxMaterialDefaults.Apply(mat, PsxSurfaceKind.Glass, wettable: false);
                        PsxMaterialDefaults.SetTransparent(mat, true);
                        mat.SetFloat(PsxShaderProperties.AlphaMultiplier,
                            TransparentAlpha.TryGetValue(name, out float alpha) ? alpha : 0.4f);
                        mat.SetFloat(PsxShaderProperties.AlphaClipping, 0f);
                        break;
                    case Surface.Emissive:
                        // Glows and blooms: the globe reads as the light source at night.
                        PsxMaterialDefaults.Apply(mat, PsxSurfaceKind.Emissive, wettable: false);
                        break;
                }
            }
            AssetDatabase.SaveAssets();

            // After the materials: landmarks bind their slots to them by name at import.
            int landmarks = BuildLandmarkPrefab(CecadecNorthPrefab, "CECADEC_North");
            landmarks += BuildLandmarkPrefab(CecadecInteriorPrefab, "CecadecInterior");
            landmarks += BuildLandmarkPrefab(CecadecUpperPrefab, "CecadecUpper");
            AssetDatabase.SaveAssets();
            Debug.Log($"[KIT] {landmarks}/3 landmark prefabs written to {LandmarkPrefabFolder}.");
        }

        // ---- Placement API (used by both builders) -------------------------

        /// <summary>
        /// Instantiates a kit prefab as a scaled child, tags it static and optionally
        /// overrides its material. Returns the instance, or null if the prefab is missing.
        /// </summary>
        public static GameObject Place(GameObject prefab, Transform parent, Vector3 localPos,
            Quaternion localRot, Vector3 localScale, string name, Material matOverride = null)
        {
            if (prefab == null)
                return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;

            if (matOverride != null)
                foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterial = matOverride;

            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);
            return go;
        }

        /// <summary>
        /// Fills a 1-D run [<paramref name="from"/>, <paramref name="to"/>] (world units,
        /// along one axis) with full <see cref="Module"/> pieces plus one shorter offcut.
        /// <paramref name="place"/> maps an along-axis coordinate and a base Y to a local
        /// position; <paramref name="crossScaleYZ"/> is the piece's Y and Z scale factors
        /// (the X factor is derived from the run). Used for walls, window bands and kerbs.
        /// </summary>
        public static void TileLinear(Transform parent, GameObject prefab, Material mat,
            System.Func<float, float, Vector3> place, Quaternion rot, float from, float to,
            float baseY, Vector2 crossScaleYZ, string name)
        {
            if (prefab == null)
                return;

            float span = to - from;
            if (span <= 0.02f)
                return;

            float native = NativeModule;
            int full = Mathf.FloorToInt(span / native + 1e-3f);
            float scale = UtezDimensions.WorldScale;

            float cursor = from;
            int idx = 0;
            for (int i = 0; i < full; i++)
            {
                Place(prefab, parent, place(cursor + native * 0.5f, baseY), rot,
                    new Vector3(scale, crossScaleYZ.x, crossScaleYZ.y), $"{name}_{idx++}", mat);
                cursor += native;
            }

            float rem = to - cursor;
            if (rem > 0.02f)
                Place(prefab, parent, place(cursor + rem * 0.5f, baseY), rot,
                    new Vector3(rem / Module, crossScaleYZ.x, crossScaleYZ.y), $"{name}_{idx}", mat);
        }

        /// <summary>
        /// Tiles module pieces across a rectangle in the parent's local XZ, at height
        /// <paramref name="y"/>. Edge pieces are scaled to fit — a flat slab takes that
        /// without reading as distorted. Used for interior floors, roofs and footprint pads.
        /// </summary>
        public static void TileArea(Transform parent, GameObject prefab, Material mat,
            float minX, float maxX, float minZ, float maxZ, float y, string name)
        {
            if (prefab == null || maxX - minX <= 0.02f || maxZ - minZ <= 0.02f)
                return;

            float native = NativeModule;
            float scale = UtezDimensions.WorldScale;

            float cx = minX;
            int ix = 0;
            while (cx < maxX - 0.02f)
            {
                float w = Mathf.Min(native, maxX - cx);
                float cz = minZ;
                int iz = 0;
                while (cz < maxZ - 0.02f)
                {
                    float d = Mathf.Min(native, maxZ - cz);
                    Place(prefab, parent, new Vector3(cx + w * 0.5f, y, cz + d * 0.5f),
                        Quaternion.identity, new Vector3(w / Module, scale, d / Module),
                        $"{name}_{ix}_{iz}", mat);
                    cz += d;
                    iz++;
                }
                cx += w;
                ix++;
            }
        }

        // ---- Prefab ---------------------------------------------------------

        /// <summary>
        /// Writes the prefab for one piece: the mesh from <c>&lt;pieceName&gt;.blend</c>, the
        /// given material, a primitive box collider sized to the mesh, static flags, and a
        /// root scale of <see cref="UtezDimensions.WorldScale"/> so that dragging the prefab
        /// straight into a scene by hand matches the campus the builders raise (which scale
        /// every instance by the same factor). Returns 1 on success, 0 if the .blend has not
        /// imported a mesh yet.
        /// </summary>
        private static int BuildPrefab(string prefabPath, string pieceName, Material material, bool collider)
        {
            string blendPath = $"{MeshFolder}/{pieceName}.blend";
            var mesh = LoadBlendMesh(blendPath);
            if (mesh == null)
            {
                Debug.LogError($"[KIT] No mesh imported from {blendPath}. If it exists, Unity may " +
                               "not have found Blender (Preferences > External Tools). To recreate " +
                               "it from the box default, run Tools/blender/gen_kit_pieces.py.");
                return 0;
            }

            var go = new GameObject(pieceName);
            try
            {
                go.transform.localScale = Vector3.one * UtezDimensions.WorldScale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;

                if (collider)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center;
                    box.size = mesh.bounds.size;
                }

                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);

                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                return 1;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Writes the prefab for a landmark <c>.blend</c>: its whole object hierarchy (slots
        /// bound to Kit materials by name at import, see PsxKitImporter), axis-corrected
        /// from the file's probe empties, then per-object rules by name — see
        /// <see cref="ApplyLandmarkRules"/>. Returns 1 on success.
        /// </summary>
        private static int BuildLandmarkPrefab(string prefabPath, string pieceName)
        {
            string blendPath = $"{LandmarkFolder}/{pieceName}.blend";
            AssetDatabase.ImportAsset(blendPath, ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(blendPath);
            if (model == null)
            {
                Debug.LogError($"[KIT] Nothing imported from {blendPath}. Is Blender set in " +
                               "Preferences > External Tools? Regenerate it with " +
                               $"Tools/blender/gen_{pieceName.ToLowerInvariant()}.py.");
                return 0;
            }

            if (!AssetDatabase.IsValidFolder(LandmarkPrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Landmarks");

            var root = new GameObject(pieceName);
            try
            {
                root.transform.localScale = Vector3.one * UtezDimensions.WorldScale;
                var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                body.name = "Model";

                if (!AlignAxes(root.transform, body.transform, pieceName))
                    return 0;
                ApplyLandmarkRules(root.transform, pieceName);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return 1;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Landmarks are authored Blender-native (X east, Y north, Z up) with three empties
        /// 10 m out along those axes. Wherever the .blend -> FBX -> Unity chain sends them,
        /// turn (and if it mirrored, flip) the model so east = +X, north = +Z, up = +Y.
        /// </summary>
        internal static bool AlignAxes(Transform root, Transform body, string pieceName)
        {
            var east = FindDeep(body, "Probe_East");
            var north = FindDeep(body, "Probe_North");
            var up = FindDeep(body, "Probe_Up");
            if (east == null || north == null || up == null)
            {
                Debug.LogError($"[KIT] {pieceName}: Probe_East/North/Up empties missing; can't orient it.");
                return false;
            }

            Vector3 e = root.InverseTransformPoint(east.position).normalized;
            Vector3 n = root.InverseTransformPoint(north.position).normalized;
            Vector3 u = root.InverseTransformPoint(up.position).normalized;

            // A rotation keeps e . (u x n) = +1; the chain mirrored the file if it is negative.
            bool mirrored = Vector3.Dot(e, Vector3.Cross(u, n)) < 0f;
            var flip = new Vector3(mirrored ? -1f : 1f, 1f, 1f);
            var q = Quaternion.LookRotation(Vector3.Scale(n, flip), Vector3.Scale(u, flip));
            body.localScale = flip;
            body.localRotation = Quaternion.Inverse(q);
            return true;
        }

        /// <summary>
        /// Per-object setup by Blender object name:
        /// <c>Door_*</c> hinged leaf (box collider, kinematic body, <see cref="HingedDoor"/>);
        /// <c>*_Fluoro</c> empty gets an interior fluorescent light; <c>*_Light</c> empty gets
        /// the campus lamp light; <c>Plant_*</c>, <c>*_Canopy</c>,
        /// <c>Decal_*</c> and glass get no collider; everything else a static mesh collider.
        /// </summary>
        private static void ApplyLandmarkRules(Transform root, string pieceName)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                            StaticEditorFlags.OccluderStatic |
                                            StaticEditorFlags.OccludeeStatic;
            int doors = 0, lights = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null)
                    continue;
                string n = t.name;
                if (n.StartsWith("Probe_"))
                {
                    Object.DestroyImmediate(t.gameObject);
                    continue;
                }
                if (n.EndsWith("_Fluoro"))
                {
                    AddFluorescentLight(t);
                    lights++;
                    continue;
                }
                if (n.EndsWith("_Light"))
                {
                    AddLampLight(t);
                    lights++;
                    continue;
                }

                var filter = t.GetComponent<MeshFilter>();
                if (n.StartsWith("Door_") && filter != null)
                {
                    SetupDoor(t, root);
                    doors++;
                    continue;
                }

                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                // Ceil_* (light fittings, vents) are out of reach and Detail_* (handles, hinges,
                // switches) too small to be worth a MeshCollider each.
                bool soft = n.StartsWith("Plant_") || n.EndsWith("_Canopy") || n.Contains("Glass") ||
                            n.StartsWith("Decal_") || n.StartsWith("Ceil_") || n.StartsWith("Detail_");
                if (filter != null && !soft)
                    t.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;

                var renderer = t.GetComponent<MeshRenderer>();
                if (renderer != null)
                    foreach (var m in renderer.sharedMaterials)
                        if (m == null || !AssetDatabase.GetAssetPath(m).StartsWith(MaterialFolder, System.StringComparison.Ordinal))
                            Debug.LogWarning($"[KIT] {pieceName}/{n}: slot '{(m != null ? m.name : "none")}' " +
                                             $"is not a Kit material. Name the Blender slot after one in {MaterialFolder}.");
            }
            Debug.Log($"[KIT] {pieceName}: {doors} hinged doors, {lights} lamp lights.");
        }

        /// <summary>
        /// Leaf pivots on its hinge (authored so). The swing sign is measured, not assumed:
        /// try +90 and keep it if the leaf moves the right way — out of the building (+Z of
        /// the root) for a leaf on the facade line, away from the corridor axis (x = 0 of the
        /// root, so into its room, or outdoors for the cross corridor's east doors) otherwise.
        /// </summary>
        private static void SetupDoor(Transform leaf, Transform root)
        {
            var go = leaf.gameObject;
            go.AddComponent<BoxCollider>();
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            Vector3 axis = leaf.InverseTransformDirection(root.up).normalized;
            var closed = leaf.localRotation;
            Vector3 centre = go.GetComponent<MeshFilter>().sharedMesh.bounds.center;
            Vector3 before = root.InverseTransformPoint(leaf.TransformPoint(centre));
            leaf.localRotation = closed * Quaternion.AngleAxis(90f, axis);
            Vector3 after = root.InverseTransformPoint(leaf.TransformPoint(centre));
            leaf.localRotation = closed;

            // Root-local units are real metres (the root carries WorldScale).
            bool facade = Mathf.Abs(before.z) < 0.5f;
            bool plus = facade ? after.z > before.z : Mathf.Abs(after.x) > Mathf.Abs(before.x);
            go.AddComponent<HingedDoor>().Configure(axis, plus ? 90f : -90f);
        }

        /// <summary>
        /// Interior ceiling fitting (<c>*_Fluoro</c> empty): a dim, greenish fluorescent pool,
        /// enough to read the whole corridor without the flat white of a lit office. No
        /// shadows (mobile), short range so each floor or wall piece sits under only a few.
        /// <see cref="FluorescentFlicker"/> makes it stutter and dims failing tubes.
        /// </summary>
        private static void AddFluorescentLight(Transform anchor)
        {
            float scale = UtezDimensions.WorldScale;
            var light = anchor.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.86f, 0.95f, 0.78f);
            light.range = 5.5f * scale;
            light.intensity = 3.2f * scale * scale;
            light.shadows = LightShadows.None;
            anchor.gameObject.AddComponent<FluorescentFlicker>();
        }

        /// <summary>Same light as the campus lamp posts (UtezPropBuilder), a touch whiter.</summary>
        private static void AddLampLight(Transform anchor)
        {
            float scale = UtezDimensions.WorldScale;
            var light = anchor.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.66f);
            light.range = 18f * scale;
            light.intensity = 45f * scale * scale;
            light.shadows = LightShadows.None;
            anchor.gameObject.AddComponent<LampFlicker>();
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        /// <summary>First mesh sub-asset of an imported model file, or null.</summary>
        private static Mesh LoadBlendMesh(string blendPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(blendPath))
                if (asset is Mesh mesh)
                    return mesh;
            return null;
        }

        // ---- Material + texture ------------------------------------------------

        /// <summary>
        /// A painted albedo map in <see cref="TextureFolder"/> (see
        /// <c>Tools/blender/gen_facade_textures.py</c>) and the Tiling that fits it to the
        /// per-metre UVs: 1 for a map covering one metre, 0.25 for one covering 4 x 4 m.
        /// </summary>
        private readonly struct Authored
        {
            public readonly string File;
            public readonly Vector2 Tiling;

            public Authored(string file, float tiling) : this(file, tiling, tiling) { }

            public Authored(string file, float u, float v)
            {
                File = file;
                Tiling = new Vector2(u, v);
            }
        }

        private static Material WriteMaterial(string name, Shader shader, Color baseColor,
            Color accent, Joints joints, Authored? authored = null)
        {
            var placeholder = WriteTexture(name, baseColor, accent, joints);
            var art = authored.HasValue
                ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{authored.Value.File}.png")
                : null;

            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            // A texture dragged into MainTex by hand wins over both defaults, Tiling included.
            var current = mat.GetTexture(PsxShaderProperties.MainTex);
            bool handAssigned = current != null && current != placeholder && current != art;
            if (!handAssigned)
            {
                mat.SetTexture(PsxShaderProperties.MainTex, art != null ? art : placeholder);
                mat.SetVector(PsxShaderProperties.Tiling, art != null ? authored.Value.Tiling : Vector2.one);
            }
            PsxMaterialDefaults.Apply(mat, PsxMaterialDefaults.Classify(name), PsxMaterialDefaults.IsWettable(name));
            PsxMaterialDefaults.SetTransparent(mat, false);
            return mat;
        }

        /// <summary>
        /// A 128px placeholder mapping to one square metre: a horizontal panel joint per
        /// metre (walls, trim), a scored grid (floors, pads, kerbs), or flat (roof, glass).
        /// Replaced when real art lands — drop a texture in Assets/_Project/Art/Textures/Kit/
        /// and point MainTex at it.
        /// </summary>
        private static Texture2D WriteTexture(string name, Color baseColor, Color accent, Joints joints)
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            var rng = new System.Random(name.GetHashCode());

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool joint = joints switch
                {
                    Joints.Horizontal => y < 2 || y > size - 2,
                    Joints.Grid => x % 64 < 1 || y % 64 < 1,
                    _ => false,
                };
                Color c = joint ? accent : Color.Lerp(baseColor, accent, (float)rng.NextDouble() * 0.06f);
                pixels[y * size + x] = c;
            }

            // Mipmapped: without them a point-sampled texture shimmers into noise at distance.
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 0,
            };
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: true);

            string path = $"{TextureFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(tex, path);
                return tex;
            }

            EditorUtility.CopySerialized(tex, existing);
            Object.DestroyImmediate(tex);
            return existing;
        }
    }
}
