using System.Collections.Generic;
using UnityEngine;

namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Turns the per-material half of the PS1 look on and off across the loaded scenes.
    ///
    /// The screen half — CRT, dithering, 240p crunch, fog — is gated inside the render
    /// features by a single check on <see cref="PsxLook"/>. This handles the other half,
    /// which lives in the materials: vertex jitter, affine warping, texture pixelation and
    /// colour depth. The vendored URP-PSX graphs expose those as ordinary per-material
    /// Booleans rather than as shader keywords, so there is no global switch to flip and
    /// no keyword to disable.
    ///
    /// The way out is a MaterialPropertyBlock per renderer. It overrides the material's own
    /// values without touching the material asset, which matters more than it sounds:
    /// writing to a shared material during play mode edits the .mat file on disk, and a
    /// crash mid-session would leave the project's authored look silently zeroed.
    ///
    /// Cost: a renderer carrying a property block drops out of the SRP Batcher. That is a
    /// deliberate trade — it only happens while the filter is OFF, which is a working mode,
    /// not a shipping one, and static batching is unaffected either way.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PsxLookBinder : MonoBehaviour
    {
        /// <summary>Every material Boolean that makes a surface look like PS1 hardware.</summary>
        private static readonly int[] Switches =
        {
            Shader.PropertyToID(PsxShaderProperties.UseVertexJitter),
            Shader.PropertyToID(PsxShaderProperties.UseAffine),
            Shader.PropertyToID(PsxShaderProperties.UsePixelation),
            Shader.PropertyToID(PsxShaderProperties.UseColorPrecision),
        };

        private const string PsxShaderPrefix = "Shader Graphs/URP_PSX";

        private static readonly List<Material> MaterialBuffer = new();

        private void OnEnable()
        {
            PsxLook.Changed += Apply;
            Apply(PsxLook.Enabled);
        }

        private void OnDisable()
        {
            PsxLook.Changed -= Apply;
            // Leave the scene showing what the materials actually say. Anything else would
            // make "the look is off" outlive the component that turned it off.
            Apply(true);
        }

        private static void Apply(bool enabled) => ApplyToLoadedScenes(enabled);

        /// <summary>
        /// Walks every renderer once and adds or removes the override. Public so the editor
        /// menu can drive it outside play mode, which is the whole point of the switch.
        /// </summary>
        public static void ApplyToLoadedScenes(bool enabled)
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var block = new MaterialPropertyBlock();
            int touched = 0;

            foreach (var renderer in renderers)
            {
                if (renderer == null || !UsesPsxShader(renderer))
                    continue;

                if (enabled)
                {
                    // Null clears the block entirely, handing the renderer back to the
                    // material's own values and to the SRP Batcher.
                    renderer.SetPropertyBlock(null);
                }
                else
                {
                    block.Clear();
                    foreach (int id in Switches)
                        block.SetFloat(id, 0f);
                    renderer.SetPropertyBlock(block);
                }

                touched++;
            }

            Debug.Log($"[PSX] Look {(enabled ? "on" : "off")} across {touched} renderers.");
        }

        private static bool UsesPsxShader(Renderer renderer)
        {
            // The non-allocating overload: this runs over every renderer in the scene, and
            // renderer.sharedMaterials would hand back a fresh array for each one.
            renderer.GetSharedMaterials(MaterialBuffer);

            foreach (var material in MaterialBuffer)
            {
                if (material != null && material.shader != null &&
                    material.shader.name.StartsWith(PsxShaderPrefix, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
