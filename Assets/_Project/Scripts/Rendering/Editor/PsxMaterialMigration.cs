using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HorrorUtez.Rendering.Editor
{
    /// <summary>
    /// Moves every PSX material in the project from the vendored URP-PSX graphs to
    /// HorrorUtez/PSX/Lit, and retunes them through <see cref="PsxMaterialDefaults"/>.
    ///
    /// Before the first touch, each material is snapshotted whole (EditorJsonUtility) into
    /// Library/HorrorUtez/psx_material_backup.json. The snapshot is taken once per material
    /// and never overwritten, so re-running the migration keeps the ORIGINAL state, and
    /// "Restore" always goes back to how the material was before any of this.
    /// Library is gitignored on purpose: the backup is a local undo, git is the real one.
    /// </summary>
    public static class PsxMaterialMigration
    {
        private const string SearchRoot = "Assets/_Project";
        private static readonly string BackupPath =
            Path.Combine("Library", "HorrorUtez", "psx_material_backup.json");

        [Serializable]
        private sealed class Entry
        {
            public string path;
            public string shader;
            public string json;
        }

        [Serializable]
        private sealed class Backup
        {
            public List<Entry> entries = new();
        }

        [MenuItem("HORROR-UTEZ/Look/Migrate PSX Materials to PsxLit")]
        public static void MigrateAllMenu() => MigrateAll();

        public static int MigrateAll()
        {
            var shader = PsxMaterialDefaults.LitShader;
            if (shader == null)
            {
                Debug.LogError($"[LOOK] Shader '{PsxShaderProperties.Lit}' not found. " +
                               "Has Assets/_Project/Shaders/PsxLit.shader imported without errors?");
                return 0;
            }

            var backup = LoadBackup();
            var known = new HashSet<string>();
            foreach (var entry in backup.entries)
                known.Add(entry.path);

            int migrated = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { SearchRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null || !PsxShaderProperties.IsPsxSurface(mat.shader.name))
                    continue;

                if (!known.Contains(path))
                {
                    backup.entries.Add(new Entry
                    {
                        path = path,
                        shader = mat.shader.name,
                        json = EditorJsonUtility.ToJson(mat),
                    });
                    known.Add(path);
                }

                bool transparent = mat.shader.name == PsxShaderProperties.PbrMasterTransparent ||
                                   (mat.HasProperty(PsxShaderProperties.Surface) &&
                                    mat.GetFloat(PsxShaderProperties.Surface) > 0.5f);

                Retune(mat, shader, transparent);
                EnsureMipmaps(mat.GetTexture(PsxShaderProperties.MainTex) as Texture2D);
                migrated++;
            }

            SaveBackup(backup);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LOOK] {migrated} materials on {PsxShaderProperties.Lit}. " +
                      $"Originals kept in {BackupPath}.");
            return migrated;
        }

        /// <summary>Puts one material on PsxLit with the house settings for what it is.</summary>
        public static void Retune(Material mat, Shader litShader, bool transparent)
        {
            // Read before the swap. A material its generator marked specular is bare metal,
            // whatever its name says (Prop_BenchSeat is the chromed frame, not the slats).
            bool specular = mat.HasProperty(PsxShaderProperties.IsSpecular) &&
                            mat.GetFloat(PsxShaderProperties.IsSpecular) > 0.5f;

            mat.shader = litShader;
            var kind = specular ? PsxSurfaceKind.Metal : PsxMaterialDefaults.Classify(mat.name);
            if (transparent)
                kind = PsxSurfaceKind.Glass;
            PsxMaterialDefaults.Apply(mat, kind, PsxMaterialDefaults.IsWettable(mat.name));
            PsxMaterialDefaults.SetTransparent(mat, transparent);
        }

        [MenuItem("HORROR-UTEZ/Look/Restore Materials From Backup")]
        public static void RestoreAll()
        {
            var backup = LoadBackup();
            if (backup.entries.Count == 0)
            {
                Debug.LogWarning($"[LOOK] Nothing to restore; {BackupPath} is empty or missing.");
                return;
            }

            int restored = 0;
            foreach (var entry in backup.entries)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(entry.path);
                var shader = Shader.Find(entry.shader);
                if (mat == null || shader == null)
                {
                    Debug.LogWarning($"[LOOK] Skipped {entry.path}: material or shader '{entry.shader}' missing.");
                    continue;
                }

                mat.shader = shader;
                EditorJsonUtility.FromJsonOverwrite(entry.json, mat);
                EditorUtility.SetDirty(mat);
                restored++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[LOOK] Restored {restored}/{backup.entries.Count} materials to their pre-migration state.");
        }

        /// <summary>
        /// Point sampling without mipmaps makes every distant surface shimmer: each screen
        /// pixel lands on a random texel of a texture far denser than the screen, which
        /// reads as crawling noise on grass and concrete. With mips, distance averages out
        /// and the crunch stays where it belongs, up close.
        /// </summary>
        public static void EnsureMipmaps(Texture2D texture)
        {
            if (texture == null || texture.mipmapCount > 1)
                return;

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
                return;

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                return;
            }

            // Generated .asset textures: rebuild with a mip chain in place, same GUID.
            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || !texture.isReadable)
                return;

            var pixels = texture.GetPixels32();
            var rebuilt = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, mipChain: true)
            {
                name = texture.name,
                filterMode = texture.filterMode,
                wrapMode = texture.wrapMode,
                anisoLevel = 0,
            };
            rebuilt.SetPixels32(pixels);
            rebuilt.Apply(updateMipmaps: true);
            EditorUtility.CopySerialized(rebuilt, texture);
            UnityEngine.Object.DestroyImmediate(rebuilt);
            EditorUtility.SetDirty(texture);
        }

        private static Backup LoadBackup()
        {
            if (!File.Exists(BackupPath))
                return new Backup();
            return JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath)) ?? new Backup();
        }

        private static void SaveBackup(Backup backup)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup, prettyPrint: true));
        }
    }
}
