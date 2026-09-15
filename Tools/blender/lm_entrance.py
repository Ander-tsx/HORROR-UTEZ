"""
CECADEC north entrance, one object per element:

  Entrance_Pilaster_West / _East   stucco pilasters, sloped caps
  Entrance_TopBand                 red band flush with the roofline
  Entrance_Spandrel                red box jutting over the door
  Entrance_WindowFrame             upper-storey frame: one welded panel, 6 openings
  Entrance_WindowGlass             two-sided glass sheet behind it
  Entrance_DoorFrame               fixed frame: door opening + transom opening
  Entrance_TransomGlass            fixed glass over the doors
  Door_West / Door_East            leaves (frame + glass + push bar), CLOSED, origin on the hinge
  Entrance_Floodlight, Entrance_EquipmentBox   on top of the bay

Only the leaves may be named Door_*: Unity turns every Door_* object into a hinged door.

Blender axes: X east, Y north (out of the building), Z up. Origin: doorway centre, ground
level, wall's outer face. The builder cuts the door hole and the glazing opening.
"""

from geolib import MeshBuilder, make_object, rect

DOOR_HALF = 2.2                  # UtezDimensions.LandmarkDoorWidth / 2 = the corridor's half-width
DOOR_TOP = 3.0                   # UtezDimensions.DoorHeight
PILASTER_W = 1.5
PILASTER_BACK, PILASTER_FRONT = -0.2, 1.0
CAP_INNER, CAP_OUTER = 9.3, 8.8   # caps slope down away from the door
ROOFLINE = 8.5
BAY = DOOR_HALF + 0.05           # bay parts run into the pilasters: no gap shows
SPANDREL = (DOOR_TOP - 0.02, 4.4)
GLAZING = (4.4, 6.6)             # UtezBuildingBuilder.GlazingRaw
TRANSOM_Z = 5.5
MULLION_X = (-0.5, 0.5)
MULLION_W = 0.06
FRAME_BAR = 0.08
GLASS_Y = 0.21
WIN_FRAME_Y = (0.225, 0.29)

DOOR_Y = (-0.26, -0.14)          # fixed frame, mid-wall
JAMB = 0.08
LEAF_TOP = 2.55
LEAF_Y = (-0.225, -0.175)
LEAF_W = DOOR_HALF - JAMB


def _box(col, name, lo, hi, mat, cap=None):
    mb = MeshBuilder([mat])
    mb.box(lo, hi, mat, cap=cap)
    return make_object(name, mb, col)


def _glass(col, name, x0, x1, z0, z1, y):
    mb = MeshBuilder(["Kit_Glass_Clear"])
    mb.pane([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], "Kit_Glass_Clear",
            uvs=[(x0, z0), (x1, z0), (x1, z1), (x0, z1)])
    return make_object(name, mb, col)


def pilasters(col):
    out = []
    for s, side in ((-1.0, "West"), (1.0, "East")):
        inner, outer = s * DOOR_HALF, s * (DOOR_HALF + PILASTER_W)
        cap = lambda x, y: CAP_INNER + (CAP_OUTER - CAP_INNER) * (abs(x) - DOOR_HALF) / PILASTER_W
        out.append(_box(col, "Entrance_Pilaster_" + side, (min(inner, outer), PILASTER_BACK, 0.0),
                        (max(inner, outer), PILASTER_FRONT, CAP_INNER), "Kit_Trim", cap=cap))
    return out


def bay(col):
    return [
        _box(col, "Entrance_TopBand", (-BAY, -0.2, GLAZING[1]), (BAY, 0.55, ROOFLINE), "Kit_Wall_Red"),
        _box(col, "Entrance_Spandrel", (-BAY, -0.38, SPANDREL[0]), (BAY, 0.65, SPANDREL[1]), "Kit_Wall_Red"),
    ]


def window(col):
    # Openings between the frame bars; the outer 5 cm on each side hide in the pilasters.
    xs = [(-DOOR_HALF + FRAME_BAR / 2, MULLION_X[0] - MULLION_W / 2),
          (MULLION_X[0] + MULLION_W / 2, MULLION_X[1] - MULLION_W / 2),
          (MULLION_X[1] + MULLION_W / 2, DOOR_HALF - FRAME_BAR / 2)]
    zs = [(GLAZING[0] + FRAME_BAR, TRANSOM_Z - 0.03), (TRANSOM_Z + 0.03, GLAZING[1] - FRAME_BAR)]
    holes = [rect(x0, z0, x1, z1) for x0, x1 in xs for z0, z1 in zs]
    mb = MeshBuilder(["Kit_Frame"])
    mb.frame_panel(rect(-BAY, GLAZING[0], BAY, GLAZING[1]), holes, *WIN_FRAME_Y, "Kit_Frame")
    return [make_object("Entrance_WindowFrame", mb, col),
            _glass(col, "Entrance_WindowGlass", -BAY, BAY, GLAZING[0], GLAZING[1], GLASS_Y)]


def door_frame(col):
    inner = DOOR_HALF - JAMB
    head = SPANDREL[0]
    holes = [rect(-inner, 0.02, inner, LEAF_TOP), rect(-inner, LEAF_TOP + 0.08, inner, head - 0.08)]
    mb = MeshBuilder(["Kit_Frame"])
    mb.frame_panel(rect(-DOOR_HALF, 0.0, DOOR_HALF, head), holes, *DOOR_Y, "Kit_Frame")
    return [make_object("Entrance_DoorFrame", mb, col),
            _glass(col, "Entrance_TransomGlass", -inner, inner, LEAF_TOP + 0.08, head - 0.08,
                   (DOOR_Y[0] + DOOR_Y[1]) / 2)]


def door_leaf(col, side):
    """side = -1 west leaf, +1 east leaf. Hinge on the jamb, free edge at the centre."""
    hinge_x = side * (DOOR_HALF - JAMB)
    a, b = sorted((hinge_x, hinge_x - side * LEAF_W))
    y0, y1 = LEAF_Y
    mb = MeshBuilder(["Kit_Frame", "Kit_Glass_Clear"])
    mb.frame_panel(rect(a, 0.0, b, LEAF_TOP), [rect(a + 0.08, 0.16, b - 0.08, LEAF_TOP - 0.1)],
                   y0, y1, "Kit_Frame")
    mb.box((a + 0.1, y0 - 0.045, 1.0), (b - 0.1, y0 - 0.005, 1.05), "Kit_Frame")   # push bar, inside
    ym = (y0 + y1) / 2
    x0, x1, z0, z1 = a + 0.08, b - 0.08, 0.16, LEAF_TOP - 0.1
    mb.pane([(x0, ym, z0), (x1, ym, z0), (x1, ym, z1), (x0, ym, z1)], "Kit_Glass_Clear",
            uvs=[(x0, z0), (x1, z0), (x1, z1), (x0, z1)])
    return make_object("Door_West" if side < 0 else "Door_East", mb, col, pivot=(hinge_x, ym, 0.0))


def roof_fixtures(col):
    return [
        _box(col, "Entrance_Floodlight", (0.2, 0.15, ROOFLINE), (0.55, 0.35, ROOFLINE + 0.22), "Kit_Frame"),
        _box(col, "Entrance_EquipmentBox", (-0.45, -0.15, ROOFLINE), (-0.1, 0.2, ROOFLINE + 0.35), "Kit_Trim"),
    ]


def build_body(col):
    return pilasters(col) + bay(col) + window(col) + door_frame(col) + roof_fixtures(col)


def build_doors(col):
    return [door_leaf(col, -1), door_leaf(col, 1)]
