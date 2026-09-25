"""
The east side of the plaza in front of CECADEC north: the auditorium's west front, the
covered walkway that joins CDS to it, and the ground and planting between them.

From docs/map/reference/cds-south/ (four photos taken from CECADEC's main door, 2026-09-16):
photo 3 looks east-north-east at the auditorium across the dark aggregate plaza, photo 4
looks at its entrance corner with CECADEC's own blank red wall on the right.

The auditorium reads as one storey of cream stucco:

* a **colonnade** down its whole west front — square fins standing proud of the wall, and
  between every pair a tall narrow window slot with a red panel over and under it;
* the **entrance**, a bay set back behind the fin line with a dark glazed door;
* a **corner pavilion** at the south end, stepping out and up, carrying the red twin-bar
  bracket with its lamp that both photos show;
* a plain **parapet band** over a horizontal reveal, with floodlights on it;
* a **volcanic-stone plinth** at the base, the same stone as every kerb on the campus.

Fin rhythm and the storey height are estimated (+-0.3 m): the photos are dusk shots at a
glancing angle and nothing in them gives a clean scale. The footprint is not: 10 x 20 m
at -4.5 degrees, centred on campus (25, 7), measured (UtezDimensions.Auditorium).

Frame, as lm_cds.py: CECADEC's (X east, Y north, origin on its main door threshold).
The auditorium's own frame is
  u  south along its west wall from the north-west corner,
  v  west out of that wall, towards the plaza,
  z  up.
"""

import math

from geolib import MeshBuilder, make_empty, make_object, rect
import lm_plants
import lm_props

# West wall's outer face, in CECADEC's frame: the campus footprint turned into this frame
# the same way lm_cds.CENTRE was (CECADEC -13 deg, auditorium -4.5, so -8.5 between them).
NW_CORNER = (29.664, 20.510)
U_DIR = (-0.14776, -0.98905)         # along the wall, running south
V_DIR = (-0.98905, 0.14776)          # out of the wall, towards the plaza
WALL_LEN = 20.0
SOUTH_LEN = 10.0                     # the south face, seen at a glancing angle in photo 3

PLINTH_Z = 0.55
WALL_TOP = 3.60                      # horizontal reveal: wall below, parapet band above
PARAPET = 4.55
PAV_TOP = 5.35                       # the corner pavilion's own parapet, over PARAPET
PAV_U = (16.2, 20.0)                 # the pavilion's stretch of the west wall
PAV_PROUD = 0.62                     # more than the parapet band, so the block reads

# The fins were 0.52 x 0.45 proud with a 1.70 m lawn at their foot, which left barely two
# metres of walkway in the passage between CDS and the auditorium (the user's image 9).
# Slimmer fins, less proud, and the lawn pulled back give back about 1.8 m.
FIN_W, FIN_GAP, FIN_PROUD = 0.46, 0.54, 0.32
FIN_RUN = (0.0, 13.0)                # colonnade, north of the entrance
SLOT_Z = (0.90, 3.10)                # glazed part of each slot; red panels above and below
DOOR_U = (13.0, 16.2)
DOOR_SETBACK = 0.55

GRASS_V = 0.90
LAWN_U = (2.0, 21.5)                 # the lawn stops short of the north end: that is the
                                     # mouth of the passage and it has to stay clear
BED_Z = 0.34
KERB_W = 0.30
KERB_TOP = 0.45
GROUND_Z = 0.03                      # just under the plaza's own slabs (lm_plaza.LOW_Z)


def P(u, v, z=0.0):
    return (NW_CORNER[0] + U_DIR[0] * u + V_DIR[0] * v,
            NW_CORNER[1] + U_DIR[1] * u + V_DIR[1] * v, z)


def S(t, v, z=0.0):
    """Point on the south face: t east from the south-west corner, v out of that face."""
    base = P(WALL_LEN, 0.0, 0.0)
    e = (-V_DIR[0], -V_DIR[1])       # east along the south wall
    n = (-U_DIR[0], -U_DIR[1])       # north, into the building; -n is out of the south face
    return (base[0] + e[0] * t - n[0] * v, base[1] + e[1] * t - n[1] * v, z)


def _plan(mb, poly, z0, z1, mat, side_mat=None, frame=P):
    mb.transform = lambda q: frame(q[0], q[1], q[2])
    mb.prism(poly, z0, z1, mat, side_mat=side_mat, bottom=True)
    mb.transform = None


def _box(mb, u0, u1, v0, v1, z0, z1, mat, frame=P):
    _plan(mb, rect(u0, v0, u1, v1), z0, z1, mat, frame=frame)


def _panel(mb, outline, holes, v0, v1, mat, frame=P):
    """Panel in the wall plane: outline and holes given as (u, z), v0..v1 thick."""
    mb.transform = lambda q: frame(q[0], q[2], q[1])
    mb.prism(outline, v0, v1, mat, bottom=True, holes=holes)
    mb.transform = None


def _pane(mb, u0, u1, z0, z1, v, mat, frame=P):
    mb.pane([frame(u0, v, z0), frame(u1, v, z0), frame(u1, v, z1), frame(u0, v, z1)], mat)


def fins():
    """(u0, u1) of every fin in the colonnade, and the slot after each one."""
    out, u = [], FIN_RUN[0]
    while u + FIN_W + FIN_GAP <= FIN_RUN[1] + 1e-6:
        out.append((u, u + FIN_W))
        u += FIN_W + FIN_GAP
    return out


# ---- Auditorium ------------------------------------------------------------------------

def auditorium(col):
    made = []
    fin_list = fins()

    # The wall itself, in three pieces so no single object spans the whole front.
    for i, (a, b) in enumerate(((0.0, 7.0), (7.0, 13.6), (13.6, WALL_LEN)), 1):
        mb = MeshBuilder(["Kit_Trim"])
        _box(mb, a, b, 0.0, 0.18, 0.0, WALL_TOP, "Kit_Trim")
        made.append(make_object(f"Aud_West_Wall{i}", mb, col))
    band = MeshBuilder(["Kit_Trim"])
    _box(band, -0.05, PAV_U[0], 0.0, FIN_PROUD + 0.06, WALL_TOP + 0.06, PARAPET, "Kit_Trim")
    made.append(make_object("Aud_West_Parapet", band, col))

    # Colonnade: a fin, then the slot behind the next gap.
    for i, (a, b) in enumerate(fin_list, 1):
        mb = MeshBuilder(["Kit_Trim"])
        _box(mb, a, b, 0.0, FIN_PROUD, 0.0, WALL_TOP + 0.04, "Kit_Trim")
        made.append(make_object(f"Aud_Fin{i}", mb, col))
    slots = MeshBuilder(["Kit_Wall_Red", "Kit_Frame"])
    glass = MeshBuilder(["Kit_Glass_Tinted"])
    for i, (a, b) in enumerate(fin_list[:-1], 1):
        g0, g1 = b + 0.02, b + FIN_GAP - 0.02
        # The wall runs solid behind all this, so the slot is built in front of its face
        # (v = 0.18) and stays well inside the fins (v = FIN_PROUD): a 0.21 m reveal.
        _box(slots, g0, g1, 0.18, 0.25, 0.10, SLOT_Z[0], "Kit_Wall_Red")
        _box(slots, g0, g1, 0.18, 0.25, SLOT_Z[1], WALL_TOP, "Kit_Wall_Red")
        _box(slots, g0, g1, 0.18, 0.21, SLOT_Z[0], SLOT_Z[1], "Kit_Frame")
        _pane(glass, g0, g1, SLOT_Z[0] + 0.01, SLOT_Z[1] - 0.01, 0.23, "Kit_Glass_Tinted")
    made.append(make_object("Aud_SlotPanels", slots, col))
    made.append(make_object("Aud_SlotGlass", glass, col))

    # Entrance: the wall steps back, a dark glazed door with a transom over it.
    d0, d1 = DOOR_U
    reveal = MeshBuilder(["Kit_Trim"])
    _box(reveal, d0, d0 + 0.62, 0.0, DOOR_SETBACK, 0.0, WALL_TOP, "Kit_Trim")
    _box(reveal, d1 - 0.62, d1, 0.0, DOOR_SETBACK, 0.0, WALL_TOP, "Kit_Trim")
    made.append(make_object("Aud_EntranceReveal", reveal, col))
    w0, w1 = d0 + 0.62, d1 - 0.62
    head = 2.55
    frame = MeshBuilder(["Kit_Frame"])
    _panel(frame, rect(w0, 0.0, w1, WALL_TOP),
           [rect(w0 + 0.06, 0.12, w1 - 0.06, head), rect(w0 + 0.06, head + 0.08, w1 - 0.06, WALL_TOP - 0.10)],
           DOOR_SETBACK - 0.08, DOOR_SETBACK, "Kit_Frame")
    made.append(make_object("Aud_EntranceFrame", frame, col))
    panes = MeshBuilder(["Kit_Glass_Tinted"])
    _pane(panes, w0 + 0.05, w1 - 0.05, 0.11, head + 0.01, DOOR_SETBACK - 0.04, "Kit_Glass_Tinted")
    _pane(panes, w0 + 0.05, w1 - 0.05, head + 0.07, WALL_TOP - 0.09, DOOR_SETBACK - 0.04,
          "Kit_Glass_Tinted")
    made.append(make_object("Aud_EntranceGlass", panes, col))

    # Corner pavilion: proud of the wall, taller parapet, wrapping the south-west corner.
    p0, p1 = PAV_U
    pav = MeshBuilder(["Kit_Trim"])
    _box(pav, p0, p1, 0.0, PAV_PROUD, 0.0, WALL_TOP + 0.06, "Kit_Trim")
    _box(pav, p0 - 0.06, p1, 0.0, PAV_PROUD + 0.08, WALL_TOP + 0.06, PAV_TOP, "Kit_Trim")
    made.append(make_object("Aud_Pavilion", pav, col))
    wrap = MeshBuilder(["Kit_Trim"])
    _box(wrap, 0.0, 2.6, 0.0, PAV_PROUD, 0.0, WALL_TOP + 0.06, "Kit_Trim", frame=S)
    _box(wrap, 0.0, 2.6, 0.0, PAV_PROUD + 0.08, WALL_TOP + 0.06, PAV_TOP, "Kit_Trim", frame=S)
    made.append(make_object("Aud_PavilionReturn", wrap, col))
    # Recessed panel on the pavilion, the outline the photos show on its plain faces.
    line = MeshBuilder(["Kit_Trim"])
    _panel(line, rect(p0 + 0.55, 0.70, p1 - 0.55, WALL_TOP - 0.25),
           [rect(p0 + 0.68, 0.83, p1 - 0.68, WALL_TOP - 0.38)], PAV_PROUD, PAV_PROUD + 0.035, "Kit_Trim")
    made.append(make_object("Aud_PavilionPanel", line, col))

    # The red twin-bar bracket with its lamp (photos 3 and 4), high on the pavilion.
    bracket = MeshBuilder(["Kit_Wall_Red", "Kit_Frame", "Kit_Globe"])
    uc = (p0 + p1) / 2 - 0.4
    for du in (-0.46, 0.46):
        _box(bracket, uc + du - 0.09, uc + du + 0.09, PAV_PROUD, PAV_PROUD + 0.10, 3.05, 4.15,
             "Kit_Wall_Red")
    _box(bracket, uc - 0.16, uc + 0.16, PAV_PROUD, PAV_PROUD + 0.22, 3.42, 3.78, "Kit_Frame")
    bx, by, _ = P(uc, PAV_PROUD + 0.24)
    bracket.sphere((bx, by, 3.60), 0.10, "Kit_Globe", segs=8, rings=4)
    made.append(make_object("Aud_LampBracket", bracket, col))
    make_empty("Aud_LampBracket_Light", (bx, by, 3.60), col)

    # Floodlights on the parapet.
    flood = MeshBuilder(["Kit_Frame"])
    for u in (3.4, 9.4, 15.4):
        _box(flood, u - 0.16, u + 0.16, FIN_PROUD, FIN_PROUD + 0.22, PARAPET - 0.30, PARAPET - 0.10,
             "Kit_Frame")
    made.append(make_object("Detail_Aud_Floodlights", flood, col))

    # Green running-man sign on the fin beside the entrance (photo 4).
    sign = MeshBuilder(["Kit_Paint_Green"])
    a, b = fin_list[-1]
    _box(sign, a + 0.08, b - 0.08, FIN_PROUD + 0.006, FIN_PROUD + 0.026, 2.30, 2.52,
         "Kit_Paint_Green")
    made.append(make_object("Decal_Aud_Exit", sign, col))

    # South face: plain wall with its own recessed panel, seen at a glancing angle.
    south = MeshBuilder(["Kit_Trim"])
    _box(south, 2.6, SOUTH_LEN, 0.0, 0.18, 0.0, WALL_TOP, "Kit_Trim", frame=S)
    _box(south, 2.6, SOUTH_LEN + 0.05, 0.0, 0.24, WALL_TOP + 0.06, PARAPET, "Kit_Trim", frame=S)
    made.append(make_object("Aud_South_Wall", south, col))

    # Volcanic-stone plinth round the base, as everywhere else on the campus.
    plinth = MeshBuilder(["Kit_Stone"])
    _box(plinth, -0.05, WALL_LEN + 0.05, 0.0, FIN_PROUD + 0.20, 0.0, PLINTH_Z, "Kit_Stone")
    _box(plinth, 0.0, SOUTH_LEN, 0.0, PAV_PROUD + 0.20, 0.0, PLINTH_Z, "Kit_Stone", frame=S)
    made.append(make_object("Aud_Plinth", plinth, col))
    return made


# ---- Covered walkway (UtezDimensions.Canopy) ---------------------------------------------

# UtezDimensions.Canopy in CECADEC's frame. Stretched from 15 to 19 m and moved with CDS
# on 2026-09-16 so its west end still lands on CDS's east wall after that building went
# 4 m west to open up the passage.
CANOPY_CENTRE = (31.934, 22.469)
CANOPY_LEN, CANOPY_DEPTH = 19.0, 7.0
CANOPY_H, CANOPY_SLAB = 3.55, 0.35
COL_W = 0.38
COL_INSET = 0.30                     # from the roof edge, so nothing narrows the passage


def C(a, b, z=0.0):
    """Canopy frame: a east along its 15 m run, b north across its 7 m depth."""
    e = (-V_DIR[0], -V_DIR[1])
    n = (-U_DIR[0], -U_DIR[1])
    return (CANOPY_CENTRE[0] + e[0] * a + n[0] * b,
            CANOPY_CENTRE[1] + e[1] * a + n[1] * b, z)


def canopy_walk(col):
    """Roof on pillars joining CDS to the auditorium (photo 2, right edge). No walls: you
    see and walk straight through it, so it is columns, slab and nothing else."""
    made = []
    half_a, half_b = CANOPY_LEN / 2, CANOPY_DEPTH / 2
    slab = MeshBuilder(["Kit_Trim"])
    slab.transform = lambda q: C(q[0], q[1], q[2])
    slab.prism(rect(-half_a - 0.3, -half_b - 0.3, half_a + 0.3, half_b + 0.3),
               CANOPY_H, CANOPY_H + CANOPY_SLAB, "Kit_Trim", bottom=True)
    slab.transform = None
    made.append(make_object("Canopy_Roof", slab, col))

    n = 5
    for i in range(n):
        a = -half_a + COL_INSET + (CANOPY_LEN - 2 * COL_INSET) * i / (n - 1)
        for side, b in (("S", -half_b + COL_INSET), ("N", half_b - COL_INSET)):
            mb = MeshBuilder(["Kit_Trim"])
            mb.transform = lambda q: C(q[0], q[1], q[2])
            mb.prism(rect(a - COL_W / 2, b - COL_W / 2, a + COL_W / 2, b + COL_W / 2),
                     0.0, CANOPY_H, "Kit_Trim", bottom=True)
            mb.transform = None
            made.append(make_object(f"Canopy_Column{side}{i + 1}", mb, col))
    return made


# ---- Ground and planting between the plaza and the auditorium ------------------------------

def east_ground(col):
    """The paving, lawns and planting east of the aggregate plaza (photos 3 and 4): the
    concrete slabs, the auditorium's lawn strip behind its stone kerb, coconut palms,
    lamp posts, flagpoles and the red service cover lying in the grass."""
    made = []
    paving = MeshBuilder(["Kit_Slab"])
    _plan(paving, rect(-5.0, GRASS_V, 23.0, 15.0), GROUND_Z - 0.01, GROUND_Z, "Kit_Slab")
    made.append(make_object("Ground_EastPaving", paving, col))

    lawn = MeshBuilder(["Kit_Grass", "Kit_Stone"])
    _plan(lawn, rect(LAWN_U[0], 0.0, LAWN_U[1], GRASS_V), 0.0, BED_Z, "Kit_Grass",
          side_mat="Kit_Stone")
    made.append(make_object("Planter_Aud_Lawn", lawn, col))
    kerb = MeshBuilder(["Kit_Stone"])
    a, b = P(LAWN_U[0], GRASS_V), P(LAWN_U[1], GRASS_V)
    kerb.sweep([(a[0], a[1]), (b[0], b[1])], KERB_W, 0.0, KERB_TOP, "Kit_Stone")
    made.append(make_object("Planter_Aud_Kerb", kerb, col))

    # A second lawn on the far side of the paving, between it and the plaza (photo 3).
    lawn2 = MeshBuilder(["Kit_Grass", "Kit_Stone"])
    _plan(lawn2, rect(-4.0, 9.5, 10.0, 12.5), 0.0, BED_Z, "Kit_Grass", side_mat="Kit_Stone")
    made.append(make_object("Planter_East_Lawn2", lawn2, col))
    kerb2 = MeshBuilder(["Kit_Stone"])
    c, d = P(-4.0, 9.5), P(10.0, 9.5)
    kerb2.sweep([(c[0], c[1]), (d[0], d[1])], KERB_W, 0.0, KERB_TOP, "Kit_Stone")
    made.append(make_object("Planter_East_Kerb2", kerb2, col))

    for i, (u, v, h) in enumerate(((2.2, 11.0, 12.5), (6.0, 11.6, 11.0)), 1):
        x, y, _ = P(u, v)
        made += lm_plants.coconut_palm(col, f"Palm_Plaza{i}", x, y, BED_Z, height=h, seed=310 + i)
    for i, (u, v, hh, fk, cr) in enumerate(((-1.4, 10.6, 7.5, 1.8, 3.0), (8.6, 10.9, 8.2, 2.0, 3.4),
                                            (18.4, 0.5, 6.0, 1.6, 2.6)), 1):
        x, y, _ = P(u, v)
        made += lm_plants.tree(col, f"Tree_Plaza{i}", x, y, BED_Z, height=hh, fork=fk, crown=cr,
                               seed=320 + i)
    for i, (u, v, r) in enumerate(((4.2, 0.5, 0.45), (7.4, 0.5, 0.5), (11.8, 0.5, 0.45),
                                   (16.4, 0.5, 0.45), (3.4, 10.4, 0.6), (8.0, 11.4, 0.5)), 1):
        x, y, _ = P(u, v)
        made.append(lm_plants.shrub(col, f"Plant_East_Shrub{i}", x, y, BED_Z, radius=r, seed=330 + i))

    for i, (u, v) in enumerate(((0.4, 9.0), (9.4, 9.2)), 1):
        x, y, _ = P(u, v)
        made.append(lm_props.lamp_post(col, f"Lamp_Plaza{i}", x, y, BED_Z, height=4.6))
    for i, (u, v) in enumerate(((4.0, 6.6), (7.6, 6.9), (11.2, 7.2), (14.8, 7.5)), 1):
        x, y, _ = P(u, v)
        made.append(lm_props.flagpole(col, f"Flagpole_Plaza{i}", x, y, GROUND_Z, height=8.4))
    x, y, _ = P(6.8, 10.6)
    made.append(lm_props.lid(col, "Lid_PlazaEast", x, y, BED_Z))
    return made
