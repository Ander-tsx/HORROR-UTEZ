"""
CECADEC above the ground floor, v1 (2026-09-16): the intermediate floor between storeys
and the upper floor's corridor.

Two things live in this file because they share one origin and one Unity prefab:

* **Intermediate floor** (`mezzanine`). Between the ground floor's suspended ceiling at
  2.70 m and the upper floor's finished floor at 4.00 m there is 1.30 m of structure —
  the thickness the user noticed. It is modelled as a real, walkable level: a concrete
  deck at 2.82, the upper slab's soffit at 3.86 (1.04 m clear), the perimeter, the
  columns coming through and the downstand beams. **Empty on purpose**: the hatches, air
  vents and pipework are the next pass, and they go in this same unit.

* **Upper floor** (`f2_*`). Same corridor as the ground floor — 4.4 m clear, 2.70 m
  ceiling, same structural columns, the same stairwell — with its own rooms, read off
  docs/map/reference/cecadec-interior-piso2/ (photos 1-19 as delivered). Where a photo
  did not fix something, the ground floor's grid decided it, so the two floors line up.

Layout defined from the photos (s = metres south of the main door, as on the ground floor):

| s             | east side                         | west side                              |
|---------------|-----------------------------------|----------------------------------------|
| 0.70 - 4.69   | Aula Virtual (photo 5)            | Aula de Capacitacion en TI 1 (photo 3) |
| 5.14 - 8.38   | Aula de Capacitacion en TI 6 (5)  | Area de Consultoria / Sala de maestros (3) |
| 8.83 - 13.83  | Aula de Capacitacion en TI 3 (6)  | Cisco Networking Academy (13, 16, 17)  |
| 14.28 - 21.76 | Aula de Capacitacion en TI 2 (6)  | Centro de Certificacion (4)            |
| 22.36 - 28.36 | cross corridor: Site de Comunicaciones on its north wall (15, 18), stairwell west (1) |
| 28.96 - 38.16 | Sala de Juntas, almacen (12)      | Coordinacion, oficinas (16, 17)        |
| 38.16         | Centro de Computo, glazed screen across the corridor's dead end (14)   |

Axes and origin as gen_cecadec_interior.py: X east, Y north, Z up, metres, origin on the
main door threshold at ground level. Every `f2_*` unit is authored with its floor at
z = 0 and lifted by LIFT afterwards, so lm_interior's helpers (which assume a floor at
zero and a 2.70 m ceiling) can be reused unchanged.

The layout constants below are copied from gen_cecadec_interior.py on purpose: the two
files are separate landmarks with separate .blend files, and the user edits both by hand.
Change one, change the other.

Prefer running through Tools/blender/run_landmark.py.
"""

import importlib
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
from lm_interior import BAR, BASE_H, BASE_T, CEIL, PART_T, PILLAR_PROUD, Wall  # noqa: E402

I = lm_interior
PREFIX = "CecadecUpper_"

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
    "Kit_Panel_Grey": ("T_Panel_Grey.png", 1.0, 1.0),
    "Kit_Metal_Grey": ("T_Metal_Grey.png", 1.0, 1.0),
    "Kit_Metal_White": ("T_Metal_White.png", 1.0, 1.0),
    "Kit_Metal_Red": ("T_Metal_Red.png", 1.0, 1.0),
    "Kit_Chrome": ("T_Chrome.png", 2.0, 2.0),
    "Kit_Plastic_Grey": ("T_Plastic_Grey.png", 1.0, 1.0),
    "Kit_Light": ("T_Light.png", 1.0, 1.0),
    "Kit_Signs": ("T_Signs.png", 1.0, 1.0),
    "Kit_Concrete": ("T_Concrete.png", 0.5, 0.5),
    "Kit_Wood_Desk": ("T_Wood_Desk.png", 1.0, 1.0),
}

# ---- Layout (copied from gen_cecadec_interior.py; keep the two in step) ----------------

CORR = 2.2
IN_X = 9.6
N_S, S_S = 0.4, 44.6
BAND = (22.36, 28.36)
S_END = 38.16
FIELD = 1.4
WALL_X = CORR + PART_T
COL_W, COL_PROUD = 0.6, 0.28
E_PILLARS = (4.915, 8.605, 14.055, 33.485)
W_PILLARS = (5.715, 10.975, 16.235, 33.80)

# ---- Levels ----------------------------------------------------------------------------

F2 = 4.0                 # UtezDimensions.FloorHeight: finished floor of the upper storey
DECK = 2.82              # walking surface of the intermediate floor
DECK_BASE = 2.72         # its underside, on top of the ground floor's ceiling slab
SOFFIT = 3.86            # underside of the upper storey's structural slab
BEAM_D = 0.34            # depth of the downstand beams hanging from that slab

E = Wall((CORR, 0.0), (0.0, -1.0), (1.0, 0.0))
W = Wall((-CORR, 0.0), (0.0, -1.0), (-1.0, 0.0))
BN = Wall((0.0, -BAND[0]), (1.0, 0.0), (0.0, 1.0))
BS = Wall((0.0, -BAND[1]), (1.0, 0.0), (0.0, -1.0))
XW = Wall((-IN_X, 0.0), (0.0, -1.0), (-1.0, 0.0))
XN = Wall((0.0, -N_S), (1.0, 0.0), (0.0, 1.0))

# Stairwell void in the upper slab: the flight from the ground floor lands at x = -4.4
# (STAIR in gen_cecadec_interior.py), so the slab starts there.
WELL = dict(x0=-IN_X, x1=-4.40, y0=-26.68, y1=-22.96)
# The intermediate floor's own hole is the bigger one: it has to clear the whole stair
# enclosure, including the ground floor's ceiling notch (Ceiling_Band1 in the interior
# generator), or a 15 cm ledge hangs over the flight at 2.82 m.
WELL_MEZZ = dict(x0=-IN_X, x1=-4.25, y0=-26.76, y1=-22.91)
# The tall window lighting the well, in the west outer wall (photo 1). The room lining
# stops either side of it. s is measured south of the main door, as everywhere else.
WINDOW = (23.30, 26.30, 0.95, 2.52)      # s0, s1, sill, head

# Upper corridor ceiling: same 61 cm grid and the same fitting rhythm as the ground floor.
PAIR = 1.22
N_ROWS = range(3, 35, 5)
S_ROWS = range(50, 63, 5)
BAND_ROWS = (38.5, 41.5, 44.5)


def cell(m):
    return -0.61 * m


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
SMOKE = [(0.0, cell(m)) for m in (6, 26, 58)]

FLUOROS = ([(f"N{i}", 0.0, cell(m), CEIL - 0.08) for i, m in enumerate(N_ROWS, 1)]
           + [(f"S{i}", 0.0, cell(m), CEIL - 0.08) for i, m in enumerate(S_ROWS, 1)]
           + [(f"Band{i}", x, cell(BAND_ROWS[1]), CEIL - 0.08)
              for i, x in enumerate((-3.66, 0.0, 3.66, 7.32), 1)]
           + [("Well", -6.4, -24.8, CEIL - 0.08)])

N_BREAKS = [0.0, cell(5.5), cell(15.5), cell(25.5), -BAND[0]]
S_BREAKS = [-BAND[1], cell(53), -S_END]
BAND_X = [-IN_X, -CORR, CORR, 5.9, IN_X]

E_DIVIDERS = (4.915, 8.605, 14.055, 33.485)
W_DIVIDERS = (5.715, 10.975, 16.235, 33.80)


def _pieces(breaks):
    return list(zip(breaks[:-1], breaks[1:]))


def _slab(col, name, poly, z0, z1, mat, holes=(), side_mat=None):
    mb = MeshBuilder([mat])
    mb.prism(poly, z0, z1, mat, side_mat=side_mat, bottom=True, holes=holes)
    return make_object(name, mb, col)


def _boxes(col, name, boxes, mat):
    mb = MeshBuilder([mat])
    for lo, hi in boxes:
        mb.box(lo, hi, mat)
    return make_object(name, mb, col)


# ---- Intermediate floor ------------------------------------------------------------------

def u_mezzanine(col):
    """The level between the storeys: deck, soffit, perimeter, columns, beams.

    Deliberately bare. The hatches down into the ceiling void, the air handling and the
    pipe runs come next and belong here, so this unit can be rebuilt on its own.
    """
    wm = WELL_MEZZ
    well = rect(wm["x0"], wm["y0"], wm["x1"], wm["y1"])
    x_breaks = (-IN_X, -3.0, 3.0, IN_X)
    y_breaks = (-S_S, -30.0, -15.0, -N_S)
    for i, (x0, x1) in enumerate(zip(x_breaks[:-1], x_breaks[1:]), 1):
        for j, (y0, y1) in enumerate(zip(y_breaks[:-1], y_breaks[1:]), 1):
            poly = rect(x0, y0, x1, y1)
            holes = [well] if (x0 < wm["x1"] and wm["y0"] > y0 and wm["y1"] < y1) else []
            _slab(col, f"Mezz_Deck{i}{j}", poly, DECK_BASE, DECK, "Kit_Concrete", holes=holes)
            _slab(col, f"Mezz_Soffit{i}{j}", poly, SOFFIT, SOFFIT + 0.12, "Kit_Concrete", holes=holes)

    lining = [
        ((-IN_X, -S_S, DECK), (-IN_X + 0.02, -N_S, SOFFIT)),
        ((IN_X - 0.02, -S_S, DECK), (IN_X, -N_S, SOFFIT)),
        ((-IN_X, -N_S - 0.02, DECK), (IN_X, -N_S, SOFFIT)),
        ((-IN_X, -S_S, DECK), (IN_X, -S_S + 0.02, SOFFIT)),
    ]
    _boxes(col, "Mezz_Perimeter", lining, "Kit_Concrete")

    # Columns carried up through the void, on the ground floor's own column lines.
    cols = []
    for s in E_PILLARS:
        cols.append(((CORR - PILLAR_PROUD, -s - 0.225, DECK), (CORR + PART_T, -s + 0.225, SOFFIT)))
    for s in W_PILLARS:
        cols.append(((-CORR - PART_T, -s - 0.225, DECK), (-CORR + PILLAR_PROUD, -s + 0.225, SOFFIT)))
    for sx in (-1, 1):
        for s in (BAND[0] - COL_W / 2, BAND[1] + COL_W / 2):
            a = sx * (CORR - COL_PROUD)
            b = sx * (CORR + PART_T)
            cols.append(((min(a, b), -s - COL_W / 2, DECK), (max(a, b), -s + COL_W / 2, SOFFIT)))
    _boxes(col, "Mezz_Columns", cols, "Kit_Concrete")

    # Downstand beams on the column lines, spanning east to west under the upper slab.
    beams = []
    for s in sorted(set(E_PILLARS + W_PILLARS + (BAND[0] - COL_W / 2, BAND[1] + COL_W / 2))):
        y = -s
        if wm["y0"] < y < wm["y1"]:
            beams.append(((wm["x1"], y - 0.12, SOFFIT - BEAM_D), (IN_X, y + 0.12, SOFFIT)))
        else:
            beams.append(((-IN_X, y - 0.12, SOFFIT - BEAM_D), (IN_X, y + 0.12, SOFFIT)))
    _boxes(col, "Mezz_Beams", beams, "Kit_Concrete")
    # Two spine beams north-south, either side of the corridor below.
    _boxes(col, "Mezz_SpineBeams",
           [((-CORR - 0.12, -S_S, SOFFIT - BEAM_D), (-CORR + 0.12, -N_S, SOFFIT)),
            ((CORR - 0.12, -S_S, SOFFIT - BEAM_D), (CORR + 0.12, -N_S, SOFFIT))], "Kit_Concrete")
    # Edge upstand round the stairwell void, so nobody walks off it in the dark.
    _boxes(col, "Mezz_WellKerb",
           [((wm["x0"], wm["y1"], DECK), (wm["x1"], wm["y1"] + 0.12, DECK + 0.25)),
            ((wm["x0"], wm["y0"] - 0.12, DECK), (wm["x1"], wm["y0"], DECK + 0.25)),
            ((wm["x1"], wm["y0"] - 0.12, DECK), (wm["x1"] + 0.12, wm["y1"] + 0.12, DECK + 0.25))],
           "Kit_Concrete")


# ---- Upper floor: shell --------------------------------------------------------------

def u_f2_floors(col):
    z0, z1 = -0.02, 0.0
    f = FIELD
    for label, breaks in (("Corridor", N_BREAKS), ("South", S_BREAKS)):
        for i, (ya, yb) in enumerate(_pieces(breaks), 1):
            _slab(col, f"F2_Floor_{label}_Field{i}", rect(-f, yb, f, ya), z0, z1, "Kit_Tile_Floor")
            _boxes(col, f"F2_Floor_{label}_Border{i}", [((f, yb, z0), (CORR, ya, z1)),
                                                        ((-CORR, yb, z0), (-f, ya, z1))], "Kit_Tile_Border")
    b0, b1 = -BAND[0], -BAND[1]
    for i, (xa, xb) in enumerate(zip(BAND_X[:-1], BAND_X[1:]), 1):
        if xa == -IN_X:
            continue                      # the stairwell void takes this bay; see below
        if xa == -CORR:
            _slab(col, f"F2_Floor_Band_Field{i}", rect(xa, b1, xb, b0), z0, z1, "Kit_Tile_Floor")
            continue
        _slab(col, f"F2_Floor_Band_Field{i}", rect(xa, b1 + 0.8, xb, b0 - 0.8), z0, z1, "Kit_Tile_Floor")
        _boxes(col, f"F2_Floor_Band_Border{i}", [((xa, b0 - 0.8, z0), (xb, b0, z1)),
                                                 ((xa, b1, z0), (xb, b1 + 0.8, z1))], "Kit_Tile_Border")
    # West bay of the cross corridor: floor everywhere the stairwell void is not.
    _slab(col, "F2_Floor_Well_N", rect(-IN_X, WELL["y1"], -CORR, b0), z0, z1, "Kit_Tile_Floor")
    _slab(col, "F2_Floor_Well_S", rect(-IN_X, b1, -CORR, WELL["y0"]), z0, z1, "Kit_Tile_Floor")
    _slab(col, "F2_Floor_Well_E", rect(WELL["x1"], WELL["y0"], -CORR, WELL["y1"]), z0, z1, "Kit_Tile_Floor")

    for side, x0, x1 in (("E", WALL_X, IN_X), ("W", -IN_X, -WALL_X)):
        _slab(col, f"F2_Floor_Rooms_N{side}", rect(x0, -(BAND[0] - PART_T), x1, -N_S), z0, z1, "Kit_Tile_Floor")
        _slab(col, f"F2_Floor_Rooms_S{side}", rect(x0, -S_END, x1, -(BAND[1] + PART_T)), z0, z1, "Kit_Tile_Floor")
    for i, (x0, x1) in enumerate(zip((-IN_X, 0.0), (0.0, IN_X)), 1):
        _slab(col, f"F2_Floor_Back{i}", rect(x0, -S_S, x1, -S_END), z0, z1, "Kit_Tile_Floor")


def _holes_in(x0, y0, x1, y1):
    return [rect(x - 0.3, y - 0.3, x + 0.3, y + 0.3) for x, y in TROFFERS
            if x0 < x - 0.3 and x + 0.3 < x1 and y0 < y - 0.3 and y + 0.3 < y1]


def u_f2_ceilings(col):
    z0, z1 = CEIL, CEIL + 0.02
    b0, b1 = -BAND[0], -BAND[1]
    for label, breaks in (("Corridor", [-N_S] + N_BREAKS[1:]), ("South", S_BREAKS)):
        for i, (ya, yb) in enumerate(_pieces(breaks), 1):
            _slab(col, f"F2_Ceiling_{label}{i}", rect(-CORR, yb, CORR, ya), z0, z1, "Kit_Ceiling",
                  holes=_holes_in(-CORR, yb, CORR, ya))
    for i, (xa, xb) in enumerate(zip(BAND_X[:-1], BAND_X[1:]), 1):
        _slab(col, f"F2_Ceiling_Band{i}", rect(xa, b1, xb, b0), z0, z1, "Kit_Ceiling",
              holes=_holes_in(xa, b1, xb, b0))
    for side, x0, x1 in (("E", WALL_X, IN_X), ("W", -IN_X, -WALL_X)):
        _slab(col, f"F2_Ceiling_Rooms_N{side}", rect(x0, -(BAND[0] - PART_T), x1, -N_S), z0, z1, "Kit_Ceiling")
        _slab(col, f"F2_Ceiling_Rooms_S{side}", rect(x0, -S_END, x1, -(BAND[1] + PART_T)), z0, z1, "Kit_Ceiling")
    for i, (x0, x1) in enumerate(zip((-IN_X, 0.0), (0.0, IN_X)), 1):
        _slab(col, f"F2_Ceiling_Back{i}", rect(x0, -S_S, x1, -S_END), z0, z1, "Kit_Ceiling")


def u_f2_ceiling_fixtures(col):
    for i, (x, y) in enumerate(TROFFERS, 1):
        I.troffer(col, "F2_" + str(i), x, y)
    for i, (x, y) in enumerate(DIFFUSERS, 1):
        I.diffuser(col, "F2_" + str(i), x, y)
    for i, (x, y) in enumerate(RETURNS, 1):
        I.return_grille(col, "F2_" + str(i), x, y)
    for i, (x, y) in enumerate(SMOKE, 1):
        I.smoke_detector(col, "F2_" + str(i), x, y)


def u_f2_rooms(col):
    """Room shells behind the upper fronts: dividers and a plaster lining over the kit's
    outer walls. Interiors (photos 7-13, 16-18) come in their own phase."""
    t = PART_T / 2
    walls = []
    for s in E_DIVIDERS:
        walls.append(((WALL_X, -s - t, 0.0), (IN_X - 0.02, -s + t, CEIL)))
    for s in W_DIVIDERS:
        walls.append(((-IN_X + 0.02, -s - t, 0.0), (-WALL_X, -s + t, CEIL)))
    _boxes(col, "F2_Room_Dividers", walls, "Kit_Wall_White")
    lin = [
        ((WALL_X, -N_S - 0.02, 0.0), (IN_X, -N_S, CEIL)), ((-IN_X, -N_S - 0.02, 0.0), (-WALL_X, -N_S, CEIL)),
        ((IN_X - 0.02, -(BAND[0] - PART_T), 0.0), (IN_X, -N_S - 0.02, CEIL)),
        ((-IN_X, -(BAND[0] - PART_T), 0.0), (-IN_X + 0.02, -N_S - 0.02, CEIL)),
        ((IN_X - 0.02, -S_S + 0.02, 0.0), (IN_X, -(BAND[1] + PART_T), CEIL)),
        ((-IN_X, -S_S + 0.02, 0.0), (-IN_X + 0.02, -(BAND[1] + PART_T), CEIL)),
        ((-IN_X, -S_S, 0.0), (IN_X, -S_S + 0.02, CEIL)),
    ]
    _boxes(col, "F2_Lining_Rooms", lin, "Kit_Wall_White")
    _boxes(col, "F2_Room_BackDivider",
           [((-IN_X + 0.02, -S_END, 0.0), (-WALL_X, -S_END + PART_T, CEIL)),
            ((WALL_X, -S_END, 0.0), (IN_X - 0.02, -S_END + PART_T, CEIL))], "Kit_Wall_White")
    # East wall of the cross corridor, and the west wall either side of the well's window.
    _boxes(col, "F2_Lining_Band",
           [((IN_X - 0.02, -BAND[1], 0.0), (IN_X, -BAND[0], CEIL)),
            ((-IN_X, -BAND[1], 0.0), (-IN_X + 0.02, -WINDOW[1], CEIL)),
            ((-IN_X, -WINDOW[0], 0.0), (-IN_X + 0.02, -BAND[0], CEIL))], "Kit_Wall_White")


# ---- Upper floor: corridor ---------------------------------------------------------------

def u_f2_corridor_east(col):
    """East side of the north leg (photos 5 and 6): two pairs of classroom doors, each
    pair meeting at a structural column, with frosted glazed fronts between them."""
    I.plaster(col, "F2_E_Jamb", E, N_S, 0.7)
    # Every blue room plaque is taped to its own leaf, so it swings with the door.
    I.glazed_front(col, "AulaVirtual", E, 0.7, 4.69, [0.59, 1.18, 1.20],
                   [dict(at="end", name="AulaVirtual", signs=[("AULA_VIRTUAL", 0.0, 1.80),
                                                              ("AULA_VIRTUAL_CARD", 0.0, 1.56),
                                                              ("PROHIBIDO_TI", 0.0, 1.32)])])
    I.pillar(col, "F2_Pillar_E1", E, 4.915)
    I.decal(col, "Triangle", E, "TRIANGLE", 4.915, 1.55, d=-PILLAR_PROUD - 0.004)
    I.glazed_front(col, "TI6", E, 5.14, 8.38, [0.93, 0.92, 0.37],
                   [dict(at="start", name="TI6", transom="panel",
                         signs=[("TI6", 0.0, 1.80)])])
    I.pillar(col, "F2_Pillar_E2", E, 8.605)
    I.glazed_front(col, "TI3", E, 8.83, 13.83, [1.10, 1.10, 0.60],
                   [dict(at="end", name="TI3", signs=[("TI3", 0.0, 1.80),
                                                      ("AULA_DOBLE", 0.0, 1.56),
                                                      ("HORARIO_TI3", 0.0, 1.30)])])
    I.pillar(col, "F2_Pillar_E3", E, 14.055)
    I.box_prop(col, "Detail_GelDispenser", (CORR - PILLAR_PROUD - 0.10, -14.12, 1.18),
               (CORR - PILLAR_PROUD, -13.99, 1.38), "Kit_Plastic_Grey")
    I.glazed_front(col, "TI2", E, 14.28, 19.00, [1.0, 1.0, 1.0],
                   [dict(at="start", name="TI2", signs=[("TI2", 0.0, 1.80),
                                                        ("LINEAMIENTOS", 0.0, 1.45)])])
    I.plain_front(col, "SalaTI", E, 19.00, BAND[0] - COL_W)
    I.decal(col, "F2_E_Evac", E, "EVAC_RIGHT", 20.4, 1.95)
    I.pillar(col, "F2_Column_NE", E, BAND[0] - COL_W / 2, width=COL_W, proud=COL_PROUD)
    I.extinguisher(col, "F2_Extinguisher_NE", E, BAND[0] - COL_W / 2, top=1.55, face=-COL_PROUD)
    I.decal(col, "F2_Extintor_NE", E, "EXTINTOR", BAND[0] - COL_W / 2, 2.05, d=-COL_PROUD - 0.004)


def u_f2_corridor_west(col):
    """West side of the north leg (photos 3 and 4): a classroom, the consultancy suite
    behind its knee wall and clear glass, the Cisco academy, and the certification centre
    next to the stairwell."""
    I.plaster(col, "F2_W_Jamb", W, N_S, 0.7)
    I.glazed_front(col, "TI1", W, 0.7, 5.49, [1.0, 1.0, 1.0],
                   [dict(at="end", name="TI1", signs=[("TI1", 0.0, 1.80),
                                                      ("CARD_WHITE", 0.0, 1.56)])])
    I.pillar(col, "F2_Pillar_W1", W, 5.715)
    I.extinguisher(col, "F2_Extinguisher_W1", W, 5.715, top=1.55, face=-PILLAR_PROUD)
    I.decal(col, "F2_Extintor_W1", W, "EXTINTOR", 5.715, 2.05, d=-PILLAR_PROUD - 0.004)
    # Sala de maestros door, then the consultancy's knee wall with clear glass over it.
    I.glazed_front(col, "Consultoria", W, 5.94, 10.75, [1.0, 1.10, 1.10],
                   [dict(at="start", name="SalaMaestros", infill="Kit_Panel_Grey",
                         signs=[("AREA_CONSULTORIA", 0.0, 1.80),
                                ("SALA_MAESTROS", 0.0, 1.56)])],
                   knee=1.05, lower="Kit_Glass_Clear", upper="Kit_Glass_Clear", head=2.25)
    I.decal(col, "SoloPersonal", W, "SOLO_PERSONAL", 8.60, 1.30, d=0.055)
    I.pillar(col, "F2_Pillar_W2", W, 10.975)
    I.plain_front(col, "Cisco", W, 11.20, 16.01,
                  door=dict(at="start", name="Cisco", signs=[("CARD_WHITE", 0.0, 1.55)]))
    I.decal(col, "Cisco", W, "CISCO", 11.71, 2.45, d=0.055)
    I.pillar(col, "F2_Pillar_W3", W, 16.235)
    # Certification centre: blind white panel front, dark transom band, door by the stairs.
    I.plain_front(col, "CentroCert", W, 16.46, 21.76,
                  door=dict(at="end", name="CentroCert",
                            signs=[("CENTRO_CERT", 0.0, 1.90), ("NOTICE_CERT", -0.22, 1.50),
                                   ("NOTICE_CERT2", 0.18, 1.50)]))
    I.pillar(col, "F2_Column_NW", W, BAND[0] - COL_W / 2, width=COL_W, proud=COL_PROUD)
    I.box_prop(col, "Detail_AlarmBell", (-CORR + COL_PROUD - 0.09, -22.13, 2.28),
               (-CORR + COL_PROUD, -21.99, 2.42), "Kit_Metal_Red")


def u_f2_band(col):
    """Cross corridor (photos 1, 15, 19): the communications room on its north wall, the
    open stairwell to the west, plain plaster and signage elsewhere."""
    I.plaster(col, "F2_BandN_West", BN, -IN_X, -WALL_X)
    I.plain_front(col, "SiteCom", BN, 2.60, 5.40,
                  door=dict(at="start", name="SiteCom",
                            signs=[("SITE_COM", 0.0, 1.90), ("PUERTA_CERRADA", 0.0, 1.55)]))
    I.decal(col, "CiscoBand", BN, "CISCO", 3.11, 2.45, d=0.055)
    I.pillar(col, "F2_Pillar_B1", BN, 5.62)
    I.box_prop(col, "Detail_GelDispenserB", (5.55, -BAND[0] + PILLAR_PROUD - 0.10, 1.18),
               (5.69, -BAND[0] + PILLAR_PROUD, 1.38), "Kit_Plastic_Grey")
    I.plaster(col, "F2_BandN_East", BN, 5.84, IN_X)
    I.extinguisher(col, "F2_Extinguisher_BandN", BN, 7.6, top=1.55, face=0.0)
    I.decal(col, "F2_Extintor_BandN", BN, "EXTINTOR", 7.6, 2.05)

    I.plaster(col, "F2_BandS_East", BS, WALL_X, IN_X)
    I.plaster(col, "F2_BandS_West", BS, -IN_X, -WALL_X)
    I.decal(col, "F2_BandS_Evac", BS, "EVAC_LEFT", 6.4, 1.95)
    I.decal(col, "F2_Salida", BS, "SALIDA", -3.2, 2.35)
    for name, wall, s in (("F2_Bench_Band", BS, 3.0),):
        x, y, _ = wall.p(s, -0.33, 0.0)
        I.bench(col, name, x, y, (-wall.n[0], -wall.n[1]))
    I.pillar(col, "F2_Column_SE", E, BAND[1] + COL_W / 2, width=COL_W, proud=COL_PROUD)
    I.pillar(col, "F2_Column_SW", W, BAND[1] + COL_W / 2, width=COL_W, proud=COL_PROUD)


def u_f2_stairwell(col):
    """The head of the stairs (photo 1): the slab's edge parapets with their red rails,
    and the tall black-framed window in the west wall that lights the whole well."""
    p = 0.12
    walls = [((WELL["x0"], WELL["y1"] - p, 0.0), (WELL["x1"], WELL["y1"], 1.02)),
             ((WELL["x0"], WELL["y0"], 0.0), (WELL["x1"], WELL["y0"] + p, 1.02)),
             ((WELL["x1"] - p, WELL["y0"], 0.0), (WELL["x1"], -24.86, 1.02))]
    _boxes(col, "F2_Well_Parapet", walls, "Kit_Wall_White")
    _boxes(col, "F2_Well_Parapet_Skirting",
           [((WELL["x0"], WELL["y1"], 0.0), (WELL["x1"], WELL["y1"] + BASE_T, BASE_H)),
            ((WELL["x0"], WELL["y0"] - BASE_T, 0.0), (WELL["x1"], WELL["y0"], BASE_H))], "Kit_Frame")

    rails = MeshBuilder(["Kit_Metal_Red"])
    yn = WELL["y1"] - p / 2
    ys = WELL["y0"] + p / 2
    I.rail(rails, [(WELL["x0"] + 0.15, yn, 1.09), (WELL["x1"] - 0.10, yn, 1.09)])
    I.rail(rails, [(WELL["x0"] + 0.15, ys, 1.09), (WELL["x1"] - 0.10, ys, 1.09)])
    I.rail(rails, [(WELL["x1"] - p / 2, WELL["y0"] + 0.15, 1.09), (WELL["x1"] - p / 2, -24.90, 1.09)])
    make_object("F2_Well_Rails", rails, col)

    # Window: five lights over a transom, sill above the parapet, head under the ceiling.
    s0, s1, sill, head = WINDOW
    mid = (sill + head) / 2
    lights = []
    n = 5
    for k in range(n):
        a = s0 + (s1 - s0) * k / n + BAR / 2
        b = s0 + (s1 - s0) * (k + 1) / n - BAR / 2
        lights += [rect(a, sill + BAR, b, mid - BAR / 2), rect(a, mid + BAR / 2, b, head - BAR)]
    frame = MeshBuilder(["Kit_Frame"])
    XW.panel(frame, rect(s0, sill, s1, head), lights, 0.02, 0.10, "Kit_Frame")
    make_object("F2_Well_WindowFrame", frame, col)
    panes = MeshBuilder(["Kit_Glass_Clear"])
    for h in lights:
        XW.quad(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, 0.06,
                "Kit_Glass_Clear")
    make_object("F2_Well_WindowGlass", panes, col)
    # Plaster reveal under the sill and over the head; the lining stops at the opening.
    below = MeshBuilder(["Kit_Wall_White"])
    XW.box(below, s0, s1, 0.0, 0.02, 0.0, sill, "Kit_Wall_White")
    XW.box(below, s0, s1, 0.0, 0.02, head, CEIL, "Kit_Wall_White")
    make_object("F2_Well_WindowReveal", below, col)


def u_f2_north_screen(col):
    """North end of the corridor (photo 2): the glazed screen over the main entrance,
    two leaves and fixed lights, looking out at the trees and CDS's red south wall."""
    d0, d1 = 0.14, 0.22
    head = 2.20
    m0, m1 = -0.92, 0.92
    outline = [(-CORR, 0.0), (m0, 0.0), (m0, head), (m1, head), (m1, 0.0), (CORR, 0.0),
               (CORR, CEIL), (-CORR, CEIL)]
    lights = [rect(-CORR + BAR, 0.30, m0 - BAR, head - BAR), rect(m1 + BAR, 0.30, CORR - BAR, head - BAR),
              rect(-CORR + BAR, CEIL - 0.44, CORR - BAR, CEIL - BAR)]
    frame = MeshBuilder(["Kit_Frame"])
    XN.panel(frame, outline, lights, d0, d1, "Kit_Frame")
    make_object("F2_NorthScreen_Frame", frame, col)
    panes = MeshBuilder(["Kit_Glass_Clear"])
    for h in lights:
        XN.quad(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, (d0 + d1) / 2,
                "Kit_Glass_Clear")
    make_object("F2_NorthScreen_Glass", panes, col)
    _boxes(col, "F2_NorthScreen_Kerb", [((-CORR, -N_S, 0.0), (CORR, -N_S + 0.10, 0.30))], "Kit_Frame")
    mid = 0.0
    I.door_leaf(col, "F2_NorthWest", XN, m0, mid - 0.005, "Kit_Glass_Clear", d=(d0 + d1) / 2,
                top=head - 0.01)
    I.door_leaf(col, "F2_NorthEast", XN, m1, mid + 0.005, "Kit_Glass_Clear", d=(d0 + d1) / 2,
                top=head - 0.01)


def u_f2_corridor_south(col):
    """South leg (photos 12, 14, 16, 17): the meeting room and store east, the offices
    west, and the computer centre's glazed screen across the dead end."""
    s0 = BAND[1] + COL_W
    I.plain_front(col, "SalaJuntas", E, s0, 33.26,
                  door=dict(at="start", name="SalaJuntas",
                            signs=[("AREA_CONSULTORIA", 0.0, 1.80), ("CARD_WHITE", 0.0, 1.56)]))
    I.pillar(col, "F2_Pillar_S1", E, 33.485)
    I.plain_front(col, "Almacen", E, 33.71, S_END, door=dict(at="start", name="Almacen"))

    I.glazed_front(col, "Coordinacion", W, s0, 33.58, [1.0, 1.0, 1.0],
                   [dict(at="start", name="Coordinacion", infill="Kit_Panel_Grey",
                         glass_from=1.35, transom="Kit_Glass_Clear")],
                   knee=1.05, lower="Kit_Glass_Clear", upper="Kit_Glass_Clear", head=2.25)
    I.pillar(col, "F2_Pillar_S2", W, 33.80)
    I.plain_front(col, "OficinaSW", W, 34.02, S_END)

    # Dead end: the computer centre's glazed screen, its two leaves lettered on the glass.
    XS = Wall((0.0, -S_END), (1.0, 0.0), (0.0, -1.0))
    d0, d1 = 0.06, 0.14
    head = 2.20
    m0, m1 = -0.92, 0.92
    outline = [(-CORR, 0.0), (m0, 0.0), (m0, head), (m1, head), (m1, 0.0), (CORR, 0.0),
               (CORR, CEIL), (-CORR, CEIL)]
    lights = [rect(-CORR + BAR, 0.06, m0 - BAR, head - BAR), rect(m1 + BAR, 0.06, CORR - BAR, head - BAR),
              rect(-CORR + BAR, head + BAR, CORR - BAR, CEIL - BAR)]
    frame = MeshBuilder(["Kit_Frame"])
    XS.panel(frame, outline, lights, d0, d1, "Kit_Frame")
    make_object("F2_Computo_Frame", frame, col)
    panes = MeshBuilder(["Kit_Glass_Clear"])
    for h in lights:
        XS.quad(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, (d0 + d1) / 2,
                "Kit_Glass_Clear")
    make_object("F2_Computo_Glass", panes, col)
    I.door_leaf(col, "CentroComputoW", XS, m0, -0.005, "Kit_Glass_Clear", d=(d0 + d1) / 2,
                top=head - 0.01, signs=[("CENTRO_COMPUTO", -0.46, 1.60)])
    I.door_leaf(col, "CentroComputoE", XS, m1, 0.005, "Kit_Glass_Clear", d=(d0 + d1) / 2,
                top=head - 0.01)


def u_f2_lights(col):
    for name, x, y, z in FLUOROS:
        make_empty(f"Light_F2{name}_Fluoro", (x, y, z), col, 0.3)


def u_probes(col):
    make_empty("Probe_East", (10.0, 0.0, 0.0), col, 1.0)
    make_empty("Probe_North", (0.0, 10.0, 0.0), col, 1.0)
    make_empty("Probe_Up", (0.0, 0.0, 10.0), col, 1.0)


UNITS = {
    "mezzanine": ("Mezzanine", u_mezzanine, 0.0),
    "f2_floors": ("F2_Floors", u_f2_floors, F2),
    "f2_ceilings": ("F2_Ceilings", u_f2_ceilings, F2),
    "f2_ceiling_fixtures": ("F2_CeilingFixtures", u_f2_ceiling_fixtures, F2),
    "f2_rooms": ("F2_Rooms", u_f2_rooms, F2),
    "f2_corridor_east": ("F2_CorridorEast", u_f2_corridor_east, F2),
    "f2_corridor_west": ("F2_CorridorWest", u_f2_corridor_west, F2),
    "f2_band": ("F2_Band", u_f2_band, F2),
    "f2_stairwell": ("F2_Stairwell", u_f2_stairwell, F2),
    "f2_north_screen": ("F2_NorthScreen", u_f2_north_screen, F2),
    "f2_corridor_south": ("F2_CorridorSouth", u_f2_corridor_south, F2),
    "f2_lights": ("F2_Lights", u_f2_lights, F2),
    "probes": ("Probes", u_probes, 0.0),
}


def _collection(key):
    name = PREFIX + key
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def run_unit(key):
    """Build a unit and lift it to its storey. Every f2_* unit is authored with its floor
    at z = 0 so lm_interior's helpers apply unchanged; the lift moves the objects, not
    their meshes, so each keeps the pivot make_object gave it."""
    col_key, build, lift = UNITS[key]
    before = set(bpy.data.objects)
    build(_collection(col_key))
    made = [o for o in bpy.data.objects if o not in before]
    for o in made:
        o["lm_unit"] = key
        o.location.z += lift
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
    print("[cecadec_upper] built %s: %d objects, %d tris" % ((out_blend,) + stats()))


def rebuild(out_blend, keys):
    if os.path.abspath(bpy.data.filepath or "") != os.path.abspath(out_blend):
        bpy.ops.wm.open_mainfile(filepath=out_blend)
    ensure_materials(texture_dir_for(out_blend))
    for key in keys:
        remove_unit(key)
        run_unit(key)
    bpy.ops.wm.save_mainfile()
    print("[cecadec_upper] rebuilt %s in %s: %d objects, %d tris" % ((keys, out_blend) + stats()))


# ---- Preview (not saved into the asset) --------------------------------------------------

VIEWS = {
    "photo2_corridor_north": ((0.4, -12.0, F2 + 1.55), (0.0, -0.4, F2 + 1.45), 20),
    "photo5_aula_virtual": ((-1.5, -6.6, F2 + 1.40), (2.2, -6.6, F2 + 1.40), 20),
    "photo6_ti3": ((-1.5, -12.5, F2 + 1.40), (2.2, -12.5, F2 + 1.40), 20),
    "photo3_consultoria": ((1.6, -7.6, F2 + 1.45), (-2.2, -7.6, F2 + 1.35), 20),
    "photo4_certificacion": ((1.4, -20.6, F2 + 1.45), (-2.2, -20.9, F2 + 1.35), 20),
    "photo1_stairhead": ((-3.2, -24.8, F2 + 1.60), (-9.5, -24.8, F2 + 1.30), 18),
    "photo15_sitecom": ((3.6, -26.4, F2 + 1.50), (3.4, -22.4, F2 + 1.40), 20),
    "photo14_computo": ((0.2, -30.5, F2 + 1.55), (0.0, -38.2, F2 + 1.30), 20),
    "mezzanine": ((0.0, -10.0, DECK + 0.70), (0.0, -26.0, DECK + 0.50), 18),
    "mezzanine_well": ((-3.0, -24.8, DECK + 0.70), (-9.4, -24.8, DECK + 0.30), 18),
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
    scene.render.filepath = os.path.join(out_dir, "upper_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    return scene.render.filepath


def preview(out_dir):
    scene = bpy.context.scene
    col = bpy.data.collections.new("Preview_Lights")
    scene.collection.children.link(col)
    for name, x, y, z in FLUOROS:
        data = bpy.data.lights.new(f"Preview_Light_{name}", "POINT")
        data.energy = 140.0
        data.color = (0.86, 0.95, 0.78)
        data.shadow_soft_size = 0.4
        ob = bpy.data.objects.new(f"Preview_Light_{name}", data)
        ob.location = (x, y, z + F2)
        col.objects.link(ob)
    for i, (x, y) in enumerate(((0.0, -8.0), (0.0, -20.0), (-6.0, -24.8), (0.0, -33.0)), 1):
        data = bpy.data.lights.new(f"Preview_Mezz{i}", "POINT")
        data.energy = 40.0
        data.color = (0.9, 0.86, 0.8)
        ob = bpy.data.objects.new(f"Preview_Mezz{i}", data)
        ob.location = (x, y, DECK + 0.8)
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
        if o.name.startswith("F2_Ceiling") or o.name.startswith("Ceil_") or o.name.startswith("Mezz_Soffit"):
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
