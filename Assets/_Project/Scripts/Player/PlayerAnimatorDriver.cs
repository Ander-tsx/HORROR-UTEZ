using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorUtez.Player
{
    /// <summary>
    /// Drives the player's Humanoid body (PlayerCharacter.fbx + Player.controller) from
    /// the FirstPersonController, which stays the one source of movement: the animations
    /// are in place, root motion is off, and this only tells the Animator what the capsule
    /// is doing.
    ///
    /// - Omnidirectional locomotion: ground velocity in the body's local frame feeds a 2-D
    ///   freeform blend tree (MoveX = strafe, MoveZ = forward), damped so starts, stops and
    ///   direction changes blend instead of snapping.
    /// - Crouch, jump and fall from the controller's own state.
    /// - Look-at IK: head, neck and a little of the spine turn toward where the camera
    ///   looks, so the reflection in a mirror looks where the player looks.
    /// - First person: the head mesh goes to the PlayerHead layer, which the player's own
    ///   camera skips (it sits inside that head) and every mirror renders.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        /// <summary>User layer 8; also named in PsxPlanarReflection and TagManager.</summary>
        public const string HeadLayerName = "PlayerHead";

        private static readonly int MoveXId = Animator.StringToHash("MoveX");
        private static readonly int MoveZId = Animator.StringToHash("MoveZ");
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int CrouchId = Animator.StringToHash("Crouch");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int VerticalId = Animator.StringToHash("VerticalSpeed");
        private static readonly int EmoteId = Animator.StringToHash("Emote");

        [SerializeField] private FirstPersonController controller;
        [Tooltip("Name of the head mesh object hidden from the player's own camera.")]
        [SerializeField] private string headMeshName = "PlayerHead";

        [Header("Smoothing")]
        [Tooltip("Seconds for the locomotion parameters to settle. Lower = snappier.")]
        [SerializeField] private float moveDamp = 0.12f;
        [SerializeField] private float crouchDamp = 0.15f;

        [Header("Look IK")]
        [SerializeField, Range(0f, 1f)] private float lookWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float bodyWeight = 0.2f;
        [SerializeField, Range(0f, 1f)] private float headWeight = 0.85f;
        [SerializeField, Range(0f, 1f)] private float clampWeight = 0.55f;

        [Header("Emotes (testing)")]
        [Tooltip("Filled by the prefab builder from the Emote_* clips. Keys 1-9 play them; " +
                 "the same key again, or moving, stops.")]
        [SerializeField] private string[] emoteNames = new string[0];
        [SerializeField] private bool showEmoteHelp = true;

        private Animator _animator;
        private Transform _camera;
        private int _emote;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animator.applyRootMotion = false;
            if (controller == null)
                controller = GetComponentInParent<FirstPersonController>();

            var cam = controller != null ? controller.GetComponentInChildren<Camera>() : null;
            _camera = cam != null ? cam.transform : null;
            HideHeadFromOwnCamera(cam);
        }

        private void HideHeadFromOwnCamera(Camera cam)
        {
            int layer = LayerMask.NameToLayer(HeadLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[PLAYER] Layer '{HeadLayerName}' missing from Tags and Layers; " +
                                 "the head will fill the first-person view.");
                return;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.name == headMeshName)
                    renderer.gameObject.layer = layer;

            if (cam != null)
                cam.cullingMask &= ~(1 << layer);
        }

        private void Update()
        {
            if (controller == null)
                return;

            float dt = Time.deltaTime;
            Vector3 local = transform.InverseTransformDirection(controller.Velocity);
            _animator.SetFloat(MoveXId, local.x, moveDamp, dt);
            _animator.SetFloat(MoveZId, local.z, moveDamp, dt);
            _animator.SetFloat(SpeedId, controller.CurrentSpeed, moveDamp, dt);
            _animator.SetFloat(CrouchId, controller.CrouchBlend, crouchDamp, dt);
            _animator.SetBool(GroundedId, controller.IsGrounded);
            _animator.SetFloat(VerticalId, controller.VerticalVelocity);

            UpdateEmotes();
        }

        private void UpdateEmotes()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && emoteNames.Length > 0)
            {
                for (int i = 0; i < Mathf.Min(9, emoteNames.Length); i++)
                {
                    if (!keyboard[Key.Digit1 + i].wasPressedThisFrame)
                        continue;
                    _emote = _emote == i + 1 ? 0 : i + 1;
                }
            }

            // Walking off cancels the dance.
            if (_emote != 0 && controller.CurrentSpeed > 0.4f)
                _emote = 0;
            _animator.SetInteger(EmoteId, _emote);
        }

        private void OnGUI()
        {
            if (!showEmoteHelp || emoteNames.Length == 0 || !Debug.isDebugBuild)
                return;
            var text = "EMOTES (test)\n";
            for (int i = 0; i < Mathf.Min(9, emoteNames.Length); i++)
                text += $"{i + 1}  {emoteNames[i]}{(_emote == i + 1 ? "  <" : "")}\n";
            GUI.Label(new Rect(12, Screen.height - 24 - 18 * Mathf.Min(10, emoteNames.Length + 1), 400, 220), text);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_camera == null)
                return;

            _animator.SetLookAtWeight(lookWeight, bodyWeight, headWeight, 0f, clampWeight);
            _animator.SetLookAtPosition(_camera.position + _camera.forward * 10f);
        }
    }
}
