using UnityEngine;

namespace HorrorUtez.World
{
    /// <summary>
    /// One door leaf swinging on its hinge. The GameObject's pivot is the hinge (authored
    /// that way in Blender) and its rest pose is closed. Nothing here listens to input:
    /// whatever interacts with doors calls <see cref="Open"/>, <see cref="Close"/> or
    /// <see cref="Toggle"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HingedDoor : MonoBehaviour
    {
        [Tooltip("Hinge axis in this transform's local space (world up at authoring time).")]
        [SerializeField] private Vector3 hingeAxis = Vector3.up;

        [Tooltip("Signed swing when fully open. The sign picks the side the leaf opens to.")]
        [SerializeField] private float openAngle = 90f;

        [SerializeField, Min(1f)] private float degreesPerSecond = 160f;
        [SerializeField] private bool startOpen;

        private Quaternion _closed;
        private float _angle;
        private float _target;

        public bool IsOpen => _target != 0f;

        /// <summary>Editor setup: axis and swing are measured once the leaf is placed.</summary>
        public void Configure(Vector3 localHingeAxis, float signedOpenAngle)
        {
            hingeAxis = localHingeAxis.normalized;
            openAngle = signedOpenAngle;
        }

        public void Open() => _target = openAngle;
        public void Close() => _target = 0f;
        public void Toggle() => _target = IsOpen ? 0f : openAngle;

        private void Awake()
        {
            _closed = transform.localRotation;
            _angle = _target = startOpen ? openAngle : 0f;
            Apply();
        }

        private void Update()
        {
            if (Mathf.Approximately(_angle, _target))
                return;
            _angle = Mathf.MoveTowards(_angle, _target, degreesPerSecond * Time.deltaTime);
            Apply();
        }

        private void Apply() =>
            transform.localRotation = _closed * Quaternion.AngleAxis(_angle, hingeAxis);

        [ContextMenu("Toggle")]
        private void ToggleFromInspector() => Toggle();
    }
}
