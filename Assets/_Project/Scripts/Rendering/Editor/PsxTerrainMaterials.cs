using System.IO;
using UnityEditor;
using UnityEngine;

namespace HorrorUtez.Rendering.Editor
{
    /// <summary>
    /// Generates the placeholder PSX materials the terrain uses, plus the small
    /// procedural textures they sample.
    ///
    /// The PSX master graphs expose no base colour — albedo comes only from MainTex —
    /// so flat-coloured terrain needs real textures. These are 128px, point filtered
    /// and mip-free on purpose: that IS the PS1 look, and it gives the affine warping
    /// and pixelation something to act on.
    ///
    /// Placeholder quality. Replaced when real art lands.
    /// </summary>
    public static class PsxTerrainMaterials
    {
        public const string MaterialFolder = "Assets/_Project/Art/Materials";
        public const string TextureFolder = "Assets/_Project/Art/Textures";
        private const int TexSize = 128;

        private enum Pattern { Grid, Noise, Blocks, Panels, Foliage }

        private readonly struct Def
        {
            public readonly string Name;
            public readonly Color Base;
            public readonly Color Accent;
            public readonly Pattern Pattern;
            /// <summary>Baked per material: these slabs are scaled cubes, so UVs do not follow world size.</summary>
            public readonly Vector2 Tiling;

            public Def(string name, Color baseColor, Color accent, Pattern pattern, Vector2 tiling)
            {
                Name = name;
                Base = baseColor;
                Accent = accent;
                Pattern = pattern;
                Tiling = tiling;
            }
        }

        private static readonly Def[] Defs =
        {
            // Scored concrete slabs, as in the CECADEC entrance photo.
            new("UTEZ_Concrete", new Color(0.62f, 0.62f, 0.60f), new Color(0.50f, 0.50f, 0.49f), Pattern.Grid, new Vector2(19f, 27f)),
            new("UTEZ_Pad", new Color(0.70f, 0.70f, 0.70f), new Color(0.60f, 0.60f, 0.60f), Pattern.Grid, new Vector2(5f, 5f)),
            new("UTEZ_Grass", new Color(0.20f, 0.28f, 0.13f), new Color(0.14f, 0.21f, 0.09f), Pattern.Noise, new Vector2(7f, 13f)),
            new("UTEZ_Dirt", new Color(0.36f, 0.28f, 0.19f), new Color(0.28f, 0.21f, 0.14f), Pattern.Noise, new Vector2(49f, 57f)),
            // Irregular stacked stone, as in the kerbs around the grass.
            new("UTEZ_Stone", new Color(0.45f, 0.43f, 0.40f), new Color(0.30f, 0.29f, 0.27f), Pattern.Blocks, new Vector2(5f, 2f)),
            new("UTEZ_ForestMarker", new Color(0.10f, 0.16f, 0.10f), new Color(0.05f, 0.09f, 0.05f), Pattern.Noise, new Vector2(10f, 4f)),

            // CECADEC's oxblood facade, with the horizontal panel joints from the entrance photo.
            new("UTEZ_WallRed", new Color(0.42f, 0.10f, 0.09f), new Color(0.32f, 0.07f, 0.06f), Pattern.Panels, new Vector2(6f, 3f)),
            // The pale pilasters flanking the entrance, reused for CDS and the auditorium.
            new("UTEZ_WallBeige", new Color(0.72f, 0.66f, 0.55f), new Color(0.60f, 0.55f, 0.45f), Pattern.Panels, new Vector2(6f, 3f)),
            new("UTEZ_Roof", new Color(0.30f, 0.29f, 0.28f), new Color(0.23f, 0.22f, 0.21f), Pattern.Noise, new Vector2(8f, 8f)),

            // Foliage. The vendored forest pack ships no tree textures, so its materials
            // render pure white; these replace them and, being PSX materials, also give the
            // trees the vertex jitter the pack's plain URP/Lit materials could not.
            new("UTEZ_Bark", new Color(0.24f, 0.17f, 0.12f), new Color(0.16f, 0.11f, 0.08f), Pattern.Panels, new Vector2(1f, 3f)),
            new("UTEZ_Leaves", new Color(0.15f, 0.26f, 0.12f), new Color(0.07f, 0.13f, 0.06f), Pattern.Foliage, new Vector2(2f, 2f)),
            new("UTEZ_LeavesDry", new Color(0.24f, 0.23f, 0.13f), new Color(0.13f, 0.13f, 0.07f), Pattern.Foliage, new Vector2(2f, 2f)),
            new("UTEZ_Glass", new Color(0.04f, 0.05f, 0.07f), new Color(0.16f, 0.18f, 0.22f), Pattern.Grid, new Vector2(10f, 1f)),
            new("UTEZ_RockGrey", new Color(0.34f, 0.33f, 0.31f), new Color(0.22f, 0.21f, 0.20f), Pattern.Blocks, new Vector2(2f, 2f)),
        };

        [MenuItem("HORROR-UTEZ/Rebuild PSX Terrain Materials")]
        public static void EnsureAll()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(TextureFolder);

            var shader = PsxMaterialDefaults.LitShader;
            if (shader == null)
            {
                Debug.LogError($"[PSX] Shader not found: {PsxShaderProperties.Lit}. Has PsxLit.shader imported without errors?");
                return;
            }

            foreach (var def in Defs)
            {
                var tex = BuildTexture(def);
                string texPath = $"{TextureFolder}/{def.Name}.asset";
                var existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (existingTex == null)
                    AssetDatabase.CreateAsset(tex, texPath);
                else
                {
                    EditorUtility.CopySerialized(tex, existingTex);
                    Object.DestroyImmediate(tex);
                }

                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                WriteMaterial(def, shader, tex);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PSX] {Defs.Length} terrain materials written to {MaterialFolder}");
        }

        /// <summary>
        /// Re-bakes only the generated texture assets, in place (same GUID), leaving every
        /// material alone — so a texture someone dragged onto a material by hand survives.
        /// </summary>
        public static void RegenerateTextures()
        {
            int rebuilt = 0;
            foreach (var def in Defs)
            {
                string texPath = $"{TextureFolder}/{def.Name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (existing == null)
                    continue;

                var tex = BuildTexture(def);
                EditorUtility.CopySerialized(tex, existing);
                Object.DestroyImmediate(tex);
                EditorUtility.SetDirty(existing);
                rebuilt++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PSX] Re-baked {rebuilt} terrain textures (clumped noise, mipmapped).");
        }

        private static void WriteMaterial(Def def, Shader shader, Texture2D tex)
        {
            string path = $"{MaterialFolder}/{def.Name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            mat.SetTexture(PsxShaderProperties.MainTex, tex);
            mat.SetVector(PsxShaderProperties.Tiling, def.Tiling);

            PsxMaterialDefaults.Apply(mat, PsxMaterialDefaults.Classify(def.Name),
                PsxMaterialDefaults.IsWettable(def.Name));
            PsxMaterialDefaults.SetTransparent(mat, false);
        }

        private static Texture2D BuildTexture(Def def)
        {
            // Mipmapped: without them the point-sampled grass and dirt crawl at distance.
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, mipChain: true)
            {
                name = def.Name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 0,
            };

            // Seeded per material so repeated runs produce identical assets.
            var rng = new System.Random(def.Name.GetHashCode());
            var pixels = new Color32[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            for (int x = 0; x < TexSize; x++)
            {
                Color c = def.Pattern switch
                {
                    Pattern.Grid => GridPixel(def, x, y),
                    Pattern.Blocks => BlocksPixel(def, x, y, rng),
                    Pattern.Panels => PanelsPixel(def, y),
                    Pattern.Foliage => FoliagePixel(def, x, y),
                    _ => NoisePixel(def, x, y),
                };
                pixels[y * TexSize + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: true);
            return tex;
        }

        /// <summary>Scored slabs: a darker joint line on a fixed grid.</summary>
        private static Color GridPixel(Def def, int x, int y)
        {
            const int cell = 32;
            bool joint = x % cell == 0 || y % cell == 0 || x % cell == 1 || y % cell == 1;
            return joint ? def.Accent : def.Base;
        }

        /// <summary>
        /// Clumped, not per-pixel. Full-range noise on every texel was the television static
        /// on the grass: at 7x13 tiling each screen pixel landed on an unrelated value.
        /// Blocks of 4 with a narrow range read as tufts and soil grains instead.
        /// </summary>
        private static Color NoisePixel(Def def, int x, int y)
        {
            const int block = 4;
            int h = (x / block) * 73856093 ^ (y / block) * 19349663 ^ def.Name.Length * 83492791;
            float t = ((h & 0x7fffffff) % 1000) / 1000f;
            return Color.Lerp(def.Base, def.Accent, 0.2f + t * 0.6f);
        }

        /// <summary>
        /// Leaf clumps, not per-pixel noise.
        ///
        /// Random noise at 128px reads as television static once it is stretched over a
        /// canopy. Quantising to blocks gives the eye something the size of a leaf cluster
        /// to latch onto, which is what actually looks like foliage at PS1 resolution.
        /// </summary>
        private static Color FoliagePixel(Def def, int x, int y)
        {
            const int block = 8;
            int bx = x / block;
            int by = y / block;
            // Cheap deterministic hash per block.
            int h = bx * 73856093 ^ by * 19349663;
            float t = ((h & 0x7fffffff) % 1000) / 1000f;
            return Color.Lerp(def.Base, def.Accent, t);
        }

        /// <summary>Flat facade broken only by horizontal panel joints, as on CECADEC.</summary>
        private static Color PanelsPixel(Def def, int y)
        {
            const int panelHeight = 42;
            bool joint = (y % panelHeight) < 2;
            return joint ? def.Accent : def.Base;
        }

        /// <summary>Irregular stacked stone: offset rows of blocks with dark mortar.</summary>
        private static Color BlocksPixel(Def def, int x, int y, System.Random rng)
        {
            const int rowH = 24;
            int row = y / rowH;
            int offset = (row % 2) * 16;
            int blockW = 30;
            bool mortar = (y % rowH) < 2 || ((x + offset) % blockW) < 2;
            if (mortar)
                return def.Accent;
            return Color.Lerp(def.Base, def.Accent, (float)rng.NextDouble() * 0.35f);
        }
    }
}
