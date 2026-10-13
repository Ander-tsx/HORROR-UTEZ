using UnityEngine;

namespace HorrorUtez.Remake
{
    // Authored entrance: owns only menu framing and surface binding, never geometry or lights.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("HORROR-UTEZ/Mapa/Entrada y encuadre del menu")]
    public sealed class RemakeEntrance : MonoBehaviour
    {
        [Tooltip("Punto de vista del menu. Mueve todo el prefab para reubicar entrada y encuadre juntos.")]
        public Transform MenuView;
        [Tooltip("Punto hacia el que mira la camara; deja libre el lado izquierdo para los controles.")]
        public Transform MenuFocus;
        [Range(35,80)] public float FieldOfView = 62;
        [Range(0,.5f)] public float MenuSway = .12f;

        private void OnEnable() => RefreshMaterials();
        private void OnValidate() => RefreshMaterials();

        public bool Frame(Camera camera, float time)
        {
            if (camera == null || MenuView == null || MenuFocus == null) return false;
            var direction = MenuFocus.position - MenuView.position;
            if (direction.sqrMagnitude < .01f) return false;
            camera.transform.SetPositionAndRotation(MenuView.position,
                Quaternion.LookRotation(direction, transform.up) * Quaternion.Euler(0, Mathf.Sin(time * .2f) * MenuSway, 0));
            camera.fieldOfView = FieldOfView;
            return true;
        }

        [ContextMenu("Actualizar materiales del atlas")]
        public void RefreshMaterials()
        {
            int regionProperty = Shader.PropertyToID("_AtlasRect");
            var block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (material == null || !material.name.StartsWith("Entrance_") || !material.HasProperty(regionProperty)) continue;
                    var region = material.GetVector(regionProperty);
                    if (region.x <= 0 || region.x >= .999f || region.y <= 0) continue;
                    block.Clear(); renderer.GetPropertyBlock(block,slot);
                    block.SetVector(regionProperty,region); renderer.SetPropertyBlock(block,slot);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (MenuView == null || MenuFocus == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(MenuView.position,.25f);
            Gizmos.DrawLine(MenuView.position,MenuFocus.position);
            Gizmos.DrawWireSphere(MenuFocus.position,.25f);
        }
    }
}
