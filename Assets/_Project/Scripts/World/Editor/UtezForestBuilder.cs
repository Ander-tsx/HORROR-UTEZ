using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Scatters the vendored PSX forest pack around the campus.
    ///
    /// The pack's meshes are authored tiny (PSX_Tree3 is 0.31 units tall), so nothing here
    /// trusts the FBX import scale: every instance is scaled by its own bounds to hit a
    /// target height in world units. That keeps the scatter correct even if the import
    /// settings are changed later.
    ///
    /// Placement is seeded, so the forest is identical on every rebuild — a forest that
    /// reshuffles itself makes it impossible to tell a layout change from noise.
    /// </summary>
    public static class UtezForestBuilder
    {
        private const string RootName = "UTEZ_Forest";
        private const string FbxPath =
            "Assets/ThirdParty/PSX_Forest/PSX_Forest_AssetCollection_byStarkCrafts.fbx";
        private const int Seed = 20260908;

        private readonly struct Species
        {
            public readonly string Mesh;
            public readonly int Count;
            public readonly float MinHeight;   // real metres, before WorldScale
            public readonly float MaxHeight;
            public readonly bool Collides;
            /// <summary>Material per submesh, replacing the pack's untextured white ones.</summary>
            public readonly string[] Materials;

            public Species(string mesh, int count, float minHeight, float maxHeight, bool collides,
                string[] materials)
            {
                Mesh = mesh;
                Count = count;
                MinHeight = minHeight;
                MaxHeight = maxHeight;
                Collides = collides;
                Materials = materials;
            }
        }

        private static readonly Species[] Palette =
        {
            new("PSX_Tree1", 110, 9f, 15f, true, new[] { "UTEZ_Bark", "UTEZ_Leaves" }),
            new("PSX_Tree2", 90, 10f, 16f, true, new[] { "UTEZ_Bark", "UTEZ_Leaves" }),
            new("PSX_Tree3", 100, 9f, 15f, true, new[] { "UTEZ_Bark", "UTEZ_LeavesDry" }),
            new("PSX_Tree4", 80, 8f, 14f, true, new[] { "UTEZ_Bark", "UTEZ_Leaves" }),
            // Bare trunks read as dead wood; they carry most of the dread in a treeline.
            new("PSX_Treetrunk", 45, 5f, 9f, true, new[] { "UTEZ_Bark", "UTEZ_Bark" }),
            new("PSX_Rock", 120, 0.6f, 2.2f, false, new[] { "UTEZ_RockGrey" }),
            new("PSX_Grass", 260, 0.3f, 0.7f, false, new[] { "UTEZ_Leaves" }),
            new("PSX_Reed", 90, 0.4f, 0.9f, false, new[] { "UTEZ_LeavesDry" }),
            new("PSX_Dandelion", 70, 0.2f, 0.4f, false, new[] { "UTEZ_Leaves" }),
        };

        [MenuItem("HORROR-UTEZ/Build UTEZ Forest")]
        public static void Build()
        {
            if (!UtezProjectSetup.GuardWorkingScene(RootName))
                return;

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var sources = LoadSources();
            if (sources.Count == 0)
            {
                Debug.LogError($"[FOREST] No meshes loaded from {FbxPath}");
                return;
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build UTEZ Forest");

            var random = new System.Random(Seed);
            int placed = 0;

            foreach (var species in Palette)
            {
                if (!sources.TryGetValue(species.Mesh, out var source))
                {
                    Debug.LogWarning($"[FOREST] Missing mesh '{species.Mesh}' in the pack.");
                    continue;
                }

                var group = new GameObject(species.Mesh).transform;
                group.SetParent(root.transform, false);

                for (int i = 0; i < species.Count; i++)
                {
                    if (!TryPickPoint(random, out Vector3 position))
                        continue;

                    var instance = Object.Instantiate(source, group);
                    instance.name = $"{species.Mesh}_{i}";
                    instance.transform.position = position;

                    float yaw = (float)random.NextDouble() * 360f;
                    // A couple of degrees of lean stops the scatter reading as a pin cushion.
                    float tiltX = ((float)random.NextDouble() - 0.5f) * 6f;
                    float tiltZ = ((float)random.NextDouble() - 0.5f) * 6f;

                    // COMPOSE with the source rotation, never replace it. This FBX is authored
                    // Z-up and Unity carries the correction on the object's own rotation, so
                    // assigning a fresh rotation lays every tree on its side.
                    instance.transform.rotation =
                        Quaternion.Euler(tiltX, yaw, tiltZ) * source.transform.rotation;

                    float target = Mathf.Lerp(species.MinHeight, species.MaxHeight,
                        (float)random.NextDouble()) * UtezDimensions.WorldScale;
                    ApplyHeight(instance, target);

                    ApplyMaterials(instance, species.Materials);
                    if (i == 0) LogFirstInstance(species.Mesh, instance);

                    if (species.Collides)
                        AddTrunkCollider(instance);

                    GameObjectUtility.SetStaticEditorFlags(instance,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.OccludeeStatic);
                    placed++;
                }
            }

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[FOREST] Placed {placed} props across {Palette.Length} species.");
        }

        /// <summary>Every GameObject sub-asset in the FBX, keyed by name.</summary>
        private static Dictionary<string, GameObject> LoadSources()
        {
            var sources = new Dictionary<string, GameObject>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (asset is GameObject go && go.GetComponent<MeshFilter>() != null)
                    sources[go.name.Trim()] = go;
            }
            return sources;
        }

        /// <summary>
        /// Rejection-samples a point on the dirt ring: outside the concrete explanada,
        /// inside the outer tree line. Trees standing on the plaza would be nonsense.
        /// </summary>
        private static bool TryPickPoint(System.Random random, out Vector3 point)
        {
            Vector2 slabHalf = UtezDimensions.SlabSize * 0.5f;
            Vector2 outerHalf = UtezDimensions.DirtSize * 0.5f + Vector2.one * UtezDimensions.ForestRingWidth;
            Vector2 center = UtezDimensions.SlabCenter;

            // Keep the wood off the campus edge. Trees this tall crowding the kerb read
            // as a wall right at the boundary instead of as a treeline in the distance.
            float clearance = 18f * UtezDimensions.WorldScale;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                float x = center.x + ((float)random.NextDouble() * 2f - 1f) * outerHalf.x;
                float z = center.y + ((float)random.NextDouble() * 2f - 1f) * outerHalf.y;

                bool onSlab = Mathf.Abs(x - center.x) < slabHalf.x + clearance &&
                              Mathf.Abs(z - center.y) < slabHalf.y + clearance;
                if (onSlab)
                    continue;

                point = new Vector3(x, 0f, z);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Replaces the pack's materials, one per submesh. The forest FBX arrives with no
        /// albedo textures, so its stock materials render white; these are PSX materials so
        /// the trees also pick up vertex jitter and colour crunch.
        /// </summary>
        private static void ApplyMaterials(GameObject instance, string[] names)
        {
            if (names == null || names.Length == 0)
                return;

            var renderer = instance.GetComponentInChildren<MeshRenderer>();
            var filter = instance.GetComponentInChildren<MeshFilter>();
            if (renderer == null || filter == null || filter.sharedMesh == null)
                return;

            int slots = filter.sharedMesh.subMeshCount;
            var materials = new Material[slots];
            for (int i = 0; i < slots; i++)
                materials[i] = LoadMaterial(names[Mathf.Min(i, names.Length - 1)]);

            renderer.sharedMaterials = materials;
        }

        private static readonly Dictionary<string, Material> MaterialCache = new();

        private static Material LoadMaterial(string name)
        {
            if (MaterialCache.TryGetValue(name, out var cached))
                return cached;

            var material = AssetDatabase.LoadAssetAtPath<Material>(
                $"Assets/_Project/Art/Materials/{name}.mat");
            if (material == null)
                Debug.LogWarning($"[FOREST] Missing material '{name}'.");

            MaterialCache[name] = material;
            return material;
        }

        /// <summary>
        /// Scales an instance so it stands a given height in WORLD space.
        ///
        /// Measured from the renderer's world bounds, not the mesh's local bounds: the mesh
        /// is authored in a different axis convention, so local Y is not the height. World
        /// bounds already account for whatever rotation the import applied.
        /// </summary>
        private static void ApplyHeight(GameObject instance, float targetHeight)
        {
            var renderer = instance.GetComponentInChildren<MeshRenderer>();
            if (renderer == null)
                return;

            instance.transform.localScale = Vector3.one;
            float current = renderer.bounds.size.y;
            if (current <= 0.0001f)
                return;

            instance.transform.localScale = Vector3.one * (targetHeight / current);
        }

        /// <summary>Reports what a species actually ended up measuring in the scene.</summary>
        private static void LogFirstInstance(string mesh, GameObject instance)
        {
            var renderer = instance.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
                Debug.Log($"[FOREST] {mesh} world size {renderer.bounds.size}");
        }

        /// <summary>
        /// A capsule around the trunk, not a mesh collider on the canopy.
        ///
        /// FBX imports carry no colliders, so trees would otherwise be walk-through. The
        /// capsule is deliberately narrow: blocking on the full canopy radius makes a wood
        /// feel like a wall, and grass and pebbles get nothing at all — hundreds of tiny
        /// colliders cost physics time for nothing and snagging on a blade feels broken.
        /// </summary>
        private static void AddTrunkCollider(GameObject instance)
        {
            var filter = instance.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;

            Vector3 size = filter.sharedMesh.bounds.size;
            var capsule = instance.AddComponent<CapsuleCollider>();
            capsule.direction = 1; // Y
            capsule.height = size.y;
            capsule.radius = Mathf.Max(size.x, size.z) * 0.12f;
            capsule.center = new Vector3(0f, filter.sharedMesh.bounds.center.y, 0f);
        }
    }
}
