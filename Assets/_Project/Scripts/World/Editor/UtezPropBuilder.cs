using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// Places the campus street furniture: lamp posts around the explanada, and the two
    /// flagpoles that stand beside the CECADEC entrance in the reference photo.
    ///
    /// Lamps matter more than they look. They give the plaza pools of light to move
    /// between, which is what turns an evenly-lit space into one with somewhere to hide —
    /// and they give the flashlight something to compete with instead of being the only
    /// light source in the game.
    /// </summary>
    public static class UtezPropBuilder
    {
        private const string RootName = "UTEZ_Props";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";

        [MenuItem("HORROR-UTEZ/Build UTEZ Props")]
        public static void Build()
        {
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

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[PROPS] Placed {lamps} lamp posts and 2 flagpoles.");
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
            // Inverse-square again, so this needs to be large to reach the ground at all.
            light.intensity = 140f;
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

        private static Material LoadMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
            if (material == null)
                Debug.LogWarning($"[PROPS] Missing material '{name}'.");
            return material;
        }
    }
}
