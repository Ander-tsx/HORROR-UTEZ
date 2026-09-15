"""
Every albedo map for CECADEC north (facade, entrance and plaza).

Real grain comes from the site photo docs/map/reference/cecadec-north-entrance.png
(tex_surfaces.py); props and planting are procedural (tex_props.py). Output lands in
Assets/_Project/Art/Textures/Kit/, where PsxKitImporter picks the import settings. The
PSX master graphs have one MainTex slot, so relief is baked into the colour.

Tile sizes (set the material Tiling to 1 / metres): see MAPS below.

Usage (background):
    "$BLENDER" -b -P Tools/blender/gen_facade_textures.py -- "$PWD/Assets/_Project/Art/Textures/Kit"
From a live session: exec() with OUT_DIR and TOOLS_DIR set in the globals.
"""

import importlib
import os
import sys

_g = globals()
HERE = _g.get("TOOLS_DIR") or os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import texlib  # noqa: E402
import tex_props  # noqa: E402
import tex_surfaces  # noqa: E402

for _m in (texlib, tex_surfaces, tex_props):
    importlib.reload(_m)

from texlib import rgb  # noqa: E402

PHOTO = os.path.normpath(os.path.join(HERE, "..", "..", "docs", "map", "reference",
                                      "cecadec-north-entrance.png"))

# name -> (builder(photo), metres covered by one tile)
MAPS = {
    "T_Wall_Red": (tex_surfaces.wall_red, 4.0),
    "T_Stucco": (tex_surfaces.stucco, 1.0),
    "T_Aggregate": (tex_surfaces.aggregate, 3.0),
    "T_Slab": (tex_surfaces.slab, 4.0),
    "T_Apron": (tex_surfaces.apron, 2.0),
    "T_Paint_Green": (tex_surfaces.paint_green, 3.0),
    "T_Paint_Blue": (tex_surfaces.paint_blue, 1.0),
    "T_Grass": (tex_surfaces.grass, 2.0),
    "T_Soil": (lambda p: tex_surfaces.soil(), 2.0),
    "T_Stone": (lambda p: tex_props.stone_wall(), 2.0),
    "T_Rock": (lambda p: tex_props.rock(), 2.0),
    "T_Bark": (lambda p: tex_props.bark(), 1.0),
    "T_Foliage": (lambda p: tex_props.foliage_atlas(), 1.0),
    "T_Generator": (lambda p: tex_props.generator(), 4.0),
    "T_Glass": (lambda p: tex_props.flat(rgb(40, 50, 52), 256, 121, 0.06), 1.0),
    "T_Frame": (lambda p: tex_props.flat(rgb(40, 40, 43), 64, 122), 1.0),
    "T_Metal_Red": (lambda p: tex_props.flat(rgb(150, 42, 34), 64, 123, 0.04, rgb(110, 60, 50)), 1.0),
    "T_Metal_White": (lambda p: tex_props.flat(rgb(226, 226, 222), 64, 124, 0.03, rgb(180, 176, 168)), 1.0),
    "T_Paint_Yellow": (lambda p: tex_props.flat(rgb(222, 182, 40), 64, 125, 0.05, rgb(150, 140, 110)), 1.0),
    "T_Globe": (lambda p: tex_props.flat(rgb(242, 240, 232), 64, 126, 0.02), 1.0),
    # CDS south facade and CECADEC east facade (photos 33, 34, 35).
    "T_Canvas_Red": (lambda p: tex_props.canvas_red(), 2.0),
    "T_Palm": (lambda p: tex_props.palm_frond(), 1.0),
    "T_PalmTrunk": (lambda p: tex_props.palm_trunk(), 1.0),
    # Interior (CECADEC ground-floor corridor).
    "T_Tile_Floor": (tex_surfaces.tile_floor, 3.2),
    "T_Ceiling": (tex_surfaces.ceiling_grid, 3.2),
    "T_Wall_White": (tex_surfaces.wall_white, 2.0),
    "T_Wood_Door": (tex_surfaces.wood_door, 1.0),
    "T_Glass_Frosted": (lambda p: tex_props.flat(rgb(206, 208, 210), 64, 180, 0.03), 1.0),
}


def main(out_dir, only=None):
    os.makedirs(out_dir, exist_ok=True)
    photo = texlib.load_photo(PHOTO)
    written = []
    for name, (build, _metres) in MAPS.items():
        if only and name not in only:
            continue
        out = build(photo)
        colour, alpha = out if isinstance(out, tuple) else (out, None)
        written.append(texlib.save_png(name, colour, out_dir, alpha))
        print("[facade_tex] wrote", written[-1])
    return written


if "OUT_DIR" in _g:
    RESULT = main(_g["OUT_DIR"], _g.get("ONLY"))
elif "--" in sys.argv:
    main(sys.argv[sys.argv.index("--") + 1])
