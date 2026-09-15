"""
Procedural albedo maps for the plaza's props and planting: volcanic stone masonry,
bark, the foliage atlas (RGBA, alpha-tested), the emergency generator and flat paints.

Arrays are in Blender pixel order (row 0 = bottom).
"""

import numpy as np

from texlib import mix, rgb, spectral_noise


def stone_wall(w=1024, h=512, ppm=512.0, seed=0):
    """Dark volcanic stones in light mortar, like the low planter walls (2 x 1 m)."""
    rng = np.random.default_rng(seed)
    size = 0.22 * ppm
    gx, gy = int(w / size), int(h / size)
    pts = np.stack(np.meshgrid(np.arange(gx) + 0.5, np.arange(gy) + 0.5), -1).reshape(-1, 2)
    pts = (pts + rng.uniform(-0.38, 0.38, pts.shape)) * [w / gx, h / gy]

    yy, xx = np.mgrid[0:h, 0:w].astype(np.float64)
    # Domain warp: rounds the Voronoi cells into irregular, pillowy stones.
    xx = (xx + spectral_noise(h, seed + 7, 2.8, m=w) * 0.012 * ppm) % w
    yy = (yy + spectral_noise(h, seed + 8, 2.8, m=w) * 0.012 * ppm) % h
    d1 = np.full((h, w), 1e9); d2 = np.full((h, w), 1e9); idx = np.zeros((h, w), int)
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xx - px); dx = np.minimum(dx, w - dx)
        dy = np.abs(yy - py); dy = np.minimum(dy, h - dy)
        d = np.hypot(dx, dy * 1.15)
        closer = d < d1
        d2 = np.where(closer, d1, np.minimum(d2, d))
        idx = np.where(closer, i, idx)
        d1 = np.where(closer, d, d1)

    edge = (d2 - d1) / 2.0
    mortar_w = 0.012 * ppm * (1.0 + 0.3 * spectral_noise(h, seed + 9, 2.5, m=w))
    stone = np.clip((edge - mortar_w) / 2.0, 0.0, 1.0)
    dome = np.clip(edge / (0.07 * ppm), 0.0, 1.0) ** 0.5

    palette = np.array([rgb(58, 56, 54), rgb(72, 66, 60), rgb(46, 46, 46),
                        rgb(84, 76, 66), rgb(64, 60, 58)])
    base = palette[rng.integers(0, len(palette), len(pts))][idx]
    pores = np.clip(spectral_noise(h, seed + 1, 0.0, ring=(w / 12, w / 30), m=w) - 1.4, 0, None)
    grain = spectral_noise(h, seed + 2, 0.8, m=w)
    lit = np.roll(dome, -2, axis=0) - dome
    stone_col = base * (0.75 + 0.35 * dome + 0.08 * grain)[..., None]
    stone_col -= (pores * 0.06 - lit * 0.6)[..., None]

    mortar = np.broadcast_to(rgb(140, 136, 128), (h, w, 3)).copy()
    mortar *= 1.0 + spectral_noise(h, seed + 3, 1.2, m=w)[..., None] * 0.06
    return mix(mortar, stone_col, stone)


def rock(n=512):
    """Weathered volcanic boulders on the garden slope (2 m tile)."""
    col = mix(np.broadcast_to(rgb(92, 88, 82), (n, n, 3)).copy(), rgb(130, 124, 114),
              np.clip(spectral_noise(n, 131, 2.2) * 0.35 + 0.5, 0, 1))
    cracks = np.exp(-(spectral_noise(n, 132, 1.4) / 0.08) ** 2)
    col *= (1.0 - 0.35 * cracks)[..., None]
    col *= 1.0 + spectral_noise(n, 134, 0.5)[..., None] * 0.05
    return mix(col, rgb(84, 98, 60), np.clip(spectral_noise(n, 133, 2.6) - 1.0, 0, 0.5))


def bark(w=256, h=512):
    """Grey-brown tropical bark with vertical fissures (1 x 2 m)."""
    fibres = spectral_noise(h, 101, 1.2, aniso=(1.0, 0.12), m=w)
    col = mix(np.broadcast_to(rgb(92, 78, 64), (h, w, 3)).copy(), rgb(128, 116, 100),
              np.clip(fibres * 0.35 + 0.5, 0, 1))
    fissure = np.clip(-fibres - 1.0, 0, 1)
    col *= (1.0 - fissure * 0.5)[..., None]
    lichen = np.clip(spectral_noise(h, 102, 2.5, m=w) - 1.1, 0, 1)
    return mix(col, rgb(150, 150, 132), lichen * 0.6)


def _leaves(rng, size, count, radius, length, width, greens, dark_below=0.35, blobs=None):
    """Scatter elliptical leaves in a round cluster, or around several sub-clusters
    (blobs = [(x, y, spread)]) for a ragged crown edge. Returns colour and alpha."""
    col = np.zeros((size, size, 3)); alpha = np.zeros((size, size))
    c = size / 2.0
    for _ in range(count):
        if blobs:
            bx, by, br = blobs[rng.integers(0, len(blobs))]
            cx, cy = bx + rng.normal(0.0, br), by + rng.normal(0.0, br)
        else:
            r = radius * np.sqrt(rng.random()); t = rng.random() * 2 * np.pi
            cx, cy = c + r * np.cos(t), c + r * np.sin(t)
        L = rng.uniform(*length); W = rng.uniform(*width); ang = rng.random() * np.pi
        ext = int(L) + 2
        x0, x1 = int(max(cx - ext, 0)), int(min(cx + ext, size))
        y0, y1 = int(max(cy - ext, 0)), int(min(cy + ext, size))
        if x1 <= x0 or y1 <= y0:
            continue
        yy, xx = np.mgrid[y0:y1, x0:x1] + 0.5
        u = (xx - cx) * np.cos(ang) + (yy - cy) * np.sin(ang)
        v = -(xx - cx) * np.sin(ang) + (yy - cy) * np.cos(ang)
        inside = (u / L) ** 2 + (v / W) ** 2 < 1.0
        g = greens[rng.integers(0, len(greens))] * rng.uniform(0.85, 1.12)
        shade = 1.0 - dark_below * (1.0 - cy / size)       # underside of a crown is darker
        vein = 1.0 - 0.18 * np.exp(-(v / (W * 0.18)) ** 2)
        leaf = g * (shade * vein)[..., None] * (0.9 + 0.2 * (u / L + 1) / 2)[..., None]
        col[y0:y1, x0:x1][inside] = leaf[inside]
        alpha[y0:y1, x0:x1][inside] = 1.0
    return col, alpha


def foliage_atlas(n=1024, seed=0):
    """Four 512 px cells: tree crown clump, shrub clump, blade tuft, broad taro leaf.

    UV cells: (0,0) crown, (0.5,0) shrub, (0,0.5) tuft, (0.5,0.5) taro.
    """
    rng = np.random.default_rng(seed)
    q = n // 2
    col = np.zeros((n, n, 3)); alpha = np.zeros((n, n))

    crown_greens = [rgb(52, 80, 36), rgb(70, 98, 42), rgb(88, 112, 50), rgb(42, 66, 32)]
    blobs = [(q / 2 + rng.uniform(-0.28, 0.28) * q, q / 2 + rng.uniform(-0.22, 0.3) * q,
              rng.uniform(0.06, 0.12) * q) for _ in range(9)]
    c, a = _leaves(rng, q, 1500, q * 0.42, (8, 13), (3, 5), crown_greens, blobs=blobs)
    col[:q, :q], alpha[:q, :q] = c, a

    shrub_greens = [rgb(78, 112, 46), rgb(96, 130, 56), rgb(62, 94, 40)]
    c, a = _leaves(rng, q, 1500, q * 0.40, (7, 11), (5, 8), shrub_greens, dark_below=0.45)
    col[:q, q:], alpha[:q, q:] = c, a

    # Tuft: blades fanning up from the bottom centre.
    yy, xx = (np.mgrid[0:q, 0:q] + 0.5)
    for _ in range(22):
        ang = rng.uniform(-0.7, 0.7); length = rng.uniform(0.55, 0.95) * q
        u = (xx - q / 2) * np.cos(ang) - yy * np.sin(ang) * -1 * 0
        s = yy / length
        centre = q / 2 + np.sin(ang) * yy * 0.9 + np.sin(s * 3) * 6
        halfw = np.clip((1.0 - s), 0, 1) * rng.uniform(5, 9)
        blade = (np.abs(xx - centre) < halfw) & (s < 1.0)
        g = rgb(70, 110, 46) * rng.uniform(0.85, 1.15)
        edge = np.abs(xx - centre) > halfw * 0.7
        cell = np.where(edge[..., None], rgb(140, 150, 70), g)
        col[q:, :q][blade] = cell[blade]; alpha[q:, :q][blade] = 1.0

    # Taro: one broad heart-shaped leaf with pale veins, stem at the bottom.
    y, x = (np.mgrid[0:q, 0:q] + 0.5) / q
    x = x - 0.5; y = y - 0.2
    heart = (x / 0.42) ** 2 + ((y - 0.38) / 0.38) ** 2 < 1.0
    notch = (np.abs(x) < 0.06 * (1 - y)) & (y < 0.12)
    leaf = heart & ~notch
    veins = np.exp(-(x / 0.012) ** 2) + sum(
        np.exp(-((y - 0.38 - k * x) / 0.01) ** 2) * (np.abs(x) < 0.35) for k in (-1.2, 1.2))
    taro = rgb(64, 108, 48) * (1 + 0.35 * np.clip(veins, 0, 1))[..., None]
    stem = (np.abs(x) < 0.015) & (y < 0.05) & (y > -0.2)
    col[q:, q:][leaf] = taro[leaf]; alpha[q:, q:][leaf] = 1.0
    col[q:, q:][stem] = rgb(90, 120, 60); alpha[q:, q:][stem] = 1.0

    # Transparent texels take the average leaf colour so mips don't fringe dark.
    mean = col[alpha > 0].mean(axis=0)
    col[alpha == 0] = mean
    return col, alpha


def generator(w=1024, h=512, ppm=256.0):
    """Grey enclosure of the Generac set: panel seams, louvre bank, warning sticker."""
    col = np.broadcast_to(rgb(96, 100, 101), (h, w, 3)).copy()
    col *= 1.0 + spectral_noise(h, 111, 2.5, m=w)[..., None] * 0.03
    y, x = (np.mgrid[0:h, 0:w] + 0.5) / ppm
    for sx in (0.9, 1.8, 2.7):
        col[np.abs(x - sx) < 0.006] *= 0.6
    louvre = (x > 2.85) & (x < 3.5) & (y > 0.35) & (y < 1.55)
    slat = ((y * 14) % 1.0) < 0.45
    col[louvre & slat] = rgb(40, 42, 44)
    col[louvre & ~slat] = rgb(118, 122, 124)
    sticker = (x > 2.3) & (x < 2.5) & (y > 1.2) & (y < 1.45)
    col[sticker] = rgb(226, 190, 40)
    tri = sticker & (np.abs(x - 2.4) < (1.42 - y) * 0.35) & (y > 1.25)
    col[tri] = rgb(20, 20, 20)
    col[(x > 0.15) & (x < 0.75) & (np.abs(y - 1.62) < 0.035)] = rgb(30, 30, 30)
    for hx in (0.6, 1.5, 2.4):
        col[(np.abs(x - hx) < 0.05) & (np.abs(y - 0.95) < 0.02)] = rgb(28, 28, 28)
    grime = np.clip(0.35 - y, 0, 0.35)
    return col * (1.0 - grime * 0.5)[..., None]


def flat(colour, n=64, seed=0, amount=0.02, scuff=None):
    col = np.broadcast_to(colour, (n, n, 3)).copy()
    col *= 1.0 + spectral_noise(n, seed, 1.5)[..., None] * amount
    if scuff is not None:
        col = mix(col, scuff, np.clip(spectral_noise(n, seed + 1, 2.0) - 1.0, 0, 0.6))
    return col


def canvas_red(n=512, metres=2.0, seam=0.5):
    """Awning canvas over CDS's ground floor (photos 33, 34): red acrylic with a seam every
    `seam` m, faded patches where the sun hits, darker streaks of dirt."""
    ppm = n / metres
    col = np.broadcast_to(rgb(204, 56, 42), (n, n, 3)).copy()
    col *= 1.0 + spectral_noise(n, 601, 2.2)[..., None] * 0.04
    fade = np.clip(spectral_noise(n, 602, 2.8) * 0.5 + 0.3, 0, 1)
    col = mix(col, rgb(226, 112, 96), fade * 0.35)
    cols = (np.arange(n)[None, :] + 0.5) / ppm
    d = np.abs((cols % seam) - seam / 2)
    col *= (1.0 - 0.22 * np.exp(-((seam / 2 - d) / 0.008) ** 2))[..., None]
    streak = np.clip(spectral_noise(n, 603, 1.6, aniso=(1.0, 0.1)) - 1.3, 0, None)
    return mix(col, rgb(120, 40, 34), np.clip(streak, 0, 0.4))


def palm_frond(n=512, seed=5):
    """One pinnate Areca frond (photo 35), for arching cards: V runs base (0) to tip (1)
    along the rachis at U = 0.5, leaflets angle forward on both sides. RGBA, alpha-tested."""
    rng = np.random.default_rng(seed)
    col = np.zeros((n, n, 3)); alpha = np.zeros((n, n))
    y, x = (np.mgrid[0:n, 0:n] + 0.5) / n
    xc = x - 0.5
    for i in range(36):
        t = 0.05 + 0.9 * i / 36
        L = 0.47 * np.sin(np.pi * min(1.0, t * 1.08)) * (0.75 + 0.25 * rng.random())
        for side in (-1.0, 1.0):
            ang = np.radians(58 + rng.uniform(-8, 8))
            ux, uy = side * np.sin(ang), np.cos(ang) * 0.5
            nrm = np.hypot(ux, uy)
            ux, uy = ux / nrm, uy / nrm
            px, py = xc, y - t
            s = px * ux + py * uy
            w = -px * uy + py * ux
            inside = (s > 0) & (s < L) & (np.abs(w) < 0.011 * (1 - s / max(L, 1e-3)) + 0.002)
            g = rgb(62, 116, 50) * rng.uniform(0.85, 1.15)
            col[inside] = g * (0.8 + 0.35 * (s[inside] / max(L, 1e-3)))[..., None]
            alpha[inside] = 1.0
    rachis = (np.abs(xc) < 0.010 * (1.3 - y)) & (y < 0.97)
    col[rachis] = rgb(126, 132, 72)
    alpha[rachis] = 1.0
    mean = col[alpha > 0].mean(axis=0)
    col[alpha == 0] = mean
    return col, alpha


def palm_trunk(n=256, metres=1.0):
    """Slender Areca stem: grey-green, a scar ring every ~9 cm (V runs up the stem)."""
    ppm = n / metres
    col = np.broadcast_to(rgb(146, 142, 118), (n, n, 3)).copy()
    col *= 1.0 + spectral_noise(n, 611, 1.5, aniso=(0.2, 1.0))[..., None] * 0.06
    rows = (np.arange(n)[:, None] + 0.5) / ppm
    ring = np.exp(-(((rows % 0.09) - 0.045) / 0.006) ** 2)
    col *= (1.0 - 0.32 * ring)[..., None]
    return col
