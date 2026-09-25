using UnityEditor;
using UnityEngine;

namespace HorrorUtez.Rendering.Editor
{
    /// <summary>
    /// Import settings for characters:
    /// <list type="bullet">
    /// <item><c>Assets/_Project/Art/Characters/Player/PlayerCharacter.fbx</c> — the rigged
    ///   body (Tools/blender/gen_player_v2.py). Humanoid, avatar from this model, slots bound
    ///   to the Player_* materials by name, no clips.</item>
    /// <item><c>Assets/_Project/Art/Characters/Animations/*.fbx</c> — free humanoid clips
    ///   (Mixamo, "without skin", "in place"). Humanoid with their own avatar, so Unity
    ///   retargets them onto the player. Locomotion loops; the root is baked into the pose
    ///   so the FirstPersonController alone moves the player.</item>
    /// <item><c>Assets/_Project/Art/Textures/Player/</c> — already PS1-sized maps: point
    ///   filter, no compression (block compression smears the few texels of the face).</item>
    /// </list>
    /// </summary>
    public sealed class PsxCharacterImporter : AssetPostprocessor
    {
        private const string TextureRoot = "Assets/_Project/Art/Textures/Player/";
        private const string CharacterRoot = "Assets/_Project/Art/Characters/Player/";
        private const string AnimationRoot = "Assets/_Project/Art/Characters/Animations/";
        public const string MaterialFolder = "Assets/_Project/Art/Materials/Player";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureRoot, System.StringComparison.Ordinal))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 0;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = 512;
        }

        private void OnPreprocessModel()
        {
            bool character = assetPath.StartsWith(CharacterRoot, System.StringComparison.Ordinal);
            bool clip = assetPath.StartsWith(AnimationRoot, System.StringComparison.Ordinal);
            if (!character && !clip)
                return;

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.addCollider = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            if (character)
            {
                importer.importAnimation = false;
                importer.isReadable = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (material != null)
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), material);
                }
                return;
            }

            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        /// <summary>
        /// Clip settings go through clipAnimations, which is only populated after the first
        /// import; so they are applied here and the file is reimported once.
        /// </summary>
        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(AnimationRoot, System.StringComparison.Ordinal))
                return;

            if (((ModelImporter)assetImporter).clipAnimations.Length > 0)
                return;

            string path = assetPath;
            EditorApplication.delayCall += () => ConfigureClips(path);
        }

        private static void ConfigureClips(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer || importer.clipAnimations.Length > 0)
                return;

            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
                return;

            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            bool loop = !file.StartsWith("Jump") && !file.StartsWith("Land");
            foreach (var c in clips)
            {
                c.name = file;
                c.loopTime = loop;
                c.loopPose = loop;
                // Root baked into the pose on every axis: in place, facing forward, feet on
                // the ground. The capsule does the travelling.
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = false;
                c.heightFromFeet = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }
}
