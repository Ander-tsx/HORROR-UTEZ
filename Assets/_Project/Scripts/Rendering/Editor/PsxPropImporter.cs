using UnityEditor;
using UnityEngine;

namespace HorrorUtez.Rendering.Editor
{
    /// <summary>
    /// Import settings for the vendored prop meshes and their textures.
    ///
    /// Downloaded models arrive tuned for offline rendering: 1k+ textures, trilinear
    /// filtering, mesh read/write left on. None of that survives contact with a mobile
    /// build, and the crisp filtering actively fights the PSX look. Doing it here rather
    /// than by hand keeps the source files untouched and reversible — delete the .meta
    /// and the asset comes back with these settings, not with somebody's forgotten click.
    /// </summary>
    public sealed class PsxPropImporter : AssetPostprocessor
    {
        private const string PropRoot = "Assets/ThirdParty/Props/";

        /// <summary>
        /// PS1 textures were 256px at most. 512 keeps the downloaded detail readable at
        /// arm's length without paying for pixels the pixelation pass throws away anyway.
        /// </summary>
        private const int MaxTextureSize = 512;

        private bool IsProp => assetPath.StartsWith(PropRoot, System.StringComparison.Ordinal);

        private void OnPreprocessTexture()
        {
            if (!IsProp)
                return;

            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = MaxTextureSize;
            // Point filtering IS the look. Bilinear on a 512 map smears the pixelation pass
            // into mush, and anisotropic filtering undoes the affine warping at grazing angles.
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 0;
            importer.mipmapEnabled = true;
            importer.mipMapBias = 0f;
            // The bench map is 1728x1080. Non-power-of-two blocks compression outright, so
            // it has to be rescaled at import or it lands in memory uncompressed.
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }

        private void OnPreprocessModel()
        {
            if (!IsProp)
                return;

            var importer = (ModelImporter)assetImporter;
            // Every prop is exported at real metres with FBX_SCALE_ALL, so Unity must not
            // apply a unit conversion on top.
            importer.globalScale = 1f;
            importer.useFileScale = true;

            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;

            // Colliders come from the prefab pass, which picks a primitive per prop. A mesh
            // collider on a decimated bench is far more physics cost than the shape is worth.
            importer.addCollider = false;

            // Nothing reads these meshes from script, so keeping a CPU copy only doubles
            // their memory. This is the single biggest mobile win in the whole importer.
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshVertices = true;
            importer.optimizeMeshPolygons = true;
            importer.weldVertices = true;

            // Materials are authored by UtezPropAssets against the PSX master graphs; letting
            // the importer generate URP/Lit ones just creates duplicates to delete later.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
