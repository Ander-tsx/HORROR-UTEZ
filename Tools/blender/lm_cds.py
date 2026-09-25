"""
CDS south facade (the wall facing CECADEC's north entrance) and what joins it to the
plaza. It lives in the CECADEC_North landmark because the plaza is one place; it is posed
on CDS's kit wall.

v2 (2026-09-16), from the four photos in docs/map/reference/cds-south/ taken from
CECADEC's main door. What changed against v1, which had been read off two distant shots:

* The red band over the storefront is **not a canvas awning**. It is a rigid painted
  visor: a shallow sloping top that leaves the wall just under the window sill, a deep
  vertical fascia hanging off its front edge, and a dark soffit going back to the wall.
  Its front fascia carries a vertical joint every 1.2 m or so (T_Canvas_Red already has
  exactly that pattern, so the material name stays even though it is not canvas).
* The parapet is capped with a **cream coping**, and the pilasters stop 1.0 m under it —
  the red fascia runs on past them to the coping.
* Heights re-measured against the 8.5 m parapet (photo 2, 50.2 px per metre, +-0.15 m):
  storefront head 2.73, visor eaves 4.15, visor at the wall 4.76, window sill 4.92,
  window head 6.31, pilaster top 7.47, coping 8.28, parapet 8.50.
* Added: the entrance doors, the cream plinth under the storefront, the roof-top air
  handling unit, the green exit sign, and the grass strip with its volcanic-stone kerb
  that runs the whole length of the facade (v1 only had it under the east bays).

CDS is turned 8.5 degrees against CECADEC (UtezDimensions: CECADEC -13, CDS -4.5). In
this file's frame (CECADEC's: X east, Y north, origin on its main door threshold) CDS's
south wall's outer face is centred on CENTRE and runs along AXIS for 31 m. Facade frame:
  u  along the wall from its west end,  v  out from the wall towards CECADEC,  z  up.
"""

import math

from geolib import MeshBuilder, make_object, rect
import lm_plants
import lm_props

ANGLE = math.radians(-8.5)
AXIS = (math.cos(ANGLE), math.sin(ANGLE))
OUT = (AXIS[1], -AXIS[0])            # from the wall towards CECADEC
# UtezDimensions.Cds, outer face of the south wall, in CECADEC's frame. Recomputed on
# 2026-09-16 when the footprint moved 4 m west: campus (-4, 21) -> here.
CENTRE = (6.399, 19.162)
LENGTH = 31.0
WEST_END = (CENTRE[0] - AXIS[0] * LENGTH / 2, CENTRE[1] - AXIS[1] * LENGTH / 2)

PIL_W, PIL_PROUD = 0.75, 0.35
BAY_W = [4.2, 4.2, 4.2, 4.2, 4.2, 4.6]
RED_PANEL_BAY = 4                    # zero-based: the fifth bay (photo 2)
DOOR_BAY = 2                         # the entrance sits at the east end of the third bay
VISOR_BAYS = range(6)                # every bay carries the visor (photos 1, 2)

PLINTH = 0.25
STORE_TOP = 2.73                     # storefront head = the visor's soffit
EAVE = 4.15                          # top of the visor's front fascia
VISOR_TOP = 4.76                     # where the visor meets the wall
SILL = 4.92
WIN_TOP = 6.31
PIL_TOP = 7.47
COPING = 8.28
TOP = 8.50
VISOR_OUT = 1.40                     # how far the visor stands off the wall
BAR = 0.05

STRIP_Z = 0.212                      # 1 cm over the plaza's entrance level (0.20)
GRASS_V = 1.55                       # width of the grass strip at the foot of the facade
BED_Z, KERB_TOP, KERB_W = 0.36, 0.45, 0.30


def P(u, v, z=0.0):
    return (WEST_END[0] + AXIS[0] * u + OUT[0] * v, WEST_END[1] + AXIS[1] * u + OUT[1] * v, z)


def _plan_prism(mb, poly, z0, z1, mat, side_mat=None):
    """Prism with its outline in the facade's (u, v) plan."""
    mb.transform = lambda q: P(q[0], q[1], q[2])
    mb.prism(poly, z0, z1, mat, side_mat=side_mat, bottom=True)
    mb.transform = None


def _section(mb, profile, u0, u1, mat):
    """Prism whose outline is given in the facade's (v, z) section, run from u0 to u1."""
    mb.transform = lambda q: P(q[2], q[0], q[1])
    mb.prism(profile, u0, u1, mat, bottom=True)
    mb.transform = None


def _box(mb, u0, u1, v0, v1, z0, z1, mat):
    _plan_prism(mb, rect(u0, v0, u1, v1), z0, z1, mat)


def _panel(mb, outline, holes, v0, v1, mat):
    """Panel in the facade plane: outline and holes as (u, z), v0..v1 thick."""
    mb.transform = lambda q: P(q[0], q[2], q[1])
    mb.prism(outline, v0, v1, mat, bottom=True, holes=holes)
    mb.transform = None


def _pane(mb, u0, u1, z0, z1, v, mat):
    mb.pane([P(u0, v, z0), P(u1, v, z0), P(u1, v, z1), P(u0, v, z1)], mat)


def layout():
    """u ranges of the pilasters and the bays, stretched to exactly LENGTH."""
    total = PIL_W * (len(BAY_W) + 1) + sum(BAY_W)
    k = LENGTH / total
    pils, bays, u = [], [], 0.0
    for w in BAY_W:
        pils.append((u, u + PIL_W * k))
        u += PIL_W * k
        bays.append((u, u + w * k))
        u += w * k
    pils.append((u, u + PIL_W * k))
    return pils, bays


def _glazing(col, name, u0, u1, z0, z1, rail=None, light=1.05):
    """Aluminium glazing in front of the kit wall: black frame with lights, dark tinted
    glass, and a dark backing panel that stands in for the unmodelled room behind."""
    n = max(2, round((u1 - u0) / light))
    w = (u1 - u0) / n
    holes = []
    for i in range(n):
        a = u0 + i * w + (BAR if i == 0 else BAR / 2)
        b = u0 + (i + 1) * w - (BAR if i == n - 1 else BAR / 2)
        if rail:
            holes += [rect(a, z0 + BAR, b, rail - BAR / 2), rect(a, rail + BAR / 2, b, z1 - BAR)]
        else:
            holes.append(rect(a, z0 + BAR, b, z1 - BAR))
    frame = MeshBuilder(["Kit_Frame"])
    _panel(frame, rect(u0, z0, u1, z1), holes, 0.03, 0.09, "Kit_Frame")
    panes = MeshBuilder(["Kit_Glass_Tinted"])
    for h in holes:
        _pane(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, 0.06, "Kit_Glass_Tinted")
    back = MeshBuilder(["Kit_Frame"])
    _box(back, u0, u1, 0.0, 0.02, z0, z1, "Kit_Frame")
    return [make_object(name + "_Frame", frame, col), make_object(name + "_Pane", panes, col),
            make_object(name + "_Back", back, col)]


def _entrance(col, name, u0, u1):
    """Double glass doors in the storefront: fixed side lights, two leaves, a transom
    over them. The leaves are their own objects, hinged and closed (photo 1)."""
    made = []
    leaf = 1.05
    mid = (u0 + u1) / 2
    d0, d1 = mid - leaf, mid + leaf
    head = 2.35
    holes = [rect(u0 + BAR, PLINTH + BAR, d0 - BAR / 2, STORE_TOP - BAR),
             rect(d1 + BAR / 2, PLINTH + BAR, u1 - BAR, STORE_TOP - BAR),
             rect(d0 + BAR / 2, head + BAR / 2, d1 - BAR / 2, STORE_TOP - BAR)]
    frame = MeshBuilder(["Kit_Frame"])
    _panel(frame, rect(u0, PLINTH, u1, STORE_TOP),
           holes + [rect(d0 + BAR / 2, PLINTH, d1 - BAR / 2, head - BAR / 2)], 0.03, 0.11, "Kit_Frame")
    made.append(make_object(name + "_Frame", frame, col))
    panes = MeshBuilder(["Kit_Glass_Tinted"])
    for h in holes:
        _pane(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, 0.07,
              "Kit_Glass_Tinted")
    made.append(make_object(name + "_Pane", panes, col))
    for tag, hinge, free in (("West", d0 + BAR, mid - 0.01), ("East", d1 - BAR, mid + 0.01)):
        a, b = sorted((hinge, free))
        mb = MeshBuilder(["Kit_Frame", "Kit_Glass_Tinted", "Kit_Metal_White"])
        _panel(mb, rect(a, PLINTH + 0.02, b, head - BAR / 2),
               [rect(a + 0.06, PLINTH + 0.10, b - 0.06, head - 0.12)], 0.055, 0.085, "Kit_Frame")
        _pane(mb, a + 0.05, b - 0.05, PLINTH + 0.09, head - 0.11, 0.07, "Kit_Glass_Tinted")
        pull = free + (0.10 if hinge < free else -0.10)
        _box(mb, pull - 0.02, pull + 0.02, 0.02, 0.05, 0.95, 1.20, "Kit_Metal_White")
        made.append(make_object("Door_CDS" + tag, mb, col, pivot=P(hinge, 0.07, 0.0)))
    return made


def _visor(col, name, u0, u1):
    """The rigid red visor over the storefront (photos 1, 2): a sloping top from the wall
    down to the eaves, a deep front fascia, and the soffit back to the wall. One closed
    prism per bay, so the ends read as cut panels the way they do on site."""
    profile = [(0.0, VISOR_TOP), (VISOR_OUT, EAVE), (VISOR_OUT, STORE_TOP),
               (VISOR_OUT - 0.12, STORE_TOP), (VISOR_OUT - 0.12, STORE_TOP + 0.10),
               (0.0, STORE_TOP + 0.28)]
    mb = MeshBuilder(["Kit_Canvas_Red"])
    _section(mb, profile, u0 + 0.02, u1 - 0.02, "Kit_Canvas_Red")
    return [make_object(name, mb, col)]


def facade(col):
    made = []
    pils, bays = layout()
    for i, (a, b) in enumerate(pils, 1):
        mb = MeshBuilder(["Kit_Trim"])
        _box(mb, a, b, 0.0, PIL_PROUD + 0.1, 0.0, PIL_TOP, "Kit_Trim")
        made.append(make_object(f"CDS_Pilaster{i}", mb, col))

    # Red wall: the band between the window head and the coping, and the sill band under
    # the ribbon windows that the visor hangs off.
    fascia = MeshBuilder(["Kit_Wall_Red"])
    _box(fascia, 0.0, LENGTH, 0.0, PIL_PROUD + 0.07, WIN_TOP, COPING, "Kit_Wall_Red")
    made.append(make_object("CDS_Fascia", fascia, col))
    cap = MeshBuilder(["Kit_Trim"])
    _box(cap, -0.05, LENGTH + 0.05, 0.0, PIL_PROUD + 0.14, COPING, TOP, "Kit_Trim")
    made.append(make_object("CDS_Coping", cap, col))
    for i, (a, b) in enumerate(bays):
        tag = f"CDS_Bay{i + 1}"
        # Red spandrel between the visor's head and the window sill.
        span = MeshBuilder(["Kit_Wall_Red"])
        _box(span, a, b, 0.0, 0.25, VISOR_TOP - 0.05, SILL, "Kit_Wall_Red")
        made.append(make_object(tag + "_Spandrel", span, col))
        plinth = MeshBuilder(["Kit_Trim"])
        _box(plinth, a, b, 0.0, 0.16, 0.0, PLINTH, "Kit_Trim")
        made.append(make_object(tag + "_Plinth", plinth, col))

        if i == RED_PANEL_BAY:
            panel = MeshBuilder(["Kit_Wall_Red"])
            _box(panel, a, b, 0.0, 0.08, PLINTH, STORE_TOP, "Kit_Wall_Red")
            made.append(make_object(tag + "_Panel", panel, col))
        elif i == DOOR_BAY:
            made += _glazing(col, tag + "_Store", a, b - 2.9, PLINTH, STORE_TOP, rail=2.3)
            made += _entrance(col, "CDS_Entrance", b - 2.9, b)
        else:
            made += _glazing(col, tag + "_Store", a, b, PLINTH, STORE_TOP, rail=2.3)
        made += _glazing(col, tag + "_Window", a, b, SILL, WIN_TOP, light=1.0)
        if i in VISOR_BAYS:
            made += _visor(col, tag + "_Visor", a, b)

    # Roof-top air handling unit, just showing over the parapet (photos 1, 2).
    ahu = MeshBuilder(["Kit_Metal_Grey"])
    _box(ahu, 14.6, 16.4, -2.6, -1.4, TOP - 0.55, TOP + 0.45, "Kit_Metal_Grey")
    made.append(make_object("CDS_RoofUnit", ahu, col))

    # Green running-man sign over the entrance bay's pilaster (photo 2).
    sign = MeshBuilder(["Kit_Paint_Green"])
    u = pils[DOOR_BAY + 1][0] + PIL_W / 2
    _box(sign, u - 0.20, u + 0.20, PIL_PROUD + 0.106, PIL_PROUD + 0.126, 2.95, 3.20,
         "Kit_Paint_Green")
    made.append(make_object("Decal_CDS_Exit", sign, col))
    return made


def front(col):
    """What lies between the facade and the plaza (photos 1, 2): a grass strip behind a
    volcanic-stone kerb running the whole length, the paved strip in front of it, the
    magnolia bed at the east end, flagpoles, and the red lamp post at the foot of the hill."""
    made = []
    strip = MeshBuilder(["Kit_Slab"])
    _plan_prism(strip, rect(0.0, GRASS_V, LENGTH, 3.4), 0.0, STRIP_Z, "Kit_Slab")
    made.append(make_object("Paving_CDSStrip", strip, col))

    # Grass at the foot of the wall, held by a stone kerb; it steps up over the paving.
    lawn = MeshBuilder(["Kit_Grass", "Kit_Stone"])
    _plan_prism(lawn, rect(0.0, 0.02, LENGTH, GRASS_V), 0.0, BED_Z, "Kit_Grass", side_mat="Kit_Stone")
    made.append(make_object("Planter_CDS_Lawn", lawn, col))
    kerb = MeshBuilder(["Kit_Stone"])
    path = [P(-0.1, GRASS_V), P(LENGTH + 0.1, GRASS_V)]
    kerb.sweep([(p[0], p[1]) for p in path], KERB_W, 0.0, KERB_TOP, "Kit_Stone")
    made.append(make_object("Planter_CDS_Kerb", kerb, col))

    x, y, _ = P(27.8, 0.95)                    # in front of the wide east bay (photo 2)
    made += lm_plants.tree(col, "Tree_CDSMagnolia", x, y, BED_Z, height=6.8, fork=1.5, crown=3.4, seed=31)
    for i, (u, v, r) in enumerate(((22.0, 0.9, 0.55), (28.9, 0.8, 0.6), (24.0, 0.6, 0.45),
                                   (6.4, 0.8, 0.5), (12.1, 0.7, 0.45)), 1):
        x, y, _ = P(u, v)
        made.append(lm_plants.shrub(col, f"Plant_CDS_Shrub{i}", x, y, BED_Z, radius=r, seed=120 + i))
    for i, (u, v) in enumerate(((21.2, 1.2), (27.4, 1.3), (30.0, 1.0), (3.2, 1.1), (16.8, 1.2)), 1):
        x, y, _ = P(u, v)
        made.append(lm_plants.tuft(col, f"Plant_CDS_Tuft{i}", x, y, BED_Z, size=0.7, seed=140 + i))

    # Photo 2: a line of white poles stands on the paved strip in front of the facade.
    for i, u in enumerate((11.4, 21.2, 25.6), 1):
        x, y, _ = P(u, 2.6)
        made.append(lm_props.flagpole(col, f"Flagpole_CDS{i}", x, y, STRIP_Z, height=8.4))
    made.append(lm_props.lamp_post(col, "Lamp_CDSWest", -10.6, 18.2, 0.2, height=5.2))
    return made
