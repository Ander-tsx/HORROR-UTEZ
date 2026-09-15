"""
Street furniture, one object per prop so each can be moved or tweaked on its own:

  Lamp_*          red post with a white globe; an empty <name>_Light marks the bulb
  Flagpole_*      white pole with sleeve and ball finial
  Ramp_*          blue-painted concrete ramp between two ground levels
  Generator_Pad / Generator   Generac set on a pad with a yellow-painted curb
  Lid_*           red service lid in the lawn

Blender axes: X east, Y north, Z up.
"""

from geolib import MeshBuilder, make_empty, make_object


def lamp_post(col, name, x, y, z=0.0, height=4.6):
    mb = MeshBuilder(["Kit_Metal_Red", "Kit_Globe", "Kit_Frame"])
    mb.box((x - 0.15, y - 0.15, z), (x + 0.15, y + 0.15, z + 0.04), "Kit_Metal_Red")
    mb.cylinder((x, y, z + 0.04), 0.07, 0.05, height - 0.04, "Kit_Metal_Red", segs=8)
    mb.cylinder((x, y, z + height), 0.09, 0.09, 0.06, "Kit_Frame", segs=8)
    mb.sphere((x, y, z + height + 0.26), 0.2, "Kit_Globe", segs=10, rings=6)
    mb.cylinder((x, y, z + height + 0.46), 0.08, 0.02, 0.06, "Kit_Frame", segs=8)
    obj = make_object(name, mb, col)
    make_empty(name + "_Light", (x, y, z + height + 0.26), col)
    return obj


def flagpole(col, name, x, y, z=0.0, height=9.0):
    mb = MeshBuilder(["Kit_Metal_White"])
    mb.cylinder((x, y, z), 0.09, 0.09, 0.6, "Kit_Metal_White", segs=8)
    mb.cylinder((x, y, z + 0.6), 0.055, 0.035, height - 0.6, "Kit_Metal_White", segs=8)
    mb.sphere((x, y, z + height + 0.08), 0.07, "Kit_Metal_White", segs=8, rings=4)
    return make_object(name, mb, col)


def ramp_x(col, name, x_top, x_foot, y0, y1, z_top, z_foot):
    """Ramp running along X: z_top at x_top, down to z_foot at x_foot."""
    def height(x):
        return z_top + (z_foot - z_top) * (x - x_top) / (x_foot - x_top)
    mb = MeshBuilder(["Kit_Paint_Blue"])
    xa, xb = sorted((x_top, x_foot))
    mb.box((xa, y0, z_foot - 0.03), (xb, y1, z_top), "Kit_Paint_Blue", cap=lambda x, y: height(x))
    return make_object(name, mb, col)


def ramp_height(x, x_top, x_foot, z_top, z_foot):
    return z_top + (z_foot - z_top) * (x - x_top) / (x_foot - x_top)


def generator(col, x0, y0, z=0.0, length=3.6, depth=1.35, height=1.8):
    """Set on a 0.12 m pad whose front edge is painted yellow; exhaust rises up the wall."""
    pad = MeshBuilder(["Kit_Apron"])
    pad.box((-0.25, -0.25, 0.0), (length + 0.25, depth + 0.35, 0.12), "Kit_Apron")
    pad_obj = make_object("Generator_Pad", pad, col, pivot=(0.0, 0.0, 0.0))
    pad_obj.location = (x0, y0, z)

    curb = MeshBuilder(["Kit_Paint_Yellow"])
    curb.box((-0.3, depth + 0.35, 0.0), (length + 0.3, depth + 0.45, 0.14), "Kit_Paint_Yellow")
    curb_obj = make_object("Generator_Curb", curb, col, pivot=(0.0, 0.0, 0.0))
    curb_obj.location = (x0, y0, z)

    mb = MeshBuilder(["Kit_Generator", "Kit_Metal_White", "Kit_Frame"])
    mb.box((0.0, 0.0, 0.12), (length, depth, 0.12 + height), "Kit_Generator")
    mb.box((0.2, 0.2, 0.12 + height), (0.7, 0.7, 0.12 + height + 0.08), "Kit_Frame")
    top = mb.cylinder((0.45, 0.45, 0.2 + height), 0.07, 0.07, 0.35, "Kit_Metal_White", segs=6)
    mb.cylinder(tuple(top), 0.07, 0.07, 0.6, "Kit_Metal_White", segs=6, tilt=(0, -1, 0.25))
    gen = make_object("Generator", mb, col, pivot=(0.0, 0.0, 0.0))
    gen.location = (x0, y0, z)

    box = MeshBuilder(["Kit_Metal_White"])
    box.box((0.25, -y0, 2.3), (0.65, -y0 + 0.15, 2.8), "Kit_Metal_White")      # wall junction box
    box_obj = make_object("Generator_WallBox", box, col, pivot=(0.0, 0.0, 0.0))
    box_obj.location = (x0, y0, z)
    return [pad_obj, curb_obj, gen, box_obj]


def lid(col, name, x, y, z):
    mb = MeshBuilder(["Kit_Metal_Red"])
    mb.box((x - 0.22, y - 0.15, z), (x + 0.22, y + 0.15, z + 0.06), "Kit_Metal_Red")
    return make_object(name, mb, col)
