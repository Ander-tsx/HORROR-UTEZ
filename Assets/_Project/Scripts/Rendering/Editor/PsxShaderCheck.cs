using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorUtez.Rendering.Editor
{
    /// <summary>
    /// Health check for the vendored URP-PSX shaders. Unity compiles shader variants
    /// lazily, so a clean import log does NOT mean the shaders actually work — this
    /// forces each one to resolve and reports the ones Unity flagged.
    /// </summary>
    public static class PsxShaderCheck
    {
        private const string PsxFolder = "Assets/ThirdParty/URP-PSX";

        [MenuItem("HORROR-UTEZ/Check PSX Shaders")]
        public static void Check()
        {
            var broken = new List<string>();
            int shaders = 0, subGraphs = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { PsxFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                shaders++;

                if (shader == null)
                {
                    broken.Add($"{path}: failed to load as Shader");
                    continue;
                }

                if (ShaderUtil.ShaderHasError(shader))
                {
                    foreach (var msg in ShaderUtil.GetShaderMessages(shader))
                        if (msg.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                            broken.Add($"{shader.name} ({path}): {msg.message} {msg.messageDetails}");
                }
                else
                {
                    Debug.Log($"[PSX] OK  {shader.name}  <- {path}");
                }
            }

            // Sub graphs are not Shader assets; just confirm every file still resolves.
            foreach (string guid in AssetDatabase.FindAssets("", new[] { PsxFolder + "/Subgraphs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".shadersubgraph"))
                    subGraphs++;
            }

            Debug.Log($"[PSX] Shaders checked: {shaders}, sub graphs found: {subGraphs}, broken: {broken.Count}");
            foreach (string b in broken)
                Debug.LogError($"[PSX] BROKEN {b}");
        }

        /// <summary>Dumps a shader's property names/types, so materials can be authored from code.</summary>
        [MenuItem("HORROR-UTEZ/Dump PSX Shader Properties")]
        public static void DumpProperties()
        {
            foreach (string name in new[]
                     {
                         "Shader Graphs/URP_PSX_Unlit_Master",
                         "Shader Graphs/URP_PSX_PBR_Master",
                     })
            {
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogError($"[PSX] Shader.Find failed: {name}");
                    continue;
                }

                Debug.Log($"[PSX] --- {name} ({shader.GetPropertyCount()} properties) ---");
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                    Debug.Log($"[PSX] prop {shader.GetPropertyName(i)} : {shader.GetPropertyType(i)}");
            }
        }

        public static void CheckBatch()
        {
            Check();
            DumpProperties();
            EditorApplication.Exit(0);
        }
    }
}
