using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorUtez.Player;
using HorrorUtez.Rendering;
using HorrorUtez.Rendering.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorUtez.World.Editor
{
    /// <summary>
    /// The player's body, v2: a skinned Humanoid (PlayerCharacter.fbx, built by
    /// Tools/blender/gen_player_v2.py from photos) animated by free humanoid clips.
    ///
    /// <c>HORROR-UTEZ > Player > Build Player Character</c> writes the materials, the
    /// Animator Controller (from whatever clips are in Characters/Animations), the prefab,
    /// and puts it on the open scene's player. Rerun it after adding clips.
    /// </summary>
    public static class UtezPlayerModel
    {
        private const string ModelPath = "Assets/_Project/Art/Characters/Player/PlayerCharacter.fbx";
        private const string AnimationFolder = "Assets/_Project/Art/Characters/Animations";
        private const string ControllerPath = "Assets/_Project/Art/Characters/Player/Player.controller";
        private const string TextureFolder = "Assets/_Project/Art/Textures/Player";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Player";
        public const string PrefabPath = PrefabFolder + "/PlayerCharacter.prefab";

        /// <summary>Pupil height of the model; FirstPersonController's eye height matches it.</summary>
        private const float EyeHeight = 1.575f;

        /// <summary>
        /// The camera sits at the eyes, which are at the front of the head. Moving the body
        /// back puts the camera ahead of the face and the neck, so looking down shows the
        /// chest and the feet instead of the inside of the collar.
        /// </summary>
        private const float BodyBackOffset = 0.12f;

        /// <summary>
        /// Ground speed each clip family is authored at (m/s). The blend tree plays a clip
        /// faster or slower so the feet keep up with the capsule, within limits that still
        /// look like the same gait.
        /// </summary>
        private const float WalkClipSpeed = 1.45f;
        private const float RunClipSpeed = 3.9f;
        private const float CrouchClipSpeed = 1.0f;

        private static readonly (string Name, PsxSurfaceKind Kind)[] Materials =
        {
            ("Player_Head", PsxSurfaceKind.Satin),
            ("Player_Skin", PsxSurfaceKind.Satin),
            ("Player_ShirtWhite", PsxSurfaceKind.Matte),
            ("Player_ShirtGrey", PsxSurfaceKind.Matte),
            ("Player_ShirtNavy", PsxSurfaceKind.Matte),
            ("Player_Pants", PsxSurfaceKind.Matte),
            ("Player_Shoes", PsxSurfaceKind.Satin),
        };

        [MenuItem("HORROR-UTEZ/Player/Build Player Character (rigged, animated) and Put It On the Player")]
        public static void BuildAndPlace()
        {
            EnsureMaterials();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var controller = BuildController();
            if (BuildPrefab(controller))
                PlaceOnPlayer();
        }

        public static void EnsureMaterials()
        {
            Directory.CreateDirectory(PsxCharacterImporter.MaterialFolder);
            var shader = PsxMaterialDefaults.LitShader;

            foreach (var (name, kind) in Materials)
            {
                string path = $"{PsxCharacterImporter.MaterialFolder}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader) { name = name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                mat.SetTexture(PsxShaderProperties.MainTex,
                    AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/T_{name}.png"));
                mat.SetVector(PsxShaderProperties.Tiling, Vector2.one);
                PsxMaterialDefaults.Apply(mat, kind, wettable: false);
                PsxMaterialDefaults.SetTransparent(mat, false);
                // The maps are already PS1-sized and point-filtered; square texel snapping
                // would halve the face's resolution on its 2:1 map.
                mat.SetFloat(PsxShaderProperties.UsePixelation, 0f);
                // A skinned body wobbling to a snap grid reads as broken, not retro.
                mat.SetFloat(PsxShaderProperties.UseVertexJitter, 0f);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        // ---- Animator ------------------------------------------------------------------

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
                return clips;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip != null)
                    clips[Path.GetFileNameWithoutExtension(path)] = clip;
            }
            return clips;
        }

        /// <summary>
        /// Base layer, IK pass on (look-at):
        ///   Grounded — 1-D blend on Crouch between two 2-D freeform directional trees
        ///              (MoveX, MoveZ): standing (idle, walk and run in 4 directions, the
        ///              diagonals blended) and crouched (idle, crouch-walk in 4 directions).
        ///   Air      — 1-D blend on VerticalSpeed between falling and jumping.
        /// Every clip slot is optional: a missing file just leaves that direction to its
        /// neighbours.
        /// </summary>
        public static AnimatorController BuildController()
        {
            var clips = LoadClips();
            Debug.Log($"[PLAYER] Clips found: {(clips.Count == 0 ? "none" : string.Join(", ", clips.Keys))}");

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Emote", AnimatorControllerParameterType.Int);

            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            var sm = controller.layers[0].stateMachine;

            float walk = 2.6f, run = 5.2f, crouch = walk * 0.45f;

            var ground = controller.CreateBlendTreeInController("Grounded", out var groundTree, 0);
            groundTree.blendType = BlendTreeType.Simple1D;
            groundTree.blendParameter = "Crouch";
            groundTree.useAutomaticThresholds = false;

            var stand = groundTree.CreateBlendTreeChild(0f);
            stand.name = "Standing";
            SetupDirectional(stand);
            Add(stand, clips, "Idle", Vector2.zero, 1f);
            Add(stand, clips, "WalkF", new Vector2(0, walk), walk / WalkClipSpeed);
            Add(stand, clips, "WalkB", new Vector2(0, -walk), walk / WalkClipSpeed);
            Add(stand, clips, "WalkL", new Vector2(-walk, 0), walk / WalkClipSpeed);
            Add(stand, clips, "WalkR", new Vector2(walk, 0), walk / WalkClipSpeed);
            Add(stand, clips, "RunF", new Vector2(0, run), run / RunClipSpeed);
            Add(stand, clips, "RunB", new Vector2(0, -run), run / RunClipSpeed);
            Add(stand, clips, "RunL", new Vector2(-run, 0), run / RunClipSpeed);
            Add(stand, clips, "RunR", new Vector2(run, 0), run / RunClipSpeed);

            var crouched = groundTree.CreateBlendTreeChild(1f);
            crouched.name = "Crouched";
            SetupDirectional(crouched);
            Add(crouched, clips, "CrouchIdle", Vector2.zero, 1f);
            Add(crouched, clips, "CrouchF", new Vector2(0, crouch), crouch / CrouchClipSpeed);
            Add(crouched, clips, "CrouchB", new Vector2(0, -crouch), crouch / CrouchClipSpeed);
            Add(crouched, clips, "CrouchL", new Vector2(-crouch, 0), crouch / CrouchClipSpeed);
            Add(crouched, clips, "CrouchR", new Vector2(crouch, 0), crouch / CrouchClipSpeed);
            if (crouched.children.Length == 0)
                Add(crouched, clips, "Idle", Vector2.zero, 1f);

            sm.defaultState = ground;

            if (clips.ContainsKey("Jump") || clips.ContainsKey("Fall"))
            {
                var air = controller.CreateBlendTreeInController("Air", out var airTree, 0);
                airTree.blendType = BlendTreeType.Simple1D;
                airTree.blendParameter = "VerticalSpeed";
                airTree.useAutomaticThresholds = false;
                if (clips.TryGetValue("Fall", out var fall)) airTree.AddChild(fall, -4f);
                if (clips.TryGetValue("Jump", out var jump)) airTree.AddChild(jump, 3f);

                var takeOff = ground.AddTransition(air);
                takeOff.hasExitTime = false;
                takeOff.duration = 0.12f;
                takeOff.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
                takeOff.AddCondition(AnimatorConditionMode.Greater, 1f, "VerticalSpeed");

                var drop = ground.AddTransition(air);
                drop.hasExitTime = false;
                drop.duration = 0.2f;
                drop.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
                drop.AddCondition(AnimatorConditionMode.Less, -2.5f, "VerticalSpeed");

                var land = air.AddTransition(ground);
                land.hasExitTime = false;
                land.duration = 0.1f;
                land.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            }

            AddEmotes(controller, sm, ground, clips);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>Emote_* clips, sorted by name, numbered from 1. Testing only.</summary>
        public static List<string> EmoteNames(Dictionary<string, AnimationClip> clips) =>
            clips.Keys.Where(k => k.StartsWith("Emote_")).OrderBy(k => k).ToList();

        /// <summary>
        /// One state per emote, entered from Any State when the Emote parameter equals its
        /// number and left as soon as it changes (PlayerAnimatorDriver sets it back to 0
        /// when the player moves or presses the key again).
        /// </summary>
        private static void AddEmotes(AnimatorController controller, AnimatorStateMachine sm,
            AnimatorState ground, Dictionary<string, AnimationClip> clips)
        {
            var names = EmoteNames(clips);
            for (int i = 0; i < names.Count; i++)
            {
                int id = i + 1;
                var state = sm.AddState(names[i], new Vector3(300f, 120f + 60f * i, 0f));
                state.motion = clips[names[i]];

                var enter = sm.AddAnyStateTransition(state);
                enter.hasExitTime = false;
                enter.duration = 0.2f;
                enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.Equals, id, "Emote");

                var leave = state.AddTransition(ground);
                leave.hasExitTime = false;
                leave.duration = 0.25f;
                leave.AddCondition(AnimatorConditionMode.NotEqual, id, "Emote");
            }
        }

        private static void SetupDirectional(BlendTree tree)
        {
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
        }

        private static void Add(BlendTree tree, Dictionary<string, AnimationClip> clips, string key,
            Vector2 position, float timeScale)
        {
            if (!clips.TryGetValue(key, out var clip))
                return;
            tree.AddChild(clip, position);
            var children = tree.children;
            // Keep the gait recognisable: past these limits a walk looks like a scurry.
            children[^1].timeScale = Mathf.Clamp(timeScale, 0.75f, 1.6f);
            tree.children = children;
        }

        // ---- Prefab and placement ------------------------------------------------------

        public static bool BuildPrefab(AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[PLAYER] Nothing at {ModelPath}. Build it with Tools/blender/gen_player_v2.py.");
                return false;
            }

            Directory.CreateDirectory(PrefabFolder);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "PlayerCharacter";

                var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                // Mirrors and puddles show the body even when the player's camera does not
                // (its head is hidden from it): never let the Animator pause off screen.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                var driver = root.GetComponent<PlayerAnimatorDriver>() ?? root.AddComponent<PlayerAnimatorDriver>();
                var so = new SerializedObject(driver);
                var list = so.FindProperty("emoteNames");
                var names = EmoteNames(LoadClips());
                list.arraySize = names.Count;
                for (int i = 0; i < names.Count; i++)
                    list.GetArrayElementAtIndex(i).stringValue = names[i].Substring("Emote_".Length);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[PLAYER] Prefab written to {PrefabPath}.");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static void PlaceOnPlayer()
        {
            var controller = Object.FindFirstObjectByType<FirstPersonController>();
            if (controller == null)
            {
                Debug.LogWarning("[PLAYER] No FirstPersonController in the open scene; prefab built but not placed.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                return;

            var old = controller.transform.Find("Body");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, controller.transform);
            Undo.RegisterCreatedObjectUndo(body, "Place player body");
            body.name = "Body";
            body.transform.localPosition = new Vector3(0f, 0f, -BodyBackOffset);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            var driver = body.GetComponent<PlayerAnimatorDriver>();
            var so = new SerializedObject(driver);
            so.FindProperty("controller").objectReferenceValue = controller;
            so.ApplyModifiedPropertiesWithoutUndo();

            var fpc = new SerializedObject(controller);
            var eye = fpc.FindProperty("eyeHeight");
            if (eye != null)
            {
                eye.floatValue = EyeHeight;
                fpc.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log($"[PLAYER] Body placed on '{controller.name}'. Save the scene to keep it.");
        }
    }
}
