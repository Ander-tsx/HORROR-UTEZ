"""
Albedo maps for the CECADEC ground-floor interior. Grain comes from the interior photos
(docs/map/reference/cecadec-interior-pasillo/, numbered as they arrived): the corridor's
white "tirol rayado" plaster from photo 7, the fissured ceiling tile from photo 18.
Colours are the photos' ratios to that white plaster (the camera under-exposes: the
plaster reads ~154/255 on screen, ~230 in reality), not the raw screen values.

The photos were delivered as 256-colour palette PNGs, so every crop is blurred a little
before its grain is lifted, or the palette dither turns into speckle.

Arrays are in Blender pixel order (row 0 = bottom). Tile sizes: see MAPS in
gen_interior_textures.py.
"""

import numpy as np

from texlib import blur, mix, photo_detail, rgb, spectral_noise, synth_tile, tint


def _undither(photo, box, sigma=0.9):
    x0, y0, x1, y1 = box
    a = blur(photo[y0:y1, x0:x1], sigma)
    return a


def plaster(photo7, n=1024):
    """White tirol rayado wall plaster, 2 m tile, grain from the big bare wall in photo 7
    (~405 photo px per metre there, 512 in the map)."""
    src = _undither(photo7, (60, 440, 1440, 1310))
    d = photo_detail(src, (0, 0, src.shape[1], src.shape[0]), sigma=40, clip=0.22)
    d = synth_tile(d[::-1], n, scale=1.26, patch=300, seed=201)
    return tint(d, rgb(232, 231, 227), gain=1.15)


def _tile_field(n, metres, cell, base, grout, seed, variation, offset_x=0.5, grout_w=0.005):
    """Ceramic floor tile: per-tile tone jitter, fine speckle, recessed grout.

    offset_x shifts the grid by that fraction of a tile along U, so a wall standing on a
    half-tile line (the corridor walls sit at x = +-2.2 m on a 0.4 m grid) meets whole
    grout lines instead of slivers.
    """
    ppm = n / metres
    rows = (np.arange(n)[:, None] + 0.5) / ppm
    cols = (np.arange(n)[None, :] + 0.5) / ppm + offset_x * cell
    ti = np.floor(rows / cell).astype(int)
    tj = np.floor(cols / cell).astype(int) % int(round(metres / cell))
    rng = np.random.default_rng(seed)
    count = int(round(metres / cell))
    jitter = 1.0 + rng.uniform(-variation, variation, (count, count))
    col = np.broadcast_to(base, (n, n, 3)).copy() * jitter[ti % count, tj][..., None]
    col *= 1.0 + spectral_noise(n, seed + 1, 1.0)[..., None] * 0.025
    col *= 1.0 + spectral_noise(n, seed + 2, -0.6)[..., None] * 0.02
    fr = rows % cell; fc = cols % cell
    dist = np.minimum(np.minimum(fr, cell - fr), np.minimum(fc, cell - fc))
    g = np.clip(1.0 - dist / grout_w, 0, 1)
    edge = np.clip(1.0 - dist / (grout_w * 2.5), 0, 1) * 0.08   # bevel shading at each edge
    col *= (1.0 - edge)[..., None]
    return mix(col, grout, g * 0.9)


def tile_floor(n=1024, metres=3.2):
    """Light beige 40 cm ceramic field tile (photos 14, 23)."""
    return _tile_field(n, metres, 0.4, rgb(216, 206, 188), rgb(160, 152, 138), 301, 0.035)


def tile_border(n=1024, metres=3.2):
    """Darker beige-brown tile of the 2-tile band along every corridor wall."""
    return _tile_field(n, metres, 0.4, rgb(196, 176, 150), rgb(146, 134, 116), 311, 0.05)


def ceiling(photo18, n=1024, metres=2.44, cell=0.61, bar=0.024):
    """61 cm lay-in acoustic tile (fissured, photo 18) in its white T-bar grid.

    Grid lines at half-tile offsets, so fixtures centred on the corridor axis sit in a
    whole tile (fixture centres on multiples of 0.61 m in world X/Y).
    """
    ppm = n / metres
    src = _undither(photo18, (820, 640, 1140, 860), sigma=0.7)
    d = photo_detail(src, (0, 0, src.shape[1], src.shape[0]), sigma=30, clip=0.18)
    d = synth_tile(d[::-1], n, scale=1.3, patch=200, seed=401)
    col = tint(d, rgb(236, 235, 231), gain=1.1)
    rows = (np.arange(n)[:, None] + 0.5) / ppm + cell / 2
    cols = (np.arange(n)[None, :] + 0.5) / ppm + cell / 2
    fr = rows % cell; fc = cols % cell
    dist = np.minimum(np.minimum(fr, cell - fr), np.minimum(fc, cell - fc))
    t_bar = np.clip(1.0 - (dist - bar / 2) / 0.002, 0, 1)
    shadow = np.clip(1.0 - (dist - bar / 2) / 0.012, 0, 1) * 0.12
    col *= (1.0 - shadow)[..., None]
    return mix(col, rgb(226, 225, 221), t_bar)


def wood_louvre(n=512):
    """Varnished pine of the electrical closet's louvre doors (photo 2), grain along U."""
    base = np.broadcast_to(rgb(198, 146, 86), (n, n, 3)).copy()
    grain = spectral_noise(n, 501, 1.6, aniso=(1.0, 0.08))
    streak = spectral_noise(n, 502, 0.6, aniso=(1.0, 0.05))
    base *= 1.0 + grain[..., None] * 0.07 + streak[..., None] * 0.03
    knots = np.clip(spectral_noise(n, 503, 2.8) - 2.2, 0, None)
    return mix(base, rgb(140, 92, 48), np.clip(knots, 0, 0.6))


def wood_header(n=512):
    """Plywood header over the closet (photo 2): darker, cathedral figure, sun-faded right."""
    base = np.broadcast_to(rgb(166, 118, 72), (n, n, 3)).copy()
    figure = spectral_noise(n, 511, 2.2, aniso=(1.0, 0.25))
    base *= 1.0 + figure[..., None] * 0.10
    fade = np.broadcast_to(np.linspace(0.0, 1.0, n)[None, :] ** 2, (n, n))
    return mix(base, rgb(150, 124, 98), fade * 0.35)


def vinyl_blue(n=256):
    """Waiting-bench cushions: royal blue vinyl, with the worn, paler creases of photo 6."""
    col = np.broadcast_to(rgb(34, 72, 164), (n, n, 3)).copy()
    col *= 1.0 + spectral_noise(n, 521, 2.0)[..., None] * 0.05
    wear = np.clip(spectral_noise(n, 522, 1.8, aniso=(1.0, 0.3)) - 1.6, 0, None)
    return mix(col, rgb(120, 130, 150), np.clip(wear, 0, 0.5))


def perforated(n=512, metres=0.25, pitch=0.006, hole=0.0022):
    """Bench seat and back pans: satin steel sheet, round holes on a staggered grid."""
    ppm = n / metres
    y, x = (np.mgrid[0:n, 0:n] + 0.5) / ppm
    row = np.floor(y / pitch)
    xs = (x + (row % 2) * pitch / 2) % pitch - pitch / 2
    ys = y % pitch - pitch / 2
    holes = np.hypot(xs, ys) < hole / 2
    col = np.broadcast_to(rgb(186, 188, 190), (n, n, 3)).copy()
    col *= 1.0 + spectral_noise(n, 531, 0.8, aniso=(1.0, 0.05))[..., None] * 0.04
    return np.where(holes[..., None], rgb(52, 54, 58), col)


def panel_grey(n=256):
    """Light grey laminate of the blind aluminium doors and door-head panels (photos 7, 13)."""
    col = np.broadcast_to(rgb(200, 201, 197), (n, n, 3)).copy()
    col *= 1.0 + spectral_noise(n, 541, 2.4)[..., None] * 0.02
    return col


def metal_grey(n=128):
    """Grey enamelled steel: the security counter, the locker (photos 10, 11)."""
    col = np.broadcast_to(rgb(168, 166, 160), (n, n, 3)).copy()
    col *= 1.0 + spectral_noise(n, 551, 2.0)[..., None] * 0.03
    return col


def chrome(n=128):
    """Chromed tube and parabolic louvre blades: bright with streaks (no reflection probe)."""
    col = np.broadcast_to(rgb(206, 208, 212), (n, n, 3)).copy()
    streak = spectral_noise(n, 561, 0.5, aniso=(0.05, 1.0))
    return col * (1.0 + streak[..., None] * 0.12)


def plastic_grey(n=128):
    """Grey recycling bins (photo 8), scuffed."""
    col = np.broadcast_to(rgb(112, 114, 114), (n, n, 3)).copy()
    scuff = np.clip(spectral_noise(n, 571, 1.4) - 1.2, 0, None)
    return mix(col, rgb(80, 80, 80), np.clip(scuff, 0, 0.5))


def nosing(n=256):
    """Grey terrazzo nosing strips on the stair treads (photo 10)."""
    col = np.broadcast_to(rgb(118, 114, 108), (n, n, 3)).copy()
    chips = np.clip(spectral_noise(n, 581, -0.5) - 1.5, 0, None)
    return mix(col, rgb(190, 186, 176), np.clip(chips, 0, 0.7))


def glass_frosted(n=64):
    return np.broadcast_to(rgb(214, 218, 219), (n, n, 3)).copy() * (
        1.0 + spectral_noise(n, 591, 2.0)[..., None] * 0.015)


def glass_tinted(n=64):
    """The smoked-glass front of the north-west room (photos 1, 4)."""
    return np.broadcast_to(rgb(46, 48, 50), (n, n, 3)).copy()


def light_panel(n=64):
    """Emissive back plate of the parabolic troffers: tired fluorescent tubes, a little
    green, not office white."""
    return np.broadcast_to(rgb(226, 238, 206), (n, n, 3)).copy()
