using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorUtez.Rendering.Editor
{
    /// <summary>What a surface is made of, as far as light is concerned.</summary>
    public enum PsxSurfaceKind
    {
        /// <summary>Concrete, plaster, soil, bark. Rough, no visible highlight.</summary>
        Matte,
        /// <summary>Paint, plastic, wood, vinyl. A soft sheen.</summary>
        Satin,
        /// <summary>Glazed tile, porcelain, polished counters. Clear highlights.</summary>
        Gloss,
        /// <summary>Chrome, steel, mirrors. Coloured reflectance, dark diffuse.</summary>
        Metal,
        /// <summary>Window glass. Nearly a mirror.</summary>
        Glass,
        /// <summary>Leaf cards. Alpha-cut and double sided when the texture has alpha.</summary>
        Foliage,
        /// <summary>Lamps, globes, screens. Glows and blooms.</summary>
        Emissive,
        /// <summary>Signs and lettering. Kept sharper so they stay readable.</summary>
        Text,
    }

    /// <summary>
    /// The single source of truth for PSX material settings. Every generator and the
    /// migration write through here, so the look cannot drift material by material — the
    /// style guide's first hard rule.
    ///
    /// Why these numbers, against the reference "PS1 render" look:
    /// - Colour depth is OFF per material. At 6 levels with floor() it turned every lightly
    ///   noisy texture into salt-and-pepper speckle; the screen pass already crushes colour.
    /// - Vertex snapping on a 320-row grid: the PS1 wobble is visible when the camera moves
    ///   and invisible when it stops.
    /// - Affine warping off: the references have none, and on the campus's big floor quads it
    ///   swims enough to read as broken.
    /// - 256 texels per repeat, 1024 for text, point sampled with mipmaps.
    /// </summary>
    public static class PsxMaterialDefaults
    {
        public const float VertexSnapRows = 320f;
        public const float TexelsPerRepeat = 256f;
        public const float TextTexelsPerRepeat = 1024f;
        public const float AffineStrength = 0.3f;

        public static Shader LitShader => Shader.Find(PsxShaderProperties.Lit);

        /// <summary>Writes the house settings for <paramref name="kind"/> onto a PsxLit material.</summary>
        public static void Apply(Material mat, PsxSurfaceKind kind, bool wettable)
        {
            var texture = mat.GetTexture(PsxShaderProperties.MainTex) as Texture2D;

            mat.SetColor(PsxShaderProperties.BaseColor, Color.white);
            mat.SetFloat(PsxShaderProperties.IsLit, 1f);
            mat.SetFloat(PsxShaderProperties.Wettable, wettable ? 1f : 0f);

            mat.SetFloat(PsxShaderProperties.UseVertexJitter, 1f);
            mat.SetFloat(PsxShaderProperties.VertexResolution, VertexSnapRows);
            // Leaf cards have no one plane to slide along; see PsxSnapVertex.
            mat.SetFloat(PsxShaderProperties.SnapSlide, kind == PsxSurfaceKind.Foliage ? 0f : 1f);

            mat.SetFloat(PsxShaderProperties.UseAffine, 0f);
            mat.SetFloat(PsxShaderProperties.AffineThreshold, AffineStrength);

            float cap = kind == PsxSurfaceKind.Text ? TextTexelsPerRepeat : TexelsPerRepeat;
            float texels = texture != null ? Mathf.Min(texture.width, cap) : cap;
            mat.SetFloat(PsxShaderProperties.UsePixelation, 1f);
            mat.SetFloat(PsxShaderProperties.TextureResolution, texels);

            mat.SetFloat(PsxShaderProperties.UseColorPrecision, 0f);
            mat.SetFloat(PsxShaderProperties.ColorPrecision, 32f);
            mat.SetFloat(PsxShaderProperties.UseCameraClipping, 0f);

            var (smoothness, metalSpecular) = kind switch
            {
                PsxSurfaceKind.Satin => (0.42f, (Color?)null),
                PsxSurfaceKind.Gloss => (0.72f, null),
                PsxSurfaceKind.Metal => (0.78f, new Color(0.72f, 0.73f, 0.76f)),
                PsxSurfaceKind.Glass => (0.95f, null),
                PsxSurfaceKind.Foliage => (0.12f, null),
                PsxSurfaceKind.Emissive => (0.5f, null),
                PsxSurfaceKind.Text => (0.35f, null),
                _ => (0.15f, null),
            };
            mat.SetFloat(PsxShaderProperties.Smoothness, smoothness);
            mat.SetFloat(PsxShaderProperties.IsSpecular, metalSpecular.HasValue ? 1f : 0f);
            if (metalSpecular.HasValue)
                mat.SetColor(PsxShaderProperties.Specular, metalSpecular.Value);

            bool cutout = kind == PsxSurfaceKind.Foliage && HasAlpha(texture);
            SetAlphaClip(mat, cutout, 0.5f);
            mat.SetFloat(PsxShaderProperties.Cull, cutout ? (float)CullMode.Off : (float)CullMode.Back);

            if (kind == PsxSurfaceKind.Emissive)
                SetEmission(mat, Color.white * 2.4f, texture);
            else
                SetEmission(mat, Color.black, null);

            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Premultiplied transparency: diffuse fades with alpha, reflections do not, which is
        /// what makes glass read as glass rather than as a tinted hole.
        /// </summary>
        public static void SetTransparent(Material mat, bool transparent)
        {
            mat.SetFloat(PsxShaderProperties.Surface, transparent ? 1f : 0f);
            if (transparent)
            {
                mat.SetFloat(PsxShaderProperties.SrcBlend, (float)BlendMode.One);
                mat.SetFloat(PsxShaderProperties.DstBlend, (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat(PsxShaderProperties.SrcBlendAlpha, (float)BlendMode.One);
                mat.SetFloat(PsxShaderProperties.DstBlendAlpha, (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat(PsxShaderProperties.ZWrite, 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat(PsxShaderProperties.SrcBlend, (float)BlendMode.One);
                mat.SetFloat(PsxShaderProperties.DstBlend, (float)BlendMode.Zero);
                mat.SetFloat(PsxShaderProperties.SrcBlendAlpha, (float)BlendMode.One);
                mat.SetFloat(PsxShaderProperties.DstBlendAlpha, (float)BlendMode.Zero);
                mat.SetFloat(PsxShaderProperties.ZWrite, 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.SetOverrideTag("RenderType", "");
                mat.renderQueue = -1;
            }
            EditorUtility.SetDirty(mat);
        }

        public static void SetAlphaClip(Material mat, bool on, float threshold)
        {
            mat.SetFloat(PsxShaderProperties.AlphaClipping, threshold);
            if (on)
            {
                mat.EnableKeyword("_ALPHATEST_ON");
                if (mat.GetFloat(PsxShaderProperties.Surface) < 0.5f)
                {
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                }
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
                if (mat.GetFloat(PsxShaderProperties.Surface) < 0.5f)
                {
                    mat.SetOverrideTag("RenderType", "");
                    mat.renderQueue = -1;
                }
            }
        }

        public static void SetEmission(Material mat, Color hdrColor, Texture map)
        {
            bool on = hdrColor.maxColorComponent > 0.001f;
            mat.SetColor(PsxShaderProperties.EmissionColor, hdrColor);
            mat.SetTexture(PsxShaderProperties.EmissionMap, map);
            if (on)
                mat.EnableKeyword("_EMISSION");
            else
                mat.DisableKeyword("_EMISSION");
            // Lets the lightmapper treat the surface as a light when the scene is baked.
            mat.globalIlluminationFlags = on
                ? MaterialGlobalIlluminationFlags.BakedEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        /// <summary>
        /// Best guess from the material's name. The campus materials are named after what
        /// they are (Kit_Tile_Floor, Prop_Chrome, UTEZ_Grass), which is enough for a default;
        /// anything misjudged is one Inspector edit away and survives until the next retune.
        /// </summary>
        public static PsxSurfaceKind Classify(string materialName)
        {
            string n = materialName.ToLowerInvariant();

            // Bare metal only. Kit_Metal_Red and friends are painted, which is Satin below.
            if (Has(n, "mirror", "chrome", "steel", "alumin", "perforated"))
                return PsxSurfaceKind.Metal;
            if (Has(n, "glass"))
                return PsxSurfaceKind.Glass;
            if (Has(n, "light", "globe", "lamp", "bulb", "neon", "screen"))
                return PsxSurfaceKind.Emissive;
            if (Has(n, "sign", "letter", "poster", "label", "text"))
                return PsxSurfaceKind.Text;
            if (Has(n, "foliage", "leaves", "leaf") || (n.Contains("palm") && !n.Contains("trunk")))
                return PsxSurfaceKind.Foliage;
            if (Has(n, "tile", "porcelain", "counter", "glaze"))
                return PsxSurfaceKind.Gloss;
            if (Has(n, "paint", "metal", "wood", "plastic", "vinyl", "canvas", "frame", "door", "panel",
                    "nosing", "bench", "extinguisher", "generator"))
                return PsxSurfaceKind.Satin;

            return PsxSurfaceKind.Matte;
        }

        /// <summary>Outdoor ground and anything flat that sits under open sky.</summary>
        public static bool IsWettable(string materialName)
        {
            string n = materialName.ToLowerInvariant();
            return Has(n, "concrete", "pad", "kerb", "slab", "stone", "aggregate", "apron",
                "asphalt", "soil", "dirt", "grass", "rock", "roof", "paver", "bench");
        }

        public static bool HasAlpha(Texture2D texture)
        {
            if (texture == null)
                return false;

            string path = AssetDatabase.GetAssetPath(texture);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                return importer.DoesSourceTextureHaveAlpha() &&
                       importer.alphaSource != TextureImporterAlphaSource.None;

            return UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(texture.graphicsFormat);
        }

        private static bool Has(string haystack, params string[] needles)
        {
            foreach (string needle in needles)
                if (haystack.Contains(needle))
                    return true;
            return false;
        }
    }
}
