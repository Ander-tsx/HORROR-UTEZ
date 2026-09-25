"""
CECADEC ground floor, v3: the straight corridor from the main door, the wider cross
corridor (glass doors east, stairs west), the short corridor south of it, the rooms'
fronts on both sides, fittings and signage. Room interiors stay empty (floor, ceiling,
plain walls) until their own phase.

v3 fixes the corridor's length. v2 took it from the user's rough floor plan
(docs/map/reference/cecadec-interior-pasillo/plano-usuario-28.png) and made the first
leg 28.40 m, which left 6.44 m of dead wall between the electrical closet and the glazed
fronts once those, the toilet alcoves and the signs block were placed at their measured
lengths. The first leg is now 21.96 m (door at s = 0.4, cross corridor at s = 22.36) and
the 6.44 m went south of the cross: the corridor dead-ends at S_END = 38.16 and the last
bay of the 45 m footprint is a room, not corridor. East of the first leg there are three
rooms only, not six: Aula 1 (one room, one door, its front running past the column
between the benches), Aula 2 and CC9. Lab de Procesos and the south-east lab moved to
the east side of the south leg.

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
import lm_bath  # noqa: E402
import lm_interior  # noqa: E402

for _m in (geolib, lm_interior, lm_bath):
    importlib.reload(_m)

from geolib import MeshBuilder, make_empty, make_object, rect  # noqa: E402
from lm_interior import (BAR, BASE_T, CEIL, PART_T, PILLAR_PROUD, PILLAR_W, RISE, TREAD, Wall)  # noqa: E402

I = lm_interior
B = lm_bath
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
    # Toilets and the waiting tables of the cross corridor (2026-09-16).
    "Kit_Tile_Wall": ("T_Tile_Wall.png", 0.5, 0.5),
    "Kit_Tile_Counter": ("T_Tile_Counter.png", 1 / 1.6, 1 / 1.6),
    "Kit_Porcelain": ("T_Porcelain.png", 1.0, 1.0),
    "Kit_Paint_Grey": ("T_Paint_Grey.png", 1.0, 1.0),
    "Kit_Mirror": ("T_Mirror.png", 1.0, 1.0),
    "Kit_Wood_Desk": ("T_Wood_Desk.png", 1.0, 1.0),
}

# ---- Layout (metres; s = distance south of the main door along the corridor walls) ----

CORR = 2.2                       # corridor half-width: 11 tiles of 40 cm = 4.4 m
IN_X = 9.6                       # inner face of the east / west outer walls
N_S, S_S = 0.4, 44.6             # inner faces of the north / south outer walls
BAND = (22.36, 28.36)            # cross corridor, 6 m
S_END = 38.16                    # the corridor's south dead end; rooms fill S_END..S_S
FIELD = 1.4                      # light field tiles; 2 darker tiles each side
WALL_X = CORR + PART_T           # room side of the corridor partitions
COL_W, COL_PROUD = 0.6, 0.28     # the four columns at the cross corridor's corners
EAST_DOOR = (22.86, 27.86)       # glass doors in the east wall (UtezBuildingBuilder.CecadecEastDoorRaw)
STAIR = dict(x_foot=-4.4, x_land=-7.7, f1=(-24.66, -23.08), f2=(-26.56, -24.86),
             parapet=(-23.08, -22.96), stringer=(-26.68, -26.56), centre=(-24.86, -24.66))

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
N_ROWS = range(3, 35, 5)
S_ROWS = range(50, 63, 5)
BAND_ROWS = (38.5, 41.5, 44.5)
TROFFERS = (
    [(side * PAIR, cell(m)) for m in N_ROWS for side in (-1, 1)]
    + [(0.61 * k, cell(BAND_ROWS[0])) for k in range(-6, 16, 3)]
    + [(0.61 * k, cell(BAND_ROWS[2])) for k in range(-15, 16, 3)]
    + [(side * PAIR, cell(m)) for m in S_ROWS for side in (-1, 1)]
)
DIFFUSERS = ([(0.0, cell(m)) for i, m in enumerate(N_ROWS) if i % 2 == 0]
             + [(0.0, cell(m)) for i, m in enumerate(S_ROWS) if i % 2 == 0]
             + [(0.61 * k, cell(BAND_ROWS[1])) for k in (-6, 0, 6, 12)])
RETURNS = ([(0.0, cell(m)) for i, m in enumerate(N_ROWS) if i % 2 == 1]
           + [(0.0, cell(m)) for i, m in enumerate(S_ROWS) if i % 2 == 1])
SMOKE = [(0.0, cell(m)) for m in (6, 26, 58)] + [(-4.88, cell(40.5))]

# One fluorescent light per fitting pair (UtezKit turns every *_Fluoro empty into a light
# with FluorescentFlicker), four along the cross corridor, one in the open stairwell.
FLUOROS = ([(f"N{i}", 0.0, cell(m), CEIL - 0.08) for i, m in enumerate(N_ROWS, 1)]
           + [(f"S{i}", 0.0, cell(m), CEIL - 0.08) for i, m in enumerate(S_ROWS, 1)]
           + [(f"Band{i}", x, cell(BAND_ROWS[1]), CEIL - 0.08)
              for i, x in enumerate((-3.66, 0.0, 3.66, 7.32), 1)]
           + [("Stair", -6.4, -24.76, 3.7)]
           # Surface battens in the two toilets (world coords of WC's light positions).
           + [("WCMenA", -4.80, -20.46, CEIL - 0.06), ("WCMenB", -7.40, -20.46, CEIL - 0.06),
              ("WCWomen", -5.60, -17.76, CEIL - 0.06)])

# Floor and ceiling pieces: URP lights each object with at most 8 lights, so no piece may
# sit under more than a few fittings. Cuts fall between fitting rows, never through one.
N_BREAKS = [0.0, cell(5.5), cell(15.5), cell(25.5), -BAND[0]]
S_BREAKS = [-BAND[1], cell(53), -S_END]
BAND_X = [-IN_X, -CORR, CORR, 5.9, IN_X]

# Walls between rooms, behind the corridor fronts (one per front boundary).
E_DIVIDERS = (8.605, 14.055, 33.485)
W_DIVIDERS = (5.715, 10.975, 16.235, 33.80)

# ---- Toilets, west side, behind the signs block --------------------------------------
# From the user's hand plan (docs/map/reference/cecadec-interior-pasillo/
# plano-usuario-bano.png) and their own edit of the .blend on 2026-09-15, where the signs
# block was thinned from 1.52 m to 0.25 m: the corridor wall there is thin, each of the
# two openings has no leaf, and a baffle a metre behind it stops you seeing in. The men's
# room (south, the left opening facing the wall) carries the fixtures in the plan's order
# away from the door: basins under a mirror, urinals behind grey screens, WC cubicles.
# The women's room (north, right) is blocked with a steel cabinet and left empty.
#
# One change from the sketch: it draws the cubicles in the same 1.1 m strip as the other
# two bays, which at the room's real 2.59 m width would make them 0.62 m wide. They turn
# to face the door instead and take the full width of the back wall.
WC = dict(
    s0=16.46, s1=21.76,           # inner faces of the block's north and south walls
    door_n=(16.46, 17.46),        # women's opening
    door_s=(20.76, 21.76),        # men's opening
    front=0.25,                   # depth of the thin corridor wall between the openings
    baffle=(1.40, 1.52),          # screen wall a metre inside each opening
    split=(19.05, 19.17),         # divider between the two rooms
    back=7.38,                    # inner face of the west outer wall's plaster lining
    basins=(1.64, 3.44),          # depth range of the washbasin slab
    bay_wall=(3.58, 3.70),        # return wall between basins and urinals
    urinals=(4.20, 4.80, 5.40),   # depths of the three urinal centres
    screens=(4.50, 5.10, 5.70),   # depths of the grey plywood screens between them
    stalls=(5.95, 7.38),          # depth range of the cubicle row
    bay=1.15,                     # how far the fixture bays reach off the south wall
)


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
        _slab(col, f"Floor_Rooms_S{side}", rect(x0, -S_END, x1, -(BAND[1] + PART_T)), z0, z1, "Kit_Tile_Floor")
    # The corridor dead-ends at S_END; the last bay of the building is one full-width room.
    for i, (x0, x1) in enumerate(zip((-IN_X, 0.0), (0.0, IN_X)), 1):
        _slab(col, f"Floor_Back{i}", rect(x0, -S_S, x1, -S_END), z0, z1, "Kit_Tile_Floor")


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
        _slab(col, f"Ceiling_Rooms_S{side}", rect(x0, -S_END, x1, -(BAND[1] + PART_T)), z0, z1, "Kit_Ceiling")
    for i, (x0, x1) in enumerate(zip((-IN_X, 0.0), (0.0, IN_X)), 1):
        _slab(col, f"Ceiling_Back{i}", rect(x0, -S_S, x1, -S_END), z0, z1, "Kit_Ceiling")


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
    for s in E_DIVIDERS:
        walls.append(((WALL_X, -s - t, 0.0), (IN_X - 0.02, -s + t, CEIL)))
    for s in W_DIVIDERS:
        walls.append(((-IN_X + 0.02, -s - t, 0.0), (-WALL_X, -s + t, CEIL)))
    _boxes(col, "Room_Dividers", walls, "Kit_Wall_White")
    lin = [
        ((WALL_X, -N_S - 0.02, 0.0), (IN_X, -N_S, CEIL)), ((-IN_X, -N_S - 0.02, 0.0), (-WALL_X, -N_S, CEIL)),
        ((IN_X - 0.02, -(BAND[0] - PART_T), 0.0), (IN_X, -N_S - 0.02, CEIL)),
        ((-IN_X, -(BAND[0] - PART_T), 0.0), (-IN_X + 0.02, -N_S - 0.02, CEIL)),
        ((IN_X - 0.02, -S_S + 0.02, 0.0), (IN_X, -(BAND[1] + PART_T), CEIL)),
        ((-IN_X, -S_S + 0.02, 0.0), (-IN_X + 0.02, -(BAND[1] + PART_T), CEIL)),
        ((-IN_X, -S_S, 0.0), (IN_X, -S_S + 0.02, CEIL)),
    ]
    _boxes(col, "Lining_Rooms", lin, "Kit_Wall_White")
    # Back wall of the corridor: the dead end between the fronts, and the room divider
    # that carries the same line out to the outer walls.
    _boxes(col, "Wall_CorridorEnd", [((-CORR, -S_END, 0.0), (CORR, -S_END + PART_T, CEIL))], "Kit_Wall_White")
    _boxes(col, "Wall_CorridorEnd_Skirting",
           [((-CORR, -S_END + PART_T, 0.0), (CORR, -S_END + PART_T + BASE_T, 0.10))], "Kit_Frame")
    _boxes(col, "Room_BackDivider", [((-IN_X + 0.02, -S_END, 0.0), (-WALL_X, -S_END + PART_T, CEIL)),
                                     ((WALL_X, -S_END, 0.0), (IN_X - 0.02, -S_END + PART_T, CEIL))],
           "Kit_Wall_White")
    _boxes(col, "Lining_Stairwell", [((-IN_X, -BAND[1], 0.0), (-IN_X + 0.02, -BAND[0], 4.0))], "Kit_Wall_White")
    _boxes(col, "Lining_EastDoor", [((IN_X - 0.02, -EAST_DOOR[0], 0.0), (IN_X, -BAND[0], CEIL)),
                                    ((IN_X - 0.02, -BAND[1], 0.0), (IN_X, -EAST_DOOR[1], CEIL))], "Kit_Wall_White")


def u_corridor_east(col):
    """East side, main door to the cross corridor (photos 5, 6, 7, 13, 22, 23, 24).

    Three rooms only: Aula 1 (one room, one door — its glazed front runs past the column
    that stands between the two benches), Aula 2, and CC9, which is the widest of them."""
    I.plaster(col, "E_Jamb", E, N_S, 0.7)
    # Aula 1, north half: the leaf and its paper signs live here.
    I.glazed_front(col, "Aula1", E, 0.7, 4.69, [0.59, 1.18, 1.20],
                   [dict(at="end", name="Aula1", signs=[("CARD_WHITE", 0.0, 1.55)])])
    I.decal(col, "Aula1", E, "AULA1", 3.34, 1.65, d=0.055)
    I.decal(col, "Aula1_Prohibido", E, "PROHIBIDO_A1", 3.34, 1.44, d=0.055)
    I.pillar(col, "Pillar_E1", E, 4.915)
    I.extinguisher(col, "Extinguisher_E1", E, 4.915, top=1.55, face=-PILLAR_PROUD)
    # Aula 1, south half: same partition past the column, glazing only.
    I.glazed_front(col, "Aula1South", E, 5.14, 8.38, [0.93, 0.92, 0.37], [])
    I.pillar(col, "Pillar_E2", E, 8.605)
    I.glazed_front(col, "Aula2", E, 8.83, 13.83, [0.93, 0.92, 0.37],
                   [dict(at="start", name="Aula2", transom="panel", signs=[("HORARIO_A2", 0.0, 1.53)])])
    I.decal(col, "Aula2", E, "AULA2", 10.25, 1.48, d=0.055)
    I.pillar(col, "Pillar_E3", E, 14.055)
    I.extinguisher(col, "Extinguisher_E3", E, 14.055, top=1.55, face=-PILLAR_PROUD)
    I.decal(col, "Extintor_E3", E, "EXTINTOR", 14.055, 2.05, d=-PILLAR_PROUD - 0.004)
    I.plain_front(col, "CC9", E, 14.28, BAND[0] - COL_W, door=dict(at="end", name="CC9", signs=[
        ("CC9", 0.0, 1.90), ("PROHIBIDO_CC9", 0.0, 1.66), ("AHORREMOS", -0.22, 1.50), ("HORARIO_CC9", 0.18, 1.50)]))
    I.pillar(col, "Column_NE", E, BAND[0] - COL_W / 2, width=COL_W, proud=COL_PROUD)


def u_corridor_west(col):
    """West side, main door to the cross corridor (photos 1, 2, 3, 4, 8, 23, 24).

    Two smoked-glass fronts, the electrical closet, then the toilet alcoves either side of
    the signs block, hard against the cross corridor's column. Layout corrected on the
    user's own hand placement of those elements (2026-09-15)."""
    I.plaster(col, "W_Jamb", W, N_S, 0.7)
    I.tinted_front(col, "RoomNW1", W, 0.7, 5.5, sign="PROHIBIDO_A1")
    I.pillar(col, "Pillar_W1", W, 5.715)
    I.tinted_front(col, "RoomNW2", W, 5.93, 10.73, sign="PROHIBIDO_A1")
    I.pillar(col, "Pillar_W2", W, 10.975)
    I.closet(col, "Electrical", W, 11.21, 16.01)
    I.pillar(col, "Pillar_W3", W, 16.235, skirt=0.04)

    # Toilets: thin front wall, two doorless openings, a baffle inside each (see WC).
    b0, b1 = WC["baffle"]
    for name, (s0, s1), side in (("ToiletA", WC["door_n"], "north"), ("ToiletB", WC["door_s"], "south")):
        mb = MeshBuilder(["Kit_Wall_White"])
        W.box(mb, s0, s1, b0, b1, 0.0, CEIL, "Kit_Wall_White")
        if side == "north":
            W.box(mb, s0 - PART_T, s0, 0.0, WC["back"], 0.0, CEIL, "Kit_Wall_White")
        else:
            W.box(mb, s1, s1 + PART_T, 0.0, WC["back"], 0.0, CEIL, "Kit_Wall_White")
        make_object(name + "_Walls", mb, col)
        sk = MeshBuilder(["Kit_Frame"])
        W.box(sk, s0, s1, b0 - 0.006, b0, 0.0, 0.10, "Kit_Frame")
        make_object(name + "_Skirting", sk, col)
    # Photo 8 (facing west): the light switch sits on the baffle of the men's entrance.
    I.decal(col, "ToiletB_Switch", W, "SWITCH", 21.26, 1.05, d=b0 - 0.004)
    I.decal(col, "WC_Men", W, "WC_MEN", 20.62, 1.85)
    I.decal(col, "WC_Women", W, "WC_WOMEN", 17.60, 1.85)
    I.decal(col, "WC_FueraServicio", W, "FUERA_SERVICIO", 17.60, 1.50)

    blk = MeshBuilder(["Kit_Wall_White"])
    W.box(blk, 17.46, 20.76, 0.0, WC["front"], 0.0, CEIL, "Kit_Wall_White")
    make_object("SignsBlock", blk, col)
    sk = MeshBuilder(["Kit_Frame"])
    W.box(sk, 17.46, 20.76, -0.006, 0.0, 0.0, 0.10, "Kit_Frame")
    make_object("SignsBlock_Skirting", sk, col)
    I.decal(col, "Block_Evac", W, "EVAC_RIGHT", 19.11, 2.20)
    I.decal(col, "Block_Agua", W, "AHORRA_AGUA", 19.11, 1.80)
    I.decal(col, "Block_Residuos", W, "COLOCA", 19.82, 1.52)
    I.decal(col, "Block_Switch", W, "SWITCH", 19.21, 0.94)
    for name, s, label, lid in (("PET", 20.00, "BIN_PET", False), ("Organico", 19.26, "BIN_ORG", False),
                                ("Otros", 18.45, "BIN_OTROS", True)):
        x, y, _ = W.p(s, -0.30, 0.0)
        I.recycling_bin(col, "Bin_" + name, x, y, (1.0, 0.0), label, lid=lid)
    broom = MeshBuilder(["Kit_Metal_Red", "Kit_Wood_Door"])
    hx, hy, _ = W.p(16.76, 1.2, 0.0)
    broom.box((hx - 0.04, hy - 0.15, 0.0), (hx + 0.04, hy + 0.15, 0.12), "Kit_Metal_Red")
    tx, ty, _ = W.p(16.61, 1.33, 0.0)
    broom.tube([(hx, hy, 0.12), (tx, ty, 1.35)], [0.012, 0.012], "Kit_Wood_Door", segs=6)
    make_object("Detail_Broom", broom, col)

    I.pillar(col, "Column_NW", W, BAND[0] - COL_W / 2, width=COL_W, proud=COL_PROUD, skirt=0.035)


def u_toilets(col):
    """The two toilets behind the signs block (layout and sources in WC).

    Men's room (south): washbasins on a cast concrete slab under a wall mirror, a return
    wall, three urinals behind grey plywood screens, three WC cubicles across the back.
    Women's room (north): tiled, empty, and blocked at its opening by a steel cabinet.
    Both are tiled to 2.10 m; above that the rooms' plaster lining shows, as on site.
    """
    men0, men1 = WC["split"][1], WC["s1"]
    wom0, wom1 = WC["s0"], WC["split"][0]
    front, back = WC["front"], WC["back"]
    t = 0.012

    B.partition(col, "WC_Divider", W, WC["split"][0], WC["split"][1], front, back, 0.0, CEIL)
    for tag, s_a, s_b, d_a, d_b in (
            ("Men_South", men1 - t, men1, front, back), ("Men_Divider", men0, men0 + t, front, back),
            ("Men_Front", men0, WC["door_s"][0], front, front + t),
            ("Men_Back", men0, men1, back - t, back),
            ("Women_North", wom0, wom0 + t, front, back), ("Women_Divider", wom1 - t, wom1, front, back),
            ("Women_Front", WC["door_n"][1], wom1, front, front + t),
            ("Women_Back", wom0, wom1, back - t, back)):
        B.partition(col, "WC_Tile_" + tag, W, s_a, s_b, d_a, d_b, 0.0, B.TILE_TOP, mat="Kit_Tile_Wall")

    face = men1 - t                      # the tiled face the men's fittings hang on
    B.basin_counter(col, "WC_Basins", W, men1, -1.0, *WC["basins"])
    B.mirror(col, "WC_Mirror", W, face, -1.0, *WC["basins"])
    B.partition(col, "WC_BayWall", W, men1 - WC["bay"], men1, *WC["bay_wall"], 0.0, B.TILE_TOP,
                mat="Kit_Tile_Wall")
    for i, dc in enumerate(WC["urinals"], 1):
        B.urinal(col, f"WC_Urinal{i}", W, men1, -1.0, dc)
    for i, dc in enumerate(WC["screens"], 1):
        B.urinal_screen(col, f"WC_Screen{i}", W, face, -1.0, dc)
    B.cubicle_row(col, "WC_Stall", W, men0, men1, WC["stalls"][0], WC["stalls"][1] - t)

    disp = MeshBuilder(["Kit_Frame"])
    W.box(disp, men0 + t, men0 + t + 0.10, 2.25, 2.55, 1.25, 1.60, "Kit_Frame")
    make_object("Detail_TowelDispenser", disp, col)

    # The women's room is shut: a steel cabinet shoved across the opening from the inside.
    cx, cy, _ = W.p(sum(WC["door_n"]) / 2, 0.50, 0.0)
    B.steel_cabinet(col, "WC_Women_Blockage", cx, cy, (1.0, 0.0))

    for name, s, d in (("MenA", 20.46, 2.60), ("MenB", 20.46, 5.20), ("Women", 17.76, 3.40)):
        lx, ly, _ = W.p(s, d, 0.0)
        B.batten_light(col, name, lx, ly)
    for name, s, d in (("Men", 20.30, 4.60), ("Women", 17.76, 5.60)):
        dx, dy, _ = W.p(s, d, 0.0)
        B.floor_drain(col, "Detail_Drain" + name, dx, dy)


def u_band(col):
    """Cross corridor walls and what stands against them (photos 10, 11, 20)."""
    I.plaster(col, "BandN_East", BN, WALL_X, IN_X)
    for i, x in enumerate((3.9, 5.2, 6.5, 7.8), 1):
        I.decal(col, f"Plaque{i}", BN, "PLAQUE", x, 1.75)
    # Waiting tables along the wall on the way to the east doors ("entrada 2"), from
    # mesas-entrada2-a/b/c.png: 1.22 x 0.60 m panel-sided tables, their backs 3 cm off
    # the wall. The one nearest the corridor carries the book set and the magazines.
    for i, x in enumerate((3.6, 5.4, 7.2), 1):
        B.coffee_table(col, f"LowBench{i}", x, -BAND[0] - 0.33,
                       books=(-0.20, 0.12, 0.05, -0.14) if i == 1 else None)
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
    """Cross corridor to the corridor's dead end (photos 11, 12). East: Lab de Procesos
    and the south-east lab; west: Lab IoT and the south-west room. The last bay of the
    building, past S_END, is a room, not corridor."""
    b1 = BAND[1]
    I.pillar(col, "Column_SE", E, b1 + COL_W / 2, width=COL_W, proud=COL_PROUD)
    I.pillar(col, "Column_SW", W, b1 + COL_W / 2, width=COL_W, proud=COL_PROUD)
    s0 = b1 + COL_W

    I.plain_front(col, "LabProcesos", E, s0, 33.26, door=dict(at="start", name="LabProcesos", signs=[
        ("LAB_PROCESOS", 0.0, 1.93), ("LAB_NOTICE", 0.0, 1.60)]))
    I.decal(col, "Procesos_Evac", E, "EVAC_LEFT", s0 + 1.37, 1.92)
    I.decal(col, "Procesos_Apaga", E, "APAGA", s0 + 1.30, 1.62)
    I.pillar(col, "Pillar_S1", E, 33.485)
    I.plain_front(col, "LabSE", E, 33.71, S_END, door=dict(at="start", name="LabSE"))

    I.glazed_front(col, "LabIoT", W, s0, s0 + 4.84, [1.0, 1.0, 1.0],
                   [dict(at="start", name="LabIoTSide", transom="Kit_Glass_Clear"),
                    dict(at="start", name="LabIoT", infill="Kit_Panel_Grey", glass_from=1.10,
                         transom="Kit_Glass_Clear")],
                   knee=0.95, lower="Kit_Glass_Clear", upper="Kit_Glass_Clear", head=2.25)
    I.decal(col, "LabIoT", W, "LAB_IOT", s0 + 2.54, 1.90, d=0.055)
    I.plain_front(col, "RoomSW", W, s0 + 4.84, S_END)


def u_furniture(col):
    for name, wall, s in (("Bench_Aula1", E, 2.0), ("Bench_Aula2", E, 7.4), ("Bench_IoT", W, 32.46)):
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
    "toilets": ("Toilets", u_toilets),
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
    "photo24_looking_back": ((-0.4, -11.0, 1.55), (0.0, 0.0, 1.45), 20),
    "photo5_aula2": ((-1.6, -11.3, 1.35), (2.2, -11.3, 1.35), 20),
    "photo2_closet": ((1.7, -13.6, 1.40), (-2.2, -13.6, 1.35), 20),
    "photo8_bins": ((1.7, -19.11, 1.30), (-2.2, -19.11, 1.20), 20),
    "photo20_east_doors": ((-1.2, -25.36, 1.55), (9.6, -25.36, 1.25), 20),
    "photo10_stairs": ((1.6, -24.56, 1.55), (-9.6, -24.76, 1.9), 20),
    "photo11_south": ((0.4, -23.76, 1.55), (0.0, -38.16, 1.3), 20),
    # Toilets and the waiting tables (2026-09-16).
    "wc_men_in": ((-3.6, -21.0, 1.60), (-9.2, -20.6, 1.10), 18),
    "wc_men_basins": ((-4.9, -19.5, 1.50), (-4.7, -21.7, 1.00), 20),
    "wc_men_stalls": ((-6.3, -20.2, 1.60), (-9.3, -20.4, 1.20), 20),
    "wc_women_blocked": ((-1.3, -16.96, 1.55), (-5.0, -16.96, 1.15), 20),
    "tables_entrance2": ((1.9, -24.6, 1.45), (8.4, -22.7, 0.55), 22),
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
