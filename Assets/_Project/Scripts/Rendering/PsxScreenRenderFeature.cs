using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Pixelation, colour crunch with ordered dithering, and the CRT tube pass — all in
    /// one full-screen blit. Runs after <see cref="PsxFogRenderFeature"/> so the fog gets
    /// crushed and scanned along with everything else, rather than sitting on top clean.
    ///
    /// Needs no depth, so unlike the fog pass this can use the plain blit helper.
    /// </summary>
    [DisallowMultipleRendererFeature("PSX Screen")]
    public sealed class PsxScreenRenderFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "HorrorUtez/PSX/Screen";

        [SerializeField, HideInInspector] private Shader shader;

        private Material _material;
        private PsxScreenPass _pass;

        public override void Create()
        {
            if (shader == null)
                shader = Shader.Find(ShaderName);

            if (shader == null)
            {
                Debug.LogError($"[PSX] Shader '{ShaderName}' not found; PSX screen effects disabled.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new PsxScreenPass(_material)
            {
                // Not AfterRenderingPostProcessing: by then URP may already be drawing to
                // the backbuffer, which leaves nothing to ping-pong through.
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null)
                return;

            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private sealed class PsxScreenPass : ScriptableRenderPass
        {
            private static readonly int PixelHeightId = Shader.PropertyToID("_PixelHeight");
            private static readonly int ColorLevelsId = Shader.PropertyToID("_ColorLevels");
            private static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");
            private static readonly int WarpId = Shader.PropertyToID("_Warp");
            private static readonly int AberrationId = Shader.PropertyToID("_Aberration");
            private static readonly int ScanlineStrengthId = Shader.PropertyToID("_ScanlineStrength");
            private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");
            private static readonly int GrainStrengthId = Shader.PropertyToID("_GrainStrength");

            private readonly Material _material;

            public PsxScreenPass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("PSX Screen");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                // One switch for the whole look, so the editor can be worked in
                // without the filter in the way. Off by default; see PsxLook.
                if (!PsxLook.Enabled)
                    return;

                var volume = VolumeManager.instance.stack?.GetComponent<PsxScreenVolume>();
                if (volume == null || !volume.IsActive())
                    return;

                _material.SetFloat(PixelHeightId, volume.pixelHeight.value);
                _material.SetFloat(ColorLevelsId, volume.colorLevels.value);
                _material.SetFloat(DitherStrengthId, volume.ditherStrength.value);
                _material.SetFloat(WarpId, volume.warp.value);
                _material.SetFloat(AberrationId, volume.aberration.value);
                _material.SetFloat(ScanlineStrengthId, volume.scanlineStrength.value);
                _material.SetFloat(VignetteStrengthId, volume.vignetteStrength.value);
                _material.SetFloat(GrainStrengthId, volume.grainStrength.value);

                var source = resourceData.activeColorTexture;

                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_PsxScreen";
                desc.clearBuffer = false;
                var destination = renderGraph.CreateTexture(desc);

                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
                renderGraph.AddBlitPass(parameters, "PSX Screen");

                resourceData.cameraColor = destination;
            }
        }
    }
}
