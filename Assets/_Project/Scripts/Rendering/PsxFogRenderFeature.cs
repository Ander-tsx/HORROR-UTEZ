using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Screen-space PSX fog, written against URP 17's Render Graph API.
    ///
    /// This is a rewrite, not a port. The original URP-PSX render features target an API
    /// that URP 17 REMOVED — no ScriptableRenderPass.Execute, no cameraColorTargetHandle
    /// — so "compatibility mode" was never an option. See docs/technical/psx-style-guide.md.
    /// </summary>
    [DisallowMultipleRendererFeature("PSX Fog")]
    public sealed class PsxFogRenderFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "HorrorUtez/PSX/Fog";

        [SerializeField, HideInInspector] private Shader shader;

        private Material _material;
        private PsxFogPass _pass;

        public override void Create()
        {
            if (shader == null)
                shader = Shader.Find(ShaderName);

            if (shader == null)
            {
                Debug.LogError($"[PSX] Shader '{ShaderName}' not found; PSX fog disabled.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new PsxFogPass(_material)
            {
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

            // Without this the depth texture is not guaranteed to exist when we sample it.
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private sealed class PsxFogPass : ScriptableRenderPass
        {
            private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
            private static readonly int AmbientColorId = Shader.PropertyToID("_AmbientColor");
            private static readonly int FogDensityId = Shader.PropertyToID("_FogDensity");
            private static readonly int FogStartId = Shader.PropertyToID("_FogStart");
            private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
            private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
            private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
            private static readonly int SceneDepthId = Shader.PropertyToID("_PsxSceneDepth");

            private readonly Material _material;

            private class PassData
            {
                public Material Material;
                public TextureHandle Source;
                public TextureHandle Depth;
            }

            public PsxFogPass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("PSX Fog");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                // Rendering straight to the backbuffer leaves us nothing to ping-pong
                // through, and reading the texture we are writing is undefined.
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                var volume = VolumeManager.instance.stack?.GetComponent<PsxFogVolume>();
                if (volume == null || !volume.IsActive())
                    return;

                _material.SetColor(FogColorId, volume.fogColor.value);
                _material.SetColor(AmbientColorId, volume.ambientColor.value);
                _material.SetFloat(FogDensityId, volume.density.value);
                _material.SetFloat(FogStartId, volume.startDistance.value);
                _material.SetFloat(NoiseScaleId, volume.noiseScale.value);
                _material.SetFloat(NoiseStrengthId, volume.noiseStrength.value);
                _material.SetFloat(NoiseSpeedId, volume.noiseSpeed.value);

                var source = resourceData.activeColorTexture;
                var depth = resourceData.cameraDepthTexture;
                if (!depth.IsValid())
                    return;

                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_PsxFog";
                desc.clearBuffer = false;
                var destination = renderGraph.CreateTexture(desc);

                // A plain AddBlitPass cannot declare the depth read, and the global
                // _CameraDepthTexture is not bound for this pass under Render Graph — every
                // pixel came back as the far plane. Bind depth explicitly instead.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           "PSX Fog", out var passData, profilingSampler))
                {
                    passData.Material = _material;
                    passData.Source = source;
                    passData.Depth = depth;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(depth, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        data.Material.SetTexture(SceneDepthId, data.Depth);
                        Blitter.BlitTexture(context.cmd, data.Source,
                            new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                    });
                }

                // Ping-pong rather than copying back: one fullscreen pass instead of two,
                // which matters on the mobile target.
                resourceData.cameraColor = destination;
            }
        }
    }
}
