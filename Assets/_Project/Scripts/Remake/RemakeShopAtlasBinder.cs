using UnityEngine;

namespace HorrorUtez.Remake
{
    // Explicit region binding avoids atlas rendering differences observed in Unity 6000.6 SRP batches.
    // Bind the region explicitly for these surfaces only, leaving campus batching enabled.
    // This component never creates geometry, materials, furniture or lights.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("HORROR-UTEZ/Tienda/Materiales del atlas")]
    public sealed class RemakeShopAtlasBinder : MonoBehaviour
    {
        private static readonly int Region = Shader.PropertyToID("_AtlasRect");

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();

        [ContextMenu("Actualizar materiales del atlas")]
        public void Refresh()
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (material == null || !material.name.StartsWith("Shop_") || !material.HasProperty(Region)) continue;
                    var region = material.GetVector(Region);
                    if (region.x <= 0 || region.x >= .999f || region.y <= 0) continue;
                    block.Clear();
                    renderer.GetPropertyBlock(block, slot);
                    block.SetVector(Region, region);
                    renderer.SetPropertyBlock(block, slot);
                }
            }
        }
    }
}
