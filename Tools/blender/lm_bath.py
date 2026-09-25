"""
Toilet fittings for the CECADEC interiors, and the waiting tables of the cross corridor.

Built from the user's hand plan (docs/map/reference/cecadec-interior-pasillo/
plano-usuario-bano.png) and the table photos (mesas-entrada2-*.png). Same contract as
lm_interior.py: one Blender object per element, pivot at its base, metres, `Wall` frames
(s along the wall, d into the room, z up).

The plan's three fixture bays run along one side wall, in this order from the door:
washbasins under a wall mirror, urinals behind grey-painted screens, WC cubicles. The
plan is not to scale — at the real 2.6 x 5.9 m of the room three 0.62 m cubicles would
be unusable, so the cubicles turn to face the door and take the full width at the back
wall, which is the only change from the sketch.
"""

import math

from geolib import MeshBuilder, make_object, rect
from lm_interior import BASE_H, BASE_T, CEIL, atlas, _circle, _solid_cylinder

TILE_TOP = 2.10          # wall tile stops here; plaster above, as in every UTEZ toilet
COUNTER_Z = 0.86         # finished top of the concrete washbasin slab
COUNTER_T = 0.14         # its thickness: a cast slab, not a board
BASIN_R = 0.165
SCREEN_T = 0.03          # grey-painted plywood of the urinal screens
STALL_T = 0.032


def _obj(col, name, mb, pivot="base"):
    return make_object(name, mb, col, pivot=pivot)


# ---- Room shell ----------------------------------------------------------------------

def tiled_lining(col, name, wall, s0, s1, d, thickness=0.012, top=TILE_TOP):
    """Glazed wall tile over an existing wall face, from the floor to `top`. `d` is the
    depth of the face being covered; the tile stands `thickness` proud of it, towards
    the room (negative side of the wall's own normal is handled by the caller's frame)."""
    mb = MeshBuilder(["Kit_Tile_Wall"])
    wall.box(mb, s0, s1, d, d + thickness, 0.0, top, "Kit_Tile_Wall")
    return [_obj(col, name, mb)]


def partition(col, name, wall, s0, s1, d0, d1, z0, z1, mat="Kit_Wall_White", skirting=False):
    """Plain block of wall in the wall's frame (room dividers, bay returns)."""
    mb = MeshBuilder([mat])
    wall.box(mb, s0, s1, d0, d1, z0, z1, mat)
    out = [_obj(col, name, mb)]
    if skirting:
        sk = MeshBuilder(["Kit_Frame"])
        wall.box(sk, s0, s1, d0 - BASE_T, d0, 0.0, BASE_H, "Kit_Frame")
        out.append(_obj(col, name + "_Skirting", sk))
    return out


# ---- Washbasins ------------------------------------------------------------------------

def basin_counter(col, name, wall, s_wall, sign, d0, d1, proj=0.55, basins=3):
    """Cast concrete slab cantilevered off the wall with glazed tile over it, `basins`
    vitreous-china bowls dropped into it, and a chrome pillar tap behind each one.

    `s_wall` is the wall face the slab grows from and `sign` (+1 / -1) the direction it
    grows in s; the run covers depth d0..d1. Slab, bowls and taps are separate objects:
    the bowls are what a torch picks out, the slab is what the player walks into.
    """
    made = []
    s_in = s_wall + sign * proj
    a, b = min(s_wall, s_in), max(s_wall, s_in)
    centres = [d0 + (d1 - d0) * (i + 0.5) / basins for i in range(basins)]
    bowl_s = s_wall + sign * (proj * 0.52)

    slab = MeshBuilder(["Kit_Tile_Counter"])
    holes = []
    for dc in centres:
        cx, cy, _ = wall.p(bowl_s, dc, 0.0)
        holes.append(_circle(cx, cy, BASIN_R, segs=14))
    (xa, ya, _), (xb, yb, _) = wall.p(a, d0, 0.0), wall.p(b, d1, 0.0)
    outer = rect(min(xa, xb), min(ya, yb), max(xa, xb), max(ya, yb))
    slab.prism(outer, COUNTER_Z - COUNTER_T, COUNTER_Z, "Kit_Tile_Counter", bottom=True, holes=holes)
    made.append(_obj(col, name + "_Slab", slab))

    bowls = MeshBuilder(["Kit_Porcelain", "Kit_Frame"])
    for dc in centres:
        cx, cy, _ = wall.p(bowl_s, dc, 0.0)
        rim = COUNTER_Z + 0.008
        # Body hanging under the slab, then the rim lipping over the hole.
        _solid_cylinder(bowls, cx, cy, rim - 0.22, rim - 0.03, 0.085, BASIN_R - 0.006,
                        "Kit_Porcelain", segs=14)
        bowls.prism(_circle(cx, cy, BASIN_R + 0.022, segs=14), rim - 0.03, rim, "Kit_Porcelain",
                    bottom=True, holes=[_circle(cx, cy, BASIN_R - 0.012, segs=14)])
        # Dark waste in the bottom of the bowl, so the inside does not read as a solid plug.
        bowls.face(_shift(_circle(cx, cy, 0.022, segs=8), rim - 0.215), "Kit_Frame")
    made.append(_obj(col, name + "_Bowls", bowls))

    taps = MeshBuilder(["Kit_Chrome"])
    for dc in centres:
        tx, ty, _ = wall.p(s_wall + sign * 0.10, dc, 0.0)
        _solid_cylinder(taps, tx, ty, COUNTER_Z, COUNTER_Z + 0.14, 0.022, 0.018, "Kit_Chrome", segs=8)
        sx, sy, _ = wall.p(s_wall + sign * 0.20, dc, 0.0)
        taps.tube([(tx, ty, COUNTER_Z + 0.13), (tx, ty, COUNTER_Z + 0.17),
                   (sx, sy, COUNTER_Z + 0.17), (sx, sy, COUNTER_Z + 0.13)], [0.0, 0.011, 0.011, 0.0],
                  "Kit_Chrome", segs=6)
    made.append(_obj(col, name + "_Taps", taps))
    return made


def _shift(poly, z):
    return [(x, y, z) for x, y in poly]


def mirror(col, name, wall, s_face, sign, d0, d1, z0=1.00, z1=1.95, t=0.016):
    """Wall mirror over the basins: a silvered pane on an aluminium edge, no frame.

    `s_face` is the tiled wall face and `sign` (+1 / -1) points from it into the room, so
    the silvered side always ends up facing the room rather than buried in the wall."""
    mb = MeshBuilder(["Kit_Mirror", "Kit_Frame"])
    e = 0.02
    wall.box(mb, s_face, s_face + sign * t, d0 - e, d1 + e, z0 - e, z1 + e, "Kit_Frame")
    s_pane = s_face + sign * (t + 0.002)
    (xa, ya, _), (xb, yb, _) = wall.p(s_pane, d0, 0.0), wall.p(s_pane, d1, 0.0)
    mb.pane([(xa, ya, z0), (xb, yb, z0), (xb, yb, z1), (xa, ya, z1)], "Kit_Mirror")
    return [_obj(col, name, mb)]


# ---- Urinals ---------------------------------------------------------------------------

def urinal(col, name, wall, s_wall, sign, dc, proj=0.34):
    """Wall-hung vitreous-china urinal with its exposed flush pipe and push valve."""
    mb = MeshBuilder(["Kit_Porcelain", "Kit_Chrome", "Kit_Frame"])
    w = 0.17
    # Bowl: a tapered shell, wide at the rim, narrowing to the waste at the bottom.
    rings = [(0.62, w, proj * 0.55), (0.80, w * 1.02, proj), (1.14, w * 1.02, proj),
             (1.20, w * 0.98, proj * 0.80)]
    prev = None
    for z, half, out in rings:
        s_out = s_wall + sign * out
        pts = [wall.p(s_wall, dc - half, z), wall.p(s_out, dc - half * 0.86, z),
               wall.p(s_out, dc + half * 0.86, z), wall.p(s_wall, dc + half, z)]
        if prev is not None:
            for k in range(3):
                mb.face([prev[k], prev[k + 1], pts[k + 1], pts[k]], "Kit_Porcelain")
        prev = pts
    mb.face(prev, "Kit_Porcelain")                                     # rim, seen from above
    mb.face([wall.p(s_wall, dc - w, 0.62), wall.p(s_wall + sign * proj * 0.55, dc - w, 0.62),
             wall.p(s_wall + sign * proj * 0.55, dc + w, 0.62), wall.p(s_wall, dc + w, 0.62)][::-1],
            "Kit_Porcelain")                                           # underside
    ex, ey, _ = wall.p(s_wall + sign * 0.03, dc, 0.0)
    _solid_cylinder(mb, ex, ey, 1.22, 1.52, 0.014, 0.014, "Kit_Chrome", segs=6)
    wall.box(mb, s_wall, s_wall + sign * 0.09, dc - 0.035, dc + 0.035, 1.50, 1.60, "Kit_Chrome")
    return [_obj(col, name, mb)]


def urinal_screen(col, name, wall, s_wall, sign, dc, proj=0.42, z0=0.55, z1=1.70):
    """Thin plywood screen between two urinals, painted grey, with a rounded front edge
    read as a chamfer (the plan calls these 'paredes delgadas de madera pintada de gris')."""
    mb = MeshBuilder(["Kit_Paint_Grey"])
    wall.box(mb, s_wall, s_wall + sign * proj, dc - SCREEN_T / 2, dc + SCREEN_T / 2, z0, z1,
             "Kit_Paint_Grey")
    return [_obj(col, name, mb)]


# ---- Cubicles ----------------------------------------------------------------------------

def wc_pan(col, name, x, y, facing):
    """Close-coupled WC: pan, seat and cistern, standing against the wall behind it.
    `facing` is the direction someone sitting on it looks (unit X or Y)."""
    fx, fy = facing
    mb = MeshBuilder(["Kit_Porcelain", "Kit_Frame"])

    def P(u, v, z):                      # u across the pan, v towards the wall behind
        return (x - fy * u - fx * v, y + fx * u - fy * v, z)

    # Pedestal: a narrow waist opening out to the bowl.
    for z0, z1, h0, h1, b0, b1 in ((0.0, 0.18, 0.11, 0.09, 0.14, 0.12),
                                   (0.18, 0.36, 0.09, 0.18, 0.12, 0.24)):
        for k in range(4):
            a0 = [(-h0, -b0), (h0, -b0), (h0, b0), (-h0, b0)][k]
            a1 = [(-h0, -b0), (h0, -b0), (h0, b0), (-h0, b0)][(k + 1) % 4]
            c0 = [(-h1, -b1), (h1, -b1), (h1, b1), (-h1, b1)][k]
            c1 = [(-h1, -b1), (h1, -b1), (h1, b1), (-h1, b1)][(k + 1) % 4]
            mb.face([P(*a0, z0), P(*a1, z0), P(*c1, z1), P(*c0, z1)], "Kit_Porcelain")
    # Bowl and seat.
    ring = [(0.185, -0.20), (0.185, 0.16), (0.0, 0.28), (-0.185, 0.16), (-0.185, -0.20), (0.0, -0.30)]
    for z0, z1, scale in ((0.36, 0.40, 1.0), (0.40, 0.42, 1.0)):
        for k in range(len(ring)):
            a, b = ring[k], ring[(k + 1) % len(ring)]
            mb.face([P(a[0] * scale, a[1] * scale, z0), P(b[0] * scale, b[1] * scale, z0),
                     P(b[0] * scale, b[1] * scale, z1), P(a[0] * scale, a[1] * scale, z1)],
                    "Kit_Porcelain")
    mb.face([P(u, v, 0.42) for u, v in ring], "Kit_Frame")             # dark water in the pan
    mb.box(*_bounds([P(-0.175, 0.20, 0.42), P(0.175, 0.36, 0.78)]), "Kit_Porcelain")   # cistern
    mb.box(*_bounds([P(-0.185, 0.19, 0.78), P(0.185, 0.37, 0.80)]), "Kit_Porcelain")   # its lid
    return [_obj(col, name, mb)]


def _bounds(points):
    lo = tuple(min(p[k] for p in points) for k in range(3))
    hi = tuple(max(p[k] for p in points) for k in range(3))
    return lo, hi


def cubicle_row(col, name, wall, s0, s1, d_front, d_back, stalls=3, z0=0.16, z1=2.00):
    """Row of WC cubicles filling s0..s1 against the back wall: grey-painted plywood
    partitions and pilasters, one hinged door per stall, one pan per stall.

    Doors are their own objects with the pivot on the hinge, closed, so HingedDoor can
    swing them. Partitions stand clear of the floor, as they do on site.
    """
    made = []
    width = (s1 - s0 - (stalls - 1) * STALL_T) / stalls
    edges = [s0]
    for i in range(stalls):
        edges.append(edges[-1] + width + (STALL_T if i < stalls - 1 else 0.0))

    panels = MeshBuilder(["Kit_Paint_Grey"])
    for i in range(1, stalls):
        s = edges[i] - STALL_T / 2
        wall.box(panels, s - STALL_T / 2, s + STALL_T / 2, d_front, d_back, z0, z1, "Kit_Paint_Grey")
    # Pilaster on each opening edge (the shared edges carry one, not two).
    posts = sorted({round(edges[i] + k * width, 4) for i in range(stalls) for k in (0, 1)})
    for s in posts:
        wall.box(panels, s - 0.03, s + 0.03, d_front - 0.015, d_front + 0.06, z0, z1, "Kit_Paint_Grey")
    made.append(_obj(col, name + "_Partitions", panels))

    for i in range(stalls):
        a, b = edges[i] + 0.04, edges[i] + width - 0.04
        leaf = MeshBuilder(["Kit_Paint_Grey", "Kit_Chrome"])
        wall.box(leaf, a, b, d_front - 0.012, d_front + 0.012, z0 + 0.04, z1 - 0.06, "Kit_Paint_Grey")
        knob = b - 0.09
        wall.box(leaf, knob - 0.02, knob + 0.02, d_front - 0.05, d_front - 0.012, 1.02, 1.08, "Kit_Chrome")
        made.append(_obj(col, f"Door_{name}{i + 1}", leaf, pivot=wall.p(a, d_front, 0.0)))
        cx, cy, _ = wall.p((a + b) / 2, d_back - 0.36, 0.0)
        nx, ny = wall.n
        made += wc_pan(col, f"{name}{i + 1}_Pan", cx, cy, (-nx, -ny))
    return made


# ---- Fittings a toilet always has ---------------------------------------------------------

def floor_drain(col, name, x, y, r=0.06):
    mb = MeshBuilder(["Kit_Chrome", "Kit_Frame"])
    mb.prism(_circle(x, y, r, segs=10), 0.002, 0.006, "Kit_Chrome", bottom=True,
             holes=[_circle(x, y, r - 0.018, segs=10)])
    mb.face(_shift(_circle(x, y, r - 0.018, segs=10), 0.004), "Kit_Frame")
    return [_obj(col, name, mb)]


def batten_light(col, name, x, y, along_x=True, length=1.20, z=CEIL):
    """Surface-mounted twin fluorescent batten: the toilets have no ceiling grid to
    recess a troffer into, so the fitting hangs under the slab. Its glowing face is a
    separate object, switched with the light by FluorescentFlicker."""
    hx, hy = (length / 2, 0.09) if along_x else (0.09, length / 2)
    body = MeshBuilder(["Kit_Metal_White"])
    body.box((x - hx, y - hy, z - 0.09), (x + hx, y + hy, z - 0.001), "Kit_Metal_White")
    glow = MeshBuilder(["Kit_Light"])
    gx, gy = (hx - 0.04, hy - 0.025) if along_x else (hx - 0.025, hy - 0.04)
    glow.face([(x - gx, y - gy, z - 0.088), (x + gx, y - gy, z - 0.088),
               (x + gx, y + gy, z - 0.088), (x - gx, y + gy, z - 0.088)][::-1], "Kit_Light", keep=True)
    return [_obj(col, "Ceil_Batten_" + name, body, pivot=(x, y, z)),
            _obj(col, "Ceil_BattenGlow_" + name, glow, pivot=(x, y, z - 0.088))]


def steel_cabinet(col, name, x, y, facing, width=0.90, depth=0.45, height=1.80):
    """Grey steel cabinet, the kind that ends up shoved across a door nobody may use."""
    fx, fy = facing
    ux, uy = -fy, fx
    mb = MeshBuilder(["Kit_Metal_Grey", "Kit_Frame", "Kit_Chrome"])

    def P(u, v, z):
        return (x + ux * u - fx * v, y + uy * u - fy * v, z)

    mb.box(*_bounds([P(-width / 2, -depth / 2, 0.04), P(width / 2, depth / 2, height)]), "Kit_Metal_Grey")
    mb.box(*_bounds([P(-width / 2 + 0.03, -depth / 2 + 0.03, 0.0),
                     P(width / 2 - 0.03, depth / 2 - 0.03, 0.04)]), "Kit_Frame")
    # Door joint and the two handles, on the face the player walks into.
    mb.box(*_bounds([P(-0.006, -depth / 2 - 0.004, 0.10), P(0.006, -depth / 2, height - 0.06)]), "Kit_Frame")
    for u in (-0.09, 0.09):
        mb.box(*_bounds([P(u - 0.02, -depth / 2 - 0.03, 1.00), P(u + 0.02, -depth / 2 - 0.004, 1.12)]),
               "Kit_Chrome")
    return [_obj(col, name, mb)]


# ---- Waiting tables of the cross corridor -------------------------------------------------

def coffee_table(col, name, x, y, along_x=True, length=1.22, depth=0.62, height=0.44,
                 books=None):
    """Panel-sided waiting table (mesas-entrada2-a/b/c.png): a dark mahogany laminate top
    on two full-depth end panels, no rails or legs. The laminate is chipped back to the
    grey particle board at the corners, and a slim black steel stretcher runs between the
    panels just under the top — the photos show it as a dark line in the shadow.

    `books` places what the photos show left on the table: "El poder del Lenguaje" in its
    slipcase with its volumes standing against the wall, and a stack of magazines.
    Each of those is its own object, so the player can knock them about later.
    """
    made = []
    ux, uy = (1.0, 0.0) if along_x else (0.0, 1.0)
    vx, vy = (0.0, 1.0) if along_x else (-1.0, 0.0)

    def P(u, v, z):
        return (x + ux * u + vx * v, y + uy * u + vy * v, z)

    L, D = length / 2, depth / 2
    top_t = 0.035
    panel_t = 0.030
    mb = MeshBuilder(["Kit_Wood_Desk", "Kit_Panel_Grey"])
    mb.box(*_bounds([P(-L, -D, height - top_t), P(L, D, height)]), "Kit_Wood_Desk")
    for side in (-1, 1):
        u = side * (L - panel_t / 2)
        mb.box(*_bounds([P(u - panel_t / 2, -D, 0.0), P(u + panel_t / 2, D, height - top_t)]),
               "Kit_Wood_Desk")
        # Bare particle board where the laminate has broken away at the foot.
        mb.box(*_bounds([P(u - panel_t / 2 - 0.002, -D, 0.0), P(u + panel_t / 2 + 0.002, -D + 0.07, 0.055)]),
               "Kit_Panel_Grey")
    made.append(_obj(col, name, mb))

    stretcher = MeshBuilder(["Kit_Frame"])
    stretcher.box(*_bounds([P(-L + panel_t, -0.012, height - top_t - 0.055),
                            P(L - panel_t, 0.012, height - top_t - 0.035)]), "Kit_Frame")
    made.append(_obj(col, name + "_Stretcher", stretcher))

    if books:
        made += book_set(col, name + "_Books", P(books[0], books[1], height), (vx, vy))
        made += magazines(col, name + "_Magazines", P(books[2], books[3], height), (ux, uy))
    return made


def book_set(col, name, corner, facing):
    """Slipcased set standing on the table: the blue case at the back and seven volumes
    leaning against it, their spines out. Covers come from the sign atlas."""
    fx, fy = facing
    ux, uy = -fy, fx
    x, y, z = corner
    mb = MeshBuilder(["Kit_Panel_Grey", "Kit_Signs"])
    w, h, t = 0.145, 0.22, 0.055

    def P(u, v, zz):
        return (x + ux * u + fx * v, y + uy * u + fy * v, zz)

    mb.box(*_bounds([P(0.0, 0.0, z), P(w, t, z + h)]), "Kit_Panel_Grey")
    mb.box(*_bounds([P(w + 0.004, 0.0, z), P(w + 0.204, 0.048, z + 0.21)]), "Kit_Panel_Grey")
    out = [_obj(col, name, mb)]

    faces = MeshBuilder(["Kit_Signs"])
    _sign_card(faces, P(0.004, -0.002, z + 0.006), (ux, uy), "BOOK_BOX")
    _sign_card(faces, P(w + 0.008, -0.002, z + 0.004), (ux, uy), "BOOK_SPINES")
    out.append(_obj(col, name + "_Covers", faces, pivot="centre"))
    return out


def magazines(col, name, corner, facing, count=3):
    """Loose magazines fanned on the table top, the top one cover-up."""
    x, y, z = corner
    fx, fy = facing
    ux, uy = -fy, fx
    mb = MeshBuilder(["Kit_Panel_Grey"])
    for i in range(count):
        a = math.radians(-7 * i)
        cs, sn = math.cos(a), math.sin(a)
        w, h, t = 0.21, 0.28, 0.004
        pts = []
        for u, v in ((0, 0), (w, 0), (w, h), (0, h)):
            uu, vv = u * cs - v * sn, u * sn + v * cs
            pts.append((x + ux * uu + fx * vv, y + uy * uu + fy * vv))
        zb = z + i * t
        for k in range(4):
            p, q = pts[k], pts[(k + 1) % 4]
            mb.face([(*p, zb), (*q, zb), (*q, zb + t), (*p, zb + t)], "Kit_Panel_Grey")
        mb.face([(*p, zb) for p in pts][::-1], "Kit_Panel_Grey")
        if i < count - 1:
            mb.face([(*p, zb + t) for p in pts], "Kit_Panel_Grey")
    out = [_obj(col, name, mb)]

    cover = MeshBuilder(["Kit_Signs"])
    a = math.radians(-7 * (count - 1))
    cs, sn = math.cos(a), math.sin(a)
    zb = z + (count - 1) * 0.004 + 0.0015
    quad = []
    for u, v in ((0, 0), (0.21, 0), (0.21, 0.28), (0, 0.28)):
        uu, vv = u * cs - v * sn, u * sn + v * cs
        quad.append((x + ux * uu + fx * vv, y + uy * uu + fy * vv, zb))
    info = _atlas_uv("MAG_HYPATIA")
    cover.pane(quad, "Kit_Signs", [(info[0], info[1]), (info[2], info[1]), (info[2], info[3]),
                                   (info[0], info[3])])
    out.append(_obj(col, name + "_Cover", cover, pivot="centre"))
    return out


def _atlas_uv(name):
    return atlas()[name]["uv"]


def _sign_card(mb, corner, along, sign):
    """Atlas card standing upright, its bottom-left at `corner`, reading along `along`."""
    info = atlas()[sign]
    w, h = info["size"]
    u0, v0, u1, v1 = info["uv"]
    ax, ay = along
    x, y, z = corner
    pts = [(x, y, z), (x + ax * w, y + ay * w, z), (x + ax * w, y + ay * w, z + h), (x, y, z + h)]
    mb.pane(pts, "Kit_Signs", [(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
