namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Property reference names for the vendored URP-PSX master shaders.
    ///
    /// The original graphs never set reference names, so every property ships with an
    /// auto-generated id like "Boolean_43476D73". These constants restore the meaning.
    /// Extracted from the .shadergraph JSON; regenerate with
    /// HORROR-UTEZ > Dump PSX Shader Properties if the graphs are ever re-authored.
    ///
    /// Note: the graphs expose NO base colour. Albedo comes from <see cref="MainTex"/> only.
    /// </summary>
    public static class PsxShaderProperties
    {
        public const string UnlitMaster = "Shader Graphs/URP_PSX_Unlit_Master";
        public const string PbrMaster = "Shader Graphs/URP_PSX_PBR_Master";
        public const string PbrMasterTransparent = "Shader Graphs/URP_PSX_PBR_Master_with_transparency";

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
    }
}
