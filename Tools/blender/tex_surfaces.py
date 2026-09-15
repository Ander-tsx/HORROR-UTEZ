"""
Surface albedo maps for CECADEC north and its plaza, with grain lifted from the site
photo docs/map/reference/cecadec-north-entrance.png (sunny, so each crop is de-lit and
re-coloured to the overcast tones of the other photos). Boxes are photo pixels.

Arrays are in Blender pixel order (row 0 = bottom = "down" on a wall).
"""

import numpy as np

from texlib import (blur, mix, photo_detail, rgb, spectral_noise, synth_tile, tint)


def _flip(d):
    return d[::-1]


def wall_red(photo, n=1024, metres=4.0, panels=3):
    """Painted precast panels: 3 per 4 m module, joint at each storey line."""
    ppm = n / metres
    src = np.concatenate([
        photo_detail(photo, (600, 400, 728, 650), sigma=30, clip=0.12),
        photo_detail(photo, (462, 450, 570, 700), sigma=30, clip=0.12),
    ], axis=1)
    d = synth_tile(_flip(src), n, scale=4.0, patch=300, seed=1)
    col = tint(d, rgb(150, 52, 42), gain=1.0)
    col[..., 0] *= 1.0 + spectral_noise(n, 2, 2.6) * 0.02     # faint hue drift, not colour noise

    rows = np.arange(n)[:, None].astype(float)
    cols = np.arange(n)[None, :].astype(float)

    # Each panel was cast and painted on its own: a faint tone step between them.
    panel = np.floor(cols / (n / panels)).astype(int) % panels
    offsets = np.array([0.0, 0.035, -0.025])
    col *= (1.0 + offsets[panel])[..., None]
    col *= 1.0 + spectral_noise(n, 3, 3.0)[..., None] * 0.035

    # Runoff below the storey joint above, broken into streaks.
    streak = np.clip(spectral_noise(n, 4, 2.0, aniso=(1.0, 9.0)) * 0.5 + 0.5, 0, 1)
    runoff = np.exp(-(n - rows) / (0.6 * ppm)) * streak
    col = mix(col, rgb(96, 38, 32), runoff * 0.35)

    col += spectral_noise(n, 5, 0.3)[..., None] * 0.018

    # Joints as soft recessed grooves, lit from above.
    def groove(dist, width):
        return np.exp(-(dist / width) ** 2)
    frac = (cols / (n / panels)) % 1.0
    dist_v = np.minimum(frac, 1.0 - frac) * (n / panels)
    dist_h = np.minimum(rows, n - rows)
    col *= (1.0 - 0.38 * groove(dist_h, 0.012 * ppm))[..., None]
    col *= (1.0 - 0.25 * groove(dist_v, 0.008 * ppm))[..., None]
    lip = groove(np.abs(rows - (n - 0.02 * ppm)), 0.006 * ppm)
    col *= (1.0 + 0.06 * lip)[..., None]
    return col


def stucco(photo, n=512):
    """Tirol render: separate knobs of thrown plaster on the pilasters (1 m tile)."""
    fine = spectral_noise(n, 21, 0.0, ring=(90.0, 30.0))
    coarse = spectral_noise(n, 24, 0.0, ring=(40.0, 14.0))
    height = np.clip(fine - 0.7, 0.0, None) ** 1.2 + 0.8 * np.clip(coarse - 1.0, 0.0, None)
    up = np.roll(height, -1, axis=0) - height
    left = np.roll(height, 1, axis=1) - height
    shade = (up * 0.6 + left * 0.4) * 0.3

    macro = synth_tile(_flip(photo_detail(photo, (745, 330, 795, 640), sigma=20)),
                       n, scale=8.0, patch=256, seed=2)
    col = tint(macro, rgb(222, 202, 176), gain=0.8)
    col += shade[..., None]
    return col


def _pebbles(n, seed, size_px):
    """Light river pebbles packed in mortar, as a colour map."""
    ring = n / (size_px * 2.2)
    h = spectral_noise(n, seed, 0.0, ring=(ring, ring * 0.35))
    stone = np.clip((h - 0.15) * 2.0, 0.0, 1.0)
    shade = np.clip(h, -1, 1) * 0.08
    tone = spectral_noise(n, seed + 1, 0.0, ring=(ring, ring * 0.5))
    pebble = mix(np.broadcast_to(rgb(206, 200, 188), (n, n, 3)).copy(), rgb(160, 150, 136),
                 np.clip(tone * 0.4 + 0.3, 0, 1))
    mortar = np.broadcast_to(rgb(92, 90, 86), (n, n, 3))
    return mix(mortar, pebble + shade[..., None], stone)


def aggregate(photo, n=1024, metres=3.0, cell=1.5, joint=0.09):
    """Dark exposed-aggregate plaza, squares of `cell` m framed by pebble joints.

    Axis-aligned here; the plaza mesh rotates its UVs 45 degrees, as on site.
    """
    ppm = n / metres
    src = photo_detail(photo, (0, 980, 600, 1204), stretch_y=2.0, sigma=45, reject_bright=1.22)
    d = synth_tile(_flip(src), n, scale=1.5, patch=320, seed=3)
    col = tint(d, rgb(86, 86, 84), gain=1.3)
    col *= 1.0 + spectral_noise(n, 31, 3.0)[..., None] * 0.05
    specks = np.clip(spectral_noise(n, 32, -0.5) - 2.2, 0, None)
    col += specks[..., None] * 0.12

    rows = np.arange(n)[:, None]; cols = np.arange(n)[None, :]
    period = cell * ppm
    wobble = spectral_noise(n, 33, 2.0) * 0.012 * ppm
    dr = np.abs(((rows + wobble) % period) - period / 2) - (period / 2 - joint * ppm / 2)
    dc = np.abs(((cols + wobble.T) % period) - period / 2) - (period / 2 - joint * ppm / 2)
    in_joint = np.clip(np.maximum(dr, dc) / 2.0 + 0.5, 0, 1)
    return mix(col, _pebbles(n, 34, 0.018 * ppm), in_joint)


def concrete(n, base, seed, mottle=0.05, speck=0.03, stains=0.3):
    """Procedural cast concrete: mottling, speckle, faint broom lines, water stains.

    The walkway crops in the photo are too foreshortened and shadow-streaked to re-tile
    cleanly, so flat concrete is built from noise tuned against the photos instead.
    """
    col = np.broadcast_to(base, (n, n, 3)).copy()
    col *= 1.0 + spectral_noise(n, seed, 3.0)[..., None] * mottle
    col *= 1.0 + spectral_noise(n, seed + 1, 1.2)[..., None] * 0.025
    col *= 1.0 + spectral_noise(n, seed + 2, -0.3)[..., None] * speck
    col *= 1.0 + spectral_noise(n, seed + 3, 0.8, aniso=(1.0, 0.12))[..., None] * 0.015
    st = np.clip(spectral_noise(n, seed + 4, 2.6) - 1.0, 0, None)
    return mix(col, base * 0.72, np.clip(st * stains, 0, 0.5))


def slab(photo, n=1024, metres=4.0, module=2.0):
    """Light broom-finished concrete walkway slabs, a joint every `module` m.

    The joints only stay straight while the mesh UVs are per-metre world coordinates:
    after extruding or moving Ground_Walkway by hand, run Tools/blender/realign_uvs.py.
    """
    ppm = n / metres
    col = concrete(n, rgb(186, 184, 178), 41)

    rows = np.arange(n)[:, None]; cols = np.arange(n)[None, :]
    period = module * ppm
    dist = np.minimum(np.minimum(rows % period, period - rows % period),
                      np.minimum(cols % period, period - cols % period))
    col *= (1.0 - 0.45 * np.exp(-(dist / (0.006 * ppm)) ** 2))[..., None]
    return col


def apron(photo, n=512, metres=2.0):
    """Darker, smoother grey concrete in front of the door."""
    return concrete(n, rgb(126, 126, 123), 51, mottle=0.06, speck=0.025, stains=0.4)


def paint_green(photo, n=1024, metres=3.0):
    """Assembly-point decal: worn green square, white disc, four arrows pointing in."""
    ppm = n / metres
    col = concrete(n, rgb(46, 128, 96), 60, mottle=0.06, speck=0.05, stains=0.2)
    wear = np.clip(spectral_noise(n, 61, 2.4) - 1.3, 0, None)
    col = mix(col, rgb(86, 86, 84), np.clip(wear * 0.9, 0, 0.8))

    y, x = (np.mgrid[0:n, 0:n] + 0.5) / ppm - metres / 2
    white = np.zeros((n, n))
    white = np.maximum(white, (np.hypot(x, y) < 0.24).astype(float))
    for ang in (0.0, 90.0, 180.0, 270.0):
        t = np.radians(ang)
        u = x * np.cos(t) + y * np.sin(t)          # along the arrow, towards the edge
        v = -x * np.sin(t) + y * np.cos(t)
        shaft = (u > 0.62) & (u < 1.15) & (np.abs(v) < 0.06)
        head = (u > 0.36) & (u <= 0.62) & (np.abs(v) < (u - 0.36) * 0.75)
        white = np.maximum(white, (shaft | head).astype(float))
    white *= np.clip(1.0 - np.clip(spectral_noise(n, 62, 1.5) - 1.0, 0, None), 0, 1)
    paint = concrete(n, rgb(226, 226, 220), 63, mottle=0.04, speck=0.08, stains=0.15)
    return mix(col, paint, white)


def paint_blue(photo, n=256):
    col = concrete(n, rgb(52, 112, 214), 70, mottle=0.05, speck=0.04, stains=0.2)
    scuff = np.clip(spectral_noise(n, 71, 2.0) - 1.2, 0, None)
    return mix(col, rgb(70, 80, 96), np.clip(scuff * 0.5, 0, 0.5))


def grass(photo, n=512, metres=2.0):
    """Short, patchy lawn and ground cover for the planters (seen from above)."""
    blades = spectral_noise(n, 81, 0.2)
    clumps = spectral_noise(n, 82, 2.4)
    col = mix(np.broadcast_to(rgb(80, 104, 44), (n, n, 3)).copy(), rgb(112, 130, 60),
              np.clip(clumps * 0.3 + 0.5, 0, 1))
    col = mix(col, rgb(138, 126, 78), np.clip(spectral_noise(n, 83, 2.8) - 1.2, 0, 0.6))
    col *= (1.0 + blades * 0.12)[..., None]
    macro = synth_tile(_flip(photo_detail(photo, (0, 700, 110, 790), sigma=15)), n,
                       scale=2.0, patch=160, seed=8)
    return col * (1.0 + (macro - 1.0) * 0.6)


def soil(n=512):
    """Planter soil under trees: dark earth, leaf litter, sparse ground cover."""
    col = np.broadcast_to(rgb(88, 76, 56), (n, n, 3)).copy()
    col *= 1.0 + spectral_noise(n, 91, 2.0)[..., None] * 0.08
    leaves = np.clip(spectral_noise(n, 92, 0.0, ring=(60, 20)) - 1.1, 0, 1)
    col = mix(col, rgb(122, 96, 58), np.clip(leaves, 0, 0.8))
    cover = np.clip(spectral_noise(n, 93, 2.2) - 0.2, 0, 1)
    col = mix(col, rgb(76, 100, 46), cover * 0.7)
    return col


# ---- Interior (CECADEC ground-floor corridor) ---------------------------------

def tile_floor(photo, n=1024, metres=3.2, cell=0.4, joint=0.012):
    """Interior beige ceramic floor tile, grout every `cell` m.

    Grid baked into the texture, not the mesh: the corridor floor stays a flat,
    unedited plane, so unlike the plaza slab (see `slab()`) this one is safe.
    """
    ppm = n / metres
    col = concrete(n, rgb(196, 190, 178), 141, mottle=0.03, speck=0.02, stains=0.08)
    rows = np.arange(n)[:, None]; cols = np.arange(n)[None, :]
    period = cell * ppm
    dist = np.minimum(np.minimum(rows % period, period - rows % period),
                      np.minimum(cols % period, period - cols % period))
    grout = np.clip(1.0 - dist / (joint * ppm), 0, 1)
    return mix(col, rgb(150, 146, 136), grout * 0.8)


def ceiling_grid(photo, n=1024, metres=3.2, cell_x=0.6, cell_y=1.2, joint=0.02):
    """Suspended acoustic ceiling, 60 x 120 cm tile grid."""
    ppm = n / metres
    col = concrete(n, rgb(224, 222, 216), 151, mottle=0.02, speck=0.01, stains=0.05)
    rows = np.arange(n)[:, None]; cols = np.arange(n)[None, :]
    px, py = cell_x * ppm, cell_y * ppm
    dr = np.minimum(rows % py, py - rows % py)
    dc = np.minimum(cols % px, px - cols % px)
    grid = np.clip(1.0 - np.minimum(dr, dc) / (joint * ppm), 0, 1)
    return mix(col, rgb(150, 150, 146), grid * 0.7)


def wall_white(photo, n=512, metres=2.0):
    """Smooth interior plaster, off-white — corridor walls."""
    return concrete(n, rgb(232, 230, 224), 161, mottle=0.025, speck=0.015, stains=0.05)


def wood_door(photo, n=512, metres=1.0):
    """Varnished pine, vertical grain — Aula doors and the electrical closet louvres."""
    base = concrete(n, rgb(168, 122, 72), 171, mottle=0.10, speck=0.0, stains=0.0)
    grain = spectral_noise(n, 172, 1.4, aniso=(0.15, 1.0))
    base *= 1.0 + grain[..., None] * 0.08
    return base
