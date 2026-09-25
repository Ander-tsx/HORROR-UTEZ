#ifndef HORRORUTEZ_PSX_LIT_INPUT_INCLUDED
#define HORRORUTEZ_PSX_LIT_INPUT_INCLUDED

// Shared by every pass of HorrorUtez/PSX/Lit. The property names are the auto-generated
// references of the vendored URP-PSX graphs (see PsxShaderProperties.cs), kept on purpose:
// swapping a material from the graph to this shader keeps every value it already had.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(Texture2D_4450AB74);      SAMPLER(samplerTexture2D_4450AB74);
TEXTURE2D(_EmissionMap);

CBUFFER_START(UnityPerMaterial)
    float2 Vector2_8044833E;        // Tiling
    half4 _BaseColor;
    half4 _EmissionColor;
    half4 Color_2E5415DE;           // Specular colour, used when IsSpecular is on
    half Boolean_8BBF99CD;          // IsLit
    half Boolean_7165A49C;          // IsSpecular (metal-like reflectance)
    half Vector1_6F288C5B;          // Smoothness
    half _Wettable;
    half Boolean_43476D73;          // UseVertexJitter
    float Vector1_B2CC132F;         // VertexResolution: vertical snap grid, in virtual pixels
    half Boolean_85059BBE;          // UseAffine
    half Vector1_B9994903;          // AffineThreshold: affine blend strength, 0..1
    half Boolean_193028D2;          // UsePixelation
    float Vector1_21C41D02;         // TextureResolution: texels per UV repeat
    half Boolean_3F1A8DAB;          // UseColorPrecision
    half Vector1_E8746023;          // ColorPrecision: levels per channel
    half Boolean_D258FF8E;          // UseCameraClipping (kept for the binder; unused)
    half _Alpha_Clipping;
    half _Alpha_Multiplier;
    half _Surface;
    half _PsxSnapSlide;             // 1 = slide along own plane (solid surfaces), 0 = classic snap (foliage)
CBUFFER_END

// Written by WeatherSystem: 0 = dry, 1 = soaked. Only surfaces marked _Wettable react.
half _PsxWetness;

// Visual-style overrides, written by PsxStyleApplier. All default to 0 = "as the material
// says", so a scene without the applier renders exactly as authored.
float _PsxSnapOverride;       // >0 snap grid rows for everything, <0 snapping off
float _PsxTexelScale;         // >0 multiplies texels per repeat (0.5 = twice as chunky)
float _PsxAlbedoLevels;       // >1 forces albedo colour depth to this many levels, <0 forces it off
float _PsxAffineOverride;     // >0 forces affine on at this strength, <0 forces it off

float PsxSnapRows()
{
    if (_PsxSnapOverride < 0.0)
        return 0.0;
    return _PsxSnapOverride > 0.0 ? _PsxSnapOverride : Vector1_B2CC132F;
}

// ---- Vertex -----------------------------------------------------------------------

/// Snaps a vertex to a coarse screen grid: the PS1 GTE had no subpixel precision, so
/// vertices jumped between whole pixels as the camera moved.
///
/// The vertex is NOT simply moved sideways on screen at constant depth. Doing that pushes
/// it off its own surface, and two nearly coplanar surfaces with different triangulations
/// (a painted marking on a slab, a pad on the plaza) get pushed off by different amounts
/// and trade places in the depth buffer — the triangles of one poke through the other
/// whenever the camera moves. Instead the vertex slides ALONG its surface plane (defined
/// by its normal) to wherever that plane meets the ray through the snapped pixel. It still
/// lands exactly on the grid, but coplanar geometry stays coplanar and keeps its order.
float4 PsxSnapVertex(float3 positionWS, float3 normalWS)
{
    float3 positionVS = TransformWorldToView(positionWS);
    float4 positionCS = TransformWViewToHClip(positionVS);

    float rows = PsxSnapRows();
    if (Boolean_43476D73 < 0.5 || rows <= 0.0 || positionCS.w <= 0.0 ||
        unity_OrthoParams.w > 0.5)
        return positionCS;

    float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
    float2 grid = float2(rows * aspect, rows) * 0.5;
    float2 ndc = positionCS.xy / positionCS.w;
    float2 snapped = floor(ndc * grid + 0.5) / grid;

    // Foliage: crossing cards with averaged normals have no single plane to slide on;
    // sliding pushed shared corners off their faces and the cards shimmered through each
    // other. Leaves never lie flat on anything, so the classic snap is safe there.
    if (_PsxSnapSlide < 0.5)
        return float4(snapped * positionCS.w, positionCS.zw);

    // View-space ray through the snapped pixel. Inverts clip.xy = P00*x + P02*z (and the
    // same for y) with clip.w = -z, so it holds for off-centre and oblique projections too.
    float4x4 proj = GetViewToHClipMatrix();
    float3 rayVS = float3((snapped.x + proj[0][2]) / proj[0][0],
                          (snapped.y + proj[1][2]) / proj[1][1],
                          -1.0);

    float3 normalVS = normalize(TransformWorldToViewDir(normalWS));
    float denom = dot(rayVS, normalVS);
    float t = dot(positionVS, normalVS) / (abs(denom) > 1e-5 ? denom : 1e-5);
    float3 slidVS = rayVS * t;

    // A surface seen edge-on meets the ray far away; there the slide would fling the
    // vertex, so it fades back to the unsnapped position. A fade, not a switch: a switch
    // made vertices pop between the two as the camera turned past the threshold.
    float facing = abs(denom) / length(rayVS);
    float weight = smoothstep(0.05, 0.15, facing) * step(0.0, t) *
                   step(distance(slidVS, positionVS), 0.25 * length(positionVS));

    return TransformWViewToHClip(lerp(positionVS, slidVS, weight));
}

float2 PsxTiledUv(float2 uv)
{
    return uv * Vector2_8044833E;
}

/// Affine UV: carry uv * w and w through perspective-correct interpolation and divide
/// per pixel. The w terms cancel into a screen-linear UV, which is what the PS1 did.
/// Works on GLES3, where the `noperspective` qualifier does not exist.
float3 PsxAffinePack(float2 uv, float4 positionCS)
{
    return float3(uv * positionCS.w, positionCS.w);
}

float2 PsxResolveUv(float2 perspectiveUv, float3 affinePacked)
{
    float strength = Boolean_85059BBE > 0.5 ? Vector1_B9994903 : 0.0;
    if (_PsxAffineOverride != 0.0)
        strength = max(_PsxAffineOverride, 0.0);
    if (strength <= 0.0)
        return perspectiveUv;

    float2 affineUv = affinePacked.xy / max(affinePacked.z, 1e-4);
    return lerp(perspectiveUv, affineUv, saturate(strength));
}

// ---- Surface ----------------------------------------------------------------------

/// Albedo + alpha, point-snapped to a virtual texel grid when pixelation is on.
/// Derivatives come from the continuous UV so mip selection still works and distant
/// surfaces average instead of sparkling.
half4 PsxSampleAlbedo(float2 uv)
{
    half4 texel;
    float texels = Vector1_21C41D02 * (_PsxTexelScale > 0.0 ? _PsxTexelScale : 1.0);
    if (Boolean_193028D2 > 0.5 && texels > 0.0)
    {
        float2 dx = ddx(uv);
        float2 dy = ddy(uv);
        float2 snapped = (floor(uv * texels) + 0.5) / texels;
        texel = SAMPLE_TEXTURE2D_GRAD(Texture2D_4450AB74, sampler_PointRepeat, snapped, dx, dy);
    }
    else
    {
        texel = SAMPLE_TEXTURE2D(Texture2D_4450AB74, samplerTexture2D_4450AB74, uv);
    }

    texel *= _BaseColor;

    half levelsPerChannel = Boolean_3F1A8DAB > 0.5 ? Vector1_E8746023 : 0.0;
    if (_PsxAlbedoLevels != 0.0)
        levelsPerChannel = _PsxAlbedoLevels;
    if (levelsPerChannel > 1.0)
    {
        // round, not floor: floor biases every surface dark by half a step.
        half levels = levelsPerChannel - 1.0;
        texel.rgb = round(texel.rgb * levels) / levels;
    }

    texel.a *= _Alpha_Multiplier;
    return texel;
}

/// Rain darkens porous surfaces and turns them into mirrors. Only faces pointing at the
/// sky get wet, so walls stay dry while the pavement in front of them shines.
void PsxApplyWetness(float3 normalWS, inout half3 albedo, inout half smoothness)
{
    half wet = _PsxWetness * _Wettable * saturate(normalWS.y * 2.0 - 1.0);
    albedo *= lerp(1.0, 0.55, wet);
    smoothness = lerp(smoothness, 0.88, wet);
}

void PsxAlphaClip(half alpha)
{
    #if defined(_ALPHATEST_ON)
    clip(alpha - _Alpha_Clipping);
    #endif
}

#endif
