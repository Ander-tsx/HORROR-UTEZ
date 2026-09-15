using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Places the campus furniture: lamp posts around the explanada, the two flagpoles
    /// beside the CECADEC entrance, and one reference copy of each imported prop.
    ///
    /// Lamps matter more than they look. They give the plaza pools of light to move
    /// between, which is what turns an evenly-lit space into one with somewhere to hide —
    /// and they give the flashlight something to compete with instead of being the only
    /// light source in the game.
    ///
    /// The imported props are not dressed into the buildings on purpose — see
    /// BuildImportedProps. Lamps and flagpoles are, because their positions follow from the
    /// campus geometry rather than from taste.
    /// </summary>
    public static class UtezPropBuilder
    {
        private const string RootName = "UTEZ_Props";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";

        [MenuItem("HORROR-UTEZ/Build UTEZ Props")]
        public static void Build()
        {
            if (!UtezProjectSetup.GuardWorkingScene(RootName))
                return;

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build UTEZ Props");

            var metal = LoadMaterial("UTEZ_Stone");
            var bulb = LoadMaterial("UTEZ_WallBeige");

            int lamps = 0;
            foreach (var position in LampPositions())
            {
                BuildLamp(root.transform, position, metal, bulb, lamps++);
            }

            BuildFlagpoles(root.transform, metal);

            int imported = BuildImportedProps(root.transform);

            // Lamp posts never move either; only their light values change at runtime.
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[PROPS] Placed {lamps} lamp posts, 2 flagpoles and " +
                      $"{imported} imported props in front of the spawn.");
        }

        /// <summary>Evenly spaced down both long edges of the explanada, inset off the kerb.</summary>
        private static IEnumerable<Vector3> LampPositions()
        {
            Vector2 half = UtezDimensions.SlabSize * 0.5f;
            Vector2 center = UtezDimensions.SlabCenter;
            float inset = 4f * UtezDimensions.WorldScale;

            const int perSide = 6;
            for (int i = 0; i < perSide; i++)
            {
                float t = (i + 0.5f) / perSide;
                float z = center.y - half.y + t * half.y * 2f;
                yield return new Vector3(center.x - half.x + inset, 0f, z);
                yield return new Vector3(center.x + half.x - inset, 0f, z);
            }
        }

        private static void BuildLamp(Transform parent, Vector3 position, Material metal, Material bulb, int index)
        {
            float scale = UtezDimensions.WorldScale;
            float height = 5.5f * scale;

            var lamp = new GameObject($"LampPost_{index}");
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = position;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(lamp.transform, false);
            pole.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            // Unity's cylinder is 2 units tall, hence the halved Y scale.
            pole.transform.localScale = new Vector3(0.12f * scale, height * 0.5f, 0.12f * scale);
            pole.GetComponent<MeshRenderer>().sharedMaterial = metal;

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(lamp.transform, false);
            head.transform.localPosition = new Vector3(0f, height, 0f);
            head.transform.localScale = new Vector3(0.5f * scale, 0.18f * scale, 0.5f * scale);
            head.GetComponent<MeshRenderer>().sharedMaterial = bulb;
            // No collider on the head: it is above head height and only costs physics time.
            Object.DestroyImmediate(head.GetComponent<Collider>());

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(lamp.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, height - 0.2f * scale, 0f);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            // Sodium-vapour orange: the colour of every neglected campus car park at night.
            light.color = new Color(1f, 0.78f, 0.48f);
            light.range = 22f * scale;
            // Inverse-square again, so this needs to be large to reach the ground at all —
            // and it has to track scale SQUARED, since the head sits `height` units up.
            light.intensity = 62f * scale * scale;
            light.shadows = LightShadows.None;
            lightGo.AddComponent<LampFlicker>();
        }

        /// <summary>The pair of white poles flanking the CECADEC entrance in the photo.</summary>
        private static void BuildFlagpoles(Transform parent, Material metal)
        {
            var fp = UtezDimensions.Cecadec;
            float scale = UtezDimensions.WorldScale;
            float height = 8f * scale;

            var group = new GameObject("Flagpoles").transform;
            group.SetParent(parent, false);
            group.localPosition = new Vector3(fp.ScaledCenter.x, 0f, fp.ScaledCenter.y);
            group.localRotation = Quaternion.Euler(0f, fp.YawDeg, 0f);

            float z = fp.ScaledSize.y * 0.5f + 3f * scale;
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * 4f * scale;
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = $"Flagpole_{i}";
                pole.transform.SetParent(group, false);
                pole.transform.localPosition = new Vector3(x, height * 0.5f, z);
                pole.transform.localScale = new Vector3(0.08f * scale, height * 0.5f, 0.08f * scale);
                pole.GetComponent<MeshRenderer>().sharedMaterial = metal;
            }
        }

        // ---- Imported props ---------------------------------------------------

        /// <summary>
        /// One bench and one extinguisher, dropped in the open ground in front of the player
        /// spawn.
        ///
        /// Deliberately ONE of each and deliberately not dressed into the buildings. Placing
        /// furniture is an art call, not a geometry call: the useful thing an automated pass
        /// can do is put a correctly scaled, correctly oriented copy where it will be seen on
        /// the first frame, and leave duplicating and arranging to whoever is looking at it.
        ///
        /// Re-running this pass wipes and re-places them, so anything moved by hand should be
        /// dragged out of UTEZ_Props first — or copied, which is the point.
        /// </summary>
        private static int BuildImportedProps(Transform parent)
        {
            var group = new GameObject("Imported").transform;
            group.SetParent(parent, false);

            int placed = 0;

            // Real metres in front of the spawn, which stands at Z +5.3 looking south.
            placed += Place(group, UtezPropAssets.BenchPrefab, "Bench_Waiting3",
                new Vector3(-1.3f, 0f, 2.0f), BenchSitterYaw);

            placed += Place(group, UtezPropAssets.ExtinguisherPrefab, "FireExtinguisher",
                new Vector3(1.4f, 0f, 2.0f), 180f);

            return placed;
        }

        /// <summary>
        /// Which way the person sitting on the bench looks, in world degrees.
        ///
        /// 180 = the sitter looks north, back toward the spawn, so the player walks up to the
        /// seats rather than to the backrest.
        ///
        /// The mesh's backrest sits on local -Z, NOT on +Z as the export axes suggested — a
        /// capture from the player's own camera settled it after the first guess put every
        /// bench the wrong way round. This is the single number that decides it: if it ever
        /// reads backwards again, add or subtract 180 here and nothing else needs touching.
        /// </summary>
        private const float BenchSitterYaw = 180f;

        private static int Place(Transform parent, string prefabPath, string name,
            Vector3 positionMetres, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[PROPS] No prefab at {prefabPath}. " +
                                 "Run HORROR-UTEZ > Rebuild PSX Prop Assets first.");
                return 0;
            }

            float scale = UtezDimensions.WorldScale;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.SetPositionAndRotation(
                positionMetres * scale, Quaternion.Euler(0f, yaw, 0f));
            // The prefabs are authored at real metres, so they carry WorldScale themselves.
            instance.transform.localScale = Vector3.one * scale;
            return 1;
        }

        private static Material LoadMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
            if (material == null)
                Debug.LogWarning($"[PROPS] Missing material '{name}'.");
            return material;
        }
    }
}
