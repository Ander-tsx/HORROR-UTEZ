using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorUtez.Remake.Editor
{
    // Inspection only: never creates or saves architecture, furniture or lights.
    public static class RemakeShopReview
    {
        [MenuItem("HORROR-UTEZ/Tienda/Abrir escena subterranea")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(RemakeBuild.ShopScene);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(200, 1.6f, 1), Quaternion.identity, 3);
        }

        [MenuItem("HORROR-UTEZ/Tienda/Auditar escena subterranea")]
        public static void Audit()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RemakeBuild.ShopScene);
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            int missingScripts = objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            var renderers = objects.SelectMany(t => t.GetComponents<MeshRenderer>()).ToArray();
            int missingMaterials = renderers.Count(r => r.sharedMaterials.Length == 0 || r.sharedMaterials.Any(m => m == null));
            int missingMeshes = objects.SelectMany(t => t.GetComponents<MeshFilter>()).Count(f => f.sharedMesh == null);
            if (missingScripts + missingMaterials + missingMeshes != 0)
                throw new Exception($"Shop references: scripts {missingScripts}, materials {missingMaterials}, meshes {missingMeshes}");
            Debug.Log($"[ShopReview] PASS: {objects.Length} objects, {renderers.Length} renderers, no missing references.");
            foreach (var material in renderers.SelectMany(r => r.sharedMaterials).Distinct().Where(m => m.name.StartsWith("Shop_")))
                Debug.Log($"[ShopReview] Material {material.name}: texture={AssetDatabase.GetAssetPath(material.mainTexture)}, region={material.GetVector("_AtlasRect")}, tint={material.color}");
            foreach (string fontPath in new[] { "Assets/_Project/Art/Remake/Resources/Fonts/VT323-Regular.ttf", "Assets/_Project/Art/Remake/Resources/Fonts/Creepster-Regular.ttf" })
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fontPath))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
                    Debug.Log($"[ShopReview] Font asset {asset.GetType().Name}: {guid} / {localId}");
                }
            Walkability();
        }

        // Meaningful collision check: can a standing student reach every counter and the hidden room?
        private static void Walkability()
        {
            Physics.SyncTransforms();
            const float step = .35f;
            const int width = 74, depth = 57;
            var floor = new bool[width, depth];
            Vector3 Point(int x, int z) => new Vector3(191 + x * step, 0, -8.7f + z * step);
            for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++)
            {
                var point = Point(x,z);
                if (!Physics.Raycast(point + Vector3.up * 2.7f, Vector3.down, out var hit, 3, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.point.y > .31f || hit.point.y < -.1f) continue;
                point.y = hit.point.y;
                floor[x,z] = !Physics.CheckCapsule(point + Vector3.up * .28f, point + Vector3.up * 1.25f, .25f, ~0, QueryTriggerInteraction.Ignore);
            }
            Vector2Int Nearest(Vector3 point)
            {
                var best = new Vector2Int(-1,-1); float distance = .65f;
                for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++)
                {
                    if (!floor[x,z]) continue;
                    float d = Vector2.Distance(new Vector2(point.x,point.z), new Vector2(Point(x,z).x,Point(x,z).z));
                    if (d < distance) { distance = d; best = new Vector2Int(x,z); }
                }
                return best;
            }
            var start = Nearest(RemakeShop.Spawn(0));
            if (start.x < 0) throw new Exception("Shop spawn blocked");
            var reachable = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= depth || !floor[next.x,next.y] || !reachable.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
            var targets = new List<Vector3>();
            for (int id = 0; id < 5; id++) targets.Add(RemakeShop.Spawn(id));
            for (int kind = 0; kind < 6; kind++) targets.Add(RemakeShop.Product(kind) + Vector3.back * 1.3f);
            targets.AddRange(new[] { new Vector3(200,0,-6.1f), new Vector3(198.7f,0,-5.3f), new Vector3(206.3f,0,-2.5f), new Vector3(204.5f,0,-4.5f), new Vector3(193.5f,0,8.1f), new Vector3(201.5f,0,6.5f), new Vector3(209.7f,0,4.5f), new Vector3(210.7f,0,4.4f) });
            foreach (var target in targets)
            {
                var cell = Nearest(target);
                if (cell.x < 0 || !reachable.Contains(cell)) throw new Exception("Shop destination blocked: " + target);
            }
            Debug.Log($"[ShopReview] PASS: {targets.Count} destinations reachable with a standing student capsule, {reachable.Count} connected cells.");
        }

        public static void Canonicalize()
        {
            Audit();
            var paths = new List<string> { RemakeBuild.ShopScene, "Assets/_Project/Prefabs/Map/Tienda_Subterranea.prefab" };
            paths.AddRange(Directory.GetFiles("Assets/_Project/Art/Remake/Materials", "Shop_*.mat").Select(p => p.Replace('\\','/')));
            AssetDatabase.ForceReserializeAssets(paths);
            Debug.Log("[ShopReview] Authored assets normalized with Unity serialization; no layout changes.");
        }

        // Run with a graphics device in the disposable validation checkout. No authored assets are saved.
        public static void Capture()
        {
            Audit();
            ShaderUtil.allowAsyncCompilation = false;
            if (Environment.GetCommandLineArgs().Contains("-shopNoBatch"))
                ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).useSRPBatcher = false;
            Debug.Log("[ShopReview] SRP batching enabled: " + ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).useSRPBatcher);
            var cameraObject = new GameObject("Temporary review camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 66; camera.nearClipPlane = .05f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.008f, .012f, .016f);
            camera.allowHDR = true;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.07f, .085f, .095f);
            RenderSettings.fog = false;
            var shots = new[] {
                ("01-entrada", new Vector3(200,1.5f,-6.3f), new Vector3(200,1.9f,4)),
                ("02-vendimia", new Vector3(202,1.5f,-3), new Vector3(207,1.1f,-3.5f)),
                ("03-mejoras", new Vector3(196,1.5f,-1.8f), new Vector3(199,1.3f,1)),
                ("04-herramientas", new Vector3(196,1.5f,5), new Vector3(192.7f,1.7f,7.5f)),
                ("05-casino", new Vector3(209.9f,1.5f,2), new Vector3(213,1.3f,5.3f)),
                ("06-escalera", new Vector3(202.5f,1.5f,-3), new Vector3(200,2.1f,-6)),
                ("07-mostrador", new Vector3(201.5f,1.5f,5), new Vector3(200,1.8f,8.5f))
            };
            string directory = Path.GetFullPath("Builds/Remake/ShopReview");
            Directory.CreateDirectory(directory);
            foreach (var shot in shots)
            {
                camera.transform.position = shot.Item2;
                camera.transform.LookAt(shot.Item3);
                var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                target.Create();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                // Warm URP's camera/global buffers before reading an offscreen editor frame.
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(directory, shot.Item1 + ".png"), image.EncodeToPNG());
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
            UnityEngine.Object.DestroyImmediate(cameraObject);
            Debug.Log("[ShopReview] Captures ready: " + directory);
        }
    }
}
