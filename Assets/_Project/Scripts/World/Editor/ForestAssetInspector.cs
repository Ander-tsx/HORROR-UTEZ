using UnityEditor;
using UnityEngine;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Lists what the vendored PSX forest FBX actually contains. The pack ships as one
    /// "asset collection" file, so the mesh names and triangle counts are unknown until
    /// Unity imports it — and the scatter pass needs to know which sub-meshes are trees.
    /// </summary>
    public static class ForestAssetInspector
    {
        private const string FbxPath =
            "Assets/ThirdParty/PSX_Forest/PSX_Forest_AssetCollection_byStarkCrafts.fbx";

        [MenuItem("HORROR-UTEZ/Inspect Forest Assets")]
        public static void Inspect()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"[FOREST] Nothing imported at {FbxPath}");
                return;
            }

            int meshes = 0;
            foreach (var asset in assets)
            {
                if (asset is Mesh mesh)
                {
                    meshes++;
                    var b = mesh.bounds.size;
                    Debug.Log($"[FOREST] MESH {mesh.name} | tris {mesh.triangles.Length / 3} | " +
                              $"verts {mesh.vertexCount} | size {b.x:F2} x {b.y:F2} x {b.z:F2} | " +
                              $"submeshes {mesh.subMeshCount}");
                }
                else if (asset is Material material)
                {
                    Debug.Log($"[FOREST] MATERIAL {material.name} | shader {material.shader.name}");
                }
                else if (asset is GameObject go)
                {
                    Debug.Log($"[FOREST] PREFAB ROOT {go.name} | children {go.transform.childCount}");
                    foreach (Transform child in go.transform)
                        Debug.Log($"[FOREST]   child {child.name}");
                }
            }

            Debug.Log($"[FOREST] Total meshes: {meshes}");
        }

        public static void InspectBatch()
        {
            Inspect();
            EditorApplication.Exit(0);
        }
    }
}
