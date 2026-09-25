"""
Every albedo map for the CECADEC ground-floor interior (tex_interior.py), except the
sign atlas, which needs Pillow and is built by gen_sign_atlas.py with the system python.
Output lands in Assets/_Project/Art/Textures/Kit/.

Usage (background):
    "$BLENDER" -b --factory-startup -P Tools/blender/gen_interior_textures.py -- "$PWD/Assets/_Project/Art/Textures/Kit"
From a live session: exec() with OUT_DIR and TOOLS_DIR (and optionally ONLY) in the globals.
"""

import importlib
import os
import sys

_g = globals()
HERE = _g.get("TOOLS_DIR") or os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import texlib  # noqa: E402
import tex_interior  # noqa: E402

for _m in (texlib, tex_interior):
    importlib.reload(_m)

T = tex_interior
PHOTOS = os.path.normpath(os.path.join(HERE, "..", "..", "docs", "map", "reference",
                                       "cecadec-interior-pasillo"))


def photo(n):
    return texlib.load_photo(os.path.join(PHOTOS, f"{n}.png"))


# name -> (builder(), metres covered by one tile). Must match MATERIALS in
# gen_cecadec_interior.py and UtezKit.LandmarkMaterials (Tiling = 1 / metres).
MAPS = {
    "T_Wall_White": (lambda: T.plaster(photo(7)), 2.0),
    "T_Tile_Floor": (T.tile_floor, 3.2),
    "T_Tile_Border": (T.tile_border, 3.2),
    "T_Ceiling": (lambda: T.ceiling(photo(18)), 2.44),
    "T_Wood_Door": (T.wood_louvre, 1.0),
    "T_Wood_Header": (T.wood_header, 2.0),
    "T_Vinyl_Blue": (T.vinyl_blue, 0.5),
    "T_Perforated": (T.perforated, 0.25),
    "T_Panel_Grey": (T.panel_grey, 1.0),
    "T_Metal_Grey": (T.metal_grey, 1.0),
    "T_Chrome": (T.chrome, 0.5),
    "T_Plastic_Grey": (T.plastic_grey, 1.0),
    "T_Nosing": (T.nosing, 0.5),
    "T_Glass_Frosted": (T.glass_frosted, 1.0),
    "T_Glass_Tinted": (T.glass_tinted, 1.0),
    "T_Light": (T.light_panel, 1.0),
    # Toilets, waiting tables and the intermediate floor (2026-09-16).
    "T_Tile_Wall": (T.tile_wall, 2.0),
    "T_Tile_Counter": (T.tile_counter, 1.6),
    "T_Porcelain": (T.porcelain, 1.0),
    "T_Paint_Grey": (T.paint_grey, 1.0),
    "T_Mirror": (T.mirror, 1.0),
    "T_Wood_Desk": (T.wood_desk, 1.0),
    "T_Concrete": (T.concrete_raw, 2.0),
}


def main(out_dir, only=None):
    os.makedirs(out_dir, exist_ok=True)
    written = []
    for name, (build, _metres) in MAPS.items():
        if only and name not in only:
            continue
        written.append(texlib.save_png(name, build(), out_dir))
        print("[interior_tex] wrote", written[-1])
    return written


if "OUT_DIR" in _g:
    RESULT = main(_g["OUT_DIR"], _g.get("ONLY"))
elif "--" in sys.argv:
    main(sys.argv[sys.argv.index("--") + 1])
