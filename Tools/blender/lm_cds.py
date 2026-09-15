"""
CDS south facade (the wall facing CECADEC's north entrance) and what joins it to the
plaza: photos 33 and 34 (docs/map/reference/cecadec-cds-plaza/). It lives in the
CECADEC_North landmark because the plaza is one place; it is posed on CDS's kit wall.

CDS is turned 8.5 degrees against CECADEC (UtezDimensions: CECADEC -13, CDS -4.5). In
this file's frame (CECADEC's: X east, Y north, origin on its main door threshold) CDS's
south wall's outer face is centred on CENTRE and runs along AXIS for 31 m. Facade frame:
  u  along the wall from its west end,  v  out from the wall towards CECADEC,  z  up.

Facade, west to east: 7 stucco pilasters and 6 bays. Bays 1-5 carry a red canvas awning
over dark aluminium storefront glazing (bay 5 has a red panel instead of glass); bay 6,
wider, is plain glazing behind the magnolia. The awnings rise straight to a ribbon window
per bay; above it the deep red fascia, with the pilasters stopping partway up it. In front: a raised paved strip, a lawn planter with a
stone kerb along the eastern bays (magnolia, shrubs, a flagpole), a second flagpole on the
strip, and the red lamp post at the foot of the hill (photo 33).
"""

import math

from geolib import MeshBuilder, make_object, rect
import lm_plants
import lm_props

ANGLE = math.radians(-8.5)
AXIS = (math.cos(ANGLE), math.sin(ANGLE))
OUT = (AXIS[1], -AXIS[0])            # from the wall towards CECADEC
CENTRE = (10.30, 18.26)              # UtezDimensions.Cds, outer face, in CECADEC's frame
LENGTH = 31.0
WEST_END = (CENTRE[0] - AXIS[0] * LENGTH / 2, CENTRE[1] - AXIS[1] * LENGTH / 2)

PIL_W, PIL_PROUD = 0.75, 0.35
BAY_W = [4.2, 4.2, 4.2, 4.2, 4.2, 4.6]
RED_PANEL_BAY = 4                    # zero-based: the fifth bay (photo 34)
AWNING_BAYS = range(5)
# The awning runs from the storefront head right up to the ribbon windows (photos 33, 34):
# no red band shows between them; the pilasters stop partway up the fascia.
STORE_TOP, AWN_TOP, WIN_TOP, PIL_TOP, TOP = 2.6, 4.5, 5.9, 6.9, 8.5
SPAN_TOP = AWN_TOP
BAR = 0.05
STRIP_Z = 0.212                      # 1 cm over the plaza's entrance level (0.20)
PLANTER_U = (20.25, 30.9)
BED_Z, KERB_TOP, KERB_W = 0.36, 0.45, 0.30


def P(u, v, z=0.0):
    return (WEST_END[0] + AXIS[0] * u + OUT[0] * v, WEST_END[1] + AXIS[1] * u + OUT[1] * v, z)


def _plan_prism(mb, poly, z0, z1, mat, side_mat=None):
    """Prism with its outline in the facade's (u, v) plan."""
    mb.transform = lambda q: P(q[0], q[1], q[2])
    mb.prism(poly, z0, z1, mat, side_mat=side_mat, bottom=True)
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


def _awning(col, name, u0, u1, rx=1.0, rz=1.55, t=0.025, segs=8):
    """Tall quarter-elliptic canvas awning: the curve leaves the wall at AWN_TOP and comes
    down to a straight valance whose hem is at STORE_TOP; closed ends."""
    cz = AWN_TOP - rz
    ang = [math.pi / 2 * k / segs for k in range(segs + 1)]
    outer = [(0.04 + math.sin(a) * rx, cz + math.cos(a) * rz) for a in ang]
    inner = [(0.04 + math.sin(a) * (rx - t), cz + math.cos(a) * (rz - t)) for a in ang]
    r = rx
    mb = MeshBuilder(["Kit_Canvas_Red"])
    mb.transform = lambda q: P(q[2], q[0], q[1])        # (v, z, u) -> world
    mb.prism(outer + inner[::-1], u0 + 0.06, u1 - 0.06, "Kit_Canvas_Red", bottom=True)
    fan = [(0.04, cz)] + outer
    for ua, ub in ((u0 + 0.04, u0 + 0.06), (u1 - 0.06, u1 - 0.04)):
        mb.prism(fan, ua, ub, "Kit_Canvas_Red", bottom=True)
    mb.transform = None
    _box(mb, u0 + 0.04, u1 - 0.04, 0.04 + r - t, 0.04 + r, STORE_TOP, cz, "Kit_Canvas_Red")
    return [make_object(name, mb, col)]


def facade(col):
    made = []
    pils, bays = layout()
    for i, (a, b) in enumerate(pils, 1):
        mb = MeshBuilder(["Kit_Trim"])
        _box(mb, a, b, 0.0, PIL_PROUD + 0.1, 0.0, PIL_TOP, "Kit_Trim")
        made.append(make_object(f"CDS_Pilaster{i}", mb, col))
    fascia = MeshBuilder(["Kit_Wall_Red"])
    _box(fascia, 0.0, LENGTH, 0.0, PIL_PROUD + 0.07, WIN_TOP, TOP, "Kit_Wall_Red")
    made.append(make_object("CDS_Fascia", fascia, col))

    for i, (a, b) in enumerate(bays):
        tag = f"CDS_Bay{i + 1}"
        span = MeshBuilder(["Kit_Wall_Red"])
        _box(span, a, b, 0.0, 0.25, STORE_TOP, SPAN_TOP, "Kit_Wall_Red")
        made.append(make_object(tag + "_Spandrel", span, col))
        if i == RED_PANEL_BAY:
            panel = MeshBuilder(["Kit_Wall_Red"])
            _box(panel, a, b, 0.0, 0.08, 0.0, STORE_TOP, "Kit_Wall_Red")
            made.append(make_object(tag + "_Panel", panel, col))
        else:
            made += _glazing(col, tag + "_Store", a, b, 0.0, STORE_TOP, rail=2.3)
        made += _glazing(col, tag + "_Window", a, b, SPAN_TOP, WIN_TOP, light=1.0)
        if i in AWNING_BAYS:
            made += _awning(col, tag + "_Awning", a, b)
    return made


def front(col):
    made = []
    u_split = PLANTER_U[0] - 0.05
    strip = MeshBuilder(["Kit_Slab"])
    _plan_prism(strip, [(0.0, 0.0), (u_split, 0.0), (u_split, 2.4), (LENGTH, 2.4), (LENGTH, 3.2), (0.0, 3.2)],
                0.0, STRIP_Z, "Kit_Slab")
    made.append(make_object("Paving_CDSStrip", strip, col))

    bed = MeshBuilder(["Kit_Grass", "Kit_Stone"])
    _plan_prism(bed, rect(PLANTER_U[0], 0.02, PLANTER_U[1], 2.3), 0.0, BED_Z, "Kit_Grass", side_mat="Kit_Stone")
    made.append(make_object("Planter_CDS_Bed", bed, col))
    kerb = MeshBuilder(["Kit_Stone"])
    path = [P(u_split, 0.0), P(u_split, 2.4), P(PLANTER_U[1] + 0.05, 2.4), P(PLANTER_U[1] + 0.05, 0.0)]
    kerb.sweep([(p[0], p[1]) for p in path], KERB_W, 0.0, KERB_TOP, "Kit_Stone")
    made.append(make_object("Planter_CDS_Kerb", kerb, col))

    x, y, _ = P(27.8, 1.25)                    # in front of the wide east bay (photo 34)
    made += lm_plants.tree(col, "Tree_CDSMagnolia", x, y, BED_Z, height=6.8, fork=1.5, crown=3.4, seed=31)
    for i, (u, v, r) in enumerate(((22.0, 1.2, 0.55), (28.9, 1.0, 0.6), (24.0, 0.7, 0.45)), 1):
        x, y, _ = P(u, v)
        made.append(lm_plants.shrub(col, f"Plant_CDS_Shrub{i}", x, y, BED_Z, radius=r, seed=120 + i))
    for i, (u, v) in enumerate(((21.2, 1.6), (27.4, 1.9), (30.0, 1.4)), 1):
        x, y, _ = P(u, v)
        made.append(lm_plants.tuft(col, f"Plant_CDS_Tuft{i}", x, y, BED_Z, size=0.7, seed=140 + i))

    # Photo 34: one pole at the planter's front edge by the red-panel bay, one past
    # CDS's east corner on the plaza.
    x, y, _ = P(21.2, 2.1)
    made.append(lm_props.flagpole(col, "Flagpole_CDS1", x, y, BED_Z))
    x, y, _ = P(33.0, 1.8)
    made.append(lm_props.flagpole(col, "Flagpole_CDS2", x, y, 0.2))
    made.append(lm_props.lamp_post(col, "Lamp_CDSWest", -10.6, 18.2, 0.2, height=5.2))
    return made
