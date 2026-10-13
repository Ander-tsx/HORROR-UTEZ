using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorUtez.Remake.Editor
{
    // Reviews authored assets. Never builds or saves geometry, furniture or lights.
    public static class RemakeEntranceReview
    {
        private const string ScenePath = "Assets/_Project/Scenes/remake.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/Map/Entrada_UTEZ.prefab";

        [MenuItem("HORROR-UTEZ/Entrada UTEZ/Abrir y enfocar entrada")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            var entrance = UnityEngine.Object.FindAnyObjectByType<RemakeEntrance>();
            Selection.activeGameObject = entrance.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(entrance.transform.position + Vector3.up * 3,
                    Quaternion.Euler(8,0,0), 35);
        }

        public static void Audit()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            var entrances = UnityEngine.Object.FindObjectsByType<RemakeEntrance>(FindObjectsSortMode.None);
            if (entrances.Length != 1) throw new Exception("Expected one entrance, found " + entrances.Length);
            var entrance = entrances[0];
            var objects = entrance.GetComponentsInChildren<Transform>(true);
            int scripts = objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            var renderers = entrance.GetComponentsInChildren<MeshRenderer>(true);
            int materials = renderers.Count(r => r.sharedMaterials.Length == 0 || r.sharedMaterials.Any(m => m == null));
            int meshes = entrance.GetComponentsInChildren<MeshFilter>(true).Count(f => f.sharedMesh == null);
            if (scripts + materials + meshes != 0)
                throw new Exception($"Entrance references: scripts {scripts}, materials {materials}, meshes {meshes}");
            if (entrance.MenuView == null || entrance.MenuFocus == null)
                throw new Exception("Menu viewpoints missing");
            entrance.RefreshMaterials();
            Debug.Log($"[EntranceReview] PASS references: {objects.Length} objects, {renderers.Length} renderers, {entrance.GetComponentsInChildren<Collider>().Length} colliders.");
            var title = objects.Single(t => t.name == "UNIVERSIDAD TECNOLOGICA EMILIANO ZAPATA - letras").GetComponent<MeshRenderer>();
            Debug.Log("[EntranceReview] University sign bounds: " + title.bounds.size);
            if (title.bounds.size.x < 12 || title.bounds.size.x > 15.5f || title.bounds.size.y > .75f)
                throw new Exception("University lettering does not fit its nameplate");
            Physics.SyncTransforms();
            foreach (var local in new[] { new Vector3(-5,0,-10), new Vector3(5,0,-10), new Vector3(-20,0,-4), new Vector3(20,0,-4) })
            {
                var point = entrance.transform.TransformPoint(local);
                if (!Physics.Raycast(point + Vector3.up * 1.5f, Vector3.down, out var floor, 2, ~0, QueryTriggerInteraction.Ignore))
                    throw new Exception("Entrance ground missing: " + local);
                if (Physics.CheckCapsule(floor.point + Vector3.up * .32f, floor.point + Vector3.up * 1.35f, .28f, ~0, QueryTriggerInteraction.Ignore))
                    throw new Exception("Entrance approach blocked: " + local);
            }
            Debug.Log("[EntranceReview] PASS ground and standing capsule clearance in both lanes and pedestrian approaches.");
            Debug.Log("[EntranceReview] Menu view " + entrance.MenuView.position + "; focus " + entrance.MenuFocus.position);
        }

        // Integration inspection: the merged rain/sky/audio assets must coexist with both authored scenes.
        public static void AuditCombined()
        {
            Audit();
            if (RenderSettings.skybox == null || RenderSettings.skybox.shader.name != "HorrorUtez/StormSky")
                throw new Exception("Merged storm sky missing");
            var main = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var shop = EditorSceneManager.OpenScene(RemakeBuild.ShopScene,OpenSceneMode.Additive);
            foreach (var scene in new[] { main,shop })
            {
                var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                int missing = objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                missing += objects.SelectMany(t => t.GetComponents<MeshFilter>()).Count(f => f.sharedMesh == null);
                missing += objects.SelectMany(t => t.GetComponents<MeshRenderer>()).Count(r => r.sharedMaterials.Any(m => m == null));
                if (missing != 0) throw new Exception("Merged scene has missing references: " + scene.path + " / " + missing);
                Debug.Log("[EntranceReview] PASS combined scene references: " + scene.path);
            }
            foreach (string name in new[] { "caminar_adentro", "caminar_afuera", "correr_adentro", "corriendo_afuera",
                "golpeado", "sonido_respiracion_cansado", "visto" })
                if (Resources.Load<AudioClip>("Audio/Player/" + name) == null)
                    throw new Exception("Merged player audio missing: " + name);
            if (Resources.Load<AudioClip>("Audio/musica_main_menu") == null)
                throw new Exception("Merged menu music missing");
            Debug.Log("[EntranceReview] PASS merged storm sky and all eight runtime audio resources.");
        }

        public static void Canonicalize()
        {
            Audit();
            var paths = new[] { ScenePath, PrefabPath,
                "Assets/_Project/Prefabs/Map/Entrada_Reja.prefab", "Assets/_Project/Prefabs/Map/Entrada_Palmera.prefab" }
                .Concat(Directory.GetFiles("Assets/_Project/Art/Remake/Materials", "Entrance_*.mat").Select(p => p.Replace('\\','/')));
            AssetDatabase.ForceReserializeAssets(paths);
            Debug.Log("[EntranceReview] Authored entrance assets normalized; no layout changes.");
        }

        public static void Capture()
        {
            Audit();
            ShaderUtil.allowAsyncCompilation = false;
            var entrance = UnityEngine.Object.FindAnyObjectByType<RemakeEntrance>();
            var root = entrance.transform;
            var cameraObject = new GameObject("Temporary entrance review camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .05f; camera.farClipPlane = 160;
            camera.clearFlags = CameraClearFlags.SolidColor;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            string directory = Path.GetFullPath("Builds/Remake/EntranceReview");
            Directory.CreateDirectory(directory);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.fog = false;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) light.intensity = .85f;
            RenderSettings.ambientLight = new Color(.28f,.31f,.34f);
            var dayProbe = new SphericalHarmonicsL2(); dayProbe.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = dayProbe;
            camera.backgroundColor = new Color(.2f,.28f,.33f);
            camera.fieldOfView = 58;
            camera.transform.position = root.TransformPoint(new Vector3(-2,3,-37));
            camera.transform.LookAt(root.TransformPoint(new Vector3(0,3,1)));
            Save(camera,directory,"01-fachada");
            camera.transform.position = root.TransformPoint(new Vector3(-11,2.2f,-13));
            camera.transform.LookAt(root.TransformPoint(new Vector3(0,2.5f,.2f)));
            Save(camera,directory,"02-caseta-y-acabados");
            camera.transform.position = root.TransformPoint(new Vector3(-26,22,-29));
            camera.transform.LookAt(root.TransformPoint(new Vector3(0,1,1)));
            Save(camera,directory,"03-conjunto");
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) light.intensity = .1f;
            RenderSettings.ambientLight = new Color(.065f,.085f,.1f);
            var nightProbe = new SphericalHarmonicsL2(); nightProbe.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = nightProbe;
            cameraData.renderPostProcessing = true;
            camera.backgroundColor = new Color(.012f,.023f,.026f);
            entrance.Frame(camera,0);
            Save(camera,directory,"04-encuadre-nocturno");
            UnityEngine.Object.DestroyImmediate(cameraObject);
            Debug.Log("[EntranceReview] Captures ready: " + directory);
        }

        private static void Save(Camera camera, string directory, string name)
        {
            var target = new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32); target.Create();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera,request); RenderPipeline.SubmitRenderRequest(camera,request);
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(directory,name + ".png"),image.EncodeToPNG());
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
