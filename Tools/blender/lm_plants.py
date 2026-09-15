"""
Planting: branching trees with card crowns, the leafless tree, round shrubs, blade
tufts and taro. Crowns and plants are alpha-tested cards on the T_Foliage atlas (cells
below), two-sided (back face 2 mm behind, see geolib.MeshBuilder.pane).

Objects:  <name>_Trunk          bark tubes, gets a collider in Unity
          <name>_Canopy, Plant_*  cards, no collider

Each limb is one welded tube (geolib.MeshBuilder.tube); a child limb starts inside its
parent's end, as branches do. Blender axes: X east, Y north, Z up.
"""

import math
import random

from mathutils import Vector

from geolib import MeshBuilder, make_object

CROWN = (0.0, 0.0, 0.5, 0.5)
SHRUB = (0.5, 0.0, 1.0, 0.5)
TUFT = (0.0, 0.5, 0.5, 1.0)
TARO = (0.5, 0.5, 1.0, 1.0)


def _clump(mb, rng, centre, size, cell, cards=3, flat=True):
    """Crossed cards around a point, plus one lying flat so it reads from below."""
    yaw0 = rng.uniform(0.0, 180.0)
    for k in range(cards):
        mb.card(tuple(centre), (size, size * 0.9), yaw0 + k * 180.0 / cards,
                rng.uniform(-20.0, 20.0), cell, "Kit_Foliage")
    if flat:
        mb.card(tuple(centre), (size, size), yaw0, 80.0, cell, "Kit_Foliage")


def _limb(mb, rng, start, direction, length, r0, r1, depth, tips, segs=3, wiggle=0.25):
    pts = [Vector(start)]
    d = Vector(direction).normalized()
    for _ in range(segs):
        d = (d + Vector((rng.uniform(-wiggle, wiggle), rng.uniform(-wiggle, wiggle),
                         rng.uniform(-0.05, 0.12)))).normalized()
        pts.append(pts[-1] + d * (length / segs))
    mb.tube(pts, [r0 + (r1 - r0) * i / segs for i in range(segs + 1)], "Kit_Bark",
            segs=6 if r0 > 0.05 else 4)
    tips.append((pts[-1].copy(), d.copy()))
    if depth <= 0:
        return
    for _ in range(rng.choice((2, 2, 3))):
        ang = rng.uniform(0.0, 2 * math.pi)
        spread = rng.uniform(0.45, 0.9)
        side = Vector((math.cos(ang), math.sin(ang), 0.0))
        nd = (d * math.cos(spread) + side * math.sin(spread)).normalized()
        if nd.z < 0.15:
            nd.z = 0.15
            nd.normalize()
        _limb(mb, rng, pts[-1], nd, length * rng.uniform(0.55, 0.75), r1, r1 * 0.55, depth - 1,
              tips, segs=2, wiggle=wiggle)


def tree(col, name, x, y, z, height=9.0, fork=2.6, crown=3.6, seed=0, lean=(0.0, 0.0),
         leafy=True, depth=2):
    """Slender forked tropical tree: short trunk, 2-3 limbs, ragged see-through crown."""
    rng = random.Random(seed)
    k = height / 9.0
    mb = MeshBuilder(["Kit_Bark"])
    tips = []
    trunk_dir = Vector((lean[0], lean[1], 1.0)).normalized()
    base = Vector((x, y, z - 0.1))
    mid = base + trunk_dir * fork * 0.5 + Vector((rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), 0))
    top = base + trunk_dir * fork
    mb.tube([base, mid, top], [0.17 * k, 0.145 * k, 0.12 * k], "Kit_Bark", segs=7)
    limbs = rng.choice((2, 3))
    for i in range(limbs):
        ang = 2 * math.pi * i / limbs + rng.uniform(-0.4, 0.4)
        side = Vector((math.cos(ang), math.sin(ang), 0.0))
        d = (trunk_dir * 0.8 + side * 0.45).normalized()
        _limb(mb, rng, top - trunk_dir * 0.1, d, (height - fork) * 0.55, 0.1 * k, 0.05 * k, depth, tips)
    objs = [make_object(name + "_Trunk", mb, col)]
    if not leafy:
        return objs

    cm = MeshBuilder(["Kit_Foliage"])
    s = crown / 3.6
    for p, d in tips:
        base_c = p + d * 0.3
        _clump(cm, rng, base_c, rng.uniform(1.3, 2.1) * s, CROWN)
        for _ in range(rng.choice((1, 2))):
            off = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.6, 0.4))) * 0.9 * s
            _clump(cm, rng, base_c + off, rng.uniform(0.8, 1.4) * s, CROWN, cards=2)
    objs.append(make_object(name + "_Canopy", cm, col))
    return objs


def shrub(col, name, x, y, z, radius=0.6, seed=0):
    rng = random.Random(seed)
    mb = MeshBuilder(["Kit_Foliage"])
    for _ in range(5):
        c = Vector((x + rng.uniform(-0.3, 0.3) * radius, y + rng.uniform(-0.3, 0.3) * radius,
                    z + radius * (0.8 + rng.uniform(0.0, 0.3))))
        _clump(mb, rng, c, radius * 1.8, SHRUB, cards=2)
    return make_object(name, mb, col)


def tuft(col, name, x, y, z, size=0.8, seed=0):
    rng = random.Random(seed)
    mb = MeshBuilder(["Kit_Foliage"])
    yaw0 = rng.uniform(0.0, 180.0)
    for kk in range(3):
        mb.card((x, y, z + size / 2), (size, size), yaw0 + kk * 60.0, rng.uniform(-8, 8), TUFT,
                "Kit_Foliage")
    return make_object(name, mb, col)


def taro(col, name, x, y, z, size=0.7, seed=0):
    rng = random.Random(seed)
    mb = MeshBuilder(["Kit_Foliage"])
    for kk in range(5):
        a = 2 * math.pi * kk / 5 + rng.uniform(-0.3, 0.3)
        r = size * 0.35
        c = (x + r * math.cos(a), y + r * math.sin(a), z + size * rng.uniform(0.6, 0.9))
        mb.card(c, (size, size), math.degrees(a) + 90.0, rng.uniform(35, 55), TARO, "Kit_Foliage")
    return make_object(name, mb, col)


PALM = (0.0, 0.0, 1.0, 1.0)      # the whole T_Palm map is one frond


def _frond(mb, rng, base, yaw, length, width, droop):
    """One arching frond: a strip of quads that rises from the crown and droops towards
    its tip, the T_Palm map running base (V = 0) to tip (V = 1). Two-sided."""
    segs = 4
    d = Vector((math.cos(yaw), math.sin(yaw), 0.0))
    side = Vector((-d.y, d.x, 0.0))
    pts = []
    for k in range(segs + 1):
        t = k / segs
        lift = (0.55 * t - droop * t * t) * length
        pts.append(Vector(base) + d * (length * t) + Vector((0.0, 0.0, lift)))
    for k in range(segs):
        a, b = pts[k], pts[k + 1]
        v0, v1 = k / segs, (k + 1) / segs
        mb.pane([tuple(a - side * width / 2), tuple(a + side * width / 2),
                 tuple(b + side * width / 2), tuple(b - side * width / 2)], "Kit_Palm",
                [(0.0, v0), (1.0, v0), (1.0, v1), (0.0, v1)])


def palm_clump(col, name, x, y, z, stems=3, height=5.5, seed=0):
    """Areca palm clump (photo 35): slender ringed stems leaning out from one base, each
    crowned with arching fronds. <name>_Trunk (collider), <name>_Canopy (cards)."""
    rng = random.Random(seed)
    trunk = MeshBuilder(["Kit_PalmTrunk"])
    crown = MeshBuilder(["Kit_Palm"])
    for i in range(stems):
        ang = 2 * math.pi * i / stems + rng.uniform(-0.4, 0.4)
        out = Vector((math.cos(ang), math.sin(ang), 0.0))
        lean = rng.uniform(0.08, 0.22)
        h = height * rng.uniform(0.7, 1.05)
        base = Vector((x, y, z - 0.1)) + out * 0.12
        up = (Vector((0.0, 0.0, 1.0)) + out * lean).normalized()
        mid = base + up * (h * 0.5) + out * 0.08
        top = base + up * h + out * 0.22
        trunk.tube([base, mid, top], [0.075, 0.065, 0.055], "Kit_PalmTrunk", segs=6)
        count = rng.randint(7, 9)
        for k in range(count):
            yaw = 2 * math.pi * k / count + rng.uniform(-0.2, 0.2)
            _frond(crown, rng, top, yaw, rng.uniform(1.7, 2.3), 0.9, rng.uniform(0.8, 1.3))
    return [make_object(name + "_Trunk", trunk, col), make_object(name + "_Canopy", crown, col)]
