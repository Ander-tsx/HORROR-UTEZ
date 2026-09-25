using System.IO;
using HorrorUtez.Rendering;
using HorrorUtez.Rendering.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Applies the night "PS1 render" look to the project and the open scene in one go.
    ///
    /// The look is three layers, and the reference renders need all three:
    /// 1. Surfaces — every PSX material onto HorrorUtez/PSX/Lit (snapped vertices, crunchy
    ///    texels, real URP lighting with reflections). See PsxMaterialMigration.
    /// 2. Light — cold moon, navy ambient, warm sodium practicals that bloom, wet ground.
    /// 3. Frame — a light-touch PSX screen pass, blue-violet fog, grading. See PsxRenderingSetup.
    ///
    /// Scene edits are additive and idempotent: existing objects are retuned in place, and
    /// the test lamp and puddles live under one root that is rebuilt on every run. Nothing
    /// hand-placed is destroyed. The scene is marked dirty, not saved.
    /// </summary>
    public static class UtezReelLook
    {
        private const string TestRootName = "UTEZ_LookTest";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string TextureFolder = "Assets/_Project/Art/Textures";
        private const int WaterLayer = 4;

        [MenuItem("HORROR-UTEZ/Look/Apply Reel Look (project + open scene)", priority = 1)]
        public static void ApplyAll()
        {
            PsxRenderingSetup.Setup();
            PsxTerrainMaterials.RegenerateTextures();
            PsxMaterialMigration.MigrateAll();
            ApplyToOpenScene();
        }

        [MenuItem("HORROR-UTEZ/Look/Apply Reel Look to Open Scene Only", priority = 2)]
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;

            var moon = ConfigureSky();
            ConfigureCamera();
            ConfigureWeather(moon);
            int lamps = UpgradeLampPosts();
            PlaceLookTest();
            int mirrors = SetupMirrors();
            EnsureStyleApplier();

            // The whole point is to see it: switch the PSX filter on (same switch as
            // HORROR-UTEZ > PSX Filter and the pause menu).
            PsxLook.Enabled = true;
            PsxLookBinder.ApplyToLoadedScenes(true);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[LOOK] Night look applied to '{scene.name}': moon, ambient, night sky, " +
                      $"camera post-processing, weather, {lamps} lamp posts, {mirrors} live mirrors, test lamp + puddles " +
                      $"under {TestRootName}. Scene marked dirty — save it to keep the changes.");
        }

        [MenuItem("HORROR-UTEZ/Look/Rebuild Lamp + Puddle Test", priority = 3)]
        public static void PlaceLookTestMenu()
        {
            PlaceLookTest();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // ---- Sky, moon, ambient -----------------------------------------------------

        private static Light ConfigureSky()
        {
            Light moon = RenderSettings.sun;
            if (moon == null)
            {
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    // The lightning rig is a directional light parked at zero; not the moon.
                    if (light.type == LightType.Directional && light.name != "LightningFlash")
                    {
                        moon = light;
                        break;
                    }
                }
            }

            if (moon != null)
            {
                Undo.RecordObject(moon, "Night look");
                moon.color = PsxNightPalette.Moon;
                moon.intensity = PsxNightPalette.MoonIntensityClear;
                moon.shadows = LightShadows.Hard;
                moon.shadowStrength = 0.75f;
                RenderSettings.sun = moon;
            }
            else
            {
                Debug.LogWarning("[LOOK] No directional light found to turn into the moon.");
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = PsxNightPalette.AmbientClear;
            RenderSettings.skybox = EnsureNightSkybox();
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = EnsureNightReflection();
            RenderSettings.reflectionIntensity = 1f;
            // Unity's own fog stays off: PsxFog does distance fog in screen space.
            RenderSettings.fog = false;

            return moon;
        }

        /// <summary>
        /// A dark procedural sky whose "sun" disk, aimed by the moon light, draws the moon.
        /// The PSX fog covers the sky in play; this is what reflections and the editor see.
        /// </summary>
        private static Material EnsureNightSkybox()
        {
            string path = $"{MaterialFolder}/UTEZ_NightSky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetFloat("_SunDisk", 1f);
            mat.SetFloat("_SunSize", 0.025f);
            mat.SetFloat("_SunSizeConvergence", 8f);
            mat.SetFloat("_AtmosphereThickness", 0.45f);
            mat.SetColor("_SkyTint", new Color(0.22f, 0.26f, 0.55f));
            mat.SetColor("_GroundColor", new Color(0.03f, 0.03f, 0.05f));
            mat.SetFloat("_Exposure", 0.22f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// The environment reflection every shiny surface falls back to: navy overhead, a
        /// violet glow on the horizon, and a scatter of warm specks along it — distant
        /// windows and lamps. Those specks are what turn wet pavement from grey to glittering
        /// even where no puddle mirror is running.
        ///
        /// Generated rather than baked so it works before anyone presses Generate Lighting.
        /// The gradient is symmetric around the horizon on purpose, so a vertical flip in
        /// the face layout could not put a bright sky under the ground.
        /// </summary>
        private static Cubemap EnsureNightReflection()
        {
            const int size = 64;
            string path = $"{TextureFolder}/UTEZ_NightReflection.cubemap";

            var cube = new Cubemap(size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "UTEZ_NightReflection",
                filterMode = FilterMode.Bilinear,
            };

            var zenith = new Color(0.035f, 0.045f, 0.11f);
            var horizon = new Color(0.17f, 0.15f, 0.32f);
            var ground = new Color(0.02f, 0.02f, 0.03f);
            var warm = new Color(1f, 0.62f, 0.3f);
            var rng = new System.Random(1990);
            var pixels = new Color[size * size];

            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sc = 2f * (x + 0.5f) / size - 1f;
                    float tc = 2f * (y + 0.5f) / size - 1f;
                    Vector3 dir = FaceDirection((CubemapFace)face, sc, tc).normalized;

                    float up = dir.y;
                    Color c = up >= 0f
                        ? Color.Lerp(horizon, zenith, Mathf.Pow(up, 0.6f))
                        : Color.Lerp(horizon, ground, Mathf.Pow(-up, 0.4f));

                    // A band of distant lights just above the horizon.
                    if (up > 0.0f && up < 0.09f && rng.NextDouble() < 0.035)
                        c = Color.Lerp(c, warm, 0.85f);

                    pixels[y * size + x] = c;
                }

                cube.SetPixels(pixels, (CubemapFace)face);
            }

            cube.Apply(updateMipmaps: true);

            var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(cube, path);
                return cube;
            }

            EditorUtility.CopySerialized(cube, existing);
            Object.DestroyImmediate(cube);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>Direct3D cube face convention, which is what Unity uses; tc runs top to bottom.</summary>
        private static Vector3 FaceDirection(CubemapFace face, float sc, float tc) => face switch
        {
            CubemapFace.PositiveX => new Vector3(1f, -tc, -sc),
            CubemapFace.NegativeX => new Vector3(-1f, -tc, sc),
            CubemapFace.PositiveY => new Vector3(sc, 1f, tc),
            CubemapFace.NegativeY => new Vector3(sc, -1f, -tc),
            CubemapFace.PositiveZ => new Vector3(sc, -tc, 1f),
            _ => new Vector3(-sc, -tc, -1f),
        };

        // ---- Camera ------------------------------------------------------------------

        /// <summary>
        /// The main camera had no URP camera data, so post-processing was off: the volume's
        /// tonemapper never ran, and bloom could not have either.
        /// </summary>
        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[LOOK] No MainCamera in the scene; post-processing not enabled.");
                return;
            }

            Undo.RecordObject(camera, "Night look");
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.Skybox;

            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                data = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
            else
                Undo.RecordObject(data, "Night look");

            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            data.requiresDepthOption = CameraOverrideOption.On;
            EditorUtility.SetDirty(data);
        }

        // ---- Weather -------------------------------------------------------------------

        /// <summary>
        /// The scene's WeatherSystem carries its own serialized copies of the old grey
        /// palette, which override the new code defaults. Rewrite them, and keep a floor of
        /// drizzle so the campus is always wet enough to shine.
        /// </summary>
        private static void ConfigureWeather(Light moon)
        {
            var weather = Object.FindFirstObjectByType<WeatherSystem>();
            if (weather == null)
            {
                Debug.LogWarning("[LOOK] No WeatherSystem in the scene; rain and wetness stay static.");
                Shader.SetGlobalFloat("_PsxWetness", 0.6f);
                return;
            }

            var so = new SerializedObject(weather);
            so.FindProperty("clearFogDensity").floatValue = PsxNightPalette.FogDensityClear;
            so.FindProperty("stormFogDensity").floatValue = PsxNightPalette.FogDensityStorm;
            so.FindProperty("clearFogColor").colorValue = PsxNightPalette.FogClear;
            so.FindProperty("stormFogColor").colorValue = PsxNightPalette.FogStorm;
            so.FindProperty("clearSunIntensity").floatValue = PsxNightPalette.MoonIntensityClear;
            so.FindProperty("stormSunIntensity").floatValue = PsxNightPalette.MoonIntensityStorm;
            so.FindProperty("clearAmbient").colorValue = PsxNightPalette.AmbientClear;
            so.FindProperty("stormAmbient").colorValue = PsxNightPalette.AmbientStorm;
            so.FindProperty("rainFloor").floatValue = 0.45f;
            if (moon != null && so.FindProperty("sun").objectReferenceValue == null)
                so.FindProperty("sun").objectReferenceValue = moon;
            so.ApplyModifiedProperties();

            // Edit-mode preview: WeatherSystem only writes the global while playing.
            Shader.SetGlobalFloat("_PsxWetness", 0.45f);
        }

        // ---- Practical lights --------------------------------------------------------

        /// <summary>
        /// The campus lamp posts had a beige wall material on their heads, so the light
        /// source itself was the one thing in the pool of light that did not glow.
        /// </summary>
        private static int UpgradeLampPosts()
        {
            var bulb = EnsureBulbMaterial();
            int count = 0;

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("LampPost_"))
                    continue;

                var head = t.Find("Head");
                if (head != null && head.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    Undo.RecordObject(renderer, "Night look");
                    renderer.sharedMaterial = bulb;
                }

                var light = t.GetComponentInChildren<Light>();
                if (light != null)
                {
                    Undo.RecordObject(light, "Night look");
                    light.color = PsxNightPalette.Sodium;
                }

                count++;
            }

            return count;
        }

        private static Material EnsureBulbMaterial()
        {
            var mat = EnsureLitMaterial("Prop_LampBulb");
            PsxMaterialDefaults.Apply(mat, PsxSurfaceKind.Emissive, wettable: false);
            mat.SetColor(PsxShaderProperties.BaseColor, new Color(1f, 0.86f, 0.62f));
            PsxMaterialDefaults.SetEmission(mat, PsxNightPalette.Sodium * 6f, null);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureLitMaterial(string name)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(PsxMaterialDefaults.LitShader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = PsxMaterialDefaults.LitShader;
            return mat;
        }

        // ---- Visual styles -----------------------------------------------------------

        /// <summary>
        /// The pause menu's "Estilo visual" needs PsxStyleApplier on the global volume.
        /// </summary>
        private static void EnsureStyleApplier()
        {
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!volume.isGlobal || volume.GetComponent<PsxStyleApplier>() != null)
                    continue;
                Undo.AddComponent<PsxStyleApplier>(volume.gameObject);
                Debug.Log($"[LOOK] PsxStyleApplier added to '{volume.name}'.");
            }
        }

        // ---- Mirrors -----------------------------------------------------------------

        /// <summary>
        /// Every landmark object named *_Mirror (the washbasin mirror in the CECADEC men's
        /// room) becomes a working mirror: its silvered slot gets the live mirror material,
        /// and a PsxPlanarReflection is placed on the glass plane. The plane is measured from
        /// the mesh itself, so it follows the landmark wherever it is placed and turned.
        /// The reflection objects live under the look-test root and are rebuilt with it.
        /// </summary>
        private static int SetupMirrors()
        {
            var live = EnsureMirrorMaterial();
            var root = GameObject.Find(TestRootName);
            int count = 0;

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.name.EndsWith("_Mirror"))
                    continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var materials = renderer.sharedMaterials;
                int slot = System.Array.FindIndex(materials, m => m != null &&
                    (m.name == "Kit_Mirror" || m == live));
                if (slot < 0 || !TryMirrorPlane(filter, slot, out var point, out var normal))
                    continue;

                Undo.RecordObject(renderer, "Live mirror");
                materials[slot] = live;
                renderer.sharedMaterials = materials;
                Undo.RecordObject(renderer.gameObject, "Live mirror");
                // The reflection cameras leave the Water layer out, so a mirror never tries
                // to render itself.
                renderer.gameObject.layer = WaterLayer;

                var planeGo = new GameObject($"MirrorPlane_{renderer.name}");
                planeGo.transform.SetParent(root != null ? root.transform : null, true);
                planeGo.transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.up, normal));
                var reflection = planeGo.AddComponent<PsxPlanarReflection>();
                var so = new SerializedObject(reflection);
                var list = so.FindProperty("surfaces");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                // A mirror is looked into up close: half resolution, not a third.
                so.FindProperty("resolutionDivisor").intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }

            return count;
        }

        /// <summary>Centre and facing of one submesh, from its first triangle (editor only:
        /// landmark meshes are not CPU-readable in a player build).</summary>
        private static bool TryMirrorPlane(MeshFilter filter, int submesh, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            var mesh = filter.sharedMesh;
            if (submesh >= mesh.subMeshCount)
                return false;

            var tris = mesh.GetTriangles(submesh);
            var verts = mesh.vertices;
            if (tris.Length < 3)
                return false;

            var t = filter.transform;
            Vector3 sum = Vector3.zero;
            foreach (int i in tris)
                sum += t.TransformPoint(verts[i]);
            point = sum / tris.Length;

            Vector3 a = t.TransformPoint(verts[tris[0]]);
            Vector3 b = t.TransformPoint(verts[tris[1]]);
            Vector3 c = t.TransformPoint(verts[tris[2]]);
            normal = Vector3.Cross(b - a, c - a).normalized;
            return normal.sqrMagnitude > 0.5f;
        }

        private static Material EnsureMirrorMaterial()
        {
            string path = $"{MaterialFolder}/UTEZ_MirrorLive.mat";
            var shader = Shader.Find("HorrorUtez/PSX/Mirror");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "UTEZ_MirrorLive" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- Test lamp + puddles -------------------------------------------------------

        /// <summary>
        /// One street lamp and two puddles in front of the spawn, placed so that from the
        /// spawn point the near puddle mirrors the lamp head and the far one mirrors the
        /// lit CECADEC corridor. Positions are real metres; the spawn stands at z = 5.3
        /// facing south toward the entrance (see UtezProjectSetup.SpawnPlayer).
        /// </summary>
        private static void PlaceLookTest()
        {
            var existing = GameObject.Find(TestRootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            float s = UtezDimensions.WorldScale;
            var root = new GameObject(TestRootName);
            Undo.RegisterCreatedObjectUndo(root, "Place look test");

            var lamp = BuildStreetLamp(root.transform, Ground(new Vector3(-3.2f * s, 0f, -1.0f * s)));

            var puddleMat = EnsurePuddleMaterial();
            var near = BuildPuddle(root.transform, "Puddle_Near", puddleMat,
                Ground(new Vector3(-0.6f * s, 0f, 3.2f * s)), new Vector2(3.4f, 2.4f) * s, 12f);
            var far = BuildPuddle(root.transform, "Puddle_Entrance", puddleMat,
                Ground(new Vector3(0.4f * s, 0f, -6.2f * s)), new Vector2(2.2f, 1.5f) * s, -31f);

            // One mirror for both: they sit on the same slab. It renders only while one of
            // them is on screen.
            var mirrorGo = new GameObject("PuddleMirror");
            mirrorGo.transform.SetParent(root.transform, false);
            mirrorGo.transform.position = new Vector3(0f, near.transform.position.y, 0f);
            var mirror = mirrorGo.AddComponent<PsxPlanarReflection>();
            var so = new SerializedObject(mirror);
            var list = so.FindProperty("surfaces");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = near.GetComponent<Renderer>();
            list.GetArrayElementAtIndex(1).objectReferenceValue = far.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[LOOK] {TestRootName}: street lamp at {lamp.transform.position}, " +
                      $"puddles at {near.transform.position} and {far.transform.position}.");
        }

        /// <summary>
        /// Snaps a point onto the ground below it; y = 0 if there is none. Cast from just
        /// above head height rather than from the sky, so an eave or a canopy overhead is
        /// never mistaken for the pavement.
        /// </summary>
        private static Vector3 Ground(Vector3 point)
        {
            Physics.SyncTransforms();
            var origin = new Vector3(point.x, 2.5f * UtezDimensions.WorldScale, point.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;

            Debug.LogWarning($"[LOOK] No ground collider under {point}; placing at y = 0.");
            return new Vector3(point.x, 0f, point.z);
        }

        /// <summary>
        /// Cobra-head street lamp: pole, arm reaching over the path, a glowing lens under
        /// the head and a shadow-casting sodium spot. Steady, no flicker — this one is for
        /// judging the look, not for scaring anybody.
        /// </summary>
        private static GameObject BuildStreetLamp(Transform parent, Vector3 foot)
        {
            float s = UtezDimensions.WorldScale;
            float height = 5f * s;
            float reach = 1.2f * s;

            var poleMat = EnsureLitMaterial("Prop_LampPole");
            PsxMaterialDefaults.Apply(poleMat, PsxSurfaceKind.Satin, wettable: true);
            poleMat.SetColor(PsxShaderProperties.BaseColor, new Color(0.18f, 0.19f, 0.2f));
            EditorUtility.SetDirty(poleMat);

            var bulbMat = EnsureBulbMaterial();

            var lamp = new GameObject("StreetLamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = foot;

            AddPart(lamp.transform, PrimitiveType.Cylinder, "Pole", poleMat,
                new Vector3(0f, height * 0.5f, 0f), new Vector3(0.14f * s, height * 0.5f, 0.14f * s), collider: true);
            AddPart(lamp.transform, PrimitiveType.Cube, "Arm", poleMat,
                new Vector3(reach * 0.5f, height - 0.05f * s, 0f), new Vector3(reach, 0.08f * s, 0.08f * s), collider: false);
            AddPart(lamp.transform, PrimitiveType.Cube, "Head", poleMat,
                new Vector3(reach, height - 0.05f * s, 0f), new Vector3(0.6f * s, 0.16f * s, 0.3f * s), collider: false);
            var lens = AddPart(lamp.transform, PrimitiveType.Cube, "Lens", bulbMat,
                new Vector3(reach, height - 0.15f * s, 0f), new Vector3(0.46f * s, 0.04f * s, 0.22f * s), collider: false);
            lens.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var lightGo = new GameObject("SodiumSpot");
            lightGo.transform.SetParent(lamp.transform, false);
            lightGo.transform.localPosition = new Vector3(reach, height - 0.22f * s, 0f);
            lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = PsxNightPalette.Sodium;
            // Inverse square from 5 m up: this puts roughly 1.5 lux-equivalent on the ground
            // under the head, a clear warm pool against the 0.45 moon.
            light.intensity = 40f * s * s;
            light.range = 16f * s;
            light.spotAngle = 125f;
            light.innerSpotAngle = 55f;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 0.9f;
            light.shadowResolution = LightShadowResolution.Medium;
            return lamp;
        }

        private static GameObject AddPart(Transform parent, PrimitiveType type, string name, Material mat,
            Vector3 localPosition, Vector3 localScale, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject BuildPuddle(Transform parent, string name, Material mat,
            Vector3 groundPoint, Vector2 size, float yaw)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = WaterLayer;
            go.transform.SetParent(parent, false);
            // A centimetre up: enough to never z-fight the slab, too little to see.
            go.transform.position = groundPoint + Vector3.up * 0.012f * UtezDimensions.WorldScale;
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private static Material EnsurePuddleMaterial()
        {
            string path = $"{MaterialFolder}/UTEZ_Puddle.mat";
            var shader = Shader.Find(PsxShaderProperties.Puddle);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "UTEZ_Puddle" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
