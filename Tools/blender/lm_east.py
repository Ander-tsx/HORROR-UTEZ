"""
CECADEC east facade and the lawn strip in front of it: photo 35
(docs/map/reference/cecadec-east/). Outer face x = 10.0 (the kit's red east wall), from
the NE corner (y = 0) south to the SE corner (y = -45). Wall frame as lm_interior.Wall:
s = -y along the wall, d < 0 outside the building, z up.

Fins: stucco pilasters 0.7 m wide standing 1.0 m proud, up to the roofline. Between two
fins: a red sloped visor at the top of the ground floor over a dark window set in a red
reveal, and a narrow dark window on the upper floor. The bay facing the cross corridor's
glass doors (gen_cecadec_interior.EAST_DOOR, y -29.3..-34.3) stays open, with a paved
landing. "CECADEC" in white letters near the top of the north end.

In front: the lawn bed that wraps round the NE corner from the north planter continues
south between a stone kerb and the wall, with clusters of Areca palms.
"""

import bpy

from geolib import MeshBuilder, make_object, rect, rounded_open
from lm_interior import Wall
import lm_plants

X = 10.0
WALL = Wall((X, 0.0), (0.0, -1.0), (-1.0, 0.0))
FINS = [5.0, 10.4, 15.8, 21.2, 26.6, 35.8, 40.6]      # s of each fin's centre
FIN_W, FIN_PROUD, FIN_TOP = 0.7, 1.0, 8.2
DOOR_BAY = (26.6, 35.8)                              # fins either side of the glass doors
END_S = 44.6
BAR = 0.05
BED_Z, KERB_TOP, KERB_W = 0.35, 0.45, 0.30
DOOR_Y = (-28.8, -34.8)                              # paved gap in the lawn at the doors


def _panel(mb, outline, holes, d0, d1, mat):
    WALL.panel(mb, outline, holes, d0, d1, mat)


def _window(col, name, s0, s1, z0, z1, rail=None, light=1.1, depth=-0.08):
    n = max(1, round((s1 - s0) / light))
    w = (s1 - s0) / n
    holes = []
    for i in range(n):
        a = s0 + i * w + (BAR if i == 0 else BAR / 2)
        b = s0 + (i + 1) * w - (BAR if i == n - 1 else BAR / 2)
        if rail:
            holes += [rect(a, z0 + BAR, b, rail - BAR / 2), rect(a, rail + BAR / 2, b, z1 - BAR)]
        else:
            holes.append(rect(a, z0 + BAR, b, z1 - BAR))
    frame = MeshBuilder(["Kit_Frame"])
    _panel(frame, rect(s0, z0, s1, z1), holes, depth, depth + 0.06, "Kit_Frame")
    panes = MeshBuilder(["Kit_Glass_Tinted"])
    for h in holes:
        WALL.quad(panes, h[0][0] - 0.01, h[2][0] + 0.01, h[0][1] - 0.01, h[2][1] + 0.01, depth + 0.03,
                  "Kit_Glass_Tinted")
    back = MeshBuilder(["Kit_Frame"])
    WALL.box(back, s0, s1, -0.02, 0.0, z0, z1, "Kit_Frame")
    return [make_object(name + "_Frame", frame, col), make_object(name + "_Pane", panes, col),
            make_object(name + "_Back", back, col)]


def _bays():
    edges = [0.3] + [e for s in FINS for e in (s - FIN_W / 2, s + FIN_W / 2)] + [END_S]
    return [(edges[i], edges[i + 1]) for i in range(0, len(edges), 2)]


def facade(col):
    made = []
    for i, s in enumerate(FINS, 1):
        mb = MeshBuilder(["Kit_Trim"])
        WALL.box(mb, s - FIN_W / 2, s + FIN_W / 2, -FIN_PROUD, 0.0, 0.0, FIN_TOP, "Kit_Trim")
        made.append(make_object(f"East_Fin{i}", mb, col))

    for i, (a, b) in enumerate(_bays(), 1):
        if a >= DOOR_BAY[0] and b <= DOOR_BAY[1]:
            continue
        tag = f"East_Bay{i}"
        visor = MeshBuilder(["Kit_Wall_Red"])
        visor.transform = lambda q: WALL.p(q[2], q[0], q[1])       # (d, z, s) -> world
        visor.prism([(0.0, 3.95), (-0.9, 3.4), (-0.9, 3.1), (0.0, 3.1)], a + 0.02, b - 0.02, "Kit_Wall_Red",
                    bottom=True)
        visor.transform = None
        made.append(make_object(tag + "_Visor", visor, col))

        reveal = MeshBuilder(["Kit_Wall_Red"])
        WALL.box(reveal, a, a + 0.3, -0.25, 0.0, 0.0, 3.1, "Kit_Wall_Red")
        WALL.box(reveal, b - 0.3, b, -0.25, 0.0, 0.0, 3.1, "Kit_Wall_Red")
        WALL.box(reveal, a + 0.3, b - 0.3, -0.25, 0.0, 0.0, 0.45, "Kit_Wall_Red")
        made.append(make_object(tag + "_Reveal", reveal, col))
        made += _window(col, tag + "_Window", a + 0.3, b - 0.3, 0.45, 3.05, rail=2.3)
        mid = (a + b) / 2
        made += _window(col, tag + "_Upper", mid - 0.6, mid + 0.6, 4.8, 6.0, light=0.6, depth=-0.07)

    letters = MeshBuilder(["Kit_Letters"])
    s0, s1, z0, z1 = 4.6, 1.0, 7.33, 8.0
    letters.pane([WALL.p(s0, -0.03, z0), WALL.p(s1, -0.03, z0), WALL.p(s1, -0.03, z1), WALL.p(s0, -0.03, z1)],
                 "Kit_Letters", [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)])
    made.append(make_object("Decal_Letters_CECADEC", letters, col))
    return made


def _outer_x():
    """Kerb line of the north planter's southern arm, as the user left it (they scaled
    that planter), so the east bed carries on from it without a step."""
    k = bpy.data.objects.get("Planter_East_Kerb")
    if k is None:
        return 12.6
    xs = [(k.matrix_world @ v.co).x for v in k.data.vertices if (k.matrix_world @ v.co).y < -2.5]
    return max(xs) - KERB_W / 2 if xs else 12.6


def garden(col):
    made = []
    xo = _outer_x()
    for name, y0, y1 in (("N", -3.0, DOOR_Y[0]), ("S", DOOR_Y[1], -(END_S - 0.3))):
        bed = MeshBuilder(["Kit_Grass", "Kit_Stone"])
        bed.prism(rect(X, y1, xo - 0.1, y0), 0.0, BED_Z, "Kit_Grass", side_mat="Kit_Stone", bottom=True)
        made.append(make_object(f"Planter_EastSide_Bed{name}", bed, col))
    for name, path in (("N", [(xo, -3.0), (xo, DOOR_Y[0]), (X, DOOR_Y[0])]),
                       ("S", [(X, DOOR_Y[1]), (xo, DOOR_Y[1]), (xo, -(END_S - 0.3)), (X, -(END_S - 0.3))])):
        kerb = MeshBuilder(["Kit_Stone"])
        kerb.sweep(rounded_open(path, [0.0] + [0.6] * (len(path) - 2) + [0.0]), KERB_W, 0.0, KERB_TOP,
                   "Kit_Stone")
        made.append(make_object(f"Planter_EastSide_Kerb{name}", kerb, col))

    pave = MeshBuilder(["Kit_Apron"])
    pave.box((X, DOOR_Y[1], 0.0), (xo + 1.5, DOOR_Y[0], 0.06), "Kit_Apron")
    made.append(make_object("Paving_EastDoor", pave, col))

    xc = (X + xo) / 2 + 0.35
    # Photo 35: the Arecas top out above the roofline (~8.5 m).
    for i, (y, stems, h) in enumerate(((-7.0, 4, 8.8), (-12.9, 3, 7.6), (-18.5, 4, 9.2), (-24.0, 3, 7.8),
                                       (-38.2, 3, 8.2), (-42.7, 4, 8.6)), 1):
        made += lm_plants.palm_clump(col, f"Palm_East{i}", xc, y, BED_Z, stems=stems, height=h, seed=200 + i)
    return made
