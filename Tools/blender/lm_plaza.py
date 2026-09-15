"""
Ground in front of CECADEC north, read off the site photos, one object per element.

Two ground levels, as on site:
  entrance level (HIGH_Z)  door apron + main walkway, west of STEP_X
  plaza level (LOW_Z)      east of STEP_X: a band of plain concrete, then, from AGG_X on,
                           the dark exposed-aggregate plaza. The blue ramp (props) climbs
                           from the plaza level up to the entrance level along the east
                           planter's kerb, rising westwards.
Both level changes are straight lines (the edge at STEP_X, the aggregate edge at AGG_X).

Objects:
  Ground_Apron, Ground_Walkway (holed around the tree bed), Ground_LowConcrete,
  Ground_Aggregate, Decal_AssemblyPoint
  TreeBed_Central                      one sunken bed: two overlapping rectangles
  Planter_East_Bed / _Kerb, Planter_West_Bed / _Kerb
  Garden_Wall (straight), Garden_Terrace, Rock_*

Blender axes: X east, Y north, Z up. Origin: doorway centre on the wall's outer face;
the wall runs x = -10..10 at y = 0. Heights are above the campus slab (z = 0).
"""

import math

from geolib import MeshBuilder, make_object, rect, rounded, rounded_open

HIGH_Z = 0.20            # entrance level: apron and main walkway
LOW_Z = 0.04             # plaza level (just above the campus slab)
KERB_TOP = 0.45          # planter kerbs stand ~0.25-0.4 m over the ground either side
KERB_W = 0.30
BED_Z = 0.35             # lawn in the wall planters, under the kerb top
TREE_BED_Z = 0.10        # sunken 10 cm under the walkway

STEP_X = 3.0             # straight N-S edge where the entrance level drops to the plaza
AGG_X = 5.0              # the aggregate starts here; plain low concrete before it
STRIP = (2.6, 3.6)       # low strip along the east planter's kerb (the ramp sits on it)
EAST_X = 16.0
NORTH_Y = 18.0
AGG_NORTH = 17.2         # stops short of CDS's south wall
WALL_X, WALL_HALF, WALL_TOP = -12.2, 0.2, 0.95

PILASTER_OUT = 3.7      # outer face of the entrance pilasters (lm_entrance: DOOR_HALF + 1.5)
APRON = rect(-PILASTER_OUT, 0.0, PILASTER_OUT, 2.4)
WALKWAY = [(WALL_X + WALL_HALF, 0.0), (-PILASTER_OUT, 0.0), (-PILASTER_OUT, 2.4), (STEP_X, 2.4),
           (STEP_X, NORTH_Y), (WALL_X + WALL_HALF, NORTH_Y)]
LOW_CONCRETE = [(STEP_X, STRIP[0]), (EAST_X, STRIP[0]), (EAST_X, STRIP[1]), (AGG_X, STRIP[1]),
                (AGG_X, NORTH_Y), (STEP_X, NORTH_Y)]
AGGREGATE = rect(AGG_X, STRIP[1], EAST_X, AGG_NORTH)

# The central tree bed: a square-ish rectangle by the generator and a second one set
# back to the north-west, merged into one outline.
TREE_BED = [(-10.0, 3.0), (-4.0, 3.0), (-4.0, 7.0), (-6.2, 7.0), (-6.2, 11.2),
            (-10.5, 11.2), (-10.5, 7.0), (-10.0, 7.0)]

EAST_BED_PTS = [(3.05, 0.0), (10.0, 0.0), (10.0, -3.0), (12.6, -3.0), (12.6, 0.6),
                (11.0, 2.6), (4.2, 2.6), (3.05, 1.4)]
EAST_BED_R = [0, 0, 0, 0, 1.4, 1.0, 0.9, 0.6]
EAST_KERB = rounded_open([(3.05, 0.0), (3.05, 1.4), (4.2, 2.6), (11.0, 2.6), (12.6, 0.6),
                          (12.6, -3.0)], [0, 0.6, 0.9, 1.0, 1.4, 0])
WEST_BED_PTS = [(-7.6, 0.0), (-3.05, 0.0), (-3.05, 0.9), (-3.8, 1.6), (-7.6, 1.6)]
WEST_BED_R = [0, 0, 0.5, 0.5, 0]
WEST_KERB = rounded_open([(-3.05, 0.0), (-3.05, 0.9), (-3.8, 1.6), (-7.6, 1.6), (-7.6, 0.0)],
                         [0, 0.5, 0.5, 0, 0])

ASSEMBLY_CENTRE = (9.55, 9.55)   # on a crossing of the 45-degree aggregate joints

ROCKS = ((-15.5, 4.0, 0.7), (-17.0, 8.5, 1.1), (-14.6, 10.8, 0.5), (-19.5, 5.5, 1.3),
         (-18.0, 14.0, 0.9), (-21.5, 11.0, 1.4), (-14.2, 16.5, 0.6))


def terrace_z(x, y):
    """Garden slope: level with the wall top, then climbing west towards the hill."""
    t = max(0.0, WALL_X - x)
    rise = 2.8 * (1.0 - math.exp(-t / 5.0))
    bumps = 0.18 * math.sin(x * 0.9 + y * 0.4) * math.cos(y * 0.7)
    return WALL_TOP - 0.1 + rise + bumps * min(1.0, t / 2.0)


def _prism(col, name, poly, z1, mat, side_mat=None, rot=0.0, holes=()):
    mb = MeshBuilder([mat])
    mb.prism(poly, 0.0, z1, mat, side_mat=side_mat, rot=rot, holes=holes)
    return make_object(name, mb, col)


def ground(col):
    out = [
        _prism(col, "Ground_Apron", APRON, HIGH_Z, "Kit_Apron"),
        _prism(col, "Ground_Walkway", WALKWAY, HIGH_Z, "Kit_Slab", holes=[TREE_BED]),
        _prism(col, "Ground_LowConcrete", LOW_CONCRETE, LOW_Z, "Kit_Slab"),
        _prism(col, "Ground_Aggregate", AGGREGATE, LOW_Z, "Kit_Aggregate", rot=45.0),
    ]
    cx, cy = ASSEMBLY_CENTRE
    du = (1 / math.sqrt(2), -1 / math.sqrt(2)); dv = (1 / math.sqrt(2), 1 / math.sqrt(2))
    z = LOW_Z + 0.008
    corners = [(cx + su * 1.5 * du[0] + sv * 1.5 * dv[0], cy + su * 1.5 * du[1] + sv * 1.5 * dv[1], z)
               for su, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    mb = MeshBuilder(["Kit_Paint_Green"])
    mb.face(corners, "Kit_Paint_Green", uvs=[(0, 0), (1, 0), (1, 1), (0, 1)])
    out.append(make_object("Decal_AssemblyPoint", mb, col))
    return out


def tree_bed(col):
    return [_prism(col, "TreeBed_Central", TREE_BED, TREE_BED_Z, "Kit_Soil", side_mat="Kit_Apron")]


def _kerb(col, name, path):
    mb = MeshBuilder(["Kit_Stone"])
    mb.sweep(path, KERB_W, 0.0, KERB_TOP, "Kit_Stone")
    return make_object(name, mb, col)


def wall_planters(col):
    return [
        _prism(col, "Planter_East_Bed", rounded(EAST_BED_PTS, EAST_BED_R), BED_Z, "Kit_Grass",
               side_mat="Kit_Stone"),
        _kerb(col, "Planter_East_Kerb", EAST_KERB),
        _prism(col, "Planter_West_Bed", rounded(WEST_BED_PTS, WEST_BED_R), BED_Z, "Kit_Grass",
               side_mat="Kit_Stone"),
        _kerb(col, "Planter_West_Kerb", WEST_KERB),
    ]


def garden(col):
    wall = MeshBuilder(["Kit_Stone"])
    wall.box((WALL_X - WALL_HALF, 0.0, 0.0), (WALL_X + WALL_HALF, NORTH_Y, WALL_TOP), "Kit_Stone")
    out = [make_object("Garden_Wall", wall, col)]

    mb = MeshBuilder(["Kit_Grass"])
    ny, nx = 12, 10
    x_edge = WALL_X - WALL_HALF
    def vert(i, j):
        y = NORTH_Y * j / ny
        x = -24.0 + (x_edge + 24.0) * i / nx
        return (x, y, terrace_z(x, y))
    for j in range(ny):
        for i in range(nx):
            mb.face([vert(i, j), vert(i + 1, j), vert(i + 1, j + 1), vert(i, j + 1)], "Kit_Grass")
    out.append(make_object("Garden_Terrace", mb, col))

    for n, (x, y, s) in enumerate(ROCKS, start=1):
        rock = MeshBuilder(["Kit_Rock"])
        rock.blob((x, y, terrace_z(x, y) + s * 0.2), s, "Kit_Rock", n)
        out.append(make_object(f"Rock_{n}", rock, col))
    return out
