"""
CECADEC ground-floor interior, built element by element from the site photos
(docs/map/reference/cecadec-interior-pasillo/, numbered as they arrived) and the user's
floor plan (plano-usuario-28.png). One Blender object per element: every door leaf,
glazing frame, pane, pillar, skirting run, light fitting, sign, bench and bin.

Axes as CECADEC_North.blend: X east, Y north, Z up, metres; origin on the main door
threshold. Every wall here is axis-aligned. A `Wall` maps its own frame to the world:
  s  distance along the wall (the corridor walls run south: s = -y),
  d  depth from the corridor face into the room (d < 0 is corridor space),
  z  height.
Partitions occupy d = 0 .. PART_T; aluminium frames sit inside that, panes mid-depth.

Measured references (photos, 40 cm floor tiles as the scale): corridor 4.4 m clear,
drop ceiling 2.70 m, aluminium profile 5 cm, doors 0.92 x 2.08 m in a 1.02 m module,
plaster walls 2.20 m under a clear transom band, glazed partitions frosted with a
0.33 m plaster knee, pillars 0.45 m wide standing 0.22 m proud.
"""

import json
import math
import os

from geolib import MeshBuilder, make_object, rect

CEIL = 2.70
PART_T = 0.12
BAR = 0.05
FRAME_D = (0.02, 0.10)
PANE_D = 0.06
BASE_H, BASE_T = 0.10, 0.006
KNEE = 0.33
HEAD = 2.10
PLASTER_TOP = 2.20
DOOR_W, DOOR_H = 0.92, 2.08
DOOR_MOD = DOOR_W + 2 * BAR
PILLAR_W, PILLAR_PROUD = 0.45, 0.22
RISE, TREAD = 2.0 / 12, 0.30

_ATLAS = None


def atlas():
    global _ATLAS
    if _ATLAS is None:
        with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "sign_atlas.json")) as f:
            _ATLAS = json.load(f)
    return _ATLAS


class Wall:
    """A straight wall run. `along` is +s, `into` points from the corridor into the room."""

    def __init__(self, origin, along, into):
        self.o, self.a, self.n = origin, along, into
        # A viewer in the corridor facing the wall reads left to right along `read` * s.
        rx, ry = into[1], -into[0]
        self.read = 1.0 if rx * along[0] + ry * along[1] > 0 else -1.0

    def p(self, s, d, z):
        return (self.o[0] + self.a[0] * s + self.n[0] * d,
                self.o[1] + self.a[1] * s + self.n[1] * d, z)

    def box(self, mb, s0, s1, d0, d1, z0, z1, mat, cap=None):
        (xa, ya, _), (xb, yb, _) = self.p(s0, d0, 0.0), self.p(s1, d1, 0.0)
        mb.box((min(xa, xb), min(ya, yb), z0), (max(xa, xb), max(ya, yb), z1), mat, cap=cap)

    def panel(self, mb, outer, holes, d0, d1, mat):
        """Flat panel in the wall plane: outline and holes as (s, z), d0..d1 thick."""
        mb.transform = lambda q: self.p(q[0], q[2], q[1])
        mb.prism(outer, d0, d1, mat, bottom=True, holes=holes)
        mb.transform = None

    def plan(self, mb, poly, z0, z1, mat):
        """Prism with its outline in the wall's (s, d) plan, extruded z0..z1."""
        mb.transform = lambda q: self.p(q[0], q[1], q[2])
        mb.prism(poly, z0, z1, mat, bottom=True)
        mb.transform = None

    def quad(self, mb, s0, s1, z0, z1, d, mat, uvs=None):
        mb.pane([self.p(s0, d, z0), self.p(s1, d, z0), self.p(s1, d, z1), self.p(s0, d, z1)], mat, uvs)


def _obj(col, name, mb, pivot="base"):
    return make_object(name, mb, col, pivot=pivot)


def _box_obj(col, name, wall, s0, s1, d0, d1, z0, z1, mat):
    mb = MeshBuilder([mat])
    wall.box(mb, s0, s1, d0, d1, z0, z1, mat)
    return _obj(col, name, mb)


# ---- Signs --------------------------------------------------------------------------

def _sign_quad(mb, wall, sign, s, z, d):
    info = atlas()[sign]
    w, h = info["size"]
    u0, v0, u1, v1 = info["uv"]
    sa, sb = s - wall.read * w / 2, s + wall.read * w / 2
    mb.pane([wall.p(sa, d, z - h / 2), wall.p(sb, d, z - h / 2), wall.p(sb, d, z + h / 2),
             wall.p(sa, d, z + h / 2)], "Kit_Signs", [(u0, v0), (u1, v0), (u1, v1), (u0, v1)])


def decal(col, name, wall, sign, s, z, d=-0.004):
    """A sign from the photo atlas, on the corridor side of whatever is at depth d."""
    mb = MeshBuilder(["Kit_Signs"])
    _sign_quad(mb, wall, sign, s, z, d)
    return [_obj(col, "Decal_" + name, mb, pivot="centre")]


# ---- Wall pieces ------------------------------------------------------------------------

def plaster(col, name, wall, s0, s1, top=CEIL, depth=PART_T, skirting=True):
    """Tirol plaster wall, floor to `top`, with its black skirting on the corridor face."""
    out = [_box_obj(col, name, wall, s0, s1, 0.0, depth, 0.0, top, "Kit_Wall_White")]
    if skirting:
        out.append(_box_obj(col, name + "_Skirting", wall, s0, s1, -BASE_T, 0.0, 0.0, BASE_H, "Kit_Frame"))
    return out


def transom(col, name, wall, s0, s1, z0=PLASTER_TOP, lite=0.75, glass="Kit_Glass_Clear"):
    """Band of fixed lights over a wall (photos 7, 13): black frame + one pane object."""
    n = max(1, round((s1 - s0) / lite))
    w = (s1 - s0) / n
    holes = [rect(s0 + i * w + (BAR if i == 0 else BAR / 2), z0 + BAR,
                  s0 + (i + 1) * w - (BAR if i == n - 1 else BAR / 2), CEIL - BAR) for i in range(n)]
    frame = MeshBuilder(["Kit_Frame"])
    wall.panel(frame, rect(s0, z0, s1, CEIL - 0.004), holes, *FRAME_D, "Kit_Frame")
    panes = MeshBuilder([glass])
    for h in holes:
        wall.quad(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, PANE_D, glass)
    return [_obj(col, name + "_TransomFrame", frame), _obj(col, name + "_TransomPane", panes)]


def pillar(col, name, wall, s, width=PILLAR_W, proud=PILLAR_PROUD):
    """Structural column standing proud of the corridor face, skirting wrapped round it."""
    a, b = s - width / 2, s + width / 2
    out = [_box_obj(col, name, wall, a, b, -proud, PART_T, 0.0, CEIL, "Kit_Wall_White")]
    e = BASE_T
    mb = MeshBuilder(["Kit_Frame"])
    wall.plan(mb, [(a - e, 0.0), (a - e, -proud - e), (b + e, -proud - e), (b + e, 0.0),
                   (b, 0.0), (b, -proud), (a, -proud), (a, 0.0)], 0.0, BASE_H, "Kit_Frame")
    out.append(_obj(col, name + "_Skirting", mb))
    return out


# ---- Doors ----------------------------------------------------------------------------

def door_leaf(col, name, wall, hinge_s, free_s, infill, glass_from=None, signs=(), d=PANE_D, top=DOOR_H):
    """Aluminium door leaf, closed, pivot on its hinge. infill fills the frame below
    `glass_from` (all of it if None); above, clear glass. Hardware (lever handles both
    faces, lock, hinges, closer) and the paper signs taped to it are part of the leaf,
    so they swing with it. signs: [(sign, s, z)] on the corridor face."""
    a, b = sorted((hinge_s, free_s))
    t = 0.045
    d0, d1 = d - t / 2, d + t / 2
    st = 0.05
    mb = MeshBuilder(["Kit_Frame", infill, "Kit_Glass_Clear", "Kit_Chrome", "Kit_Signs"])
    if glass_from is None:
        holes = [rect(a + st, 0.12, b - st, top - st)]
    else:
        holes = [rect(a + st, 0.12, b - st, glass_from - 0.03), rect(a + st, glass_from + 0.03, b - st, top - st)]
    wall.panel(mb, rect(a, 0.006, b, top), holes, d0, d1, "Kit_Frame")
    for i, h in enumerate(holes):
        mat = infill if i == 0 else "Kit_Glass_Clear"
        wall.quad(mb, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, d, mat)

    toward_hinge = 1.0 if hinge_s > free_s else -1.0
    hs = free_s + toward_hinge * 0.07
    for face, sgn in ((d0, -1.0), (d1, 1.0)):
        wall.box(mb, hs - 0.025, hs + 0.025, face + sgn * 0.001, face + sgn * 0.012, 0.96, 1.06, "Kit_Chrome")
        wall.box(mb, hs - 0.008, hs + 0.008, face + sgn * 0.012, face + sgn * 0.045, 1.003, 1.019, "Kit_Chrome")
        s_end = hs + toward_hinge * 0.13
        wall.box(mb, min(hs, s_end), max(hs, s_end), face + sgn * 0.045, face + sgn * 0.063, 1.0, 1.022, "Kit_Chrome")
        wall.box(mb, hs - 0.012, hs + 0.012, face + sgn * 0.001, face + sgn * 0.01, 1.14, 1.19, "Kit_Chrome")
    for z in (0.22, 1.02, 1.84):
        wall.box(mb, hinge_s - 0.012, hinge_s + 0.012, d0 - 0.008, d0 - 0.001, z, z + 0.1, "Kit_Chrome")
    cs0 = hinge_s - toward_hinge * 0.06
    cs1 = hinge_s - toward_hinge * 0.36
    wall.box(mb, min(cs0, cs1), max(cs0, cs1), d0 - 0.06, d0 - 0.001, top - 0.12, top - 0.05, "Kit_Frame")
    for sign, s, z in signs:
        _sign_quad(mb, wall, sign, s, z, d0 - 0.003)
    return _obj(col, "Door_" + name, mb, pivot=wall.p(hinge_s, d, 0.0))


def _outline(s0, s1, z_low, top, doors):
    """(s, z) outline of a frame panel: bottom edge at z_low, dropping to the floor round
    each door module (m0, m1, ds0, ds1) so the opening is a notch, not a hole."""
    pts, cur = [], s0
    for m0, m1, ds0, ds1 in sorted(doors):
        if cur < m0 - 1e-6:
            pts += [(cur, z_low), (m0, z_low), (m0, 0.0)]
        else:
            pts += [(m0, 0.0)]
        pts += [(ds0, 0.0), (ds0, DOOR_H + 0.005), (ds1, DOOR_H + 0.005), (ds1, 0.0), (m1, 0.0)]
        cur = m1
    if cur < s1 - 1e-6:
        pts += [(cur, z_low), (s1, z_low)]
    pts += [(s1, top), (s0, top)]
    out = []
    for p in pts:
        if not out or abs(out[-1][0] - p[0]) > 1e-6 or abs(out[-1][1] - p[1]) > 1e-6:
            out.append(p)
    if abs(out[0][0] - out[-1][0]) < 1e-6 and abs(out[0][1] - out[-1][1]) < 1e-6:
        out.pop()
    return out


def glazed_front(col, name, wall, s0, s1, bays, doors, knee=KNEE, lower="Kit_Glass_Frosted",
                 upper="Kit_Glass_Frosted", head=HEAD, transom_lite=0.62, skirting=True):
    """Aluminium partition (photos 5, 6, 12): plaster knee, glazed bays, transom row, doors.

    bays: relative widths of the glazed bays, in order along s.
    doors: list of dicts {at: "start"|"end", hinge: "outer"|"inner", infill, glass_from,
           transom: glass material or "panel", signs: [(sign, offset_from_leaf_centre, z)]}.
    Door modules are packed at the start or end of the run; the bays fill the rest.
    """
    made = []
    start = [dd for dd in doors if dd["at"] == "start"]
    end = [dd for dd in doors if dd["at"] == "end"]
    g0 = s0 + DOOR_MOD * len(start)
    g1 = s1 - DOOR_MOD * len(end)
    modules = []
    for i, dd in enumerate(start):
        m0 = s0 + i * DOOR_MOD
        modules.append((m0, m0 + DOOR_MOD, dd))
    for i, dd in enumerate(end):
        m0 = g1 + i * DOOR_MOD
        modules.append((m0, m0 + DOOR_MOD, dd))

    total = sum(bays)
    edges = [g0]
    for w in bays:
        edges.append(edges[-1] + (g1 - g0) * w / total)
    bay_holes = []
    for i in range(len(bays)):
        a = edges[i] + (BAR if (i == 0 and not start) else BAR / 2)
        b = edges[i + 1] - (BAR if (i == len(bays) - 1 and not end) else BAR / 2)
        bay_holes.append(rect(a, knee + BAR, b, head - BAR / 2))

    # Transom row over everything, one or more lights per bay, one per door.
    t_holes, t_mats = [], []
    for i in range(len(bays)):
        n = max(1, round((edges[i + 1] - edges[i]) / transom_lite))
        w = (edges[i + 1] - edges[i]) / n
        for k in range(n):
            a = edges[i] + k * w + BAR / 2 + (BAR / 2 if (i == 0 and k == 0 and not start) else 0.0)
            b = edges[i] + (k + 1) * w - BAR / 2 - (BAR / 2 if (i == len(bays) - 1 and k == n - 1 and not end) else 0.0)
            t_holes.append(rect(a, head + BAR / 2, b, CEIL - BAR))
            t_mats.append(upper)
    door_specs = []
    for m0, m1, dd in modules:
        ds0, ds1 = m0 + BAR, m1 - BAR
        door_specs.append((m0, m1, ds0, ds1))
        t_holes.append(rect(ds0, DOOR_H + 0.005 + BAR, ds1, CEIL - BAR))
        t_mats.append(dd.get("transom", upper))

    frame = MeshBuilder(["Kit_Frame"])
    wall.panel(frame, _outline(s0, s1, knee, CEIL - 0.004, door_specs), bay_holes + t_holes,
               *FRAME_D, "Kit_Frame")
    made.append(_obj(col, name + "_Frame", frame))

    panes = MeshBuilder([lower, upper, "Kit_Panel_Grey", "Kit_Glass_Clear"])
    for h in bay_holes:
        wall.quad(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, PANE_D, lower)
    for h, mat in zip(t_holes, t_mats):
        mat = "Kit_Panel_Grey" if mat == "panel" else mat
        wall.quad(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, PANE_D, mat)
    made.append(_obj(col, name + "_Pane", panes))

    # Plaster knee under the bays, with skirting.
    if knee > 0.0:
        made += plaster(col, name + "_Knee", wall, g0, g1, top=knee, skirting=skirting)

    for (m0, m1, dd), (_, _, ds0, ds1) in zip(modules, door_specs):
        outer_is_start = dd["at"] == "start"
        hinge_outer = dd.get("hinge", "outer") == "outer"
        hinge = ds0 if (outer_is_start == hinge_outer) else ds1
        free = ds1 if hinge == ds0 else ds0
        centre = (ds0 + ds1) / 2
        signs = [(sg, centre + wall.read * off, z) for sg, off, z in dd.get("signs", ())]
        made.append(door_leaf(col, dd["name"], wall, hinge, free, dd.get("infill", "Kit_Glass_Frosted"),
                              dd.get("glass_from"), signs))
    return made


MAX_PIECE = 6.0      # longest wall or slab piece: URP lights each object with at most 8 lights


def _plaster_band(col, name, wall, s0, s1, top, transom_band, lite):
    """Plaster (+ transom) between s0 and s1, cut into pieces no longer than MAX_PIECE."""
    made = []
    n = max(1, math.ceil((s1 - s0) / MAX_PIECE - 1e-6))
    w = (s1 - s0) / n
    for i in range(n):
        a, b = s0 + i * w, s0 + (i + 1) * w
        tag = name if n == 1 else f"{name}_{i + 1}"
        made += plaster(col, tag, wall, a, b, top=top)
        if transom_band:
            made += transom(col, tag, wall, a, b, lite=lite)
    return made


def plain_front(col, name, wall, s0, s1, door=None, transom_band=True, lite=0.75):
    """Plaster wall (to 2.20 m under a clear transom band, or to the ceiling) with an
    optional aluminium door module at its start or end (photos 7, 13, 23)."""
    made = []
    top = PLASTER_TOP if transom_band else CEIL
    if door is None:
        return made + _plaster_band(col, name, wall, s0, s1, top, transom_band, lite)

    if door["at"] == "start":
        m0, m1 = s0, s0 + DOOR_MOD
        p0, p1 = m1, s1
    else:
        m0, m1 = s1 - DOOR_MOD, s1
        p0, p1 = s0, m0
    made += _plaster_band(col, name, wall, p0, p1, top, transom_band, lite)

    ds0, ds1 = m0 + BAR, m1 - BAR
    head_hole = rect(ds0, DOOR_H + 0.005 + BAR, ds1, CEIL - BAR)
    frame = MeshBuilder(["Kit_Frame"])
    wall.panel(frame, _outline(m0, m1, 0.0, CEIL - 0.004, [(m0, m1, ds0, ds1)]), [head_hole],
               *FRAME_D, "Kit_Frame")
    made.append(_obj(col, name + "_DoorFrame", frame))
    panel = MeshBuilder(["Kit_Panel_Grey"])
    wall.quad(panel, ds0 - 0.01, ds1 + 0.01, head_hole[0][1] - 0.01, head_hole[2][1] + 0.01, PANE_D,
              "Kit_Panel_Grey")
    made.append(_obj(col, name + "_DoorHead", panel))

    hinge_outer = door.get("hinge", "outer") == "outer"
    hinge = ds0 if ((door["at"] == "start") == hinge_outer) else ds1
    free = ds1 if hinge == ds0 else ds0
    centre = (ds0 + ds1) / 2
    signs = [(sg, centre + wall.read * off, z) for sg, off, z in door.get("signs", ())]
    made.append(door_leaf(col, door["name"], wall, hinge, free, door.get("infill", "Kit_Panel_Grey"),
                          door.get("glass_from"), signs))
    return made


def tinted_front(col, name, wall, s0, s1, sign=None):
    """Smoked-glass front, four full-height panels (photos 1, 4): the outer two fixed on
    the room-side track, the middle two sliding on the corridor-side track, meeting with
    bar pulls and a lock."""
    made = []
    frame = MeshBuilder(["Kit_Frame"])
    wall.panel(frame, rect(s0, 0.0, s1, CEIL - 0.004), [rect(s0 + 0.06, 0.04, s1 - 0.06, CEIL - 0.12)],
               0.0, PART_T, "Kit_Frame")
    made.append(_obj(col, name + "_Frame", frame))

    inner0, inner1 = s0 + 0.06, s1 - 0.06
    overlap = 0.05
    pw = (inner1 - inner0 + 3 * overlap) / 4
    order = [inner0 + i * (pw - overlap) for i in range(4)]
    for i, a in enumerate(order):
        b = a + pw
        sliding = i in (1, 2)
        dc = 0.085 if not sliding else 0.035
        t = 0.03
        mb = MeshBuilder(["Kit_Frame", "Kit_Glass_Tinted", "Kit_Chrome"])
        wall.panel(mb, rect(a, 0.045, b, CEIL - 0.125), [rect(a + 0.05, 0.12, b - 0.05, CEIL - 0.19)],
                   dc - t / 2, dc + t / 2, "Kit_Frame")
        wall.quad(mb, a + 0.04, b - 0.04, 0.11, CEIL - 0.18, dc, "Kit_Glass_Tinted")
        if sliding:
            meet = b - 0.035 if i == 1 else a + 0.035
            wall.box(mb, meet - 0.012, meet + 0.012, dc - t / 2 - 0.045, dc - t / 2 - 0.02, 0.85, 1.35, "Kit_Chrome")
            wall.box(mb, meet - 0.008, meet + 0.008, dc - t / 2 - 0.02, dc - t / 2 - 0.001, 0.86, 0.89, "Kit_Chrome")
            wall.box(mb, meet - 0.008, meet + 0.008, dc - t / 2 - 0.02, dc - t / 2 - 0.001, 1.31, 1.34, "Kit_Chrome")
        kind = "Slide" if sliding else "Fixed"
        made.append(_obj(col, f"{name}_{kind}{i + 1}", mb))
    if sign:
        leftmost = order[3] + pw / 2 if wall.read < 0 else order[0] + pw / 2
        made += decal(col, name + "_Sign", wall, sign, leftmost + wall.read * 0.3, 1.55, d=0.085 - 0.02)
    return made


def closet(col, name, wall, s0, s1, depth=1.25, sign_leaf=5):
    """Electrical closet (photo 2): plywood header, pine jambs and centre post, eight
    bifold louvre leaves (stiles, rails, two banks of slats), strap hinges, pulls, hasp."""
    made = []
    hdr = 2.33
    made.append(_box_obj(col, name + "_Header", wall, s0, s1, 0.0, 0.10, hdr, CEIL, "Kit_Wood_Header"))
    jamb, post = 0.06, 0.12
    mid = (s0 + s1) / 2
    jambs = MeshBuilder(["Kit_Wood_Door"])
    wall.box(jambs, s0, s0 + jamb, 0.0, 0.10, 0.0, hdr, "Kit_Wood_Door")
    wall.box(jambs, s1 - jamb, s1, 0.0, 0.10, 0.0, hdr, "Kit_Wood_Door")
    wall.box(jambs, mid - post / 2, mid + post / 2, -0.01, 0.10, 0.0, hdr, "Kit_Wood_Door")
    made.append(_obj(col, name + "_Jambs", jambs))

    groups = ((s0 + jamb, mid - post / 2), (mid + post / 2, s1 - jamb))
    leaves = []
    for g0, g1 in groups:
        lw = (g1 - g0) / 4
        leaves += [(g0 + k * lw, g0 + (k + 1) * lw) for k in range(4)]
    d0, d1 = 0.02, 0.055
    banks = ((0.14, 0.95), (1.10, 2.16))
    reading = sorted(leaves, key=lambda l: l[0] * wall.read)
    for i, (a, b) in enumerate(reading):
        a, b = a + 0.004, b - 0.004
        mb = MeshBuilder(["Kit_Wood_Door"])
        holes = [rect(a + 0.05, z0, b - 0.05, z1) for z0, z1 in banks]
        wall.panel(mb, rect(a, 0.02, b, hdr - 0.02), holes, d0, d1, "Kit_Wood_Door")
        for z0, z1 in banks:
            n = int((z1 - z0) / 0.032)
            for k in range(n):
                z = z0 + 0.006 + k * (z1 - z0 - 0.012) / n
                mb.pane([wall.p(a + 0.045, d0 + 0.004, z), wall.p(b - 0.045, d0 + 0.004, z),
                         wall.p(b - 0.045, d1 - 0.004, z + 0.046), wall.p(a + 0.045, d1 - 0.004, z + 0.046)],
                        "Kit_Wood_Door")
        made.append(_obj(col, f"{name}_Leaf{i + 1}", mb))
        if i == sign_leaf:
            made += decal(col, name + "_Sign", wall, "ALTO_VOLTAJE", (a + b) / 2, 1.80, d=d0 - 0.004)

    hw = MeshBuilder(["Kit_Frame", "Kit_Chrome"])
    for i in (0, 2, 4, 6):
        a, b = reading[i]
        joint = b if wall.read > 0 else a
        wall.box(hw, joint - 0.03, joint + 0.03, d0 - 0.008, d0 - 0.001, 2.20, 2.30, "Kit_Frame")
    for i in (1, 2, 5):
        a, b = reading[i]
        edge = b - 0.05 if (i % 2 == 1) == (wall.read > 0) else a + 0.05
        wall.box(hw, edge - 0.008, edge + 0.008, d0 - 0.03, d0 - 0.001, 1.10, 1.24, "Kit_Frame")
    a, b = reading[5]
    joint = b if wall.read > 0 else a
    wall.box(hw, joint - 0.04, joint + 0.04, d0 - 0.012, d0 - 0.001, 1.02, 1.07, "Kit_Chrome")
    wall.box(hw, joint - 0.018, joint + 0.018, d0 - 0.035, d0 - 0.012, 0.97, 1.03, "Kit_Chrome")
    made.append(_obj(col, "Detail_" + name + "_Hardware", hw))

    back = MeshBuilder(["Kit_Wall_White"])
    wall.box(back, s0, s1, depth, depth + PART_T, 0.0, CEIL, "Kit_Wall_White")
    wall.box(back, s0 - PART_T, s0, 0.10, depth, 0.0, CEIL, "Kit_Wall_White")
    wall.box(back, s1, s1 + PART_T, 0.10, depth, 0.0, CEIL, "Kit_Wall_White")
    made.append(_obj(col, name + "_Room", back))
    return made


# ---- Ceiling fittings -------------------------------------------------------------------

def _square_ring_faces(mb, cx, cy, outer, inner, z, mat, down=True):
    o, i = outer / 2, inner / 2
    corners_o = [(cx - o, cy - o), (cx + o, cy - o), (cx + o, cy + o), (cx - o, cy + o)]
    corners_i = [(cx - i, cy - i), (cx + i, cy - i), (cx + i, cy + i), (cx - i, cy + i)]
    for k in range(4):
        a, b = corners_o[k], corners_o[(k + 1) % 4]
        c, d = corners_i[(k + 1) % 4], corners_i[k]
        q = [(*a, z), (*b, z), (*c, z), (*d, z)]
        mb.face(q[::-1] if down else q, mat)


def _square_band_faces(mb, cx, cy, size, z0, z1, mat):
    h = size / 2
    c = [(cx - h, cy - h), (cx + h, cy - h), (cx + h, cy + h), (cx - h, cy + h)]
    for k in range(4):
        a, b = c[k], c[(k + 1) % 4]
        mb.face([(*a, z0), (*b, z0), (*b, z1), (*a, z1)], mat)


def troffer(col, name, cx, cy, z=CEIL, size=0.60, cells=4):
    """2x2 ft parabolic troffer (photos 18, 23): white flange, recess, chrome egg-crate
    blades, emissive back plate. Its tile is cut out of the ceiling slab."""
    mb = MeshBuilder(["Kit_Metal_White", "Kit_Chrome"])
    h, fl = size / 2, 0.022
    inner = h - fl
    mb.prism(rect(cx - h, cy - h, cx + h, cy + h), z - 0.006, z, "Kit_Metal_White", bottom=True,
             holes=[rect(cx - inner, cy - inner, cx + inner, cy + inner)])
    _square_band_faces(mb, cx, cy, 2 * inner, z, z + 0.09, "Kit_Metal_White")
    step = 2 * inner / cells
    xs = [cx - inner + k * step for k in range(1, cells)]
    ys = [cy - inner + k * step for k in range(1, cells)]
    for x in xs:
        mb.pane([(x, cy - inner, z + 0.004), (x, cy + inner, z + 0.004), (x, cy + inner, z + 0.075),
                 (x, cy - inner, z + 0.075)], "Kit_Chrome")
    edges = [cx - inner] + xs + [cx + inner]
    for y in ys:
        for j in range(cells):
            a, b = edges[j] + 0.003, edges[j + 1] - 0.003
            mb.pane([(a, y, z + 0.004), (b, y, z + 0.004), (b, y, z + 0.075), (a, y, z + 0.075)], "Kit_Chrome")
    # The glowing back plate is its own object: FluorescentFlicker switches it off with
    # the light, so a stuttering tube does not keep a lit panel.
    glow = MeshBuilder(["Kit_Light"])
    glow.face([(cx - inner, cy - inner, z + 0.088), (cx + inner, cy - inner, z + 0.088),
             (cx + inner, cy + inner, z + 0.088), (cx - inner, cy + inner, z + 0.088)][::-1], "Kit_Light", keep=True)
    return [_obj(col, "Ceil_Troffer_" + name, mb, pivot=(cx, cy, z)),
            _obj(col, "Ceil_TrofferGlow_" + name, glow, pivot=(cx, cy, z + 0.088))]


def diffuser(col, name, cx, cy, z=CEIL):
    """Square four-way supply diffuser (photo 18): stepped white cones, dark slots."""
    mb = MeshBuilder(["Kit_Metal_White", "Kit_Frame"])
    sizes = [0.60, 0.48, 0.38, 0.28, 0.18, 0.11]
    zs = [z - 0.001, z - 0.009, z - 0.019, z - 0.029, z - 0.039, z - 0.05]
    top = z - 0.001
    h = sizes[0] / 2
    mb.face([(cx - h, cy - h, top), (cx + h, cy - h, top), (cx + h, cy + h, top), (cx - h, cy + h, top)],
            "Kit_Metal_White")
    _square_band_faces(mb, cx, cy, sizes[0], zs[1], top, "Kit_Metal_White")
    for i in range(1, len(sizes)):
        _square_ring_faces(mb, cx, cy, sizes[i - 1], sizes[i], zs[i], "Kit_Metal_White")
        if i + 1 < len(zs):
            _square_band_faces(mb, cx, cy, sizes[i], zs[i + 1], zs[i], "Kit_Frame")
    hc = sizes[-1] / 2
    mb.face([(cx - hc, cy - hc, zs[-1]), (cx + hc, cy - hc, zs[-1]), (cx + hc, cy + hc, zs[-1]),
             (cx - hc, cy + hc, zs[-1])][::-1], "Kit_Metal_White")
    return [_obj(col, "Ceil_Diffuser_" + name, mb, pivot=(cx, cy, z))]


def return_grille(col, name, cx, cy, z=CEIL, along_x=True):
    """Slotted return-air grille, 60 x 30 cm (photo 23): white frame, dark void, blades."""
    mb = MeshBuilder(["Kit_Metal_White", "Kit_Frame"])
    hx, hy = (0.30, 0.15) if along_x else (0.15, 0.30)
    mb.prism(rect(cx - hx, cy - hy, cx + hx, cy + hy), z - 0.008, z - 0.001, "Kit_Metal_White",
             bottom=True, holes=[rect(cx - hx + 0.025, cy - hy + 0.025, cx + hx - 0.025, cy + hy - 0.025)])
    ix, iy = hx - 0.025, hy - 0.025
    mb.face([(cx - ix, cy - iy, z - 0.002), (cx + ix, cy - iy, z - 0.002), (cx + ix, cy + iy, z - 0.002),
             (cx - ix, cy + iy, z - 0.002)][::-1], "Kit_Frame", keep=True)
    n = 9
    for k in range(1, n):
        if along_x:
            y = cy - iy + k * 2 * iy / n
            mb.pane([(cx - ix, y, z - 0.004), (cx + ix, y, z - 0.004), (cx + ix, y + 0.01, z - 0.012),
                     (cx - ix, y + 0.01, z - 0.012)], "Kit_Metal_White")
        else:
            x = cx - ix + k * 2 * ix / n
            mb.pane([(x, cy - iy, z - 0.004), (x, cy + iy, z - 0.004), (x + 0.01, cy + iy, z - 0.012),
                     (x + 0.01, cy - iy, z - 0.012)], "Kit_Metal_White")
    return [_obj(col, "Ceil_Return_" + name, mb, pivot=(cx, cy, z))]


def smoke_detector(col, name, cx, cy, z=CEIL):
    mb = MeshBuilder(["Kit_Metal_White"])
    _solid_cylinder(mb, cx, cy, z - 0.032, z - 0.001, 0.055, 0.065, "Kit_Metal_White", segs=12)
    return [_obj(col, "Ceil_Smoke_" + name, mb, pivot=(cx, cy, z))]


# ---- Furniture and fittings -----------------------------------------------------------

def _solid_cylinder(mb, cx, cy, z0, z1, r0, r1, mat, segs=16, top_mat=None):
    """Closed tapered cylinder with aligned rings: a clean solid, so its recalculated
    normals face out (geolib's tube leaves an open start ring)."""
    ang = [2 * math.pi * k / segs for k in range(segs)]
    bot = [(cx + r0 * math.cos(a), cy + r0 * math.sin(a), z0) for a in ang]
    top = [(cx + r1 * math.cos(a), cy + r1 * math.sin(a), z1) for a in ang]
    for k in range(segs):
        j = (k + 1) % segs
        mb.face([bot[k], bot[j], top[j], top[k]], mat)
    mb.face(bot[::-1], mat)
    mb.face(top, top_mat or mat)


def _circle(cx, cy, r, segs=16):
    return [(cx + r * math.cos(2 * math.pi * k / segs), cy + r * math.sin(2 * math.pi * k / segs))
            for k in range(segs)]


def bench(col, name, x, y, facing, seats=3):
    """Three-seat airport bench (photos 5, 6, 12): chrome beam, T legs and armrests,
    perforated steel pans, blue vinyl seat and head cushions. `facing` is the direction
    a sitter looks (unit X or Y); the back is the other way."""
    fx, fy = facing
    ux, uy = -fy, fx                      # along the bench

    def P(u, v, z):                       # v > 0 towards the back
        return (x + ux * u - fx * v, y + uy * u - fy * v, z)

    def box(mb, u0, u1, v0, v1, z0, z1, mat):
        (xa, ya, _), (xb, yb, _) = P(u0, v0, 0), P(u1, v1, 0)
        mb.box((min(xa, xb), min(ya, yb), z0), (max(xa, xb), max(ya, yb), z1), mat)

    pitch = 0.56
    length = pitch * seats + 0.08
    L = length / 2
    frame = MeshBuilder(["Kit_Chrome"])
    box(frame, -L + 0.06, L - 0.06, -0.03, 0.03, 0.33, 0.37, "Kit_Chrome")
    for lu in (-L + 0.30, L - 0.30):
        box(frame, lu - 0.025, lu + 0.025, -0.30, 0.30, 0.0, 0.03, "Kit_Chrome")
        box(frame, lu - 0.025, lu + 0.025, -0.03, 0.03, 0.03, 0.33, "Kit_Chrome")
    for i in range(seats):
        uc = -L + 0.04 + pitch * (i + 0.5)
        for side in (-1, 1):
            su = uc + side * (pitch / 2 - 0.02)
            box(frame, su - 0.012, su + 0.012, 0.17, 0.21, 0.37, 0.86, "Kit_Chrome")
    for end in (-1, 1):
        u = end * (L - 0.015)
        pts = [P(u, 0.18, 0.45), P(u, 0.10, 0.62), P(u, -0.10, 0.64), P(u, -0.26, 0.58), P(u, -0.28, 0.44)]
        frame.tube(pts, [0.0] + [0.014] * 4, "Kit_Chrome", segs=6)
    out = [_obj(col, name + "_Frame", frame)]

    pans = MeshBuilder(["Kit_Perforated"])
    cush = MeshBuilder(["Kit_Vinyl_Blue"])
    for i in range(seats):
        uc = -L + 0.04 + pitch * (i + 0.5)
        a, b = uc - pitch / 2 + 0.035, uc + pitch / 2 - 0.035
        box(pans, a, b, -0.30, 0.15, 0.37, 0.40, "Kit_Perforated")
        box(pans, a, b, 0.17, 0.20, 0.42, 0.62, "Kit_Perforated")
        box(cush, a + 0.005, b - 0.005, -0.29, 0.12, 0.40, 0.45, "Kit_Vinyl_Blue")
        box(cush, a + 0.005, b - 0.005, 0.12, 0.17, 0.60, 0.86, "Kit_Vinyl_Blue")
    out += [_obj(col, name + "_Pans", pans), _obj(col, name + "_Cushions", cush)]
    return out


def extinguisher(col, name, wall, s, top=1.55, face=0.0):
    """6 kg PQS extinguisher on its wall hook (photos 11, 20, 23): red body, black valve
    and hose, bracket. `face` is the depth of the surface it hangs on."""
    r = 0.085
    cx, cy, _ = wall.p(s, face - r - 0.03, 0.0)
    mb = MeshBuilder(["Kit_Metal_Red", "Kit_Frame", "Kit_Chrome"])
    z0 = top - 0.56
    _solid_cylinder(mb, cx, cy, z0, z0 + 0.42, r, r, "Kit_Metal_Red", segs=14)
    _solid_cylinder(mb, cx, cy, z0 + 0.42, z0 + 0.48, r, 0.03, "Kit_Metal_Red", segs=14)
    _solid_cylinder(mb, cx, cy, z0 + 0.48, z0 + 0.53, 0.03, 0.03, "Kit_Chrome", segs=8)
    lo = wall.p(s - 0.02, face - 0.07, 0.0)
    hi = wall.p(s + 0.10, face - 0.05, 0.0)
    mb.box((min(lo[0], hi[0]), min(lo[1], hi[1]), z0 + 0.52), (max(lo[0], hi[0]), max(lo[1], hi[1]), z0 + 0.535),
           "Kit_Frame")
    hose = [wall.p(s + 0.03, face - r - 0.03 - 0.02, z0 + 0.50), wall.p(s + 0.10, face - 0.06, z0 + 0.45),
            wall.p(s + 0.11, face - 0.06, z0 + 0.20), wall.p(s + 0.10, face - 0.07, z0 + 0.08)]
    mb.tube(hose, [0.0, 0.012, 0.012, 0.016], "Kit_Frame", segs=6)
    k0 = wall.p(s - 0.05, face - 0.03, 0.0)
    k1 = wall.p(s + 0.05, face - 0.001, 0.0)
    mb.box((min(k0[0], k1[0]), min(k0[1], k1[1]), top - 0.12), (max(k0[0], k1[0]), max(k0[1], k1[1]), top - 0.02),
           "Kit_Frame")
    return [_obj(col, name, mb)]


def recycling_bin(col, name, x, y, facing, label, lid=False):
    """Grey 100 L bin (photo 8), its paper label taped on the corridor side."""
    mb = MeshBuilder(["Kit_Plastic_Grey", "Kit_Frame", "Kit_Signs"])
    r0, r1, h = 0.21, 0.25, 0.64
    # Dark top face reads as the open inside of the bin; the rim stands just above it.
    _solid_cylinder(mb, x, y, 0.0, h, r0, r1, "Kit_Plastic_Grey", top_mat="Kit_Frame")
    mb.prism(_circle(x, y, r1 + 0.012), h - 0.015, h + 0.004, "Kit_Plastic_Grey", bottom=True,
             holes=[_circle(x, y, r1 - 0.004)])
    if lid:
        _solid_cylinder(mb, x, y, h + 0.006, h + 0.036, r1 + 0.02, r1 + 0.02, "Kit_Frame")
    info = atlas()[label]
    w, hh = info["size"]
    u0, v0, u1, v1 = info["uv"]
    fx, fy = facing
    ux, uy = -fy, fx
    zc = 0.34
    rr = r0 + (r1 - r0) * zc / h + 0.004
    cxl, cyl = x + fx * rr, y + fy * rr
    pts = [(cxl - ux * w / 2, cyl - uy * w / 2, zc - hh / 2), (cxl + ux * w / 2, cyl + uy * w / 2, zc - hh / 2),
           (cxl + ux * w / 2, cyl + uy * w / 2, zc + hh / 2), (cxl - ux * w / 2, cyl - uy * w / 2, zc + hh / 2)]
    mb.pane(pts, "Kit_Signs", [(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
    return [_obj(col, name, mb)]


def low_bench(col, name, x, y, along_x=True, length=1.0):
    """Black laminate box bench of the cross corridor (photo 20)."""
    mb = MeshBuilder(["Kit_Frame"])
    hx, hy = (length / 2, 0.22) if along_x else (0.22, length / 2)
    mb.box((x - hx, y - hy, 0.0), (x + hx, y + hy, 0.42), "Kit_Frame")
    return [_obj(col, name, mb)]


def box_prop(col, name, lo, hi, mat):
    mb = MeshBuilder([mat])
    mb.box(lo, hi, mat)
    return [_obj(col, name, mb)]


def table(col, name, x, y, along_x=True):
    """Folding table of the security post (photo 10): white top, black steel legs."""
    mb = MeshBuilder(["Kit_Panel_Grey", "Kit_Frame"])
    hx, hy = (0.60, 0.35) if along_x else (0.35, 0.60)
    mb.box((x - hx, y - hy, 0.71), (x + hx, y + hy, 0.74), "Kit_Panel_Grey")
    for sx in (-1, 1):
        for sy in (-1, 1):
            lx, ly = x + sx * (hx - 0.04), y + sy * (hy - 0.04)
            mb.box((lx - 0.015, ly - 0.015, 0.0), (lx + 0.015, ly + 0.015, 0.71), "Kit_Frame")
    return [_obj(col, name, mb)]


def chair(col, name, x, y, facing):
    """Stacking chair: black tube frame, dark seat and back."""
    fx, fy = facing
    mb = MeshBuilder(["Kit_Frame"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            lx, ly = x + sx * 0.2, y + sy * 0.2
            mb.box((lx - 0.012, ly - 0.012, 0.0), (lx + 0.012, ly + 0.012, 0.44), "Kit_Frame")
    mb.box((x - 0.23, y - 0.23, 0.44), (x + 0.23, y + 0.23, 0.48), "Kit_Frame")
    bx, by = x - fx * 0.21, y - fy * 0.21
    hx, hy = (0.02, 0.22) if fx else (0.22, 0.02)
    mb.box((bx - hx, by - hy, 0.60), (bx + hx, by + hy, 0.86), "Kit_Frame")
    return [_obj(col, name, mb)]


# ---- Stairs ----------------------------------------------------------------------------

def stair_flight(col, name, x0, dirx, y0, y1, z0, risers=12):
    """Straight concrete flight (photo 10): tiled treads and risers, plaster sides and
    soffit, grey terrazzo nosing on every tread. Climbs along dirx from its first riser
    at x0; the last riser lands on a separate landing / upper floor."""
    r, t = RISE, TREAD
    cos_a = t / math.hypot(t, r)
    waist = 0.16 / cos_a
    prof = [(0.0, z0)]
    for i in range(risers):
        prof.append((i * t, z0 + (i + 1) * r))
        if i < risers - 1:
            prof.append(((i + 1) * t, z0 + (i + 1) * r))
    t_end = (risers - 1) * t
    z_top = z0 + risers * r
    t_b = max(0.02, (waist - r) * t / r)
    prof += [(t_end, z_top - waist), (t_b, z0)]
    steps_edges = 2 * risers - 1           # edges up to the top of the last riser

    mb = MeshBuilder(["Kit_Tile_Floor", "Kit_Wall_White"])
    mb.transform = lambda q: (x0 + dirx * q[0], q[2], q[1])
    n = len(prof)
    for i in range(n):
        (ta, za), (tb, zb) = prof[i], prof[(i + 1) % n]
        mat = "Kit_Tile_Floor" if i < steps_edges else "Kit_Wall_White"
        mb.face([(ta, za, y0), (tb, zb, y0), (tb, zb, y1), (ta, za, y1)], mat)
    mb.face([(tt, zz, y0) for tt, zz in prof], "Kit_Wall_White")
    mb.face([(tt, zz, y1) for tt, zz in reversed(prof)], "Kit_Wall_White")
    mb.transform = None
    out = [_obj(col, name, mb)]

    nose = MeshBuilder(["Kit_Nosing"])
    for i in range(1, risers):
        tt = i * t - t
        z = z0 + i * r
        xa, xb = x0 + dirx * tt, x0 + dirx * (tt + 0.06)
        nose.box((min(xa, xb), y0 + 0.015, z), (max(xa, xb), y1 - 0.015, z + 0.004), "Kit_Nosing")
    out.append(_obj(col, name + "_Nosing", nose))
    return out


def sloped_wall(col, name, x0, x1, y0, y1, z_at, base_at=None, mat="Kit_Wall_White"):
    """Wall whose top follows z_at(x) (a stair parapet); bottom at base_at(x) or the floor."""
    mb = MeshBuilder([mat])
    xa, xb = min(x0, x1), max(x0, x1)
    top = [(xa, z_at(xa)), (xb, z_at(xb))]
    bot = [(xb, base_at(xb) if base_at else 0.0), (xa, base_at(xa) if base_at else 0.0)]
    prof = top + bot
    mb.transform = lambda q: (q[0], q[2], q[1])
    mb.prism(prof, y0, y1, mat, bottom=True)
    mb.transform = None
    return [_obj(col, name, mb)]


def rail(mb, pts, r=0.024, posts_every=1.1, post_drop=0.07):
    """Red steel handrail tube on short posts (photos 10, 21). Tapered start = closed end."""
    first, second = pts[0], pts[1]
    dx = [second[k] - first[k] for k in range(3)]
    ln = math.sqrt(sum(c * c for c in dx))
    tip = tuple(first[k] - dx[k] / ln * 0.01 for k in range(3))
    mb.tube([tip] + pts, [0.0] + [r] * len(pts), "Kit_Metal_Red", segs=8)
    for a, b in zip(pts, pts[1:]):
        seg = math.dist(a, b)
        count = max(1, int(seg / posts_every))
        for k in range(count + 1):
            f = k / count
            p = tuple(a[j] + (b[j] - a[j]) * f for j in range(3))
            mb.cylinder((p[0], p[1], p[2] - post_drop), 0.012, 0.012, post_drop - 0.005, "Kit_Metal_Red",
                        segs=6, top=False)
