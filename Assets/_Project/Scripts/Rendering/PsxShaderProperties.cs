namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Property reference names for the PSX surface shaders.
    ///
    /// The vendored URP-PSX graphs never set reference names, so every property ships with
    /// an auto-generated id like "Boolean_43476D73". These constants restore the meaning.
    /// <see cref="Lit"/> — the project's own shader, which replaced the graphs — declares the
    /// very same ids, so a material moves between them without losing a value.
    /// </summary>
    public static class PsxShaderProperties
    {
        /// <summary>The project's PSX surface shader. Everything new should use this.</summary>
        public const string Lit = "HorrorUtez/PSX/Lit";
        public const string Puddle = "HorrorUtez/PSX/Puddle";

        public const string UnlitMaster = "Shader Graphs/URP_PSX_Unlit_Master";
        public const string PbrMaster = "Shader Graphs/URP_PSX_PBR_Master";
        public const string PbrMasterTransparent = "Shader Graphs/URP_PSX_PBR_Master_with_transparency";

        /// <summary>True for the vendored graphs and for <see cref="Lit"/>.</summary>
        public static bool IsPsxSurface(string shaderName) =>
            shaderName == Lit ||
            shaderName.StartsWith("Shader Graphs/URP_PSX", System.StringComparison.Ordinal);

        // PsxLit only.
        public const string BaseColor = "_BaseColor";
        public const string EmissionMap = "_EmissionMap";
        public const string EmissionColor = "_EmissionColor";
        public const string Wettable = "_Wettable";
        public const string SnapSlide = "_PsxSnapSlide";
        public const string Surface = "_Surface";
        public const string SrcBlend = "_SrcBlend";
        public const string DstBlend = "_DstBlend";
        public const string SrcBlendAlpha = "_SrcBlendAlpha";
        public const string DstBlendAlpha = "_DstBlendAlpha";
        public const string ZWrite = "_ZWrite";
        public const string Cull = "_Cull";

        // Surface
        public const string MainTex = "Texture2D_4450AB74";
        public const string Tiling = "Vector2_8044833E";
        public const string IsLit = "Boolean_8BBF99CD";
        public const string IsSpecular = "Boolean_7165A49C";
        public const string Specular = "Color_2E5415DE";
        public const string Smoothness = "Vector1_6F288C5B";

        // Vertex snapping / jitter — the PS1 wobble.
        public const string UseVertexJitter = "Boolean_43476D73";
        public const string VertexResolution = "Vector1_B2CC132F";

        // Affine texture warping — the PS1 texture "swim".
        public const string UseAffine = "Boolean_85059BBE";
        public const string AffineThreshold = "Vector1_B9994903";

        // Texture pixelation.
        public const string UsePixelation = "Boolean_193028D2";
        public const string TextureResolution = "Vector1_21C41D02";

        // Colour depth reduction.
        public const string UseColorPrecision = "Boolean_3F1A8DAB";
        public const string ColorPrecision = "Vector1_E8746023";

        // Distance-based vertex clipping.
        public const string UseCameraClipping = "Boolean_D258FF8E";

        // Transparency master only: alpha = MainTex.a * multiplier, clipped below the threshold.
        public const string AlphaClipping = "_Alpha_Clipping";
        public const string AlphaMultiplier = "_Alpha_Multiplier";
    }
}
