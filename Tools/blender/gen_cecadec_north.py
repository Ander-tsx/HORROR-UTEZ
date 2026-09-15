"""
CECADEC north landmark: entrance, plaza, planting and street furniture in one .blend,
one Blender object per element.

Blender-native axes (top view = map, north up): X east, Y north, Z up, metres. Origin:
doorway centre on the north wall's outer face; the wall runs x = -10..10 at y = 0.
Three empties (Probe_East / Probe_North / Probe_Up, 10 m out) let Unity measure and undo
the .blend -> FBX -> Unity axis conversion.

Material slot names are Unity material names (Assets/_Project/Art/Materials/Kit/);
PsxKitImporter binds them by name. MATERIALS must match UtezKit.LandmarkMaterials.

The work is split into UNITS. Every object a unit creates is tagged obj["lm_unit"], so:

  full build   OUT_BLEND set, no REBUILD_UNITS: wipes the open file, builds everything,
               saves a copy to OUT_BLEND. Only for the first build: it discards hand edits.
  rebuild      OUT_BLEND and REBUILD_UNITS=["tree_bed", ...]: works on OUT_BLEND (opened if
               it isn't the open file), deletes only those units' objects, rebuilds them,
               saves. Everything else, hand edits included, is left alone.

Prefer running it through Tools/blender/run_landmark.py (separate background Blender,
preview renders, geometry report). Globals it reads: OUT_BLEND, TOOLS_DIR,
REBUILD_UNITS, PREVIEW_DIR, ENGINE. (UNITS is the registry below: never pass it in.)
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
import lm_cds  # noqa: E402
import lm_east  # noqa: E402
import lm_entrance  # noqa: E402
import lm_plants  # noqa: E402
import lm_plaza  # noqa: E402
import lm_props  # noqa: E402

for _m in (geolib, lm_entrance, lm_plaza, lm_plants, lm_props, lm_cds, lm_east):
    importlib.reload(_m)

from geolib import make_empty  # noqa: E402

PREFIX = "CECADEC_"

# Unity material -> (map, Tiling u, Tiling v). Tiling = 1 / metres covered by the map.
MATERIALS = {
    "Kit_Wall_Red": ("T_Wall_Red.png", 0.25, 0.25),
    "Kit_Trim": ("T_Stucco.png", 1.0, 1.0),
    "Kit_Frame": ("T_Frame.png", 1.0, 1.0),
    "Kit_Glass_Clear": ("T_Glass.png", 1.0, 1.0),
    "Kit_Apron": ("T_Apron.png", 0.5, 0.5),
    "Kit_Slab": ("T_Slab.png", 0.25, 0.25),
    "Kit_Aggregate": ("T_Aggregate.png", 1 / 3, 1 / 3),
    "Kit_Paint_Green": ("T_Paint_Green.png", 1.0, 1.0),
    "Kit_Paint_Blue": ("T_Paint_Blue.png", 1.0, 1.0),
    "Kit_Paint_Yellow": ("T_Paint_Yellow.png", 1.0, 1.0),
    "Kit_Grass": ("T_Grass.png", 0.5, 0.5),
    "Kit_Soil": ("T_Soil.png", 0.5, 0.5),
    "Kit_Stone": ("T_Stone.png", 0.5, 1.0),
    "Kit_Rock": ("T_Rock.png", 0.5, 0.5),
    "Kit_Bark": ("T_Bark.png", 1.0, 0.5),
    "Kit_Foliage": ("T_Foliage.png", 1.0, 1.0),
    "Kit_Generator": ("T_Generator.png", 0.25, 0.5),
    "Kit_Metal_Red": ("T_Metal_Red.png", 1.0, 1.0),
    "Kit_Metal_White": ("T_Metal_White.png", 1.0, 1.0),
    "Kit_Globe": ("T_Globe.png", 1.0, 1.0),
    # CDS south facade, CECADEC east facade (photos 33, 34, 35).
    "Kit_Glass_Tinted": ("T_Glass_Tinted.png", 1.0, 1.0),
    "Kit_Canvas_Red": ("T_Canvas_Red.png", 0.5, 0.5),
    "Kit_Palm": ("T_Palm.png", 1.0, 1.0),
    "Kit_PalmTrunk": ("T_PalmTrunk.png", 1.0, 1.0),
    "Kit_Letters": ("T_Letters.png", 1.0, 1.0),
}

HIGH = lm_plaza.HIGH_Z
LOW = lm_plaza.LOW_Z
BED = lm_plaza.BED_Z
TREE_BED = lm_plaza.TREE_BED_Z
RAMP = dict(x_top=lm_plaza.STEP_X, x_foot=6.8, y0=2.75, y1=3.55)   # rises westwards
tz = lm_plaza.terrace_z
P = lm_plants
R = lm_props


# ---- Units ---------------------------------------------------------------------

def u_ramp(col):
    R.ramp_x(col, "Ramp_Accessible", RAMP["x_top"], RAMP["x_foot"], RAMP["y0"], RAMP["y1"], HIGH, LOW)


def u_tree_bed(col):
    lm_plaza.tree_bed(col)
    P.tree(col, "Tree_Bed1", -5.2, 4.2, TREE_BED, height=8.5, fork=2.2, crown=3.4, seed=12)
    P.tree(col, "Tree_Bed2", -8.9, 4.0, TREE_BED, height=9.0, fork=2.4, crown=3.6, seed=14)
    P.tree(col, "Tree_Bed3", -8.4, 9.2, TREE_BED, height=9.5, fork=2.6, crown=3.8, seed=11)
    for i, (x, y, r) in enumerate(((-6.9, 3.6, 0.5), (-4.7, 6.1, 0.45), (-9.7, 5.4, 0.5),
                                   (-7.3, 7.8, 0.45), (-9.8, 10.4, 0.4)), start=1):
        P.shrub(col, f"Plant_Bed_Shrub{i}", x, y, TREE_BED, radius=r, seed=40 + i)
    for i, (x, y) in enumerate(((-5.8, 5.2), (-7.8, 5.6), (-6.8, 9.8), (-9.0, 8.2),
                                (-5.2, 3.6), (-8.6, 6.8)), start=1):
        P.tuft(col, f"Plant_Bed_Tuft{i}", x, y, TREE_BED, size=0.7, seed=60 + i)


def u_wall_planters(col):
    lm_plaza.wall_planters(col)
    P.tree(col, "Tree_Dry", 10.9, 1.7, BED, height=8.0, fork=1.6, seed=13, lean=(-0.25, 0.3),
           leafy=False, depth=3)
    for i, (x, y, r) in enumerate(((4.4, 0.8, 0.55), (-4.6, 0.7, 0.45), (8.0, 0.6, 0.4)), start=1):
        P.shrub(col, f"Plant_Wall_Shrub{i}", x, y, BED, radius=r, seed=50 + i)
    P.tuft(col, "Plant_Wall_Tuft1", -6.8, 0.9, BED, size=0.7, seed=70)
    P.tuft(col, "Plant_Wall_Tuft2", 5.8, 1.9, BED, size=0.7, seed=71)
    P.taro(col, "Plant_Taro1", -5.6, 0.8, BED, size=0.7, seed=80)
    P.taro(col, "Plant_Taro2", -3.9, 1.0, BED, size=0.5, seed=81)
    R.lid(col, "Lid_Red", 7.5, 1.3, BED)


def u_garden(col):
    lm_plaza.garden(col)
    for i, (x, y, h) in enumerate(((-16.0, 3.0, 10.5), (-18.5, 9.0, 11.5), (-16.5, 15.5, 10.0),
                                   (-21.0, 14.0, 12.0), (-20.5, 3.5, 9.5)), start=1):
        P.tree(col, f"Tree_Garden{i}", x, y, tz(x, y), height=h, fork=2.8, crown=4.2, seed=20 + i)
    for i, (x, y, r) in enumerate(((-14.5, 7.5, 0.7), (-13.8, 2.8, 0.6), (-13.3, 12.0, 0.5)), start=1):
        P.shrub(col, f"Plant_Garden_Shrub{i}", x, y, tz(x, y), radius=r, seed=90 + i)
    R.lamp_post(col, "Lamp_Garden1", -15.0, 6.5, tz(-15.0, 6.5), height=5.2)
    R.lamp_post(col, "Lamp_Garden2", -15.5, 13.0, tz(-15.5, 13.0), height=5.2)


def u_props(col):
    R.lamp_post(col, "Lamp_East", 6.4, 0.7, BED)
    x = 3.35
    R.flagpole(col, "Flagpole_1", x, 2.95,
               R.ramp_height(x, RAMP["x_top"], RAMP["x_foot"], HIGH, LOW) - 0.01)
    R.flagpole(col, "Flagpole_2", 8.6, 1.9, BED)
    R.flagpole(col, "Flagpole_3", 12.0, 1.0, BED)
    R.generator(col, -11.6, 0.35, HIGH)


def u_probes(col):
    make_empty("Probe_East", (10.0, 0.0, 0.0), col, 1.0)
    make_empty("Probe_North", (0.0, 10.0, 0.0), col, 1.0)
    make_empty("Probe_Up", (0.0, 0.0, 10.0), col, 1.0)


# key -> (collection, builder). Order matters only for readability.
UNITS = {
    "entrance_body": ("Entrance", lm_entrance.build_body),
    "entrance_doors": ("Entrance", lm_entrance.build_doors),
    "ground": ("Ground", lm_plaza.ground),
    "ramp": ("Ground", u_ramp),
    "tree_bed": ("TreeBed", u_tree_bed),
    "wall_planters": ("WallPlanters", u_wall_planters),
    "garden": ("Garden", u_garden),
    "props": ("Props", u_props),
    "cds_facade": ("CDS", lm_cds.facade),
    "cds_front": ("CDS", lm_cds.front),
    "east_facade": ("EastFacade", lm_east.facade),
    "east_garden": ("EastGarden", lm_east.garden),
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


def ensure_materials(tex_dir):
    for name, (file_name, tu, tv) in MATERIALS.items():
        geolib.preview_material(name, tex_dir, file_name, (tu, tv),
                                alpha={"Kit_Glass_Clear": 0.35, "Kit_Glass_Tinted": 0.7}.get(name),
                                clip=name in ("Kit_Foliage", "Kit_Palm", "Kit_Letters"),
                                emission=1.5 if name == "Kit_Globe" else 0.0)


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
    print("[cecadec_north] built %s: %d objects, %d tris" % ((out_blend,) + stats()))


def rebuild(out_blend, keys):
    if os.path.abspath(bpy.data.filepath or "") != os.path.abspath(out_blend):
        bpy.ops.wm.open_mainfile(filepath=out_blend)
    ensure_materials(texture_dir_for(out_blend))
    for key in keys:
        remove_unit(key)
        run_unit(key)
    bpy.ops.wm.save_mainfile()
    print("[cecadec_north] rebuilt %s in %s: %d objects, %d tris" % ((keys, out_blend) + stats()))


# ---- Preview (not saved into the asset) ----------------------------------------------

VIEWS = {
    # name: (camera, look-at, lens mm) — roughly where the site photos were taken
    "front": ((0.6, 17.0, 1.6), (0.0, 0.0, 4.2), 24),
    "plaza_ne": ((8.5, 8.5, 1.6), (1.0, 1.0, 2.0), 24),
    "bed_s": ((-1.8, 12.5, 1.6), (-4.5, 0.0, 2.6), 24),
    "garden_w": ((-2.5, 5.5, 1.6), (-16.0, 7.0, 1.8), 22),
    # Photos 33-35: the plaza towards CDS, and CECADEC's NE corner.
    "photo34_to_cds": ((-0.5, 3.5, 1.6), (4.0, 18.5, 3.2), 20),
    "photo33_to_cds_west": ((-9.5, 4.0, 1.6), (6.0, 17.5, 3.4), 20),
    "photo35_ne_corner": ((20.0, 11.5, 1.6), (8.5, -8.0, 4.6), 20),
}


def _context(tex_dir):
    col = bpy.data.collections.new("Preview_Context")
    bpy.context.scene.collection.children.link(col)
    mb = geolib.MeshBuilder(["Kit_Wall_Red", "Preview_Ground", "Preview_Hall"])
    t = -0.4
    dh = lm_entrance.DOOR_HALF
    mb.box((-10.0, t, 0.0), (-dh, 0.0, 8.0), "Kit_Wall_Red")
    mb.box((dh, t, 0.0), (10.0, 0.0, 8.0), "Kit_Wall_Red")
    mb.box((-dh, t, 3.0), (dh, 0.0, 4.4), "Kit_Wall_Red")
    mb.box((-dh, t, 6.6), (dh, 0.0, 8.0), "Kit_Wall_Red")
    mb.box((-10.0, -45.0, 0.0), (-9.6, t, 8.0), "Kit_Wall_Red")
    mb.box((9.6, -45.0, 0.0), (10.0, t, 8.0), "Kit_Wall_Red")
    mb.box((-10.0, -45.0, 8.0), (10.0, 0.0, 8.5), "Kit_Wall_Red")
    mb.box((-9.6, -12.0, 4.0), (9.6, t, 4.35), "Preview_Hall")
    mb.box((-9.6, -12.0, 0.0), (9.6, -11.8, 8.0), "Preview_Hall")
    mb.box((-9.6, -12.0, 0.0), (9.6, t, 0.02), "Preview_Hall")
    mb.box((-60.0, -60.0, -0.02), (60.0, 60.0, 0.0), "Preview_Ground")
    geolib.preview_material("Preview_Ground", tex_dir, "T_Slab.png", 0.25)
    geolib.preview_material("Preview_Hall", tex_dir, flat=(0.4, 0.4, 0.38))
    geolib.make_object("Preview_CECADEC", mb, col, pivot=(0.0, 0.0, 0.0))

    cds = geolib.MeshBuilder(["Kit_Trim", "Kit_Wall_Red"])
    cds.box((-15.5, -11.0, 0.0), (15.5, 11.0, 4.0), "Kit_Trim")
    cds.box((-15.5, -11.0, 4.0), (15.5, 11.0, 8.5), "Kit_Wall_Red")
    obj = geolib.make_object("Preview_CDS", cds, col, pivot=(0.0, 0.0, 0.0))
    obj.location = (11.9, 29.1, 0.0)
    obj.rotation_euler = (0.0, 0.0, math.radians(-8.5))


def _render(name, loc, target, lens, out_dir, ortho=None):
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("Cam_" + name)
    cam_data.lens = lens
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
    scene.render.filepath = os.path.join(out_dir, "north_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    return scene.render.filepath


def preview(out_dir):
    tex_dir = texture_dir_for(_g["OUT_BLEND"])
    _context(tex_dir)
    scene = bpy.context.scene
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 2.2
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(40.0), 0.0, math.radians(150.0))
    scene.collection.objects.link(sun)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is not None:
        bg.inputs["Color"].default_value = (0.66, 0.68, 0.72, 1.0)
        bg.inputs["Strength"].default_value = 1.1
    engine = _g.get("ENGINE", "BLENDER_EEVEE")
    scene.render.engine = engine
    if engine == "CYCLES":
        scene.cycles.samples = 24
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = 1280, 960
    out = [_render(n, *v, out_dir) for n, v in VIEWS.items()]
    out.append(_render("top", (1.5, 5.0, 60.0), (1.5, 5.01, 0.0), 50, out_dir, ortho=44.0))
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
