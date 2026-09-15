using UnityEditor;
using UnityEngine;

namespace HorrorUtez.Rendering.Editor
{
    /// <summary>
    /// Import settings for the environment art:
    /// <list type="bullet">
    /// <item>kit <c>.blend</c> pieces in <c>Assets/_Project/Art/Environment/Kit/</c> — one
    ///   material slot, bound by the prefab;</item>
    /// <item>landmark <c>.blend</c> pieces in <c>Assets/_Project/Art/Environment/Landmarks/</c>
    ///   — bespoke, several slots, each bound to the Kit material of the same name;</item>
    /// <item>textures dropped into <c>Assets/_Project/Art/Textures/Kit/</c>.</item>
    /// </list>
    ///
    /// Textures import at full detail. The PSX look is applied at render time — per-material
    /// UV pixelation and colour precision, then the PSX Screen pass — so crushing the source
    /// here only threw the detail away twice.
    /// </summary>
    public sealed class PsxKitImporter : AssetPostprocessor
    {
        private const string TextureRoot = "Assets/_Project/Art/Textures/Kit/";
        private const string KitMeshRoot = "Assets/_Project/Art/Environment/Kit/";
        private const string LandmarkRoot = "Assets/_Project/Art/Environment/Landmarks/";
        private const string KitMaterialFolder = "Assets/_Project/Art/Materials/Kit";

        private const int MaxTextureSize = 2048;
        private const int MaxTextureSizeAndroid = 1024;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureRoot, System.StringComparison.Ordinal))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = MaxTextureSize;
            importer.filterMode = FilterMode.Bilinear;
            // Anisotropic filtering undoes the affine warping at grazing angles.
            importer.anisoLevel = 0;
            importer.mipmapEnabled = true;
            importer.mipMapBias = 0f;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.textureCompression = TextureImporterCompression.Compressed;

            // Foliage is alpha-tested cards: without coverage-preserving mips the crowns
            // thin out to bare branches a few metres away.
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipMapsPreserveCoverage = assetPath.Contains("Foliage");
            importer.alphaTestReferenceValue = 0.5f;

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = MaxTextureSizeAndroid;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(android);
        }

        private void OnPreprocessModel()
        {
            bool kit = assetPath.StartsWith(KitMeshRoot, System.StringComparison.Ordinal);
            bool landmark = assetPath.StartsWith(LandmarkRoot, System.StringComparison.Ordinal);
            if (!kit && !landmark)
                return;

            var importer = (ModelImporter)assetImporter;
            // Pieces are authored at real metres in Blender; the builders scale by
            // WorldScale. Keep Unity's own unit conversion out of it.
            importer.globalScale = 1f;
            importer.useFileScale = true;

            // Bake Blender's Z-up into the mesh so the raw Mesh sub-asset the prefabs
            // reference is already Y-up. Without this the wall pieces import lying flat.
            importer.bakeAxisConversion = true;

            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;

            // Colliders live on the prefab: a primitive box for kit pieces.
            importer.addCollider = false;

            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshVertices = true;
            importer.optimizeMeshPolygons = true;
            importer.weldVertices = true;

            if (kit)
            {
                // Kit materials are authored by UtezKit; a URP/Lit one from the importer
                // would only have to be deleted again.
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                return;
            }

            // Landmark slots are named after the Kit materials, so adding a slot in Blender
            // needs no Unity-side step. A misnamed slot shows up as an embedded URP/Lit
            // material on the model, which is the cue to rename it.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { KitMaterialFolder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material != null)
                    importer.AddRemap(
                        new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), material);
            }
        }
    }
}
