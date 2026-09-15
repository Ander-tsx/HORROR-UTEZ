"""
CECADEC ground floor, v2: the straight corridor from the main door to the south wall,
the wider cross corridor (glass doors east, stairs west), the rooms' fronts on both
sides, fittings and signage. Layout from the user's floor plan
(docs/map/reference/cecadec-interior-pasillo/plano-usuario-28.png) normalised to the
measured 20 x 45 m footprint; every element's look from the site photos (see lm_interior.py).
Room interiors stay empty (floor, ceiling, plain walls) until their own phase.

Axes: X east, Y north, Z up, metres. Origin: the main door threshold — the same point
as CECADEC_North.blend's origin, so both share one pose in Unity. Walking in you face
south: your left is east. Inner faces of the kit's outer walls: x = +-9.6, y = -0.4 and
-44.6.

Prefer running through Tools/blender/run_landmark.py (see gen_cecadec_north.py for the
full build / rebuild-by-unit contract, which this file follows).
"""

import importlib
import math
import os
import sys

_g = globals()
HERE = _g.get("TOOLS_DIR") or os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
sys.dont_write_bytecode = True

import bpy  # noqa: E402

import geolib  # noqa: E402
import lm_interior  # noqa: E402

for _m in (geolib, lm_interior):
    importlib.reload(_m)

from geolib import MeshBuilder, make_empty, make_object, rect  # noqa: E402
from lm_interior import (BAR, CEIL, PART_T, PILLAR_PROUD, PILLAR_W, RISE, TREAD, Wall)  # noqa: E402

I = lm_interior
PREFIX = "CecadecInterior_"

# Unity material -> (map, Tiling u, Tiling v). Must match UtezKit.LandmarkMaterials.
MATERIALS = {
    "Kit_Frame": ("T_Frame.png", 1.0, 1.0),
    "Kit_Glass_Clear": ("T_Glass.png", 1.0, 1.0),
    "Kit_Glass_Frosted": ("T_Glass_Frosted.png", 1.0, 1.0),
    "Kit_Glass_Tinted": ("T_Glass_Tinted.png", 1.0, 1.0),
    "Kit_Wall_White": ("T_Wall_White.png", 0.5, 0.5),
    "Kit_Tile_Floor": ("T_Tile_Floor.png", 1 / 3.2, 1 / 3.2),
    "Kit_Tile_Border": ("T_Tile_Border.png", 1 / 3.2, 1 / 3.2),
    "Kit_Ceiling": ("T_Ceiling.png", 1 / 2.44, 1 / 2.44),
    "Kit_Wood_Door": ("T_Wood_Door.png", 1.0, 1.0),
    "Kit_Wood_Header": ("T_Wood_Header.png", 0.5, 0.5),
    "Kit_Panel_Grey": ("T_Panel_Grey.png", 1.0, 1.0),
    "Kit_Metal_Grey": ("T_Metal_Grey.png", 1.0, 1.0),
    "Kit_Metal_White": ("T_Metal_White.png", 1.0, 1.0),
    "Kit_Metal_Red": ("T_Metal_Red.png", 1.0, 1.0),
    "Kit_Chrome": ("T_Chrome.png", 2.0, 2.0),
    "Kit_Vinyl_Blue": ("T_Vinyl_Blue.png", 2.0, 2.0),
    "Kit_Perforated": ("T_Perforated.png", 4.0, 4.0),
    "Kit_Plastic_Grey": ("T_Plastic_Grey.png", 1.0, 1.0),
    "Kit_Nosing": ("T_Nosing.png", 2.0, 2.0),
    "Kit_Paint_Blue": ("T_Paint_Blue.png", 1.0, 1.0),
    "Kit_Light": ("T_Light.png", 1.0, 1.0),
    "Kit_Signs": ("T_Signs.png", 1.0, 1.0),
}

# ---- Layout (metres; s = distance south of the main door along the corridor walls) ----

CORR = 2.2                       # corridor half-width: 11 tiles of 40 cm = 4.4 m
IN_X = 9.6                       # inner face of the east / west outer walls
N_S, S_S = 0.4, 44.6             # inner faces of the north / south outer walls
BAND = (28.8, 34.8)              # cross corridor, 6 m (plan: 64-79 % of the depth)
FIELD = 1.4                      # light field tiles; 2 darker tiles each side
WALL_X = CORR + PART_T           # room side of the corridor partitions
COL_W, COL_PROUD = 0.6, 0.28     # the four columns at the cross corridor's corners
EAST_DOOR = (29.3, 34.3)         # glass doors in the east wall (UtezBuildingBuilder.CecadecEastDoorRaw)
STAIR = dict(x_foot=-4.4, x_land=-7.7, f1=(-31.1, -29.52), f2=(-33.0, -31.3),
             parapet=(-29.52, -29.40), stringer=(-33.12, -33.0), centre=(-31.3, -31.1))

E = Wall((CORR, 0.0), (0.0, -1.0), (1.0, 0.0))       # east corridor wall, rooms east
W = Wall((-CORR, 0.0), (0.0, -1.0), (-1.0, 0.0))     # west corridor wall, rooms west
BN = Wall((0.0, -BAND[0]), (1.0, 0.0), (0.0, 1.0))   # cross corridor north side (s = x)
BS = Wall((0.0, -BAND[1]), (1.0, 0.0), (0.0, -1.0))  # cross corridor south side (s = x)
XE = Wall((IN_X, 0.0), (0.0, -1.0), (1.0, 0.0))      # east outer wall, inner face


def cell(m):
    """Centre of the m-th 61 cm ceiling cell south of the door (the grid is centred on x = 0)."""
    return -0.61 * m


# Photo 23: troffers in mirrored pairs across the corridor, every 5 tiles (3.05 m), with
# a supply diffuser or a return grille on the axis between each pair.
PAIR = 1.22
N_ROWS = range(3, 46, 5)
S_ROWS = range(60, 73, 5)
TROFFERS = (
    [(side * PAIR, cell(m)) for m in N_ROWS for side in (-1, 1)]
    + [(0.61 * k, cell(49)) for k in range(-6, 16, 3)]
    + [(0.61 * k, cell(55)) for k in range(-15, 16, 3)]
    + [(side * PAIR, cell(m)) for m in S_ROWS for side in (-1, 1)]
)
DIFFUSERS = ([(0.0, cell(m)) for i, m in enumerate(N_ROWS) if i % 2 == 0]
             + [(0.0, cell(m)) for i, m in enumerate(S_ROWS) if i % 2 == 0]
             + [(0.61 * k, cell(52)) for k in (-6, 0, 6, 12)])
RETURNS = ([(0.0, cell(m)) for i, m in enumerate(N_ROWS) if i % 2 == 1]
           + [(0.0, cell(m)) for i, m in enumerate(S_ROWS) if i % 2 == 1])
SMOKE = [(0.0, cell(m)) for m in (6, 26, 68)] + [(-4.88, cell(51))]

# One fluorescent light per fitting pair (UtezKit turns every *_Fluoro empty into a light
# with FluorescentFlicker), four along the cross corridor, one in the open stairwell.
FLUOROS = ([(f"N{i}", 0.0, cell(m), CEIL - 0.08) for i, m in enumerate(N_ROWS, 1)]
           + [(f"S{i}", 0.0, cell(m), CEIL - 0.08) for i, m in enumerate(S_ROWS, 1)]
           + [(f"Band{i}", x, cell(52), CEIL - 0.08) for i, x in enumerate((-3.66, 0.0, 3.66, 7.32), 1)]
           + [("Stair", -6.4, -31.2, 3.7)])

# Floor and ceiling pieces: URP lights each object with at most 8 lights, so no piece may
# sit under more than a few fittings. Cuts fall between fitting rows, never through one.
N_BREAKS = [0.0, cell(5.5), cell(15.5), cell(25.5), cell(35.5), -BAND[0]]
S_BREAKS = [-BAND[1], cell(67.5), -S_S]
BAND_X = [-IN_X, -CORR, CORR, 5.9, IN_X]


def _pieces(breaks):
    return list(zip(breaks[:-1], breaks[1:]))


# ---- Units ---------------------------------------------------------------------------

def _slab(col, name, poly, z0, z1, mat, holes=()):
    mb = MeshBuilder([mat])
    mb.prism(poly, z0, z1, mat, bottom=True, holes=holes)
    return make_object(name, mb, col)


def _boxes(col, name, boxes, mat):
    mb = MeshBuilder([mat])
    for lo, hi in boxes:
        mb.box(lo, hi, mat)
    return make_object(name, mb, col)


def u_floors(col):
    z0, z1 = -0.02, 0.0
    f = FIELD
    for label, breaks in (("Corridor", N_BREAKS), ("South", S_BREAKS)):
        for i, (ya, yb) in enumerate(_pieces(breaks), 1):
            _slab(col, f"Floor_{label}_Field{i}", rect(-f, yb, f, ya), z0, z1, "Kit_Tile_Floor")
            _boxes(col, f"Floor_{label}_Border{i}", [((f, yb, z0), (CORR, ya, z1)),
                                                     ((-CORR, yb, z0), (-f, ya, z1))], "Kit_Tile_Border")
    b0, b1 = -BAND[0], -BAND[1]
    for i, (xa, xb) in enumerate(zip(BAND_X[:-1], BAND_X[1:]), 1):
        if xa == -CORR:
            _slab(col, f"Floor_Band_Field{i}", rect(xa, b1, xb, b0), z0, z1, "Kit_Tile_Floor")
            continue
        _slab(col, f"Floor_Band_Field{i}", rect(xa, b1 + 0.8, xb, b0 - 0.8), z0, z1, "Kit_Tile_Floor")
        _boxes(col, f"Floor_Band_Border{i}", [((xa, b0 - 0.8, z0), (xb, b0, z1)),
                                              ((xa, b1, z0), (xb, b1 + 0.8, z1))], "Kit_Tile_Border")
    for side, x0, x1 in (("E", WALL_X, IN_X), ("W", -IN_X, -WALL_X)):
        _slab(col, f"Floor_Rooms_N{side}", rect(x0, -(BAND[0] - PART_T), x1, -N_S), z0, z1, "Kit_Tile_Floor")
        _slab(col, f"Floor_Rooms_S{side}", rect(x0, -S_S, x1, -(BAND[1] + PART_T)), z0, z1, "Kit_Tile_Floor")


def _holes_in(x0, y0, x1, y1):
    return [rect(x - 0.3, y - 0.3, x + 0.3, y + 0.3) for x, y in TROFFERS
            if x0 < x - 0.3 and x + 0.3 < x1 and y0 < y - 0.3 and y + 0.3 < y1]


def u_ceilings(col):
    z0, z1 = CEIL, CEIL + 0.02
    b0, b1 = -BAND[0], -BAND[1]
    for label, breaks in (("Corridor", [-N_S] + N_BREAKS[1:]), ("South", S_BREAKS)):
        for i, (ya, yb) in enumerate(_pieces(breaks), 1):
            _slab(col, f"Ceiling_{label}{i}", rect(-CORR, yb, CORR, ya), z0, z1, "Kit_Ceiling",
                  holes=_holes_in(-CORR, yb, CORR, ya))
    notch = (STAIR["x_foot"] + 0.15, STAIR["stringer"][0] - 0.08, STAIR["parapet"][1] + 0.05)
    for i, (xa, xb) in enumerate(zip(BAND_X[:-1], BAND_X[1:]), 1):
        if xa == -IN_X:     # the stairwell stays open to the floor above
            poly = [(xa, b1), (xb, b1), (xb, b0), (xa, b0), (xa, notch[2]), (notch[0], notch[2]),
                    (notch[0], notch[1]), (xa, notch[1])]
        else:
            poly = rect(xa, b1, xb, b0)
        _slab(col, f"Ceiling_Band{i}", poly, z0, z1, "Kit_Ceiling", holes=_holes_in(xa, b1, xb, b0))
    for side, x0, x1 in (("E", WALL_X, IN_X), ("W", -IN_X, -WALL_X)):
        _slab(col, f"Ceiling_Rooms_N{side}", rect(x0, -(BAND[0] - PART_T), x1, -N_S), z0, z1, "Kit_Ceiling")
        _slab(col, f"Ceiling_Rooms_S{side}", rect(x0, -S_S, x1, -(BAND[1] + PART_T)), z0, z1, "Kit_Ceiling")


def u_ceiling_fixtures(col):
    for i, (x, y) in enumerate(TROFFERS, 1):
        I.troffer(col, str(i), x, y)
    for i, (x, y) in enumerate(DIFFUSERS, 1):
        I.diffuser(col, str(i), x, y)
    for i, (x, y) in enumerate(RETURNS, 1):
        I.return_grille(col, str(i), x, y)
    for i, (x, y) in enumerate(SMOKE, 1):
        I.smoke_detector(col, str(i), x, y)


def u_rooms(col):
    """Room shells behind the fronts: dividers and a plaster lining over the kit's outer
    walls (whose inner face is still the red facade material). Interiors come later."""
    t = PART_T / 2
    walls = []
    for s in (4.915, 8.605, 13.575, 18.545, 24.025):
        walls.append(((WALL_X, -s - t, 0.0), (IN_X - 0.02, -s + t, CEIL)))
    for s in (10.975, 16.725):
        walls.append(((-IN_X + 0.02, -s - t, 0.0), (-WALL_X, -s + t, CEIL)))
    _boxes(col, "Room_Dividers", walls, "Kit_Wall_White")
    lin = [
        ((WALL_X, -N_S - 0.02, 0.0), (IN_X, -N_S, CEIL)), ((-IN_X, -N_S - 0.02, 0.0), (-WALL_X, -N_S, CEIL)),
        ((IN_X - 0.02, -(BAND[0] - PART_T), 0.0), (IN_X, -N_S - 0.02, CEIL)),
        ((-IN_X, -(BAND[0] - PART_T), 0.0), (-IN_X + 0.02, -N_S - 0.02, CEIL)),
        ((IN_X - 0.02, -S_S + 0.02, 0.0), (IN_X, -(BAND[1] + PART_T), CEIL)),
        ((-IN_X, -S_S + 0.02, 0.0), (-IN_X + 0.02, -(BAND[1] + PART_T), CEIL)),
        ((WALL_X, -S_S, 0.0), (IN_X, -S_S + 0.02, CEIL)), ((-IN_X, -S_S, 0.0), (-WALL_X, -S_S + 0.02, CEIL)),
    ]
    _boxes(col, "Lining_Rooms", lin, "Kit_Wall_White")
    _boxes(col, "Lining_CorridorSouth", [((-CORR, -S_S, 0.0), (CORR, -S_S + 0.02, CEIL))], "Kit_Wall_White")
    _boxes(col, "Lining_CorridorSouth_Skirting", [((-CORR, -S_S + 0.02, 0.0), (CORR, -S_S + 0.026, 0.10))],
           "Kit_Frame")
    _boxes(col, "Lining_Stairwell", [((-IN_X, -BAND[1], 0.0), (-IN_X + 0.02, -BAND[0], 4.0))], "Kit_Wall_White")
    _boxes(col, "Lining_EastDoor", [((IN_X - 0.02, -EAST_DOOR[0], 0.0), (IN_X, -BAND[0], CEIL)),
                                    ((IN_X - 0.02, -BAND[1], 0.0), (IN_X, -EAST_DOOR[1], CEIL))], "Kit_Wall_White")


def u_corridor_east(col):
    """East side, main door to the cross corridor (photos 5, 6, 7, 13, 22, 23, 24)."""
    I.plaster(col, "E_Jamb", E, N_S, 0.7)
    I.glazed_front(col, "Aula1", E, 0.7, 4.69, [0.59, 1.18, 1.20],
                   [dict(at="end", name="Aula1", signs=[("CARD_WHITE", 0.0, 1.55)])])
    I.decal(col, "Aula1", E, "AULA1", 3.34, 1.65, d=0.055)
    I.decal(col, "Aula1_Prohibido", E, "PROHIBIDO_A1", 3.34, 1.44, d=0.055)
    I.pillar(col, "Pillar_E1", E, 4.915)
    I.extinguisher(col, "Extinguisher_E1", E, 4.915, top=1.55, face=-PILLAR_PROUD)
    I.glazed_front(col, "Aula2", E, 5.14, 8.38, [0.93, 0.92, 0.37],
                   [dict(at="start", name="Aula2", transom="panel", signs=[("HORARIO_A2", 0.0, 1.53)])])
    I.decal(col, "Aula2", E, "AULA2", 6.56, 1.48, d=0.055)
    I.pillar(col, "Pillar_E2", E, 8.605)
    I.plain_front(col, "CC9", E, 8.83, 13.35, door=dict(at="end", name="CC9", signs=[
        ("CC9", 0.0, 1.90), ("PROHIBIDO_CC9", 0.0, 1.66), ("AHORREMOS", -0.22, 1.50), ("HORARIO_CC9", 0.18, 1.50)]))
    I.pillar(col, "Pillar_E3", E, 13.575)
    I.plain_front(col, "LabProcesos", E, 13.80, 18.32, door=dict(at="start", name="LabProcesos", signs=[
        ("LAB_PROCESOS", 0.0, 1.93), ("LAB_NOTICE", 0.0, 1.60)]))
    I.decal(col, "Procesos_Evac", E, "EVAC_LEFT", 15.17, 1.92)
    I.decal(col, "Procesos_Apaga", E, "APAGA", 15.10, 1.62)
    I.pillar(col, "Pillar_E4", E, 18.545)
    I.extinguisher(col, "Extinguisher_E4", E, 18.545, top=1.55, face=-PILLAR_PROUD)
    I.decal(col, "Extintor_E4", E, "EXTINTOR", 18.545, 2.05, d=-PILLAR_PROUD - 0.004)
    I.plain_front(col, "RoomE5", E, 18.77, 23.80, door=dict(at="end", name="RoomE5"))
    I.pillar(col, "Pillar_E5", E, 24.025)
    I.plain_front(col, "RoomE6", E, 24.25, BAND[0] - COL_W)
    I.pillar(col, "Column_NE", E, BAND[0] - COL_W / 2, width=COL_W, proud=COL_PROUD)


def _alcove(col, name, s0, s1, depth, side_at=None):
    """Recessed entrance to the toilets (photo 8): back wall, one side wall, skirting."""
    I.plaster(col, name + "_Back", W, s0, s1, depth=PART_T, skirting=False)[0]
    bj = MeshBuilder(["Kit_Wall_White"])
    W.box(bj, s0, s1, depth, depth + PART_T, 0.0, CEIL, "Kit_Wall_White")
    if side_at is not None:
        W.box(bj, side_at - PART_T if side_at <= s0 else side_at, side_at if side_at <= s0 else side_at + PART_T,
              PART_T, depth + PART_T, 0.0, CEIL, "Kit_Wall_White")
    make_object(name + "_Walls", bj, col)
    sk = MeshBuilder(["Kit_Frame"])
    W.box(sk, s0, s1, depth - 0.006, depth, 0.0, 0.10, "Kit_Frame")
    make_object(name + "_Skirting", sk, col)


def u_corridor_west(col):
    """West side, main door to the cross corridor (photos 1, 2, 3, 4, 8, 23, 24)."""
    I.plaster(col, "W_Jamb", W, N_S, 0.7)
    I.tinted_front(col, "RoomNW", W, 0.7, 5.5, sign="PROHIBIDO_A1")
    I.pillar(col, "Pillar_W1", W, 5.725)
    I.closet(col, "Electrical", W, 5.95, 10.75)
    I.pillar(col, "Pillar_W2", W, 10.975)

    depth = 1.40
    # Alcove A (s 11.2..12.2), signs block, alcove B (15.5..16.5).
    for name, s0, s1, side in (("ToiletA", 11.2, 12.2, 11.2), ("ToiletB", 15.5, 16.5, 16.5)):
        mb = MeshBuilder(["Kit_Wall_White"])
        W.box(mb, s0, s1, depth, depth + PART_T, 0.0, CEIL, "Kit_Wall_White")
        if side == s0:
            W.box(mb, s0 - PART_T, s0, PART_T, depth + PART_T, 0.0, CEIL, "Kit_Wall_White")
        else:
            W.box(mb, s1, s1 + PART_T, PART_T, depth + PART_T, 0.0, CEIL, "Kit_Wall_White")
        make_object(name + "_Walls", mb, col)
        sk = MeshBuilder(["Kit_Frame"])
        W.box(sk, s0, s1, depth - 0.006, depth, 0.0, 0.10, "Kit_Frame")
        if side == s0:
            W.box(sk, s0, s0 + 0.006, PART_T, depth - 0.006, 0.0, 0.10, "Kit_Frame")
        else:
            W.box(sk, s1 - 0.006, s1, PART_T, depth - 0.006, 0.0, 0.10, "Kit_Frame")
        make_object(name + "_Skirting", sk, col)
    # Photo 8 (facing west): switch in the left (southern) alcove, towel dispenser and
    # broom in the right (northern) one.
    I.decal(col, "ToiletB_Switch", W, "SWITCH", 16.0, 1.05, d=depth - 0.004)

    blk = MeshBuilder(["Kit_Wall_White"])
    W.box(blk, 12.2, 15.5, 0.0, depth + PART_T, 0.0, CEIL, "Kit_Wall_White")
    make_object("SignsBlock", blk, col)
    e = 0.006
    sk = MeshBuilder(["Kit_Frame"])
    W.plan(sk, [(12.2 - e, depth - e), (12.2 - e, -e), (15.5 + e, -e), (15.5 + e, depth - e),
                (15.5, depth - e), (15.5, 0.0), (12.2, 0.0), (12.2, depth - e)], 0.0, 0.10, "Kit_Frame")
    make_object("SignsBlock_Skirting", sk, col)
    I.decal(col, "Block_Evac", W, "EVAC_RIGHT", 13.85, 2.20)
    I.decal(col, "Block_Agua", W, "AHORRA_AGUA", 13.85, 1.80)
    I.decal(col, "Block_Residuos", W, "COLOCA", 14.56, 1.52)
    I.decal(col, "Block_Switch", W, "SWITCH", 13.95, 0.94)
    for name, s, label, lid in (("PET", 14.74, "BIN_PET", False), ("Organico", 14.0, "BIN_ORG", False),
                                ("Otros", 13.19, "BIN_OTROS", True)):
        x, y, _ = W.p(s, -0.30, 0.0)
        I.recycling_bin(col, "Bin_" + name, x, y, (1.0, 0.0), label, lid=lid)
    disp = MeshBuilder(["Kit_Frame"])
    W.box(disp, 12.1, 12.2, 0.45, 0.75, 1.25, 1.60, "Kit_Frame")
    make_object("Detail_TowelDispenser", disp, col)
    broom = MeshBuilder(["Kit_Metal_Red", "Kit_Wood_Door"])
    hx, hy, _ = W.p(11.5, 1.2, 0.0)
    broom.box((hx - 0.04, hy - 0.15, 0.0), (hx + 0.04, hy + 0.15, 0.12), "Kit_Metal_Red")
    tx, ty, _ = W.p(11.35, 1.33, 0.0)
    broom.tube([(hx, hy, 0.12), (tx, ty, 1.35)], [0.012, 0.012], "Kit_Wood_Door", segs=6)
    make_object("Detail_Broom", broom, col)

    I.pillar(col, "Pillar_W3", W, 16.725)
    I.plain_front(col, "RoomW", W, 16.95, BAND[0] - COL_W, transom_band=False)
    I.pillar(col, "Column_NW", W, BAND[0] - COL_W / 2, width=COL_W, proud=COL_PROUD)


def u_band(col):
    """Cross corridor walls and what stands against them (photos 10, 11, 20)."""
    I.plaster(col, "BandN_East", BN, WALL_X, IN_X)
    for i, x in enumerate((3.9, 5.2, 6.5, 7.8), 1):
        I.decal(col, f"Plaque{i}", BN, "PLAQUE", x, 1.75)
    for i, x in enumerate((3.6, 5.4, 7.2), 1):
        I.low_bench(col, f"LowBench{i}", x, -BAND[0] - 0.25)
    I.plaster(col, "BandN_West", BN, -IN_X, -WALL_X)
    I.box_prop(col, "Detail_FirstAid", (-3.4, -BAND[0] - 0.12, 1.18), (-3.1, -BAND[0], 1.53), "Kit_Paint_Blue")
    I.decal(col, "Botiquin", BN, "BOTIQUIN", -3.25, 1.88)

    I.plaster(col, "BandS_East", BS, WALL_X, IN_X)
    frame = MeshBuilder(["Kit_Wall_White"])
    BS.panel(frame, rect(4.5, 1.0, 6.3, 2.4), [rect(4.58, 1.08, 6.22, 2.32)], -0.05, 0.0, "Kit_Wall_White")
    make_object("UTEZ_Frame", frame, col)
    I.decal(col, "UTEZ", BS, "UTEZ", 5.4, 1.70, d=-0.004)
    I.decal(col, "BandS_Evac", BS, "EVAC_LEFT", 7.7, 1.95)
    I.decal(col, "BandS_Extintor", BS, "EXTINTOR", 8.9, 2.05)
    I.extinguisher(col, "Extinguisher_BandS", BS, 8.9, top=1.6, face=0.0)
    I.plaster(col, "BandS_West", BS, -IN_X, -WALL_X)
    I.decal(col, "Alarma", BS, "ALARMA", -3.4, 1.95)
    I.box_prop(col, "Detail_AlarmBox", (-3.46, -BAND[1], 2.24), (-3.34, -BAND[1] + 0.05, 2.40), "Kit_Panel_Grey")

    # Corner columns' faces towards the cross corridor (photo 11).
    I.decal(col, "Vigilancia", BS, "VIGILANCIA", -(CORR - 0.08), 1.95)
    I.decal(col, "Extintor_SE", BS, "EXTINTOR", CORR - 0.08, 2.0)
    I.extinguisher(col, "Extinguisher_SE", BS, CORR - 0.08, top=1.5, face=0.0)

    I.box_prop(col, "SecurityCounter", (-3.95, -BAND[1] + 0.02, 0.0), (-2.95, -BAND[1] + 0.62, 1.02), "Kit_Metal_Grey")
    I.box_prop(col, "SecurityCounter_Top", (-4.0, -BAND[1] + 0.02, 1.02), (-2.9, -BAND[1] + 0.67, 1.05), "Kit_Panel_Grey")
    I.table(col, "SecurityTable", -5.2, -BAND[1] + 1.1)
    I.chair(col, "SecurityChair", -5.2, -BAND[1] + 0.45, (0.0, 1.0))
    I.box_prop(col, "Locker", (-7.3, -BAND[1] + 0.02, 0.0), (-6.4, -BAND[1] + 0.47, 1.80), "Kit_Metal_Grey")


def u_east_door(col):
    """Glass doors out of the cross corridor, east wall (photo 20): fixed side lights,
    two leaves, black header with the SALIDA sign. Sits mid-depth in the kit wall, whose
    hole UtezBuildingBuilder.CutCecadecEastDoor opens."""
    s0, s1 = EAST_DOOR
    d0, d1 = 0.14, 0.22
    dm0, dm1 = s0 + 1.0, s1 - 1.0
    head = 2.20
    outline = [(s0, 0.0), (dm0, 0.0), (dm0, head), (dm1, head), (dm1, 0.0), (s1, 0.0), (s1, CEIL), (s0, CEIL)]
    lights = [rect(s0 + BAR, 0.06, dm0 - BAR, head - BAR), rect(dm1 + BAR, 0.06, s1 - BAR, head - BAR)]
    frame = MeshBuilder(["Kit_Frame"])
    XE.panel(frame, outline, lights, d0, d1, "Kit_Frame")
    make_object("EastDoor_Frame", frame, col)
    panes = MeshBuilder(["Kit_Glass_Clear"])
    for h in lights:
        XE.quad(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, (d0 + d1) / 2, "Kit_Glass_Clear")
    make_object("EastDoor_Glass", panes, col)
    mid = (dm0 + dm1) / 2
    I.door_leaf(col, "ExitNorth", XE, dm0, mid - 0.005, "Kit_Glass_Clear", d=(d0 + d1) / 2, top=head - 0.01)
    I.door_leaf(col, "ExitSouth", XE, dm1, mid + 0.005, "Kit_Glass_Clear", d=(d0 + d1) / 2, top=head - 0.01)
    I.decal(col, "Salida", XE, "SALIDA", mid, 2.45, d=d0 - 0.004)


def u_stairs(col):
    """Dog-leg stair at the west end of the cross corridor (photos 10, 21): flight up
    westwards, landing on the outer wall, flight back east to the upper floor."""
    st = STAIR
    xf, xl = st["x_foot"], st["x_land"]
    I.stair_flight(col, "Stair_Flight1", xf, -1.0, *st["f1"], 0.0)
    I.stair_flight(col, "Stair_Flight2", xl, 1.0, *st["f2"], 2.0)
    land = MeshBuilder(["Kit_Tile_Floor", "Kit_Wall_White"])
    land.prism(rect(-IN_X + 0.02, st["stringer"][0], xl, st["parapet"][1]), 1.80, 2.0, "Kit_Tile_Floor",
               side_mat="Kit_Wall_White")
    make_object("Stair_Landing", land, col)

    pitch = RISE / TREAD
    up1 = lambda x: 0.95 + max(0.0, (xf - x)) * pitch                      # noqa: E731
    I.sloped_wall(col, "Stair_CentreWall", xl, xf + 0.1, *st["centre"], up1)
    I.sloped_wall(col, "Stair_Parapet", -IN_X + 0.02, xf + 0.1, *st["parapet"], lambda x: min(2.95, up1(x)))
    cos_a = TREAD / math.hypot(TREAD, RISE)
    waist = 0.16 / cos_a
    nose2 = lambda x: 2.0 + RISE + (x - xl) * pitch                          # noqa: E731
    I.sloped_wall(col, "Stair_Stringer", xl, xf, *st["stringer"], lambda x: nose2(x) + 0.95,
                  base_at=lambda x: nose2(x) - waist)
    lp = MeshBuilder(["Kit_Wall_White"])
    lp.box((-IN_X + 0.02, st["stringer"][0], 2.0), (xl, st["stringer"][1], 2.95), "Kit_Wall_White")
    make_object("Stair_LandingParapet", lp, col)

    rails = MeshBuilder(["Kit_Metal_Red"])
    yp = sum(st["parapet"]) / 2
    I.rail(rails, [(xf + 0.05, yp, up1(xf) + 0.07), (xl, yp, up1(xl) + 0.07),
                   (xl - 0.3, yp, 3.02), (-IN_X + 0.15, yp, 3.02)])
    yc = sum(st["centre"]) / 2
    I.rail(rails, [(xf + 0.05, yc, up1(xf) + 0.07), (xl + 0.05, yc, up1(xl) + 0.07)])
    y2 = st["f2"][1] - 0.04
    I.rail(rails, [(xl + 0.1, y2, nose2(xl + 0.1) + 0.92), (xf - 0.1, y2, nose2(xf - 0.1) + 0.92)], post_drop=0.9)
    ys = sum(st["stringer"]) / 2
    I.rail(rails, [(-IN_X + 0.15, ys, 3.02), (xl, ys, 3.02), (xl + 0.05, ys, nose2(xl + 0.05) + 1.02),
                   (xf - 0.05, ys, nose2(xf - 0.05) + 1.02)])
    make_object("Stair_Rails", rails, col)


def u_corridor_south(col):
    """Cross corridor to the south wall (photos 11, 12)."""
    b1 = BAND[1]
    I.pillar(col, "Column_SE", E, b1 + COL_W / 2, width=COL_W, proud=COL_PROUD)
    I.plain_front(col, "LabSE", E, b1 + COL_W, S_S, door=dict(at="start", name="LabSE"))
    I.pillar(col, "Column_SW", W, b1 + COL_W / 2, width=COL_W, proud=COL_PROUD)
    s0 = b1 + COL_W
    I.glazed_front(col, "LabIoT", W, s0, s0 + 4.84, [1.0, 1.0, 1.0],
                   [dict(at="start", name="LabIoTSide", transom="Kit_Glass_Clear"),
                    dict(at="start", name="LabIoT", infill="Kit_Panel_Grey", glass_from=1.10,
                         transom="Kit_Glass_Clear")],
                   knee=0.95, lower="Kit_Glass_Clear", upper="Kit_Glass_Clear", head=2.25)
    I.decal(col, "LabIoT", W, "LAB_IOT", s0 + 2.04 + 0.5, 1.90, d=0.055)
    I.plain_front(col, "RoomSW", W, s0 + 4.84, S_S)


def u_furniture(col):
    for name, wall, s in (("Bench_Aula1", E, 2.0), ("Bench_Aula2", E, 7.4), ("Bench_IoT", W, 38.9)):
        x, y, _ = wall.p(s, -0.33, 0.0)
        I.bench(col, name, x, y, (-wall.n[0], -wall.n[1]))


def u_lights(col):
    for name, x, y, z in FLUOROS:
        make_empty(f"Light_{name}_Fluoro", (x, y, z), col, 0.3)


def u_probes(col):
    make_empty("Probe_East", (10.0, 0.0, 0.0), col, 1.0)
    make_empty("Probe_North", (0.0, 10.0, 0.0), col, 1.0)
    make_empty("Probe_Up", (0.0, 0.0, 10.0), col, 1.0)


UNITS = {
    "floors": ("Floors", u_floors),
    "ceilings": ("Ceilings", u_ceilings),
    "ceiling_fixtures": ("CeilingFixtures", u_ceiling_fixtures),
    "rooms": ("Rooms", u_rooms),
    "corridor_east": ("CorridorEast", u_corridor_east),
    "corridor_west": ("CorridorWest", u_corridor_west),
    "band": ("Band", u_band),
    "east_door": ("EastDoor", u_east_door),
    "stairs": ("Stairs", u_stairs),
    "corridor_south": ("CorridorSouth", u_corridor_south),
    "furniture": ("Furniture", u_furniture),
    "lights": ("Lights", u_lights),
    "probes": ("Probes", u_probes),
}


def _collection(key):
    name = PREFIX + key
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def run_unit(key):
    col_key, build = UNITS[key]
    before = set(bpy.data.objects)
    build(_collection(col_key))
    made = [o for o in bpy.data.objects if o not in before]
    for o in made:
        o["lm_unit"] = key
    return made


def remove_unit(key):
    for o in [o for o in bpy.data.objects if o.get("lm_unit") == key]:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


ALPHA = {"Kit_Glass_Clear": 0.3, "Kit_Glass_Frosted": 0.85, "Kit_Glass_Tinted": 0.7}


def ensure_materials(tex_dir):
    for name, (file_name, tu, tv) in MATERIALS.items():
        geolib.preview_material(name, tex_dir, file_name, (tu, tv), alpha=ALPHA.get(name),
                                emission=3.0 if name == "Kit_Light" else 0.0)


def clear_file():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights,
                 bpy.data.images, bpy.data.collections):
        for block in list(coll):
            coll.remove(block)


def texture_dir_for(blend_path):
    art = os.path.dirname(os.path.dirname(os.path.dirname(blend_path)))
    return os.path.join(art, "Textures", "Kit")


def stats():
    objs = [o for o in bpy.data.objects if o.get("lm_unit")]
    tris = sum(len(p.vertices) - 2 for o in objs if o.type == "MESH" for p in o.data.polygons)
    return len(objs), tris


def build(out_blend):
    clear_file()
    ensure_materials(texture_dir_for(out_blend))
    for key in UNITS:
        run_unit(key)
    os.makedirs(os.path.dirname(out_blend), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out_blend, copy=True, relative_remap=True)
    print("[cecadec_interior] built %s: %d objects, %d tris" % ((out_blend,) + stats()))


def rebuild(out_blend, keys):
    if os.path.abspath(bpy.data.filepath or "") != os.path.abspath(out_blend):
        bpy.ops.wm.open_mainfile(filepath=out_blend)
    ensure_materials(texture_dir_for(out_blend))
    for key in keys:
        remove_unit(key)
        run_unit(key)
    bpy.ops.wm.save_mainfile()
    print("[cecadec_interior] rebuilt %s in %s: %d objects, %d tris" % ((keys, out_blend) + stats()))


# ---- Preview (not saved into the asset) ----------------------------------------------

VIEWS = {
    # Same spots and directions as the site photos they are named after.
    "photo23_walking_in": ((0.5, -1.2, 1.55), (0.4, -20.0, 1.45), 20),
    "photo24_looking_back": ((-0.4, -17.5, 1.55), (0.0, 0.0, 1.45), 20),
    "photo5_aula2": ((-1.6, -6.9, 1.35), (2.2, -6.9, 1.35), 20),
    "photo2_closet": ((1.7, -8.35, 1.40), (-2.2, -8.35, 1.35), 20),
    "photo8_bins": ((1.7, -13.85, 1.30), (-2.2, -13.85, 1.20), 20),
    "photo20_east_doors": ((-1.2, -31.8, 1.55), (9.6, -31.8, 1.25), 20),
    "photo10_stairs": ((1.6, -31.0, 1.55), (-9.6, -31.2, 1.9), 20),
    "photo11_south": ((0.4, -30.2, 1.55), (0.0, -44.6, 1.3), 20),
}


def _render(name, loc, target, lens, out_dir, ortho=None):
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("Cam_" + name)
    cam_data.lens = lens
    cam_data.clip_start = 0.05
    if ortho:
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = ortho
    cam = bpy.data.objects.new("Cam_" + name, cam_data)
    scene.collection.objects.link(cam)
    cam.location = loc
    aim = bpy.data.objects.new("Aim_" + name, None)
    aim.location = target
    scene.collection.objects.link(aim)
    track = cam.constraints.new("TRACK_TO")
    track.target = aim
    scene.camera = cam
    scene.render.filepath = os.path.join(out_dir, "interior_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    return scene.render.filepath


def preview(out_dir):
    scene = bpy.context.scene
    col = bpy.data.collections.new("Preview_Lights")
    scene.collection.children.link(col)
    # Roughly the in-game mood: a dim greenish fluorescent pool per fitting pair.
    for name, x, y, z in FLUOROS:
        data = bpy.data.lights.new(f"Preview_Light_{name}", "POINT")
        data.energy = 140.0
        data.color = (0.86, 0.95, 0.78)
        data.shadow_soft_size = 0.4
        ob = bpy.data.objects.new(f"Preview_Light_{name}", data)
        ob.location = (x, y, z)
        col.objects.link(ob)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is not None:
        bg.inputs["Color"].default_value = (0.9, 0.9, 0.92, 1.0)
        bg.inputs["Strength"].default_value = 0.06
    engine = _g.get("ENGINE", "BLENDER_EEVEE")
    scene.render.engine = engine
    if engine == "CYCLES":
        scene.cycles.samples = 32
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = 1280, 960
    out = [_render(n, *v, out_dir) for n, v in VIEWS.items()]
    for o in bpy.data.objects:
        if o.name.startswith("Ceiling_") or o.name.startswith("Ceil_"):
            o.hide_render = True
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    scene.collection.objects.link(sun)
    scene.render.resolution_x, scene.render.resolution_y = 760, 1400
    out.append(_render("plan", (0.0, -22.3, 40.0), (0.0, -22.29, 0.0), 50, out_dir, ortho=47.0))
    return out


def main():
    if "OUT_BLEND" in _g:
        if _g.get("REBUILD_UNITS"):
            rebuild(_g["OUT_BLEND"], _g["REBUILD_UNITS"])
        else:
            build(_g["OUT_BLEND"])
        _g["STATS"] = stats()
        if _g.get("PREVIEW_DIR"):
            _g["RESULT"] = preview(_g["PREVIEW_DIR"])
    elif "--" in sys.argv:
        args = sys.argv[sys.argv.index("--") + 1:]
        if len(args) > 1:
            rebuild(args[0], args[1:])
        else:
            build(args[0])


main()
