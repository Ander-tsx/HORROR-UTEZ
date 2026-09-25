using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Mirrors the scene across a plane into a low-resolution texture for the surfaces that
    /// lie on it: puddles on the ground, the mirror over the washbasins.
    ///
    /// The plane passes through this transform, with <c>transform.up</c> as its normal. The
    /// normal is flipped each frame to face the camera, so a double-sided mirror pane works
    /// from whichever side it is seen.
    ///
    /// The reflection camera is a plain rotation of the main camera to its mirrored pose, not
    /// a camera with a mirror matrix. A mirror matrix flips triangle winding and needs
    /// culling inverted around the render, which URP gives no hook for when a camera renders
    /// itself. A rotated camera keeps winding intact; its picture comes out flipped
    /// left-to-right, and the surface shaders read it back with 1 - u.
    ///
    /// Each instance owns its texture and hands it to its surfaces through a
    /// MaterialPropertyBlock, so several planes can run at once without fighting over a
    /// global. Cost: one extra scene render at <see cref="resolutionDivisor"/> of the screen,
    /// only on frames where one of its surfaces is visible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PsxPlanarReflection : MonoBehaviour
    {
        private static readonly int ReflectionTexId = Shader.PropertyToID("_PsxPlanarReflectionTex");
        private static readonly int ReflectionOnId = Shader.PropertyToID("_PsxPlanarReflectionOn");

        private static readonly HashSet<Camera> Cameras = new();

        /// <summary>User layer 8. The player's head and glasses live there.</summary>
        public const string PlayerHeadLayer = "PlayerHead";

        [Tooltip("Renderers on this plane. The reflection renders only while one is visible.")]
        [FormerlySerializedAs("puddles")]
        [SerializeField] private List<Renderer> surfaces = new();

        [Tooltip("Screen size divided by this. 3 on a 1080p phone is a 360p reflection.")]
        [SerializeField, Range(1, 8)] private int resolutionDivisor = 3;

        [Tooltip("Layers the reflection sees. Leave out the layer the reflective surfaces are on.")]
        [SerializeField] private LayerMask reflectedLayers = ~(1 << 4);

        [Tooltip("Seen in the reflection even if the main camera hides them — the player's own head.")]
        [SerializeField] private LayerMask alwaysReflected;

        [Tooltip("Pushes the clip plane off the surface so what is behind it never leaks in.")]
        [SerializeField] private float clipPlaneOffset = 0.02f;

        private Camera _reflectionCamera;
        private RenderTexture _texture;
        private MaterialPropertyBlock _block;
        private readonly List<Material> _materials = new();

        /// <summary>
        /// True for the cameras rendering mirror images. The PSX screen pass skips them:
        /// vignette and grain belong on the final frame, not inside a puddle. They stay Game
        /// cameras so the engine keeps rendering them on its own every frame.
        /// </summary>
        public static bool IsReflectionCamera(Camera camera) => camera != null && Cameras.Contains(camera);

        private void OnEnable()
        {
            _block ??= new MaterialPropertyBlock();
            // The player's head is hidden from their own camera (it would fill the view) but
            // must show in every mirror; see PlayerAnimatorDriver.
            int headLayer = LayerMask.NameToLayer(PlayerHeadLayer);
            if (headLayer >= 0)
                alwaysReflected |= 1 << headLayer;

            var go = new GameObject($"PsxReflectionCamera_{name}") { hideFlags = HideFlags.HideAndDontSave };
            _reflectionCamera = go.AddComponent<Camera>();
            _reflectionCamera.enabled = false;
            Cameras.Add(_reflectionCamera);

            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            // The PSX fog reads depth, and the reflection should fog like the world it mirrors.
            data.requiresDepthOption = CameraOverrideOption.On;
            data.antialiasing = AntialiasingMode.None;
        }

        private void OnDisable()
        {
            SetSurfaces(false);

            if (_reflectionCamera != null)
            {
                Cameras.Remove(_reflectionCamera);
                Destroy(_reflectionCamera.gameObject);
            }
            _reflectionCamera = null;

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
            _texture = null;
        }

        public void Register(Renderer surface)
        {
            if (surface != null && !surfaces.Contains(surface))
                surfaces.Add(surface);
        }

        private void LateUpdate()
        {
            var main = Camera.main;
            bool visible = main != null && AnySurfaceVisible();

            _reflectionCamera.enabled = visible;
            if (!visible)
            {
                SetSurfaces(false);
                return;
            }

            EnsureTexture(main);
            PlaceMirroredCamera(main);
            SetSurfaces(true);
        }

        private bool AnySurfaceVisible()
        {
            foreach (var surface in surfaces)
                if (surface != null && surface.isVisible)
                    return true;
            return false;
        }

        /// <summary>
        /// Only the material slots that read a reflection get the block: the mirror mesh
        /// also carries its aluminium edge, which must keep batching.
        /// </summary>
        private void SetSurfaces(bool on)
        {
            foreach (var surface in surfaces)
            {
                if (surface == null)
                    continue;

                surface.GetSharedMaterials(_materials);
                for (int i = 0; i < _materials.Count; i++)
                {
                    var mat = _materials[i];
                    if (mat == null || !mat.HasProperty(ReflectionOnId))
                        continue;

                    if (on && _texture != null)
                    {
                        surface.GetPropertyBlock(_block, i);
                        _block.SetTexture(ReflectionTexId, _texture);
                        _block.SetFloat(ReflectionOnId, 1f);
                        surface.SetPropertyBlock(_block, i);
                    }
                    else
                    {
                        surface.SetPropertyBlock(null, i);
                    }
                }
            }
        }

        private void EnsureTexture(Camera main)
        {
            int width = Mathf.Max(64, main.pixelWidth / resolutionDivisor);
            int height = Mathf.Max(64, main.pixelHeight / resolutionDivisor);

            if (_texture != null && _texture.width == width && _texture.height == height)
                return;

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            // HDR so a lamp stays brighter than 1 in the reflection and still blooms.
            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
            {
                name = $"_PsxReflection_{name}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _texture.Create();
            _reflectionCamera.targetTexture = _texture;
        }

        private void PlaceMirroredCamera(Camera main)
        {
            Vector3 point = transform.position;
            Vector3 normal = transform.up;
            var mainTransform = main.transform;

            // Face the viewer: the kept side of the clip plane is the camera's side.
            if (Vector3.Dot(mainTransform.position - point, normal) < 0f)
                normal = -normal;

            Vector3 position = Reflect(mainTransform.position - point, normal) + point;
            Vector3 forward = Reflect(mainTransform.forward, normal);
            Vector3 up = Reflect(mainTransform.up, normal);

            var cam = _reflectionCamera;
            // CopyFrom brings FOV, clipping and clear settings, but also the main camera's
            // transform and target; both are put back right after.
            cam.CopyFrom(main);
            cam.targetTexture = _texture;
            cam.cullingMask = (main.cullingMask | alwaysReflected) & reflectedLayers;
            // Render before the main camera so the texture is ready when the surface draws.
            cam.depth = main.depth - 1f;
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));

            // Oblique near plane on the surface: everything behind it is cut, or the mirror
            // would show the wall it hangs on and the puddle the ground it lies in.
            cam.ResetProjectionMatrix();
            Vector4 clipPlane = CameraSpacePlane(cam, point + normal * clipPlaneOffset, normal);
            cam.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);
        }

        private static Vector3 Reflect(Vector3 v, Vector3 n) => v - 2f * Vector3.Dot(v, n) * n;

        private static Vector4 CameraSpacePlane(Camera cam, Vector3 point, Vector3 normal)
        {
            Matrix4x4 worldToCamera = cam.worldToCameraMatrix;
            Vector3 cameraPoint = worldToCamera.MultiplyPoint(point);
            Vector3 cameraNormal = worldToCamera.MultiplyVector(normal).normalized;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z,
                -Vector3.Dot(cameraPoint, cameraNormal));
        }
    }
}
