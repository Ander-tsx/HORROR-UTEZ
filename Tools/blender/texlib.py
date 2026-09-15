"""
Shared helpers for the texture generators (run inside Blender, which ships numpy).

Two sources of detail:
  - spectral_noise: periodic procedural noise (tiles by construction);
  - photo_detail + synth_tile: real grain lifted from a site photo, de-lit and re-tiled
    onto a torus by splatting random patches, so the map is seamless.
"""

import os

import bpy
import numpy as np


def rgb(r, g, b):
    return np.array([r, g, b], dtype=np.float64) / 255.0


def mix(col, target, amount):
    return col * (1.0 - amount[..., None]) + target * amount[..., None]


def spectral_noise(n, seed, power, aniso=(1.0, 1.0), ring=None, m=None):
    """Zero-mean, unit-std periodic noise (n x m) with a 1/k^power spectrum."""
    m = m or n
    rng = np.random.default_rng(seed)
    spectrum = np.fft.fft2(rng.standard_normal((n, m)))
    ky = np.fft.fftfreq(n)[:, None] * n * aniso[1]
    kx = np.fft.fftfreq(m)[None, :] * m * aniso[0]
    k = np.sqrt(kx * kx + ky * ky)
    k[0, 0] = 1.0
    amp = k ** (-power / 2.0)
    if ring is not None:
        centre, width = ring
        amp = amp * np.exp(-(((k - centre) / width) ** 2))
    amp[0, 0] = 0.0
    field = np.real(np.fft.ifft2(spectrum * amp))
    field -= field.mean()
    return field / (field.std() + 1e-8)


def load_photo(path):
    """Photo as a top-down float RGB array (row 0 = top of the picture)."""
    img = bpy.data.images.load(path, check_existing=True)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    return px.reshape(h, w, 4)[::-1, :, :3].astype(np.float64)


def resize(a, out_h, out_w):
    """Bilinear resize of an (h, w, c) array."""
    h, w = a.shape[:2]
    ys = np.clip((np.arange(out_h) + 0.5) * h / out_h - 0.5, 0, h - 1)
    xs = np.clip((np.arange(out_w) + 0.5) * w / out_w - 0.5, 0, w - 1)
    y0 = np.floor(ys).astype(int); x0 = np.floor(xs).astype(int)
    y1 = np.minimum(y0 + 1, h - 1); x1 = np.minimum(x0 + 1, w - 1)
    fy = (ys - y0)[:, None, None]; fx = (xs - x0)[None, :, None]
    top = a[y0][:, x0] * (1 - fx) + a[y0][:, x1] * fx
    bot = a[y1][:, x0] * (1 - fx) + a[y1][:, x1] * fx
    return top * (1 - fy) + bot * fy


def blur(a, sigma):
    """Gaussian blur with reflect padding (no wrap-around bleed)."""
    pad = int(3 * sigma) + 1
    p = np.pad(a, ((pad, pad), (pad, pad), (0, 0)), mode="reflect")
    h, w = p.shape[:2]
    ky = np.fft.fftfreq(h)[:, None]; kx = np.fft.fftfreq(w)[None, :]
    g = np.exp(-2 * (np.pi ** 2) * (sigma ** 2) * (kx * kx + ky * ky))
    out = np.real(np.fft.ifft2(np.fft.fft2(p, axes=(0, 1)) * g[..., None], axes=(0, 1)))
    return out[pad:-pad, pad:-pad]


def photo_detail(photo, box, stretch_y=1.0, sigma=None, reject_bright=None, clip=0.3, mono=True):
    """Multiplicative detail (mean 1) of a photo region, lighting gradients removed.

    box = (x0, y0, x1, y1) in photo pixels. stretch_y undoes ground foreshortening.
    reject_bright: luminance ratio above which pixels (pebbles, paint) are in-painted
    from the surrounding average, so only the base material's grain survives.
    mono keeps luminance only (the target colour is set by tint); clip bounds outliers
    such as a stray leaf or shadow edge in the crop.
    """
    x0, y0, x1, y1 = box
    a = photo[y0:y1, x0:x1]
    if stretch_y != 1.0:
        a = resize(a, int(a.shape[0] * stretch_y), a.shape[1])
    sigma = sigma or max(a.shape[:2]) / 6.0
    base = blur(a, sigma)
    d = a / np.maximum(base, 1e-3)
    if reject_bright is not None:
        lum = d.mean(axis=2)
        bad = lum > reject_bright
        smooth = blur(np.where(bad[..., None], 1.0, d), 3.0)
        d = np.where(bad[..., None], smooth, d)
    d = d / d.reshape(-1, 3).mean(axis=0)
    if mono:
        d = np.repeat(d.mean(axis=2, keepdims=True), 3, axis=2)
    return np.clip(d, 1.0 - clip, 1.0 + clip)


def synth_tile(detail, n, scale=1.0, patch=None, count=None, seed=0, m=None):
    """Seamless n x m tile from `detail` by splatting windowed patches on a torus.

    scale resamples the source first (output px per source px). Patches get random
    90-degree turns and flips; the blend's lost contrast is restored afterwards.
    """
    m = m or n
    rng = np.random.default_rng(seed)
    if scale != 1.0:
        detail = resize(detail, max(8, int(detail.shape[0] * scale)),
                        max(8, int(detail.shape[1] * scale)))
    sh, sw = detail.shape[:2]
    p = patch or max(16, min(sh, sw, n, m) // 2)
    p = min(p, sh, sw)
    # A jittered grid at half-patch stride guarantees every texel is covered; extra
    # random splats break up the grid rhythm.
    stride = max(4, p // 2)
    grid = [(gy + rng.integers(-stride // 2, stride // 2 + 1), gx + rng.integers(-stride // 2, stride // 2 + 1))
            for gy in range(0, n, stride) for gx in range(0, m, stride)]
    extra = [(rng.integers(0, n), rng.integers(0, m)) for _ in range(count or len(grid) // 2)]
    win = np.outer(np.hanning(p), np.hanning(p))[..., None] + 1e-4
    acc = np.zeros((n, m, 3)); wsum = np.zeros((n, m, 1))
    for ty, tx in grid + extra:
        sy = rng.integers(0, sh - p + 1); sx = rng.integers(0, sw - p + 1)
        piece = detail[sy:sy + p, sx:sx + p]
        piece = np.rot90(piece, rng.integers(0, 4))
        if rng.random() < 0.5:
            piece = piece[:, ::-1]
        ty -= p // 2; tx -= p // 2
        rows = (np.arange(p) + ty) % n; cols = (np.arange(p) + tx) % m
        acc[np.ix_(rows, cols)] += piece * win
        wsum[np.ix_(rows, cols)] += win
    out = acc / np.maximum(wsum, 1e-6)
    src_std = detail.reshape(-1, 3).std(axis=0)
    out_mean = out.reshape(-1, 3).mean(axis=0)
    out_std = out.reshape(-1, 3).std(axis=0) + 1e-6
    out = (out - out_mean) * (src_std / out_std) + 1.0
    return out


def tint(detail, colour, gain=1.0):
    """Colour a mean-1 detail map: gain > 1 exaggerates the photo's contrast."""
    return colour * (1.0 + (detail - 1.0) * gain)


def save_png(name, colour, out_dir, alpha=None):
    h, w = colour.shape[:2]
    a = np.ones((h, w, 1)) if alpha is None else alpha[..., None]
    rgba = np.concatenate([np.clip(colour, 0.0, 1.0), np.clip(a, 0.0, 1.0)], axis=2)
    img = bpy.data.images.get(name)
    if img is not None and tuple(img.size) != (w, h):
        bpy.data.images.remove(img)
        img = None
    if img is None:
        img = bpy.data.images.new(name, width=w, height=h, alpha=alpha is not None)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(out_dir, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img.filepath_raw
